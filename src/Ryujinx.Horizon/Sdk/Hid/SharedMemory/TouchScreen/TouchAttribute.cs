using System;

namespace Ryujinx.Horizon.Sdk.Hid.SharedMemory.TouchScreen
{
    [Flags]
    public enum TouchAttribute : uint
    {
        None = 0,
        Start = 1 << 0,
        End = 1 << 1,
    }
}
