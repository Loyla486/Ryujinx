using Ryujinx.HLE.HOS.Ipc;
using System.Collections.Generic;

namespace Ryujinx.HLE.HOS.Services.Am
{
    class ILibraryAppletCreator : IpcService
    {
        private Dictionary<int, ServiceProcessRequest> m_Commands;

        public override IReadOnlyDictionary<int, ServiceProcessRequest> Commands => m_Commands;

        public ILibraryAppletCreator()
        {
            m_Commands = new Dictionary<int, ServiceProcessRequest>()
            {
                { 0,  CreateLibraryApplet },
                { 10, CreateStorage       }
            };
        }

        public long CreateLibraryApplet(ServiceCtx Context)
        {
            MakeObject(Context, new ILibraryAppletAccessor(Context.Device.System));

            return 0;
        }

        public long CreateStorage(ServiceCtx Context)
        {
            long Size = Context.RequestData.ReadInt64();

            MakeObject(Context, new IStorage(new byte[Size]));

            return 0;
        }
    }
}