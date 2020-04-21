﻿using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.SurfaceFlinger
{
    [StructLayout(LayoutKind.Sequential, Size = 0x28)]
    struct GraphicBufferHeader
    {
        public int         Magic;
        public int         Width;
        public int         Height;
        public int         Stride;
        public PixelFormat Format;
        public int         Usage;

        public int Pid;
        public int RefCount;

        public int FdsCount;
        public int IntsCount;
    }
}