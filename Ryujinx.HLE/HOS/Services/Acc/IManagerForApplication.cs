using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.Logging;
using Ryujinx.HLE.Utilities;
using System.Collections.Generic;

namespace Ryujinx.HLE.HOS.Services.Acc
{
    class IManagerForApplication : IpcService
    {
        private Dictionary<int, ServiceProcessRequest> m_Commands;

        public override IReadOnlyDictionary<int, ServiceProcessRequest> Commands => m_Commands;

        public IManagerForApplication(UInt128 Uuid)
        {
            m_Commands = new Dictionary<int, ServiceProcessRequest>()
            {
                { 0, CheckAvailability },
                { 1, GetAccountId      }
            };
        }

        // CheckAvailability()
        public long CheckAvailability(ServiceCtx Context)
        {
            Context.Device.Log.PrintStub(LogClass.ServiceAcc, "Stubbed.");

            return 0;
        }

        // GetAccountId() -> nn::account::NetworkServiceAccountId
        public long GetAccountId(ServiceCtx Context)
        {
            long NetworkServiceAccountId = 0xcafe;

            Context.Device.Log.PrintStub(LogClass.ServiceAcc, $"Stubbed. NetworkServiceAccountId: {NetworkServiceAccountId}");

            Context.ResponseData.Write(NetworkServiceAccountId);

            return 0;
        }
    }
}