﻿namespace Ryujinx.HLE.HOS.Services.Ns
{
    [Service("ns:am")]
    class IApplicationManagerInterface : IpcService
    {
        public IApplicationManagerInterface(ServiceCtx context) { }

        [Command(400)]
        // GetApplicationControlData(u8, u64) -> (unknown<4>, buffer<unknown, 6>)
        public ResultCode GetApplicationControlData(ServiceCtx context)
        {
            byte  source  = (byte)context.RequestData.ReadInt64();
            ulong titleId = context.RequestData.ReadUInt64();

            long position = context.Request.ReceiveBuff[0].Position;

            byte[] nacpData = context.Device.System.ControlData.ByteSpan.ToArray();

            context.Memory.WriteBytes(position, nacpData);

            return ResultCode.Success;
        }
    }
}