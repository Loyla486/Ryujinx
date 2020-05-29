using OpenTK.Audio;
using OpenTK.Audio.OpenAL;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;

namespace Ryujinx.Audio
{
    /// <summary>
    /// An audio renderer that uses OpenAL as the audio backend
    /// </summary>
    public class OpenALAudioOut : IAalOutput, IDisposable
    {
        /// <summary>
        /// The maximum amount of tracks we can issue simultaneously
        /// </summary>
        private const int MaxTracks = 256;

        /// <summary>
        /// The <see cref="OpenTK.Audio"/> audio context
        /// </summary>
        private AudioContext _context;

        /// <summary>
        /// An object pool containing <see cref="OpenALAudioTrack"/> objects
        /// </summary>
        private ConcurrentDictionary<int, OpenALAudioTrack> _tracks;

        /// <summary>
        /// True if the thread need to keep polling
        /// </summary>
        private bool _keepPolling;

        /// <summary>
        /// The poller thread audio context
        /// </summary>
        private Thread _audioPollerThread;

        /// <summary>
        /// The volume of audio renderer
        /// </summary>
        private float _volume = 1.0f;

        /// <summary>
        /// True if the volume of audio renderer have changed
        /// </summary>
        private bool _volumeChanged;

        /// <summary>
        /// True if OpenAL is supported on the device
        /// </summary>
        public static bool IsSupported
        {
            get
            {
                try
                {
                    return AudioContext.AvailableDevices.Count > 0;
                }
                catch
                {
                    return false;
                }
            }
        }

        public OpenALAudioOut()
        {
            _context           = new AudioContext();
            _tracks            = new ConcurrentDictionary<int, OpenALAudioTrack>();
            _keepPolling       = true;
            _audioPollerThread = new Thread(AudioPollerWork)
            {
                Name = "Audio.PollerThread"
            };

            _audioPollerThread.Start();
        }

        private void AudioPollerWork()
        {
            do
            {
                foreach (OpenALAudioTrack track in _tracks.Values)
                {
                    lock (track)
                    {
                        track.CallReleaseCallbackIfNeeded();
                    }
                }

                // If it's not slept it will waste cycles.
                Thread.Sleep(10);
            }
            while (_keepPolling);

            foreach (OpenALAudioTrack track in _tracks.Values)
            {
                track.Dispose();
            }

            _tracks.Clear();
            _context.Dispose();
        }

        /// <summary>
        /// Creates a new audio track with the specified parameters
        /// </summary>
        /// <param name="sampleRate">The requested sample rate</param>
        /// <param name="channels">The requested channels</param>
        /// <param name="callback">A <see cref="ReleaseCallback" /> that represents the delegate to invoke when a buffer has been released by the audio track</param>
        public int OpenTrack(int sampleRate, int channels, ReleaseCallback callback)
        {
            OpenALAudioTrack track = new OpenALAudioTrack(sampleRate, GetALFormat(channels), callback);

            for (int id = 0; id < MaxTracks; id++)
            {
                if (_tracks.TryAdd(id, track))
                {
                    return id;
                }
            }

            return -1;
        }

        private ALFormat GetALFormat(int channels)
        {
            switch (channels)
            {
                case 1: return ALFormat.Mono16;
                case 2: return ALFormat.Stereo16;
                case 6: return ALFormat.Multi51Chn16Ext;
            }

            throw new ArgumentOutOfRangeException(nameof(channels));
        }

        /// <summary>
        /// Stops playback and closes the track specified by <paramref name="trackId"/>
        /// </summary>
        /// <param name="trackId">The ID of the track to close</param>
        public void CloseTrack(int trackId)
        {
            if (_tracks.TryRemove(trackId, out OpenALAudioTrack track))
            {
                lock (track)
                {
                    track.Dispose();
                }
            }
        }

        /// <summary>
        /// Returns a value indicating whether the specified buffer is currently reserved by the specified track
        /// </summary>
        /// <param name="trackId">The track to check</param>
        /// <param name="bufferTag">The buffer tag to check</param>
        public bool ContainsBuffer(int trackId, long bufferTag)
        {
            if (_tracks.TryGetValue(trackId, out OpenALAudioTrack track))
            {
                lock (track)
                {
                    return track.ContainsBuffer(bufferTag);
                }
            }

            return false;
        }

        /// <summary>
        /// Gets a list of buffer tags the specified track is no longer reserving
        /// </summary>
        /// <param name="trackId">The track to retrieve buffer tags from</param>
        /// <param name="maxCount">The maximum amount of buffer tags to retrieve</param>
        /// <returns>Buffers released by the specified track</returns>
        public long[] GetReleasedBuffers(int trackId, int maxCount)
        {
            if (_tracks.TryGetValue(trackId, out OpenALAudioTrack track))
            {
                lock (track)
                {
                    return track.GetReleasedBuffers(maxCount);
                }
            }

            return null;
        }

        /// <summary>
        /// Appends an audio buffer to the specified track
        /// </summary>
        /// <typeparam name="T">The sample type of the buffer</typeparam>
        /// <param name="trackId">The track to append the buffer to</param>
        /// <param name="bufferTag">The internal tag of the buffer</param>
        /// <param name="buffer">The buffer to append to the track</param>
        public void AppendBuffer<T>(int trackId, long bufferTag, T[] buffer) where T : struct
        {
            if (_tracks.TryGetValue(trackId, out OpenALAudioTrack track))
            {
                lock (track)
                {
                    int bufferId = track.AppendBuffer(bufferTag);

                    int size = buffer.Length * Marshal.SizeOf<T>();

                    AL.BufferData(bufferId, track.Format, buffer, size, track.SampleRate);

                    AL.SourceQueueBuffer(track.SourceId, bufferId);

                    StartPlaybackIfNeeded(track);
                }
            }
        }

        /// <summary>
        /// Starts playback
        /// </summary>
        /// <param name="trackId">The ID of the track to start playback on</param>
        public void Start(int trackId)
        {
            if (_tracks.TryGetValue(trackId, out OpenALAudioTrack track))
            {
                lock (track)
                {
                    track.State = PlaybackState.Playing;

                    StartPlaybackIfNeeded(track);
                }
            }
        }

        private void StartPlaybackIfNeeded(OpenALAudioTrack track)
        {
            AL.GetSource(track.SourceId, ALGetSourcei.SourceState, out int stateInt);

            ALSourceState State = (ALSourceState)stateInt;

            if (State != ALSourceState.Playing && track.State == PlaybackState.Playing)
            {
                if (_volumeChanged)
                {
                    AL.Source(track.SourceId, ALSourcef.Gain, _volume);

                    _volumeChanged = false;
                }

                AL.SourcePlay(track.SourceId);
            }
        }

        /// <summary>
        /// Stops playback
        /// </summary>
        /// <param name="trackId">The ID of the track to stop playback on</param>
        public void Stop(int trackId)
        {
            if (_tracks.TryGetValue(trackId, out OpenALAudioTrack track))
            {
                lock (track)
                {
                    track.State = PlaybackState.Stopped;

                    AL.SourceStop(track.SourceId);
                }
            }
        }

        /// <summary>
        /// Get playback volume
        /// </summary>
        public float GetVolume() => _volume;

        /// <summary>
        /// Set playback volume
        /// </summary>
        /// <param name="volume">The volume of the playback</param>
        public void SetVolume(float volume)
        {
            if (!_volumeChanged)
            {
                _volume        = volume;
                _volumeChanged = true;
            }
        }

        /// <summary>
        /// Gets the current playback state of the specified track
        /// </summary>
        /// <param name="trackId">The track to retrieve the playback state for</param>
        public PlaybackState GetState(int trackId)
        {
            if (_tracks.TryGetValue(trackId, out OpenALAudioTrack track))
            {
                return track.State;
            }

            return PlaybackState.Stopped;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _keepPolling = false;
            }
        }
    }
}