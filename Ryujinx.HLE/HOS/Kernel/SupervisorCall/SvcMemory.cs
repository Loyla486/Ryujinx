using Ryujinx.HLE.HOS.Kernel.Common;
using Ryujinx.HLE.HOS.Kernel.Memory;
using Ryujinx.HLE.HOS.Kernel.Process;

namespace Ryujinx.HLE.HOS.Kernel.SupervisorCall
{
    partial class SvcHandler
    {
        public KernelResult SetHeapSize64([R(1)] ulong size, [R(1)] out ulong position)
        {
            return SetHeapSize(size, out position);
        }

        private KernelResult SetHeapSize(ulong size, out ulong position)
        {
            if ((size & 0xfffffffe001fffff) != 0)
            {
                position = 0;

                return KernelResult.InvalidSize;
            }

            return _process.MemoryManager.SetHeapSize(size, out position);
        }

        public KernelResult SetMemoryAttribute64(
            [R(0)] ulong           position,
            [R(1)] ulong           size,
            [R(2)] MemoryAttribute attributeMask,
            [R(3)] MemoryAttribute attributeValue)
        {
            return SetMemoryAttribute(position, size, attributeMask, attributeValue);
        }

        private KernelResult SetMemoryAttribute(
            ulong           position,
            ulong           size,
            MemoryAttribute attributeMask,
            MemoryAttribute attributeValue)
        {
            if (!PageAligned(position))
            {
                return KernelResult.InvalidAddress;
            }

            if (!PageAligned(size) || size == 0)
            {
                return KernelResult.InvalidSize;
            }

            MemoryAttribute attributes = attributeMask | attributeValue;

            if (attributes != attributeMask ||
               (attributes | MemoryAttribute.Uncached) != MemoryAttribute.Uncached)
            {
                return KernelResult.InvalidCombination;
            }

            KernelResult result = _process.MemoryManager.SetMemoryAttribute(
                position,
                size,
                attributeMask,
                attributeValue);

            return result;
        }

        public KernelResult MapMemory64([R(0)] ulong dst, [R(1)] ulong src, [R(2)] ulong size)
        {
            return MapMemory(dst, src, size);
        }

        private KernelResult MapMemory(ulong dst, ulong src, ulong size)
        {
            if (!PageAligned(src | dst))
            {
                return KernelResult.InvalidAddress;
            }

            if (!PageAligned(size) || size == 0)
            {
                return KernelResult.InvalidSize;
            }

            if (src + size <= src || dst + size <= dst)
            {
                return KernelResult.InvalidMemState;
            }

            KProcess currentProcess = _system.Scheduler.GetCurrentProcess();

            if (!currentProcess.MemoryManager.InsideAddrSpace(src, size))
            {
                return KernelResult.InvalidMemState;
            }

            if (currentProcess.MemoryManager.OutsideStackRegion(dst, size) ||
                currentProcess.MemoryManager.InsideHeapRegion  (dst, size) ||
                currentProcess.MemoryManager.InsideAliasRegion (dst, size))
            {
                return KernelResult.InvalidMemRange;
            }

            return _process.MemoryManager.Map(dst, src, size);
        }

        public KernelResult UnmapMemory64([R(0)] ulong dst, [R(1)] ulong src, [R(2)] ulong size)
        {
            return UnmapMemory(dst, src, size);
        }

        private KernelResult UnmapMemory(ulong dst, ulong src, ulong size)
        {
            if (!PageAligned(src | dst))
            {
                return KernelResult.InvalidAddress;
            }

            if (!PageAligned(size) || size == 0)
            {
                return KernelResult.InvalidSize;
            }

            if (src + size <= src || dst + size <= dst)
            {
                return KernelResult.InvalidMemState;
            }

            KProcess currentProcess = _system.Scheduler.GetCurrentProcess();

            if (!currentProcess.MemoryManager.InsideAddrSpace(src, size))
            {
                return KernelResult.InvalidMemState;
            }

            if (currentProcess.MemoryManager.OutsideStackRegion(dst, size) ||
                currentProcess.MemoryManager.InsideHeapRegion  (dst, size) ||
                currentProcess.MemoryManager.InsideAliasRegion (dst, size))
            {
                return KernelResult.InvalidMemRange;
            }

            return _process.MemoryManager.Unmap(dst, src, size);
        }

        public KernelResult QueryMemory64([R(0)] ulong infoPtr, [R(2)] ulong position, [R(1)] out ulong pageInfo)
        {
            return QueryMemory(infoPtr, position, out pageInfo);
        }

        private KernelResult QueryMemory(ulong infoPtr, ulong position, out ulong pageInfo)
        {
            KMemoryInfo blkInfo = _process.MemoryManager.QueryMemory(position);

            _process.CpuMemory.WriteUInt64((long)infoPtr + 0x00, blkInfo.Address);
            _process.CpuMemory.WriteUInt64((long)infoPtr + 0x08, blkInfo.Size);
            _process.CpuMemory.WriteInt32 ((long)infoPtr + 0x10, (int)blkInfo.State & 0xff);
            _process.CpuMemory.WriteInt32 ((long)infoPtr + 0x14, (int)blkInfo.Attribute);
            _process.CpuMemory.WriteInt32 ((long)infoPtr + 0x18, (int)blkInfo.Permission);
            _process.CpuMemory.WriteInt32 ((long)infoPtr + 0x1c, blkInfo.IpcRefCount);
            _process.CpuMemory.WriteInt32 ((long)infoPtr + 0x20, blkInfo.DeviceRefCount);
            _process.CpuMemory.WriteInt32 ((long)infoPtr + 0x24, 0);

            pageInfo = 0;

            return KernelResult.Success;
        }

        public KernelResult MapSharedMemory64([R(0)] int handle, [R(1)] ulong address, [R(2)] ulong size, [R(3)] MemoryPermission permission)
        {
            return MapSharedMemory(handle, address, size, permission);
        }

        private KernelResult MapSharedMemory(int handle, ulong address, ulong size, MemoryPermission permission)
        {
            if (!PageAligned(address))
            {
                return KernelResult.InvalidAddress;
            }

            if (!PageAligned(size) || size == 0)
            {
                return KernelResult.InvalidSize;
            }

            if (address + size <= address)
            {
                return KernelResult.InvalidMemState;
            }

            if ((permission | MemoryPermission.Write) != MemoryPermission.ReadAndWrite)
            {
                return KernelResult.InvalidPermission;
            }

            KProcess currentProcess = _system.Scheduler.GetCurrentProcess();

            KSharedMemory sharedMemory = currentProcess.HandleTable.GetObject<KSharedMemory>(handle);

            if (sharedMemory == null)
            {
                return KernelResult.InvalidHandle;
            }

            if (currentProcess.MemoryManager.IsInvalidRegion  (address, size) ||
                currentProcess.MemoryManager.InsideHeapRegion (address, size) ||
                currentProcess.MemoryManager.InsideAliasRegion(address, size))
            {
                return KernelResult.InvalidMemRange;
            }

            return sharedMemory.MapIntoProcess(
                currentProcess.MemoryManager,
                address,
                size,
                currentProcess,
                permission);
        }

        public KernelResult UnmapSharedMemory64([R(0)] int handle, [R(1)] ulong address, [R(2)] ulong size)
        {
            return UnmapSharedMemory(handle, address, size);
        }

        private KernelResult UnmapSharedMemory(int handle, ulong address, ulong size)
        {
            if (!PageAligned(address))
            {
                return KernelResult.InvalidAddress;
            }

            if (!PageAligned(size) || size == 0)
            {
                return KernelResult.InvalidSize;
            }

            if (address + size <= address)
            {
                return KernelResult.InvalidMemState;
            }

            KProcess currentProcess = _system.Scheduler.GetCurrentProcess();

            KSharedMemory sharedMemory = currentProcess.HandleTable.GetObject<KSharedMemory>(handle);

            if (sharedMemory == null)
            {
                return KernelResult.InvalidHandle;
            }

            if (currentProcess.MemoryManager.IsInvalidRegion  (address, size) ||
                currentProcess.MemoryManager.InsideHeapRegion (address, size) ||
                currentProcess.MemoryManager.InsideAliasRegion(address, size))
            {
                return KernelResult.InvalidMemRange;
            }

            return sharedMemory.UnmapFromProcess(
                currentProcess.MemoryManager,
                address,
                size,
                currentProcess);
        }

        public KernelResult CreateTransferMemory64(
            [R(1)] ulong            address,
            [R(2)] ulong            size,
            [R(3)] MemoryPermission permission,
            [R(1)] out int          handle)
        {
            return CreateTransferMemory(address, size, permission, out handle);
        }

        private KernelResult CreateTransferMemory(ulong address, ulong size, MemoryPermission permission, out int handle)
        {
            handle = 0;

            if (!PageAligned(address))
            {
                return KernelResult.InvalidAddress;
            }

            if (!PageAligned(size) || size == 0)
            {
                return KernelResult.InvalidSize;
            }

            if (address + size <= address)
            {
                return KernelResult.InvalidMemState;
            }

            if (permission > MemoryPermission.ReadAndWrite || permission == MemoryPermission.Write)
            {
                return KernelResult.InvalidPermission;
            }

            KernelResult result = _process.MemoryManager.ReserveTransferMemory(address, size, permission);

            if (result != KernelResult.Success)
            {
                return result;
            }

            KTransferMemory transferMemory = new KTransferMemory(_system, address, size);

            return _process.HandleTable.GenerateHandle(transferMemory, out handle);
        }

        public KernelResult MapPhysicalMemory64([R(0)] ulong address, [R(1)] ulong size)
        {
            return MapPhysicalMemory(address, size);
        }

        private KernelResult MapPhysicalMemory(ulong address, ulong size)
        {
            if (!PageAligned(address))
            {
                return KernelResult.InvalidAddress;
            }

            if (!PageAligned(size) || size == 0)
            {
                return KernelResult.InvalidSize;
            }

            if (address + size <= address)
            {
                return KernelResult.InvalidMemRange;
            }

            KProcess currentProcess = _system.Scheduler.GetCurrentProcess();

            if ((currentProcess.PersonalMmHeapPagesCount & 0xfffffffffffff) == 0)
            {
                return KernelResult.InvalidState;
            }

            if (!currentProcess.MemoryManager.InsideAddrSpace   (address, size) ||
                 currentProcess.MemoryManager.OutsideAliasRegion(address, size))
            {
                return KernelResult.InvalidMemRange;
            }

            return _process.MemoryManager.MapPhysicalMemory(address, size);
        }

        public KernelResult UnmapPhysicalMemory64([R(0)] ulong address, [R(1)] ulong size)
        {
            return UnmapPhysicalMemory(address, size);
        }

        private KernelResult UnmapPhysicalMemory(ulong address, ulong size)
        {
            if (!PageAligned(address))
            {
                return KernelResult.InvalidAddress;
            }

            if (!PageAligned(size) || size == 0)
            {
                return KernelResult.InvalidSize;
            }

            if (address + size <= address)
            {
                return KernelResult.InvalidMemRange;
            }

            KProcess currentProcess = _system.Scheduler.GetCurrentProcess();

            if ((currentProcess.PersonalMmHeapPagesCount & 0xfffffffffffff) == 0)
            {
                return KernelResult.InvalidState;
            }

            if (!currentProcess.MemoryManager.InsideAddrSpace   (address, size) ||
                 currentProcess.MemoryManager.OutsideAliasRegion(address, size))
            {
                return KernelResult.InvalidMemRange;
            }

            return _process.MemoryManager.UnmapPhysicalMemory(address, size);
        }

        public KernelResult MapProcessCodeMemory64([R(0)] int handle, [R(1)] ulong dst, [R(2)] ulong src, [R(3)] ulong size)
        {
            return MapProcessCodeMemory(handle, dst, src, size);
        }

        public KernelResult MapProcessCodeMemory(int handle, ulong dst, ulong src, ulong size)
        {
            if (!PageAligned(dst) || !PageAligned(src))
            {
                return KernelResult.InvalidAddress;
            }

            if (!PageAligned(size) || size == 0)
            {
                return KernelResult.InvalidSize;
            }

            KProcess currentProcess = _system.Scheduler.GetCurrentProcess();

            KProcess targetProcess = currentProcess.HandleTable.GetObject<KProcess>(handle);

            if (targetProcess == null)
            {
                return KernelResult.InvalidHandle;
            }

            if (targetProcess.MemoryManager.OutsideAddrSpace(dst, size) ||
                targetProcess.MemoryManager.OutsideAddrSpace(src, size) ||
                targetProcess.MemoryManager.InsideAliasRegion(dst, size) ||
                targetProcess.MemoryManager.InsideHeapRegion(dst, size))
            {
                return KernelResult.InvalidMemRange;
            }

            if (size + dst <= dst || size + src <= src)
            {
                return KernelResult.InvalidMemState;
            }

            return targetProcess.MemoryManager.MapProcessCodeMemory(dst, src, size);
        }

        public KernelResult UnmapProcessCodeMemory64([R(0)] int handle, [R(1)] ulong dst, [R(2)] ulong src, [R(3)] ulong size)
        {
            return UnmapProcessCodeMemory(handle, dst, src, size);
        }

        public KernelResult UnmapProcessCodeMemory(int handle, ulong dst, ulong src, ulong size)
        {
            if (!PageAligned(dst) || !PageAligned(src))
            {
                return KernelResult.InvalidAddress;
            }

            if (!PageAligned(size) || size == 0)
            {
                return KernelResult.InvalidSize;
            }

            KProcess currentProcess = _system.Scheduler.GetCurrentProcess();

            KProcess targetProcess = currentProcess.HandleTable.GetObject<KProcess>(handle);

            if (targetProcess == null)
            {
                return KernelResult.InvalidHandle;
            }

            if (targetProcess.MemoryManager.OutsideAddrSpace(dst, size) ||
                targetProcess.MemoryManager.OutsideAddrSpace(src, size) ||
                targetProcess.MemoryManager.InsideAliasRegion(dst, size) ||
                targetProcess.MemoryManager.InsideHeapRegion(dst, size))
            {
                return KernelResult.InvalidMemRange;
            }

            if (size + dst <= dst || size + src <= src)
            {
                return KernelResult.InvalidMemState;
            }

            return targetProcess.MemoryManager.UnmapProcessCodeMemory(dst, src, size);
        }

        public KernelResult SetProcessMemoryPermission64([R(0)] int handle, [R(1)] ulong src, [R(2)] ulong size, [R(3)] MemoryPermission permission)
        {
            return SetProcessMemoryPermission(handle, src, size, permission);
        }

        public KernelResult SetProcessMemoryPermission(int handle, ulong src, ulong size, MemoryPermission permission)
        {
            if (!PageAligned(src))
            {
                return KernelResult.InvalidAddress;
            }

            if (!PageAligned(size) || size == 0)
            {
                return KernelResult.InvalidSize;
            }

            if (permission != MemoryPermission.None &&
                permission != MemoryPermission.Read &&
                permission != MemoryPermission.ReadAndWrite &&
                permission != MemoryPermission.ReadAndExecute)
            {
                return KernelResult.InvalidPermission;
            }

            KProcess currentProcess = _system.Scheduler.GetCurrentProcess();

            KProcess targetProcess = currentProcess.HandleTable.GetObject<KProcess>(handle);

            if (targetProcess == null)
            {
                return KernelResult.InvalidHandle;
            }

            if (targetProcess.MemoryManager.OutsideAddrSpace(src, size))
            {
                return KernelResult.InvalidMemState;
            }

            return targetProcess.MemoryManager.SetProcessMemoryPermission(src, size, permission);
        }

        private static bool PageAligned(ulong position)
        {
            return (position & (KMemoryManager.PageSize - 1)) == 0;
        }
    }
}