namespace Ryujinx.HLE.HOS.Kernel
{
    class KReadableEvent : KSynchronizationObject
    {
        private KEvent _parent;

        private bool _signaled;

        public KReadableEvent(Horizon system, KEvent parent) : base(system)
        {
            this._parent = parent;
        }

        public override void Signal()
        {
            System.CriticalSection.Enter();

            if (!_signaled)
            {
                _signaled = true;

                base.Signal();
            }

            System.CriticalSection.Leave();
        }

        public KernelResult Clear()
        {
            _signaled = false;

            return KernelResult.Success;
        }

        public KernelResult ClearIfSignaled()
        {
            KernelResult result;

            System.CriticalSection.Enter();

            if (_signaled)
            {
                _signaled = false;

                result = KernelResult.Success;
            }
            else
            {
                result = KernelResult.InvalidState;
            }

            System.CriticalSection.Leave();

            return result;
        }

        public override bool IsSignaled()
        {
            return _signaled;
        }
    }
}