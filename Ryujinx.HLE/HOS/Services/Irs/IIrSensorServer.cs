﻿using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.HOS.Kernel;
using Ryujinx.HLE.Logging;
using System;
using System.Collections.Generic;

namespace Ryujinx.HLE.HOS.Services.Irs
{
    class IIrSensorServer : IpcService
    {
        private Dictionary<int, ServiceProcessRequest> m_Commands;

        public override IReadOnlyDictionary<int, ServiceProcessRequest> Commands => m_Commands;

        private bool Activated;

        public IIrSensorServer()
        {
            m_Commands = new Dictionary<int, ServiceProcessRequest>()
            {
                { 302, ActivateIrsensor   },
                { 303, DeactivateIrsensor }
            };
        }

        // ActivateIrsensor(nn::applet::AppletResourceUserId, pid)
        public long ActivateIrsensor(ServiceCtx Context)
        {
            long AppletResourceUserId = Context.RequestData.ReadInt64();

            Context.Device.Log.PrintStub(LogClass.ServiceIrs, $"Stubbed. AppletResourceUserId: {AppletResourceUserId}");

            return 0;
        }

        // DeactivateIrsensor(nn::applet::AppletResourceUserId, pid)
        public long DeactivateIrsensor(ServiceCtx Context)
        {
            long AppletResourceUserId = Context.RequestData.ReadInt64();

            Context.Device.Log.PrintStub(LogClass.ServiceIrs, $"Stubbed. AppletResourceUserId: {AppletResourceUserId}");

            return 0;
        }
    }
}