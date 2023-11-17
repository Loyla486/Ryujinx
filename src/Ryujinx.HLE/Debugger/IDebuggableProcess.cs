﻿using Ryujinx.Memory;
using Ryujinx.HLE.HOS.Kernel.Threading;

namespace Ryujinx.HLE.Debugger
{
    internal interface IDebuggableProcess
    {
        void DebugStop();
        void DebugContinue();
        bool DebugStep(KThread thread);
        KThread GetThread(ulong threadUid);
        DebugState GetDebugState();
        ulong[] GetThreadUids();
        IVirtualMemoryManager CpuMemory { get; }
        void InvalidateCacheRegion(ulong address, ulong size);
    }
}