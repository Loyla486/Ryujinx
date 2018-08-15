using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.Logging;
using System.Collections.Generic;

namespace Ryujinx.HLE.HOS.Services.Ns
{
    class IAddOnContentManager : IpcService
    {
        private Dictionary<int, ServiceProcessRequest> m_Commands;

        public override IReadOnlyDictionary<int, ServiceProcessRequest> Commands => m_Commands;

        public IAddOnContentManager()
        {
            m_Commands = new Dictionary<int, ServiceProcessRequest>()
            {
                { 2, CountAddOnContent },
                { 3, ListAddOnContent  }
            };
        }

        public static long CountAddOnContent(ServiceCtx Context)
        {
            Context.ResponseData.Write(0);

            Context.Device.Log.PrintStub(LogClass.ServiceNs, "Stubbed.");

            return 0;
        }

        public static long ListAddOnContent(ServiceCtx Context)
        {
            Context.Device.Log.PrintStub(LogClass.ServiceNs, "Stubbed.");

            //TODO: This is supposed to write a u32 array aswell.
            //It's unknown what it contains.
            Context.ResponseData.Write(0);

            return 0;
        }
    }
}