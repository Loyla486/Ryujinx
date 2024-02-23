namespace Ryujinx.Audio
{
    public interface IAalOutput
    {
        int OpenTrack(
            int             SampleRate,
            int             Channels,
            ReleaseCallback Callback,
            out AudioFormat Format);

        void CloseTrack(int Track);

        bool ContainsBuffer(int Track, long Tag);

        long[] GetReleasedBuffers(int Track, int MaxCount);

        void AppendBuffer(int Track, long Tag, byte[] Buffer);

        void Start(int Track);
        void Stop(int Track);

        PlaybackState GetState(int Track);
    }
}