using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Audio.AudioRendererManager
{
    [StructLayout(LayoutKind.Sequential, Size = 0x20, Pack = 4)]
    struct MemoryPoolIn
    {
        public long            Address;
        public long            Size;
        public MemoryPoolState State;
        public int             Unknown14;
        public long            Unknown18;
    }
}