using Ryujinx.HLE.OsHle.Ipc;
using System.Collections.Generic;
using System.IO;

namespace Ryujinx.HLE.OsHle.Services.FspSrv
{
    class IStorage : IpcService
    {
        private Dictionary<int, ServiceProcessRequest> m_Commands;

        public override IReadOnlyDictionary<int, ServiceProcessRequest> Commands => m_Commands;

        private Stream BaseStream;

        public IStorage(Stream BaseStream)
        {
            m_Commands = new Dictionary<int, ServiceProcessRequest>()
            {
                { 0, Read }
            };

            this.BaseStream = BaseStream;
        }

        public long Read(ServiceCtx Context)
        {
            long Offset = Context.RequestData.ReadInt64();
            long Size   = Context.RequestData.ReadInt64();

            if (Context.Request.ReceiveBuff.Count > 0)
            {
                IpcBuffDesc BuffDesc = Context.Request.ReceiveBuff[0];

                //Use smaller length to avoid overflows.
                if (Size > BuffDesc.Size)
                {
                    Size = BuffDesc.Size;
                }

                byte[] Data = new byte[Size];

                BaseStream.Seek(Offset, SeekOrigin.Begin);
                BaseStream.Read(Data, 0, Data.Length);

                Context.Memory.WriteBytes(BuffDesc.Position, Data);
            }

            return 0;
        }
    }
}