using Ryujinx.HLE.Logging;
using Ryujinx.HLE.OsHle.Ipc;
using System.Collections.Generic;

namespace Ryujinx.HLE.OsHle.Services.Prepo
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
            Context.Ns.Log.PrintStub(LogClass.ServicePrepo, "Stubbed.");

            return 0;
        }
    }
}