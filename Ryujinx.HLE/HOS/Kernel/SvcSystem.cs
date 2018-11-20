using ChocolArm64.Memory;
using ChocolArm64.State;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.Exceptions;
using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.HOS.Services;
using System;
using System.Threading;

using static Ryujinx.HLE.HOS.ErrorCode;

namespace Ryujinx.HLE.HOS.Kernel
{
    partial class SvcHandler
    {
        private const int AllowedCpuIdBitmask = 0b1111;

        private const bool EnableProcessDebugging = false;

        private void SvcExitProcess(CpuThreadState ThreadState)
        {
            System.Scheduler.GetCurrentProcess().Terminate();
        }

        private void SignalEvent64(CpuThreadState ThreadState)
        {
            ThreadState.X0 = (ulong)SignalEvent((int)ThreadState.X0);
        }

        private KernelResult SignalEvent(int Handle)
        {
            KWritableEvent WritableEvent = Process.HandleTable.GetObject<KWritableEvent>(Handle);

            KernelResult Result;

            if (WritableEvent != null)
            {
                WritableEvent.Signal();

                Result = KernelResult.Success;
            }
            else
            {
                Result = KernelResult.InvalidHandle;
            }

            if (Result != KernelResult.Success)
            {
                Logger.PrintWarning(LogClass.KernelSvc, "Operation failed with error: " + Result + "!");
            }

            return Result;
        }

        private void ClearEvent64(CpuThreadState ThreadState)
        {
            ThreadState.X0 = (ulong)ClearEvent((int)ThreadState.X0);
        }

        private KernelResult ClearEvent(int Handle)
        {
            KernelResult Result;

            KWritableEvent WritableEvent = Process.HandleTable.GetObject<KWritableEvent>(Handle);

            if (WritableEvent == null)
            {
                KReadableEvent ReadableEvent = Process.HandleTable.GetObject<KReadableEvent>(Handle);

                Result = ReadableEvent?.Clear() ?? KernelResult.InvalidHandle;
            }
            else
            {
                Result = WritableEvent.Clear();
            }

            if (Result != KernelResult.Success)
            {
                Logger.PrintWarning(LogClass.KernelSvc, "Operation failed with error: " + Result + "!");
            }

            return Result;
        }

        private void SvcCloseHandle(CpuThreadState ThreadState)
        {
            int Handle = (int)ThreadState.X0;

            object Obj = Process.HandleTable.GetObject<object>(Handle);

            Process.HandleTable.CloseHandle(Handle);

            if (Obj == null)
            {
                Logger.PrintWarning(LogClass.KernelSvc, $"Invalid handle 0x{Handle:x8}!");

                ThreadState.X0 = MakeError(ErrorModule.Kernel, KernelErr.InvalidHandle);

                return;
            }

            if (Obj is KSession Session)
            {
                Session.Dispose();
            }
            else if (Obj is KTransferMemory TransferMemory)
            {
                Process.MemoryManager.ResetTransferMemory(
                    TransferMemory.Position,
                    TransferMemory.Size);
            }

            ThreadState.X0 = 0;
        }

        private void ResetSignal64(CpuThreadState ThreadState)
        {
            ThreadState.X0 = (ulong)ResetSignal((int)ThreadState.X0);
        }

        private KernelResult ResetSignal(int Handle)
        {
            KReadableEvent ReadableEvent = Process.HandleTable.GetObject<KReadableEvent>(Handle);

            KernelResult Result;

            //TODO: KProcess support.
            if (ReadableEvent != null)
            {
                Result = ReadableEvent.ClearIfSignaled();
            }
            else
            {
                Result = KernelResult.InvalidHandle;
            }

            if (Result == KernelResult.InvalidState)
            {
                Logger.PrintDebug(LogClass.KernelSvc, "Operation failed with error: " + Result + "!");
            }
            else if (Result != KernelResult.Success)
            {
                Logger.PrintWarning(LogClass.KernelSvc, "Operation failed with error: " + Result + "!");
            }

            return Result;
        }

        private void SvcGetSystemTick(CpuThreadState ThreadState)
        {
            ThreadState.X0 = ThreadState.CntpctEl0;
        }

        private void SvcConnectToNamedPort(CpuThreadState ThreadState)
        {
            long StackPtr = (long)ThreadState.X0;
            long NamePtr  = (long)ThreadState.X1;

            string Name = MemoryHelper.ReadAsciiString(Memory, NamePtr, 8);

            //TODO: Validate that app has perms to access the service, and that the service
            //actually exists, return error codes otherwise.
            KSession Session = new KSession(ServiceFactory.MakeService(System, Name), Name);

            if (Process.HandleTable.GenerateHandle(Session, out int Handle) != KernelResult.Success)
            {
                throw new InvalidOperationException("Out of handles!");
            }

            ThreadState.X0 = 0;
            ThreadState.X1 = (uint)Handle;
        }

        private void SvcSendSyncRequest(CpuThreadState ThreadState)
        {
            SendSyncRequest(ThreadState, ThreadState.Tpidr, 0x100, (int)ThreadState.X0);
        }

        private void SvcSendSyncRequestWithUserBuffer(CpuThreadState ThreadState)
        {
            SendSyncRequest(
                      ThreadState,
                (long)ThreadState.X0,
                (long)ThreadState.X1,
                 (int)ThreadState.X2);
        }

        private void SendSyncRequest(CpuThreadState ThreadState, long MessagePtr, long Size, int Handle)
        {
            byte[] MessageData = Memory.ReadBytes(MessagePtr, Size);

            KSession Session = Process.HandleTable.GetObject<KSession>(Handle);

            if (Session != null)
            {
                System.CriticalSection.Enter();

                KThread CurrentThread = System.Scheduler.GetCurrentThread();

                CurrentThread.SignaledObj   = null;
                CurrentThread.ObjSyncResult = 0;

                CurrentThread.Reschedule(ThreadSchedState.Paused);

                IpcMessage Message = new IpcMessage(MessageData, MessagePtr);

                ThreadPool.QueueUserWorkItem(ProcessIpcRequest, new HleIpcMessage(
                    CurrentThread,
                    Session,
                    Message,
                    MessagePtr));

                System.CriticalSection.Leave();

                ThreadState.X0 = (ulong)CurrentThread.ObjSyncResult;
            }
            else
            {
                Logger.PrintWarning(LogClass.KernelSvc, $"Invalid session handle 0x{Handle:x8}!");

                ThreadState.X0 = MakeError(ErrorModule.Kernel, KernelErr.InvalidHandle);
            }
        }

        private void ProcessIpcRequest(object State)
        {
            HleIpcMessage IpcMessage = (HleIpcMessage)State;

            IpcMessage.Thread.ObjSyncResult = (int)IpcHandler.IpcCall(
                Device,
                Process,
                Memory,
                IpcMessage.Session,
                IpcMessage.Message,
                IpcMessage.MessagePtr);

            IpcMessage.Thread.Reschedule(ThreadSchedState.Running);
        }

        private void SvcBreak(CpuThreadState ThreadState)
        {
            long Reason  = (long)ThreadState.X0;
            long Unknown = (long)ThreadState.X1;
            long Info    = (long)ThreadState.X2;

            KThread CurrentThread = System.Scheduler.GetCurrentThread();

            if ((Reason & (1 << 31)) == 0)
            {
                CurrentThread.PrintGuestStackTrace();

                throw new GuestBrokeExecutionException();
            }
            else
            {
                Logger.PrintInfo(LogClass.KernelSvc, "Debugger triggered.");

                CurrentThread.PrintGuestStackTrace();
            }
        }

        private void SvcOutputDebugString(CpuThreadState ThreadState)
        {
            long Position = (long)ThreadState.X0;
            long Size     = (long)ThreadState.X1;

            string Str = MemoryHelper.ReadAsciiString(Memory, Position, Size);

            Logger.PrintWarning(LogClass.KernelSvc, Str);

            ThreadState.X0 = 0;
        }

        private void SvcGetInfo(CpuThreadState ThreadState)
        {
            long StackPtr = (long)ThreadState.X0;
            int  InfoType =  (int)ThreadState.X1;
            long Handle   = (long)ThreadState.X2;
            int  InfoId   =  (int)ThreadState.X3;

            //Fail for info not available on older Kernel versions.
            if (InfoType == 18 ||
                InfoType == 19 ||
                InfoType == 20 ||
                InfoType == 21 ||
                InfoType == 22)
            {
                ThreadState.X0 = MakeError(ErrorModule.Kernel, KernelErr.InvalidEnumValue);

                return;
            }

            switch (InfoType)
            {
                case 0:
                    ThreadState.X1 = AllowedCpuIdBitmask;
                    break;

                case 2:
                    ThreadState.X1 = (ulong)Process.MemoryManager.AliasRegionStart;
                    break;

                case 3:
                    ThreadState.X1 = (ulong)Process.MemoryManager.AliasRegionEnd -
                                     (ulong)Process.MemoryManager.AliasRegionStart;
                    break;

                case 4:
                    ThreadState.X1 = (ulong)Process.MemoryManager.HeapRegionStart;
                    break;

                case 5:
                    ThreadState.X1 = (ulong)Process.MemoryManager.HeapRegionEnd -
                                     (ulong)Process.MemoryManager.HeapRegionStart;
                    break;

                case 6:
                    ThreadState.X1 = (ulong)Process.GetMemoryCapacity();
                    break;

                case 7:
                    ThreadState.X1 = (ulong)Process.GetMemoryUsage();
                    break;

                case 8:
                    ThreadState.X1 = EnableProcessDebugging ? 1 : 0;
                    break;

                case 11:
                    ThreadState.X1 = (ulong)Rng.Next() + ((ulong)Rng.Next() << 32);
                    break;

                case 12:
                    ThreadState.X1 = (ulong)Process.MemoryManager.AddrSpaceStart;
                    break;

                case 13:
                    ThreadState.X1 = (ulong)Process.MemoryManager.AddrSpaceEnd -
                                     (ulong)Process.MemoryManager.AddrSpaceStart;
                    break;

                case 14:
                    ThreadState.X1 = (ulong)Process.MemoryManager.StackRegionStart;
                    break;

                case 15:
                    ThreadState.X1 = (ulong)Process.MemoryManager.StackRegionEnd -
                                     (ulong)Process.MemoryManager.StackRegionStart;
                    break;

                case 16:
                    //ThreadState.X1 = (ulong)(Process.MetaData?.SystemResourceSize ?? 0);
                    break;

                case 17:
                    ThreadState.X1 = (ulong)Process.MemoryManager.PersonalMmHeapUsage;
                    break;

                default:
                    //Process.PrintStackTrace(ThreadState);

                    throw new NotImplementedException($"SvcGetInfo: {InfoType} 0x{Handle:x8} {InfoId}");
            }

            ThreadState.X0 = 0;
        }

        private void CreateEvent64(CpuThreadState State)
        {
            KernelResult Result = CreateEvent(out int WEventHandle, out int REventHandle);

            State.X0 = (ulong)Result;
            State.X1 = (ulong)WEventHandle;
            State.X2 = (ulong)REventHandle;
        }

        private KernelResult CreateEvent(out int WEventHandle, out int REventHandle)
        {
            KEvent Event = new KEvent(System);

            KernelResult Result = Process.HandleTable.GenerateHandle(Event.WritableEvent, out WEventHandle);

            if (Result == KernelResult.Success)
            {
                Result = Process.HandleTable.GenerateHandle(Event.ReadableEvent, out REventHandle);

                if (Result != KernelResult.Success)
                {
                    Process.HandleTable.CloseHandle(WEventHandle);
                }
            }
            else
            {
                REventHandle = 0;
            }

            return Result;
        }
    }
}
