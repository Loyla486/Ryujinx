using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.HOS.Kernel.Common;
using Ryujinx.HLE.HOS.Kernel.Threading;
using System;

namespace Ryujinx.HLE.HOS.Services.Am.AppletAE.AllSystemAppletProxiesService.SystemAppletProxy
{
    class ISelfController : IpcService
    {
        private KEvent _libraryAppletLaunchableEvent;

        private KEvent _accumulatedSuspendedTickChangedEvent;
        private int    _accumulatedSuspendedTickChangedEventHandle = 0;

        private object _fatalSectionLock = new object();
        private int    _fatalSectionCount;

        // TODO: Set this when the game goes in suspension (go back to home menu ect), we currently don't support that so we can keep it set to 0.
        private ulong _accumulatedSuspendedTickValue = 0;

        private int _idleTimeDetectionExtension;

        public ISelfController(Horizon system)
        {
            _libraryAppletLaunchableEvent = new KEvent(system.KernelContext);
        }

        [Command(0)]
        // Exit()
        public ResultCode Exit(ServiceCtx context)
        {
            Logger.PrintStub(LogClass.ServiceAm);

            return ResultCode.Success;
        }

        [Command(1)]
        // LockExit()
        public ResultCode LockExit(ServiceCtx context)
        {
            Logger.PrintStub(LogClass.ServiceAm);

            return ResultCode.Success;
        }

        [Command(2)]
        // UnlockExit()
        public ResultCode UnlockExit(ServiceCtx context)
        {
            Logger.PrintStub(LogClass.ServiceAm);

            return ResultCode.Success;
        }

        [Command(3)] // 2.0.0+
        // EnterFatalSection()
        public ResultCode EnterFatalSection(ServiceCtx context)
        {
            lock (_fatalSectionLock)
            {
                _fatalSectionCount++;
            }

            return ResultCode.Success;
        }

        [Command(4)] // 2.0.0+
        // LeaveFatalSection()
        public ResultCode LeaveFatalSection(ServiceCtx context)
        {
            ResultCode result = ResultCode.Success;

            lock (_fatalSectionLock)
            {
                if (_fatalSectionCount != 0)
                {
                    _fatalSectionCount--;
                }
                else
                {
                    result = ResultCode.UnbalancedFatalSection;
                }
            }

            return result;
        }

        [Command(9)]
        // GetLibraryAppletLaunchableEvent() -> handle<copy>
        public ResultCode GetLibraryAppletLaunchableEvent(ServiceCtx context)
        {
            _libraryAppletLaunchableEvent.ReadableEvent.Signal();

            if (context.Process.HandleTable.GenerateHandle(_libraryAppletLaunchableEvent.ReadableEvent, out int handle) != KernelResult.Success)
            {
                throw new InvalidOperationException("Out of handles!");
            }

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(handle);

            Logger.PrintStub(LogClass.ServiceAm);

            return ResultCode.Success;
        }

        [Command(10)]
        // SetScreenShotPermission(u32)
        public ResultCode SetScreenShotPermission(ServiceCtx context)
        {
            bool enable = context.RequestData.ReadByte() != 0;

            Logger.PrintStub(LogClass.ServiceAm);

            return ResultCode.Success;
        }

        [Command(11)]
        // SetOperationModeChangedNotification(b8)
        public ResultCode SetOperationModeChangedNotification(ServiceCtx context)
        {
            bool enable = context.RequestData.ReadByte() != 0;

            Logger.PrintStub(LogClass.ServiceAm);

            return ResultCode.Success;
        }

        [Command(12)]
        // SetPerformanceModeChangedNotification(b8)
        public ResultCode SetPerformanceModeChangedNotification(ServiceCtx context)
        {
            bool enable = context.RequestData.ReadByte() != 0;

            Logger.PrintStub(LogClass.ServiceAm);

            return ResultCode.Success;
        }

        [Command(13)]
        // SetFocusHandlingMode(b8, b8, b8)
        public ResultCode SetFocusHandlingMode(ServiceCtx context)
        {
            bool flag1 = context.RequestData.ReadByte() != 0;
            bool flag2 = context.RequestData.ReadByte() != 0;
            bool flag3 = context.RequestData.ReadByte() != 0;

            Logger.PrintStub(LogClass.ServiceAm);

            return ResultCode.Success;
        }

        [Command(14)]
        // SetRestartMessageEnabled(b8)
        public ResultCode SetRestartMessageEnabled(ServiceCtx context)
        {
            bool enable = context.RequestData.ReadByte() != 0;

            Logger.PrintStub(LogClass.ServiceAm);

            return ResultCode.Success;
        }

        [Command(16)] // 2.0.0+
        // SetOutOfFocusSuspendingEnabled(b8)
        public ResultCode SetOutOfFocusSuspendingEnabled(ServiceCtx context)
        {
            bool enable = context.RequestData.ReadByte() != 0;

            Logger.PrintStub(LogClass.ServiceAm);

            return ResultCode.Success;
        }

        [Command(19)] // 3.0.0+
        public ResultCode SetScreenShotImageOrientation(ServiceCtx context)
        {
            int orientation = context.RequestData.ReadInt32();

            Logger.PrintStub(LogClass.ServiceAm);

            return ResultCode.Success;
        }

        [Command(50)]
        // SetHandlesRequestToDisplay(b8)
        public ResultCode SetHandlesRequestToDisplay(ServiceCtx context)
        {
            bool enable = context.RequestData.ReadByte() != 0;

            Logger.PrintStub(LogClass.ServiceAm);

            return ResultCode.Success;
        }

        [Command(62)]
        // SetIdleTimeDetectionExtension(u32)
        public ResultCode SetIdleTimeDetectionExtension(ServiceCtx context)
        {
            _idleTimeDetectionExtension = context.RequestData.ReadInt32();

            Logger.PrintStub(LogClass.ServiceAm, new { _idleTimeDetectionExtension });

            return ResultCode.Success;
        }

        [Command(63)]
        // GetIdleTimeDetectionExtension() -> u32
        public ResultCode GetIdleTimeDetectionExtension(ServiceCtx context)
        {
            context.ResponseData.Write(_idleTimeDetectionExtension);

            Logger.PrintStub(LogClass.ServiceAm, new { _idleTimeDetectionExtension });

            return ResultCode.Success;
        }

        [Command(90)] // 6.0.0+
        // GetAccumulatedSuspendedTickValue() -> u64
        public ResultCode GetAccumulatedSuspendedTickValue(ServiceCtx context)
        {
            context.ResponseData.Write(_accumulatedSuspendedTickValue);

            return ResultCode.Success;
        }

        [Command(91)] // 6.0.0+
        // GetAccumulatedSuspendedTickChangedEvent() -> handle<copy>
        public ResultCode GetAccumulatedSuspendedTickChangedEvent(ServiceCtx context)
        {
            if (_accumulatedSuspendedTickChangedEventHandle == 0)
            {
                _accumulatedSuspendedTickChangedEvent = new KEvent(context.Device.System.KernelContext);

                _accumulatedSuspendedTickChangedEvent.ReadableEvent.Signal();

                if (context.Process.HandleTable.GenerateHandle(_accumulatedSuspendedTickChangedEvent.ReadableEvent, out _accumulatedSuspendedTickChangedEventHandle) != KernelResult.Success)
                {
                    throw new InvalidOperationException("Out of handles!");
                }
            }

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(_accumulatedSuspendedTickChangedEventHandle);

            return ResultCode.Success;
        }
    }
}