using Ryujinx.HLE.HOS.Kernel.Common;
using Ryujinx.HLE.HOS.Kernel.Process;
using Ryujinx.HLE.HOS.Services;

namespace Ryujinx.HLE.HOS.Kernel.Ipc
{
    class KClientPort : KSynchronizationObject
    {
        private int _sessionsCount;
        private int _currentCapacity;
        private readonly int _maxSessions;

        private readonly KPort _parent;

        public bool IsLight => _parent.IsLight;

        private readonly object _countIncLock;

        // TODO: Remove that, we need it for now to allow HLE
        // SM implementation to work with the new IPC system.
        public IpcService Service { get; set; }

        public KClientPort(KernelContext context, KPort parent, int maxSessions) : base(context)
        {
            _maxSessions = maxSessions;
            _parent      = parent;

            _countIncLock = new object();
        }

        public KernelResult Connect(out KClientSession clientSession)
        {
            clientSession = null;

            KProcess currentProcess = KernelContext.Scheduler.GetCurrentProcess();

            if (currentProcess.ResourceLimit != null &&
               !currentProcess.ResourceLimit.Reserve(LimitableResource.Session, 1))
            {
                return KernelResult.ResLimitExceeded;
            }

            lock (_countIncLock)
            {
                if (_sessionsCount < _maxSessions)
                {
                    _sessionsCount++;
                }
                else
                {
                    currentProcess.ResourceLimit?.Release(LimitableResource.Session, 1);

                    return KernelResult.SessionCountExceeded;
                }

                if (_currentCapacity < _sessionsCount)
                {
                    _currentCapacity = _sessionsCount;
                }
            }

            KSession session = new KSession(KernelContext);

            if (Service != null)
            {
                session.ClientSession.Service = Service;
            }

            KernelResult result = _parent.EnqueueIncomingSession(session.ServerSession);

            if (result != KernelResult.Success)
            {
                session.ClientSession.DecrementReferenceCount();
                session.ServerSession.DecrementReferenceCount();

                return result;
            }

            clientSession = session.ClientSession;

            return result;
        }

        public KernelResult ConnectLight(out KLightClientSession clientSession)
        {
            clientSession = null;

            KProcess currentProcess = KernelContext.Scheduler.GetCurrentProcess();

            if (currentProcess.ResourceLimit != null &&
               !currentProcess.ResourceLimit.Reserve(LimitableResource.Session, 1))
            {
                return KernelResult.ResLimitExceeded;
            }

            lock (_countIncLock)
            {
                if (_sessionsCount < _maxSessions)
                {
                    _sessionsCount++;
                }
                else
                {
                    currentProcess.ResourceLimit?.Release(LimitableResource.Session, 1);

                    return KernelResult.SessionCountExceeded;
                }
            }

            KLightSession session = new KLightSession(KernelContext);

            KernelResult result = _parent.EnqueueIncomingLightSession(session.ServerSession);

            if (result != KernelResult.Success)
            {
                session.ClientSession.DecrementReferenceCount();
                session.ServerSession.DecrementReferenceCount();

                return result;
            }

            clientSession = session.ClientSession;

            return result;
        }

        public new static KernelResult RemoveName(KernelContext context, string name)
        {
            KAutoObject foundObj = FindNamedObject(context, name);

            if (!(foundObj is KClientPort))
            {
                return KernelResult.NotFound;
            }

            return KAutoObject.RemoveName(context, name);
        }
    }
}