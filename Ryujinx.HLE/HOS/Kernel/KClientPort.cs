namespace Ryujinx.HLE.HOS.Kernel
{
    class KClientPort : KSynchronizationObject
    {
        private int _sessionsCount;
        private int _currentCapacity;
        private int _maxSessions;

        private KPort _parent;

        public KClientPort(Horizon system) : base(system) { }

        public void Initialize(KPort parent, int maxSessions)
        {
            this._maxSessions = maxSessions;
            this._parent      = parent;
        }

        public new static KernelResult RemoveName(Horizon system, string name)
        {
            KAutoObject foundObj = KAutoObject.FindNamedObject(system, name);

            if (!(foundObj is KClientPort))
            {
                return KernelResult.NotFound;
            }

            return KAutoObject.RemoveName(system, name);
        }
    }
}