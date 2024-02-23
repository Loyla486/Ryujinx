using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.Logging;
using System;
using System.Collections.Generic;

namespace Ryujinx.HLE.HOS.Services.Mm
{
    class IRequest : IpcService
    {
        private Dictionary<int, ServiceProcessRequest> m_Commands;

        public override IReadOnlyDictionary<int, ServiceProcessRequest> Commands => m_Commands;

        public IRequest()
        {
            m_Commands = new Dictionary<int, ServiceProcessRequest>()
            {
                { 1, InitializeOld },
                { 4, Initialize    },
                { 6, SetAndWait    },
                { 7, Get           }
            };
        }

        // InitializeOld(u32, u32, u32)
        public long InitializeOld(ServiceCtx Context)
        {
            int Unknown0 = Context.RequestData.ReadInt32();
            int Unknown1 = Context.RequestData.ReadInt32();
            int Unknown2 = Context.RequestData.ReadInt32();

            Context.Device.Log.PrintStub(LogClass.ServiceMm, "Stubbed.");

            return 0;
        }

        public long Initialize(ServiceCtx Context)
        {
            Context.Device.Log.PrintStub(LogClass.ServiceMm, "Stubbed.");

            return 0;
        }

        public long SetAndWait(ServiceCtx Context)
        {
            Context.Device.Log.PrintStub(LogClass.ServiceMm, "Stubbed.");

            return 0;
        }

        public long Get(ServiceCtx Context)
        {
            Context.ResponseData.Write(0);

            Context.Device.Log.PrintStub(LogClass.ServiceMm, "Stubbed.");

            return 0;
        }
    }
}