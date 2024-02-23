using ChocolArm64;
using System.Collections.Generic;

namespace Ryujinx.HLE.OsHle.Handles
{
    class KThread : KSynchronizationObject
    {
        public AThread Thread { get; private set; }

        public int CoreMask { get; set; }

        public long MutexAddress   { get; set; }
        public long CondVarAddress { get; set; }

        public bool CondVarSignaled { get; set; }

        private Process Process;

        public List<KThread> MutexWaiters { get; private set; }

        public KThread MutexOwner { get; set; }

        public int ActualPriority { get; private set; }
        public int WantedPriority { get; private set; }

        public int ActualCore  { get; set; }
        public int ProcessorId { get; set; }
        public int IdealCore   { get; set; }

        public int WaitHandle { get; set; }

        public long LastPc { get; set; }

        public int ThreadId => Thread.ThreadId;

        public KThread(
            AThread Thread,
            Process Process,
            int     ProcessorId,
            int     Priority)
        {
            this.Thread      = Thread;
            this.Process     = Process;
            this.ProcessorId = ProcessorId;
            this.IdealCore   = ProcessorId;

            MutexWaiters = new List<KThread>();

            CoreMask = 1 << ProcessorId;

            ActualPriority = WantedPriority = Priority;
        }

        public void SetPriority(int Priority)
        {
            WantedPriority = Priority;

            UpdatePriority();
        }

        public void UpdatePriority()
        {
            bool PriorityChanged;

            lock (Process.ThreadSyncLock)
            {
                int OldPriority = ActualPriority;

                int CurrPriority = WantedPriority;

                foreach (KThread Thread in MutexWaiters)
                {
                    int WantedPriority = Thread.WantedPriority;

                    if (CurrPriority > WantedPriority)
                    {
                        CurrPriority = WantedPriority;
                    }
                }

                PriorityChanged = CurrPriority != OldPriority;

                ActualPriority = CurrPriority;
            }

            if (PriorityChanged)
            {
                Process.Scheduler.Resort(this);

                MutexOwner?.UpdatePriority();
            }
        }
    }
}