using System.Runtime.InteropServices;

namespace Ryujinx.HLE.OsHle.Services.Aud.AudioRenderer
{
    [StructLayout(LayoutKind.Sequential, Size = 0x10, Pack = 4)]
    struct MemoryPoolOut
    {
        public MemoryPoolState State;
        public int             Unknown14;
        public long            Unknown18;
    }
}
