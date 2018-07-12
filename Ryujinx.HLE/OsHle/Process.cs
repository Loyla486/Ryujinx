using ChocolArm64;
using ChocolArm64.Events;
using ChocolArm64.Memory;
using ChocolArm64.State;
using Ryujinx.HLE.Loaders;
using Ryujinx.HLE.Loaders.Executables;
using Ryujinx.HLE.Logging;
using Ryujinx.HLE.OsHle.Diagnostics;
using Ryujinx.HLE.OsHle.Exceptions;
using Ryujinx.HLE.OsHle.Handles;
using Ryujinx.HLE.OsHle.Kernel;
using Ryujinx.HLE.OsHle.Services.Nv;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Ryujinx.HLE.OsHle
{
    class Process : IDisposable
    {
        private const int TlsSize = 0x200;

        private const int TotalTlsSlots = (int)MemoryRegions.TlsPagesSize / TlsSize;

        private const int TickFreq = 19_200_000;

        private Switch Ns;

        public bool NeedsHbAbi { get; private set; }

        public long HbAbiDataPosition { get; private set; }

        public int ProcessId { get; private set; }

        private ATranslator Translator;

        public AMemory Memory { get; private set; }

        public KProcessScheduler Scheduler { get; private set; }

        public List<KThread> ThreadArbiterList { get; private set; }

        public object ThreadSyncLock { get; private set; }

        public KProcessHandleTable HandleTable { get; private set; }

        public AppletStateMgr AppletState { get; private set; }

        private SvcHandler SvcHandler;

        private ConcurrentDictionary<int, AThread> TlsSlots;

        private ConcurrentDictionary<long, KThread> Threads;

        private KThread MainThread;

        private List<Executable> Executables;

        private long ImageBase;

        private bool ShouldDispose;

        private bool Disposed;

        public Process(Switch Ns, KProcessScheduler Scheduler, int ProcessId)
        {
            this.Ns        = Ns;
            this.Scheduler = Scheduler;
            this.ProcessId = ProcessId;

            Memory = new AMemory();

            ThreadArbiterList = new List<KThread>();

            ThreadSyncLock = new object();

            HandleTable = new KProcessHandleTable();

            AppletState = new AppletStateMgr();

            SvcHandler = new SvcHandler(Ns, this);

            TlsSlots = new ConcurrentDictionary<int, AThread>();

            Threads = new ConcurrentDictionary<long, KThread>();

            Executables = new List<Executable>();

            ImageBase = MemoryRegions.AddrSpaceStart;

            MapRWMemRegion(
                MemoryRegions.TlsPagesAddress,
                MemoryRegions.TlsPagesSize,
                MemoryType.ThreadLocal);
        }

        public void LoadProgram(IExecutable Program)
        {
            if (Disposed)
            {
                throw new ObjectDisposedException(nameof(Process));
            }

            Ns.Log.PrintInfo(LogClass.Loader, $"Image base at 0x{ImageBase:x16}.");

            Executable Executable = new Executable(Program, Memory, ImageBase);

            Executables.Add(Executable);

            ImageBase = AMemoryHelper.PageRoundUp(Executable.ImageEnd);
        }

        public void SetEmptyArgs()
        {
            //TODO: This should be part of Run.
            ImageBase += AMemoryMgr.PageSize;
        }

        public bool Run(bool NeedsHbAbi = false)
        {
            if (Disposed)
            {
                throw new ObjectDisposedException(nameof(Process));
            }

            this.NeedsHbAbi = NeedsHbAbi;

            if (Executables.Count == 0)
            {
                return false;
            }

            MapRWMemRegion(
                MemoryRegions.MainStackAddress,
                MemoryRegions.MainStackSize,
                MemoryType.Normal);

            long StackTop = MemoryRegions.MainStackAddress + MemoryRegions.MainStackSize;

            int Handle = MakeThread(Executables[0].ImageBase, StackTop, 0, 44, 0);

            if (Handle == -1)
            {
                return false;
            }

            MainThread = HandleTable.GetData<KThread>(Handle);

            if (NeedsHbAbi)
            {
                HbAbiDataPosition = AMemoryHelper.PageRoundUp(Executables[0].ImageEnd);

                Homebrew.WriteHbAbiData(Memory, HbAbiDataPosition, Handle);

                MainThread.Thread.ThreadState.X0 = (ulong)HbAbiDataPosition;
                MainThread.Thread.ThreadState.X1 = ulong.MaxValue;
            }

            Scheduler.StartThread(MainThread);

            return true;
        }

        private void MapRWMemRegion(long Position, long Size, MemoryType Type)
        {
            Memory.Manager.Map(Position, Size, (int)Type, AMemoryPerm.RW);
        }

        public void StopAllThreadsAsync()
        {
            if (Disposed)
            {
                throw new ObjectDisposedException(nameof(Process));
            }

            if (MainThread != null)
            {
                MainThread.Thread.StopExecution();
            }

            foreach (AThread Thread in TlsSlots.Values)
            {
                Thread.StopExecution();
            }
        }

        public int MakeThread(
            long EntryPoint,
            long StackTop,
            long ArgsPtr,
            int  Priority,
            int  ProcessorId)
        {
            if (Disposed)
            {
                throw new ObjectDisposedException(nameof(Process));
            }

            AThread CpuThread = new AThread(GetTranslator(), Memory, EntryPoint);

            KThread Thread = new KThread(CpuThread, this, ProcessorId, Priority);

            Thread.LastPc = EntryPoint;

            int Handle = HandleTable.OpenHandle(Thread);

            int ThreadId = GetFreeTlsSlot(CpuThread);

            long Tpidr = MemoryRegions.TlsPagesAddress + ThreadId * TlsSize;

            CpuThread.ThreadState.ProcessId = ProcessId;
            CpuThread.ThreadState.ThreadId  = ThreadId;
            CpuThread.ThreadState.CntfrqEl0 = TickFreq;
            CpuThread.ThreadState.Tpidr     = Tpidr;

            CpuThread.ThreadState.X0  = (ulong)ArgsPtr;
            CpuThread.ThreadState.X1  = (ulong)Handle;
            CpuThread.ThreadState.X31 = (ulong)StackTop;

            CpuThread.ThreadState.Break     += BreakHandler;
            CpuThread.ThreadState.SvcCall   += SvcHandler.SvcCall;
            CpuThread.ThreadState.Undefined += UndefinedHandler;

            CpuThread.WorkFinished += ThreadFinished;

            Threads.TryAdd(CpuThread.ThreadState.Tpidr, Thread);

            return Handle;
        }

        private void BreakHandler(object sender, AInstExceptionEventArgs e)
        {
            throw new GuestBrokeExecutionException();
        }

        private void UndefinedHandler(object sender, AInstUndefinedEventArgs e)
        {
            throw new UndefinedInstructionException(e.Position, e.RawOpCode);
        }

        public void EnableCpuTracing()
        {
            Translator.EnableCpuTrace = true;
        }

        public void DisableCpuTracing()
        {
            Translator.EnableCpuTrace = false;
        }

        private void CpuTraceHandler(object sender, ACpuTraceEventArgs e)
        {
            Executable Exe = GetExecutable(e.Position);

            if (Exe == null)
            {
                return;
            }

            if (!TryGetSubName(Exe, e.Position, out string SubName))
            {
                SubName = string.Empty;
            }

            long Offset = e.Position - Exe.ImageBase;

            string ExeNameWithAddr = $"{Exe.Name}:0x{Offset:x8}";

            Ns.Log.PrintDebug(LogClass.Cpu, ExeNameWithAddr + " " + SubName);
        }

        private ATranslator GetTranslator()
        {
            if (Translator == null)
            {
                Translator = new ATranslator();

                Translator.CpuTrace += CpuTraceHandler;
            }

            return Translator;
        }

        public void PrintStackTrace(AThreadState ThreadState)
        {
            StringBuilder Trace = new StringBuilder();

            Trace.AppendLine("Guest stack trace:");

            void AppendTrace(long Position)
            {
                Executable Exe = GetExecutable(Position);

                if (Exe == null)
                {
                    return;
                }

                if (!TryGetSubName(Exe, Position, out string SubName))
                {
                    SubName = $"Sub{Position:x16}";
                }
                else if (SubName.StartsWith("_Z"))
                {
                    SubName = Demangler.Parse(SubName);
                }

                long Offset = Position - Exe.ImageBase;

                string ExeNameWithAddr = $"{Exe.Name}:0x{Offset:x8}";

                Trace.AppendLine(" " + ExeNameWithAddr + " " + SubName);
            }

            long FramePointer = (long)ThreadState.X29;

            while (FramePointer != 0)
            {
                AppendTrace(Memory.ReadInt64(FramePointer + 8));

                FramePointer = Memory.ReadInt64(FramePointer);
            }

            Ns.Log.PrintInfo(LogClass.Cpu, Trace.ToString());
        }

        private bool TryGetSubName(Executable Exe, long Position, out string Name)
        {
            Position -= Exe.ImageBase;

            int Left  = 0;
            int Right = Exe.SymbolTable.Count - 1;

            while (Left <= Right)
            {
                int Size = Right - Left;

                int Middle = Left + (Size >> 1);

                ElfSym Symbol = Exe.SymbolTable[Middle];

                long EndPosition = Symbol.Value + Symbol.Size;

                if ((ulong)Position >= (ulong)Symbol.Value && (ulong)Position < (ulong)EndPosition)
                {
                    Name = Symbol.Name;

                    return true;
                }

                if ((ulong)Position < (ulong)Symbol.Value)
                {
                    Right = Middle - 1;
                }
                else
                {
                    Left = Middle + 1;
                }
            }

            Name = null;

            return false;
        }

        private Executable GetExecutable(long Position)
        {
            string Name = string.Empty;

            for (int Index = Executables.Count - 1; Index >= 0; Index--)
            {
                if ((ulong)Position >= (ulong)Executables[Index].ImageBase)
                {
                    return Executables[Index];
                }
            }

            return null;
        }

        private int GetFreeTlsSlot(AThread Thread)
        {
            for (int Index = 1; Index < TotalTlsSlots; Index++)
            {
                if (TlsSlots.TryAdd(Index, Thread))
                {
                    return Index;
                }
            }

            throw new InvalidOperationException();
        }

        private void ThreadFinished(object sender, EventArgs e)
        {
            if (sender is AThread Thread)
            {
                TlsSlots.TryRemove(GetTlsSlot(Thread.ThreadState.Tpidr), out _);

                Threads.TryRemove(Thread.ThreadState.Tpidr, out KThread KernelThread);

                Scheduler.RemoveThread(KernelThread);

                KernelThread.WaitEvent.Set();
            }

            if (TlsSlots.Count == 0)
            {
                if (ShouldDispose)
                {
                    Dispose();
                }

                Ns.Os.ExitProcess(ProcessId);
            }
        }

        private int GetTlsSlot(long Position)
        {
            return (int)((Position - MemoryRegions.TlsPagesAddress) / TlsSize);
        }

        public KThread GetThread(long Tpidr)
        {
            if (!Threads.TryGetValue(Tpidr, out KThread Thread))
            {
                throw new InvalidOperationException();
            }

            return Thread;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool Disposing)
        {
            if (Disposing && !Disposed)
            {
                //If there is still some thread running, disposing the objects is not
                //safe as the thread may try to access those resources. Instead, we set
                //the flag to have the Process disposed when all threads finishes.
                //Note: This may not happen if the guest code gets stuck on a infinite loop.
                if (TlsSlots.Count > 0)
                {
                    ShouldDispose = true;

                    Ns.Log.PrintInfo(LogClass.Loader, $"Process {ProcessId} waiting all threads terminate...");

                    return;
                }

                Disposed = true;

                foreach (object Obj in HandleTable.Clear())
                {
                    if (Obj is KSession Session)
                    {
                        Session.Dispose();
                    }
                }

                INvDrvServices.UnloadProcess(this);

                AppletState.Dispose();

                SvcHandler.Dispose();

                Memory.Dispose();

                Ns.Log.PrintInfo(LogClass.Loader, $"Process {ProcessId} exiting...");
            }
        }
    }
}