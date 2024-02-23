using System.Threading;

namespace Ryujinx.HLE.HOS.Services.Nv.NvMap
{
    class NvMapHandle
    {
        public int  Handle;
        public int  Id;
        public int  Size;
        public int  Align;
        public int  Kind;
        public long Address;
        public bool Allocated;
        public long DmaMapAddress;

        private long Dupes;

        public NvMapHandle()
        {
            Dupes = 1;
        }

        public NvMapHandle(int Size) : this()
        {
            this.Size = Size;
        }

        public void IncrementRefCount()
        {
            Interlocked.Increment(ref Dupes);
        }

        public long DecrementRefCount()
        {
            return Interlocked.Decrement(ref Dupes);
        }
    }
}