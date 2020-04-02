﻿using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.HOS.Kernel.Common;
using Ryujinx.HLE.HOS.Services.Hid.HidServer;
using Ryujinx.HLE.Input;
using System;

namespace Ryujinx.HLE.HOS.Services.Hid.Irs
{
    [Service("irs")]
    class IIrSensorServer : IpcService
    {
        private int _irsensorSharedMemoryHandle = 0;

        public IIrSensorServer(ServiceCtx context) { }

        [Command(302)]
        // ActivateIrsensor(nn::applet::AppletResourceUserId, pid)
        public ResultCode ActivateIrsensor(ServiceCtx context)
        {
            long appletResourceUserId = context.RequestData.ReadInt64();

            Logger.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId });

            return ResultCode.Success;
        }

        [Command(303)]
        // DeactivateIrsensor(nn::applet::AppletResourceUserId, pid)
        public ResultCode DeactivateIrsensor(ServiceCtx context)
        {
            long appletResourceUserId = context.RequestData.ReadInt64();

            Logger.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId });

            return ResultCode.Success;
        }

        [Command(304)]
        // GetIrsensorSharedMemoryHandle(nn::applet::AppletResourceUserId, pid) -> handle<copy>
        public ResultCode GetIrsensorSharedMemoryHandle(ServiceCtx context)
        {
            if (_irsensorSharedMemoryHandle == 0)
            {
                if (context.Process.HandleTable.GenerateHandle(context.Device.System.IirsSharedMem, out _irsensorSharedMemoryHandle) != KernelResult.Success)
                {
                    throw new InvalidOperationException("Out of handles!");
                }
            }

            context.Response.HandleDesc = IpcHandleDesc.MakeCopy(_irsensorSharedMemoryHandle);

            return ResultCode.Success;
        }

        [Command(311)]
        // GetNpadIrCameraHandle(u32) -> nn::irsensor::IrCameraHandle
        public ResultCode GetNpadIrCameraHandle(ServiceCtx context)
        {
            HidNpadIdType npadIdType = (HidNpadIdType)context.RequestData.ReadUInt32();

            if (npadIdType >  HidNpadIdType.Player8 && 
                npadIdType != HidNpadIdType.Unknown && 
                npadIdType != HidNpadIdType.Handheld)
            {
                return ResultCode.NpadIdOutOfRange;
            }

            HidControllerID irCameraHandle = HidUtils.GetIndexFromNpadIdType(npadIdType);

            context.ResponseData.Write((int)irCameraHandle);

            // NOTE: If the irCameraHandle pointer is null this error is returned, Doesn't occur in our case. 
            //       return ResultCode.HandlePointerIsNull;

            return ResultCode.Success;
        }

        [Command(319)] // 4.0.0+
        // ActivateIrsensorWithFunctionLevel(nn::applet::AppletResourceUserId, nn::irsensor::PackedFunctionLevel, pid)
        public ResultCode ActivateIrsensorWithFunctionLevel(ServiceCtx context)
        {
            long appletResourceUserId = context.RequestData.ReadInt64();
            long packedFunctionLevel  = context.RequestData.ReadInt64();

            Logger.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId, packedFunctionLevel });

            return ResultCode.Success;
        }
    }
}