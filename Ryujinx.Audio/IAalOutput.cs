using System;

namespace Ryujinx.Audio
{
    public interface IAalOutput : IDisposable
    {
        bool SupportsChannelCount(int channels);

        private int SelectHardwareChannelCount(int targetChannelCount)
        {
            if (SupportsChannelCount(targetChannelCount))
            {
                return targetChannelCount;
            }

            return targetChannelCount switch
            {
                6 => SelectHardwareChannelCount(2),
                2 => SelectHardwareChannelCount(1),
                1 => throw new ArgumentException("No valid channel configuration found!"),
                _ => throw new ArgumentException($"Invalid targetChannelCount {targetChannelCount}"),
            };
        }

        int OpenTrack(int sampleRate, int channels, ReleaseCallback callback)
        {
            return OpenHardwareTrack(sampleRate, SelectHardwareChannelCount(channels), channels, callback);
        }

        int OpenHardwareTrack(int sampleRate, int hardwareChannels, int virtualChannels, ReleaseCallback callback);

        void CloseTrack(int trackId);

        bool ContainsBuffer(int trackId, long bufferTag);

        long[] GetReleasedBuffers(int trackId, int maxCount);

        void AppendBuffer<T>(int trackId, long bufferTag, T[] buffer) where T : struct;

        void Start(int trackId);

        void Stop(int trackId);

        uint GetBufferCount(int trackId);

        ulong GetPlayedSampleCount(int trackId);

        bool FlushBuffers(int trackId);

        float GetVolume(int trackId);

        void SetVolume(int trackId, float volume);

        PlaybackState GetState(int trackId);
    }
}