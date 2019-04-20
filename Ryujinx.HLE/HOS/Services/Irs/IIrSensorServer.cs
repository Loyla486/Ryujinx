﻿using Ryujinx.Common.Logging;
using Ryujinx.HLE.Exceptions;
using Ryujinx.HLE.HOS.Ipc;
using System;
using System.Collections.Generic;

namespace Ryujinx.HLE.HOS.Services.Irs
{
    class IIrSensorServer : IpcService
    {
        private Dictionary<int, ServiceProcessRequest> _commands;

        public override IReadOnlyDictionary<int, ServiceProcessRequest> Commands => _commands;

        private bool _activated;

        public IIrSensorServer()
        {
            _commands = new Dictionary<int, ServiceProcessRequest>
            {
                { 302, ActivateIrsensor      },
                { 303, DeactivateIrsensor    },
                { 311, GetNpadIrCameraHandle }
            };
        }

        // ActivateIrsensor(nn::applet::AppletResourceUserId, pid)
        public long ActivateIrsensor(ServiceCtx context)
        {
            long appletResourceUserId = context.RequestData.ReadInt64();

            Logger.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId });

            return 0;
        }

        // DeactivateIrsensor(nn::applet::AppletResourceUserId, pid)
        public long DeactivateIrsensor(ServiceCtx context)
        {
            long appletResourceUserId = context.RequestData.ReadInt64();

            Logger.PrintStub(LogClass.ServiceIrs, new { appletResourceUserId });

            return 0;
        }

        public long GetNpadIrCameraHandle(ServiceCtx context)
        {
            uint npadId = context.RequestData.ReadUInt32();

            if(npadId > 0x20 || ((1 << (int)npadId) & 0x1000100FF) == 0)
            {
                return ErrorCode.MakeError(ErrorModule.Hid, 0x2c5);
            }

            int npadTypeId = GetNpadTypeId(npadId);

            context.ResponseData.Write(npadTypeId);

            return 0;
        }

        private int GetNpadTypeId(uint npadId)
        {
            switch(npadId)
            {
                case 0:  return 0;
                case 1:  return 1;
                case 2:  return 2;
                case 3:  return 3;
                case 4:  return 4;
                case 5:  return 5;
                case 6:  return 6;
                case 7:  return 7;
                case 32: return 8;
                case 16: return 9;
                default: throw new InvalidOperationException("Invalid NpadId");
            }
        }
    }
}