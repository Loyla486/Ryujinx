namespace Ryujinx.HLE.HOS.Kernel
{
    class KSharedMemory
    {
        public long PA   { get; private set; }
        public long Size { get; private set; }

        public KSharedMemory(long PA, long Size)
        {
            this.PA   = PA;
            this.Size = Size;
        }
    }
}