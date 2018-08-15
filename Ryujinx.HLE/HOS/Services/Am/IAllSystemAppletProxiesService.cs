using Ryujinx.HLE.HOS.Ipc;
using System.Collections.Generic;

namespace Ryujinx.HLE.HOS.Services.Am
{
    class IAllSystemAppletProxiesService : IpcService
    {
        private Dictionary<int, ServiceProcessRequest> m_Commands;

        public override IReadOnlyDictionary<int, ServiceProcessRequest> Commands => m_Commands;

        public IAllSystemAppletProxiesService()
        {
            m_Commands = new Dictionary<int, ServiceProcessRequest>()
            {
                { 100, OpenSystemAppletProxy }
            };
        }

        public long OpenSystemAppletProxy(ServiceCtx Context)
        {
            MakeObject(Context, new ISystemAppletProxy());

            return 0;
        }
    }
}