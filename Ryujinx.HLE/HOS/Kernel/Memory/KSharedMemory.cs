using Ryujinx.Common;
using Ryujinx.HLE.HOS.Kernel.Common;
using Ryujinx.HLE.HOS.Kernel.Process;

namespace Ryujinx.HLE.HOS.Kernel.Memory
{
    class KSharedMemory : KAutoObject
    {
        private KPageList _pageList;

        private long _ownerPid;

        private MemoryPermission _ownerPermission;
        private MemoryPermission _userPermission;

        public KSharedMemory(
            Horizon          system,
            KPageList        pageList,
            long             ownerPid,
            MemoryPermission ownerPermission,
            MemoryPermission userPermission) : base(system)
        {
            _pageList        = pageList;
            _ownerPid        = ownerPid;
            _ownerPermission = ownerPermission;
            _userPermission  = userPermission;
        }

        public KernelResult MapIntoProcess(
            KMemoryManager   memoryManager,
            ulong            address,
            ulong            size,
            KProcess         process,
            MemoryPermission permission)
        {
            ulong pagesCountRounded = BitUtils.DivRoundUp(size, KMemoryManager.PageSize);

            if (_pageList.GetPagesCount() != pagesCountRounded)
            {
                return KernelResult.InvalidSize;
            }

            MemoryPermission expectedPermission = process.Pid == _ownerPid
                ? _ownerPermission
                : _userPermission;

            if (permission != expectedPermission)
            {
                return KernelResult.InvalidPermission;
            }

            return memoryManager.MapPages(address, _pageList, MemoryState.SharedMemory, permission);
        }

        public KernelResult UnmapFromProcess(
            KMemoryManager   memoryManager,
            ulong            address,
            ulong            size,
            KProcess         process)
        {
            ulong pagesCountRounded = BitUtils.DivRoundUp(size, KMemoryManager.PageSize);

            if (_pageList.GetPagesCount() != pagesCountRounded)
            {
                return KernelResult.InvalidSize;
            }

            return memoryManager.UnmapPages(address, _pageList, MemoryState.SharedMemory);
        }
    }
}