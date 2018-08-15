using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.Logging;
using System.Collections.Generic;

namespace Ryujinx.HLE.HOS.Services.Prepo
{
    class IPrepoService : IpcService
    {
        private Dictionary<int, ServiceProcessRequest> m_Commands;

        public override IReadOnlyDictionary<int, ServiceProcessRequest> Commands => m_Commands;

        public IPrepoService()
        {
            m_Commands = new Dictionary<int, ServiceProcessRequest>()
            {
                { 10101, SaveReportWithUser }
            };
        }

        public static long SaveReportWithUser(ServiceCtx Context)
        {
            Context.Device.Log.PrintStub(LogClass.ServicePrepo, "Stubbed.");

            return 0;
        }
    }
}