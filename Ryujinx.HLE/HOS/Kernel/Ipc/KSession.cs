using Ryujinx.HLE.HOS.Kernel.Common;
using Ryujinx.HLE.HOS.Kernel.Process;
using System;

namespace Ryujinx.HLE.HOS.Kernel.Ipc
{
    class KSession : KAutoObject, IDisposable
    {
        public KServerSession ServerSession { get; }
        public KClientSession ClientSession { get; }

        private bool _hasBeenInitialized;

        public KSession(KernelContext context) : base(context)
        {
            ServerSession = new KServerSession(context, this);
            ClientSession = new KClientSession(context, this);

            _hasBeenInitialized = true;
        }

        public void DisconnectClient()
        {
            if (ClientSession.State == ChannelState.Open)
            {
                ClientSession.State = ChannelState.ClientDisconnected;

                ServerSession.CancelAllRequestsClientDisconnected();
            }
        }

        public void DisconnectServer()
        {
            if (ClientSession.State == ChannelState.Open)
            {
                ClientSession.State = ChannelState.ServerDisconnected;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && ClientSession.Service is IDisposable disposableService)
            {
                disposableService.Dispose();
            }
        }

        protected override void Destroy()
        {
            if (_hasBeenInitialized)
            {
                KProcess creatorProcess = ClientSession.CreatorProcess;

                creatorProcess.ResourceLimit?.Release(LimitableResource.Session, 1);

                creatorProcess.DecrementReferenceCount();
            }
        }
    }
}