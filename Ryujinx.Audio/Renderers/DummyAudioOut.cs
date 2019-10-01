﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Ryujinx.Audio
{
    /// <summary>
    /// A Dummy audio renderer that does not output any audio
    /// </summary>
    public class DummyAudioOut : IAalOutput
    {
        private int   _lastTrackId = 1;
        private float _volume      = 1.0f;

        private ConcurrentQueue<int> _trackIds;
        private ConcurrentQueue<long> _buffers;
        private ConcurrentDictionary<int, ReleaseCallback> _releaseCallbacks;

        public DummyAudioOut()
        {
            _buffers          = new ConcurrentQueue<long>();
            _trackIds         = new ConcurrentQueue<int>();
            _releaseCallbacks = new ConcurrentDictionary<int, ReleaseCallback>();
        }

        /// <summary>
        /// Dummy audio output is always available, Baka!
        /// </summary>
        public static bool IsSupported => true;

        public PlaybackState GetState(int trackId) => PlaybackState.Stopped;

        public int OpenTrack(int sampleRate, int channels, ReleaseCallback callback)
        {
            if (!_trackIds.TryDequeue(out int trackId))
            {
                trackId = ++_lastTrackId;
            }

            _releaseCallbacks[trackId] = callback;

            return trackId;
        }

        public void CloseTrack(int trackId)
        {
            _trackIds.Enqueue(trackId);
            _releaseCallbacks.Remove(trackId, out _);
        }

        public bool ContainsBuffer(int trackID, long bufferTag) => false;

        public long[] GetReleasedBuffers(int trackId, int maxCount)
        {
            List<long> bufferTags = new List<long>();

            for (int i = 0; i < maxCount; i++)
            {
                if (!_buffers.TryDequeue(out long tag))
                {
                    break;
                }

                bufferTags.Add(tag);
            }

            return bufferTags.ToArray();
        }

        public void AppendBuffer<T>(int trackID, long bufferTag, T[] buffer) where T : struct
        {
            _buffers.Enqueue(bufferTag);

            if (_releaseCallbacks.TryGetValue(trackID, out var callback))
            {
                callback?.Invoke();
            }
        }

        public void Start(int trackId) { }

        public void Stop(int trackId) { }

        public float GetVolume() => _volume;

        public void SetVolume(float volume)
        {
            _volume = volume;
        }

        public void Dispose()
        {
            _buffers.Clear();
        }
    }
}