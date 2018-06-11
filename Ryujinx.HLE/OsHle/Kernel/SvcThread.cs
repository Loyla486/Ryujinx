using ChocolArm64.State;
using Ryujinx.HLE.Logging;
using Ryujinx.HLE.OsHle.Handles;
using System.Threading;

using static Ryujinx.HLE.OsHle.ErrorCode;

namespace Ryujinx.HLE.OsHle.Kernel
{
    partial class SvcHandler
    {
        private void SvcCreateThread(AThreadState ThreadState)
        {
            long EntryPoint  = (long)ThreadState.X1;
            long ArgsPtr     = (long)ThreadState.X2;
            long StackTop    = (long)ThreadState.X3;
            int  Priority    =  (int)ThreadState.X4;
            int  ProcessorId =  (int)ThreadState.X5;

            if ((uint)Priority > 0x3f)
            {
                Ns.Log.PrintWarning(LogClass.KernelSvc, $"Invalid priority 0x{Priority:x8}!");

                ThreadState.X0 = MakeError(ErrorModule.Kernel, KernelErr.InvalidPriority);

                return;
            }

            if (ProcessorId == -2)
            {
                //TODO: Get this value from the NPDM file.
                ProcessorId = 0;
            }
            else if ((uint)ProcessorId > 3)
            {
                Ns.Log.PrintWarning(LogClass.KernelSvc, $"Invalid core id 0x{ProcessorId:x8}!");

                ThreadState.X0 = MakeError(ErrorModule.Kernel, KernelErr.InvalidCoreId);

                return;
            }

            int Handle = Process.MakeThread(
                EntryPoint,
                StackTop,
                ArgsPtr,
                Priority,
                ProcessorId);

            ThreadState.X0 = 0;
            ThreadState.X1 = (ulong)Handle;
        }

        private void SvcStartThread(AThreadState ThreadState)
        {
            int Handle = (int)ThreadState.X0;

            KThread NewThread = Process.HandleTable.GetData<KThread>(Handle);

            if (NewThread != null)
            {
                Process.Scheduler.StartThread(NewThread);
                Process.Scheduler.SetReschedule(NewThread.ProcessorId);

                ThreadState.X0 = 0;
            }
            else
            {
                Ns.Log.PrintWarning(LogClass.KernelSvc, $"Invalid thread handle 0x{Handle:x8}!");

                ThreadState.X0 = MakeError(ErrorModule.Kernel, KernelErr.InvalidHandle);
            }
        }

        private void SvcExitThread(AThreadState ThreadState)
        {
            KThread CurrThread = Process.GetThread(ThreadState.Tpidr);

            CurrThread.Thread.StopExecution();
        }

        private void SvcSleepThread(AThreadState ThreadState)
        {
            ulong TimeoutNs = ThreadState.X0;

            KThread CurrThread = Process.GetThread(ThreadState.Tpidr);

            if (TimeoutNs == 0)
            {
                Process.Scheduler.Yield(CurrThread);
            }
            else
            {
                Process.Scheduler.Suspend(CurrThread);

                Thread.Sleep(NsTimeConverter.GetTimeMs(TimeoutNs));

                Process.Scheduler.Resume(CurrThread);
            }
        }

        private void SvcGetThreadPriority(AThreadState ThreadState)
        {
            int Handle = (int)ThreadState.X1;

            KThread Thread = GetThread(ThreadState.Tpidr, Handle);

            if (Thread != null)
            {
                ThreadState.X0 = 0;
                ThreadState.X1 = (ulong)Thread.ActualPriority;
            }
            else
            {
                Ns.Log.PrintWarning(LogClass.KernelSvc, $"Invalid thread handle 0x{Handle:x8}!");

                ThreadState.X0 = MakeError(ErrorModule.Kernel, KernelErr.InvalidHandle);
            }
        }

        private void SvcSetThreadPriority(AThreadState ThreadState)
        {
            int Handle   = (int)ThreadState.X0;
            int Priority = (int)ThreadState.X1;

            KThread Thread = GetThread(ThreadState.Tpidr, Handle);

            if (Thread != null)
            {
                Thread.SetPriority(Priority);

                ThreadState.X0 = 0;
            }
            else
            {
                Ns.Log.PrintWarning(LogClass.KernelSvc, $"Invalid thread handle 0x{Handle:x8}!");

                ThreadState.X0 = MakeError(ErrorModule.Kernel, KernelErr.InvalidHandle);
            }
        }

        private void SvcGetThreadCoreMask(AThreadState ThreadState)
        {
            int Handle = (int)ThreadState.X2;

            Ns.Log.PrintDebug(LogClass.KernelSvc, "Handle = " + Handle.ToString("x8"));

            KThread Thread = GetThread(ThreadState.Tpidr, Handle);

            if (Thread != null)
            {
                ThreadState.X0 = 0;
                ThreadState.X1 = (ulong)Thread.IdealCore;
                ThreadState.X2 = (ulong)Thread.CoreMask;
            }
            else
            {
                Ns.Log.PrintWarning(LogClass.KernelSvc, $"Invalid thread handle 0x{Handle:x8}!");

                ThreadState.X0 = MakeError(ErrorModule.Kernel, KernelErr.InvalidHandle);
            }
        }

        private void SvcSetThreadCoreMask(AThreadState ThreadState)
        {
            //FIXME: This is wrong, but the "correct" way to handle
            //this svc causes deadlocks when more often.
            //There is probably something wrong with it still.
            ThreadState.X0 = 0;

            return;

            int  Handle    =  (int)ThreadState.X0;
            int  IdealCore =  (int)ThreadState.X1;
            long CoreMask  = (long)ThreadState.X2;

            Ns.Log.PrintDebug(LogClass.KernelSvc,
                "Handle = "    + Handle   .ToString("x8") + ", " +
                "IdealCore = " + IdealCore.ToString("x8") + ", " +
                "CoreMask = "  + CoreMask .ToString("x16"));

            KThread Thread = GetThread(ThreadState.Tpidr, Handle);

            if (IdealCore == -2)
            {
                //TODO: Get this value from the NPDM file.
                IdealCore = 0;

                CoreMask = 1 << IdealCore;
            }
            else if (IdealCore != -3)
            {
                if ((uint)IdealCore > 3)
                {
                    if ((IdealCore | 2) != -1)
                    {
                        Ns.Log.PrintWarning(LogClass.KernelSvc, $"Invalid core id 0x{IdealCore:x8}!");

                        ThreadState.X0 = MakeError(ErrorModule.Kernel, KernelErr.InvalidCoreId);

                        return;
                    }
                }
                else if ((CoreMask & (1 << IdealCore)) == 0)
                {
                    Ns.Log.PrintWarning(LogClass.KernelSvc, $"Invalid core mask 0x{CoreMask:x8}!");

                    ThreadState.X0 = MakeError(ErrorModule.Kernel, KernelErr.InvalidCoreMask);

                    return;
                }
            }

            if (Thread == null)
            {
                Ns.Log.PrintWarning(LogClass.KernelSvc, $"Invalid thread handle 0x{Handle:x8}!");

                ThreadState.X0 = MakeError(ErrorModule.Kernel, KernelErr.InvalidHandle);

                return;
            }

            //-1 is used as "don't care", so the IdealCore value is ignored.
            //-2 is used as "use NPDM default core id" (handled above).
            //-3 is used as "don't update", the old IdealCore value is kept.
            if (IdealCore != -3)
            {
                Thread.IdealCore = IdealCore;
            }
            else if ((CoreMask & (1 << Thread.IdealCore)) == 0)
            {
                Ns.Log.PrintWarning(LogClass.KernelSvc, $"Invalid core mask 0x{CoreMask:x8}!");

                ThreadState.X0 = MakeError(ErrorModule.Kernel, KernelErr.InvalidCoreMask);

                return;
            }

            Thread.CoreMask = (int)CoreMask;

            Process.Scheduler.TryToRun(Thread);

            ThreadState.X0 = 0;
        }

        private void SvcGetCurrentProcessorNumber(AThreadState ThreadState)
        {
            ThreadState.X0 = (ulong)Process.GetThread(ThreadState.Tpidr).ActualCore;
        }

        private void SvcGetThreadId(AThreadState ThreadState)
        {
            int Handle = (int)ThreadState.X1;

            KThread Thread = GetThread(ThreadState.Tpidr, Handle);

            if (Thread != null)
            {
                ThreadState.X0 = 0;
                ThreadState.X1 = (ulong)Thread.ThreadId;
            }
            else
            {
                Ns.Log.PrintWarning(LogClass.KernelSvc, $"Invalid thread handle 0x{Handle:x8}!");

                ThreadState.X0 = MakeError(ErrorModule.Kernel, KernelErr.InvalidHandle);
            }
        }

        private void SvcSetThreadActivity(AThreadState ThreadState)
        {
            int  Handle = (int)ThreadState.X0;
            bool Active = (int)ThreadState.X1 == 0;

            KThread Thread = Process.HandleTable.GetData<KThread>(Handle);

            if (Thread != null)
            {
                Process.Scheduler.SetThreadActivity(Thread, Active);

                ThreadState.X0 = 0;
            }
            else
            {
                Ns.Log.PrintWarning(LogClass.KernelSvc, $"Invalid thread handle 0x{Handle:x8}!");

                ThreadState.X0 = MakeError(ErrorModule.Kernel, KernelErr.InvalidHandle);
            }
        }
    }
}