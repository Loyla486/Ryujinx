﻿using Ryujinx.HLE.HOS.Kernel.Common;
using Ryujinx.HLE.HOS.Kernel.Memory;
using Ryujinx.HLE.HOS.Kernel.Process;
using Ryujinx.HLE.HOS.Kernel.SupervisorCall;
using Ryujinx.HLE.HOS.Kernel.Threading;
using Ryujinx.Memory;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Ryujinx.HLE.HOS.Kernel
{
    class KernelContext : IDisposable
    {
        public long PrivilegedProcessLowestId { get; set; } = 1;
        public long PrivilegedProcessHighestId { get; set; } = 8;

        public bool EnableVersionChecks { get; set; }

        public bool KernelInitialized { get; }

        public Switch Device { get; }
        public MemoryBlock Memory { get; }
        public Syscall Syscall { get; }
        public SyscallHandler SyscallHandler { get; }

        public CountdownEvent ThreadCounter { get; }

        public KResourceLimit ResourceLimit { get; }

        public KMemoryRegionManager[] MemoryRegions { get; }

        public KMemoryBlockAllocator LargeMemoryBlockAllocator { get; }
        public KMemoryBlockAllocator SmallMemoryBlockAllocator { get; }

        public KSlabHeap UserSlabHeapPages { get; }

        public KCriticalSection CriticalSection { get; }
        public KScheduler Scheduler { get; }
        public KTimeManager TimeManager { get; }
        public KSynchronization Synchronization { get; }
        public KContextIdManager ContextIdManager { get; }

        public ConcurrentDictionary<long, KProcess> Processes { get; }
        public ConcurrentDictionary<string, KAutoObject> AutoObjectNames { get; }

        private long _kipId;
        private long _processId;
        private long _threadUid;

        public KernelContext(Switch device, MemoryBlock memory)
        {
            Device = device;
            Memory = memory;

            Syscall = new Syscall(device, this);

            SyscallHandler = new SyscallHandler(this);

            ThreadCounter = new CountdownEvent(1);

            ResourceLimit = new KResourceLimit(this);

            KernelInit.InitializeResourceLimit(ResourceLimit);

            MemoryRegions = KernelInit.GetMemoryRegions();

            LargeMemoryBlockAllocator = new KMemoryBlockAllocator(KernelConstants.MemoryBlockAllocatorSize * 2);
            SmallMemoryBlockAllocator = new KMemoryBlockAllocator(KernelConstants.MemoryBlockAllocatorSize);

            UserSlabHeapPages = new KSlabHeap(
                KernelConstants.UserSlabHeapBase,
                KernelConstants.UserSlabHeapItemSize,
                KernelConstants.UserSlabHeapSize);

            CriticalSection = new KCriticalSection(this);
            Scheduler = new KScheduler(this);
            TimeManager = new KTimeManager();
            Synchronization = new KSynchronization(this);
            ContextIdManager = new KContextIdManager();

            Scheduler.StartAutoPreemptionThread();

            KernelInitialized = true;

            Processes = new ConcurrentDictionary<long, KProcess>();
            AutoObjectNames = new ConcurrentDictionary<string, KAutoObject>();

            _kipId = KernelConstants.InitialKipId;
            _processId = KernelConstants.InitialProcessId;
        }

        public long NewThreadUid()
        {
            return Interlocked.Increment(ref _threadUid) - 1;
        }

        public long NewKipId()
        {
            return Interlocked.Increment(ref _kipId) - 1;
        }

        public long NewProcessId()
        {
            return Interlocked.Increment(ref _processId) - 1;
        }

        public void Dispose()
        {
            Scheduler.Dispose();
            TimeManager.Dispose();
        }
    }
}
