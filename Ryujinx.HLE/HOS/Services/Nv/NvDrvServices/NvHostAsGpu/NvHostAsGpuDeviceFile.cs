﻿using Ryujinx.Common.Collections;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.Gpu.Memory;
using Ryujinx.HLE.HOS.Kernel.Process;
using Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostAsGpu.Types;
using Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvMap;
using Ryujinx.Memory;
using System;
using System.Collections.Concurrent;

namespace Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostAsGpu
{
    class NvHostAsGpuDeviceFile : NvDeviceFile
    {
        private static ConcurrentDictionary<KProcess, AddressSpaceContext> _addressSpaceContextRegistry = new ConcurrentDictionary<KProcess, AddressSpaceContext>();
        private NvMemoryAllocator _memoryAllocator;

        public NvHostAsGpuDeviceFile(ServiceCtx context, IVirtualMemoryManager memory, long owner) : base(context, owner)
        { 
            _memoryAllocator = context.Device.MemoryAllocator; 
        }

        public override NvInternalResult Ioctl(NvIoctl command, Span<byte> arguments)
        {
            NvInternalResult result = NvInternalResult.NotImplemented;

            if (command.Type == NvIoctl.NvGpuAsMagic)
            {
                switch (command.Number)
                {
                    case 0x01:
                        result = CallIoctlMethod<BindChannelArguments>(BindChannel, arguments);
                        break;
                    case 0x02:
                        result = CallIoctlMethod<AllocSpaceArguments>(AllocSpace, arguments);
                        break;
                    case 0x03:
                        result = CallIoctlMethod<FreeSpaceArguments>(FreeSpace, arguments);
                        break;
                    case 0x05:
                        result = CallIoctlMethod<UnmapBufferArguments>(UnmapBuffer, arguments);
                        break;
                    case 0x06:
                        result = CallIoctlMethod<MapBufferExArguments>(MapBufferEx, arguments);
                        break;
                    case 0x08:
                        result = CallIoctlMethod<GetVaRegionsArguments>(GetVaRegions, arguments);
                        break;
                    case 0x09:
                        result = CallIoctlMethod<InitializeExArguments>(InitializeEx, arguments);
                        break;
                    case 0x14:
                        result = CallIoctlMethod<RemapArguments>(Remap, arguments);
                        break;
                }
            }

            return result;
        }

        public override NvInternalResult Ioctl3(NvIoctl command, Span<byte> arguments, Span<byte> inlineOutBuffer)
        {
            NvInternalResult result = NvInternalResult.NotImplemented;

            if (command.Type == NvIoctl.NvGpuAsMagic)
            {
                switch (command.Number)
                {
                    case 0x08:
                        // This is the same as the one in ioctl as inlineOutBuffer is empty.
                        result = CallIoctlMethod<GetVaRegionsArguments>(GetVaRegions, arguments);
                        break;
                }
            }

            return result;
        }

        private NvInternalResult BindChannel(ref BindChannelArguments arguments)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceNv);

            return NvInternalResult.Success;
        }

        private NvInternalResult AllocSpace(ref AllocSpaceArguments arguments)
        {
            AddressSpaceContext addressSpaceContext = GetAddressSpaceContext(Context);

            ulong size = (ulong)arguments.Pages * (ulong)arguments.PageSize;

            NvInternalResult result = NvInternalResult.Success;

            lock (addressSpaceContext)
            {
                // Note: When the fixed offset flag is not set,
                // the Offset field holds the alignment size instead.
                if ((arguments.Flags & AddressSpaceFlags.FixedOffset) != 0)
                {
                    bool regionInUse = _memoryAllocator.IsRegionInUse((ulong)arguments.Offset, size, out ulong freeAddressStartPosition);
                    ulong address;

                    if (!regionInUse)
                    {
                        _memoryAllocator.AllocateRange((ulong)arguments.Offset, size, freeAddressStartPosition);
                        address = freeAddressStartPosition;
                    }
                    else
                    {
                        address = NvMemoryAllocator.PteUnmapped;
                    }

                    arguments.Offset = (long)address;
                }
                else
                {
                    ulong address = _memoryAllocator.GetFreeAddress((ulong)size, out ulong freeAddressStartPosition, (ulong)arguments.Offset);
                    if (address != NvMemoryAllocator.PteUnmapped)
                    {
                        _memoryAllocator.AllocateRange(address, (ulong)size, freeAddressStartPosition);
                    }

                    arguments.Offset = unchecked((long)address);
                }

                if (arguments.Offset < 0)
                {
                    arguments.Offset = 0;

                    Logger.Warning?.Print(LogClass.ServiceNv, $"Failed to allocate size {size:x16}!");

                    result = NvInternalResult.OutOfMemory;
                }
                else
                {
                    addressSpaceContext.AddReservation(arguments.Offset, (long)size);
                }
            }

            return result;
        }

        private NvInternalResult FreeSpace(ref FreeSpaceArguments arguments)
        {
            AddressSpaceContext addressSpaceContext = GetAddressSpaceContext(Context);

            NvInternalResult result = NvInternalResult.Success;

            lock (addressSpaceContext)
            {
                ulong size = (ulong)arguments.Pages * (ulong)arguments.PageSize;

                if (addressSpaceContext.RemoveReservation(arguments.Offset))
                {
                    _memoryAllocator.DeallocateRange((ulong)arguments.Offset, size);
                    addressSpaceContext.Gmm.Free((ulong)arguments.Offset, size);
                }
                else
                {
                    Logger.Warning?.Print(LogClass.ServiceNv,
                        $"Failed to free offset 0x{arguments.Offset:x16} size 0x{size:x16}!");

                    result = NvInternalResult.InvalidInput;
                }
            }

            return result;
        }

        private NvInternalResult UnmapBuffer(ref UnmapBufferArguments arguments)
        {
            AddressSpaceContext addressSpaceContext = GetAddressSpaceContext(Context);

            lock (addressSpaceContext)
            {
                if (addressSpaceContext.RemoveMap(arguments.Offset, out long size))
                {
                    if (size != 0)
                    {
                        _memoryAllocator.DeallocateRange((ulong)arguments.Offset, (ulong)size);
                        addressSpaceContext.Gmm.Free((ulong)arguments.Offset, (ulong)size);
                    }
                }
                else
                {
                    Logger.Warning?.Print(LogClass.ServiceNv, $"Invalid buffer offset {arguments.Offset:x16}!");
                }
            }

            return NvInternalResult.Success;
        }

        private NvInternalResult MapBufferEx(ref MapBufferExArguments arguments)
        {
            const string mapErrorMsg = "Failed to map fixed buffer with offset 0x{0:x16}, size 0x{1:x16} and alignment 0x{2:x16}!";

            AddressSpaceContext addressSpaceContext = GetAddressSpaceContext(Context);

            NvMapHandle map = NvMapDeviceFile.GetMapFromHandle(Owner, arguments.NvMapHandle, true);

            if (map == null)
            {
                Logger.Warning?.Print(LogClass.ServiceNv, $"Invalid NvMap handle 0x{arguments.NvMapHandle:x8}!");

                return NvInternalResult.InvalidInput;
            }

            ulong pageSize = (ulong)arguments.PageSize;

            if (pageSize == 0)
            {
                pageSize = (ulong)map.Align;
            }

            long physicalAddress;

            if ((arguments.Flags & AddressSpaceFlags.RemapSubRange) != 0)
            {
                lock (addressSpaceContext)
                {
                    if (addressSpaceContext.TryGetMapPhysicalAddress(arguments.Offset, out physicalAddress))
                    {
                        long virtualAddress = arguments.Offset + arguments.BufferOffset;

                        physicalAddress += arguments.BufferOffset;
                        addressSpaceContext.Gmm.Map((ulong)physicalAddress, (ulong)virtualAddress, (ulong)arguments.MappingSize);

                        if (virtualAddress < 0)
                        {
                            string message = string.Format(mapErrorMsg, virtualAddress, arguments.MappingSize, pageSize);

                            Logger.Warning?.Print(LogClass.ServiceNv, message);

                            return NvInternalResult.InvalidInput;
                        }

                        return NvInternalResult.Success;
                    }
                    else
                    {
                        Logger.Warning?.Print(LogClass.ServiceNv, $"Address 0x{arguments.Offset:x16} not mapped!");

                        return NvInternalResult.InvalidInput;
                    }
                }
            }

            physicalAddress = map.Address + arguments.BufferOffset;

            long size = arguments.MappingSize;

            if (size == 0)
            {
                size = (uint)map.Size;
            }

            NvInternalResult result = NvInternalResult.Success;

            lock (addressSpaceContext)
            {
                // Note: When the fixed offset flag is not set,
                // the Offset field holds the alignment size instead.
                bool virtualAddressAllocated = (arguments.Flags & AddressSpaceFlags.FixedOffset) == 0;

                if (!virtualAddressAllocated)
                {
                    if (addressSpaceContext.ValidateFixedBuffer(arguments.Offset, size, pageSize))
                    {
                        addressSpaceContext.Gmm.Map((ulong)physicalAddress, (ulong)arguments.Offset, (ulong)size);
                    }
                    else
                    {
                        string message = string.Format(mapErrorMsg, arguments.Offset, size, pageSize);

                        Logger.Warning?.Print(LogClass.ServiceNv, message);

                        result = NvInternalResult.InvalidInput;
                    }
                }
                else
                {
                    ulong va = _memoryAllocator.GetFreeAddress((ulong)size, out ulong freeAddressStartPosition, (ulong) pageSize);
                    if (va != NvMemoryAllocator.PteUnmapped)
                    {
                        _memoryAllocator.AllocateRange(va, (ulong)size, freeAddressStartPosition);
                    }
                    
                    addressSpaceContext.Gmm.Map((ulong)physicalAddress, va, (ulong)size);
                    arguments.Offset = (long)va;
                }

                if (arguments.Offset < 0)
                {
                    arguments.Offset = 0;

                    Logger.Warning?.Print(LogClass.ServiceNv, $"Failed to map size 0x{size:x16}!");

                    result = NvInternalResult.InvalidInput;
                }
                else
                {
                    addressSpaceContext.AddMap(arguments.Offset, size, physicalAddress, virtualAddressAllocated);
                }
            }

            return result;
        }

        private NvInternalResult GetVaRegions(ref GetVaRegionsArguments arguments)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceNv);

            return NvInternalResult.Success;
        }

        private NvInternalResult InitializeEx(ref InitializeExArguments arguments)
        {
            Logger.Stub?.PrintStub(LogClass.ServiceNv);

            return NvInternalResult.Success;
        }

        private NvInternalResult Remap(Span<RemapArguments> arguments)
        {
            for (int index = 0; index < arguments.Length; index++)
            {
                MemoryManager gmm = GetAddressSpaceContext(Context).Gmm;

                NvMapHandle map = NvMapDeviceFile.GetMapFromHandle(Owner, arguments[index].NvMapHandle, true);

                if (map == null)
                {
                    Logger.Warning?.Print(LogClass.ServiceNv, $"Invalid NvMap handle 0x{arguments[index].NvMapHandle:x8}!");

                    return NvInternalResult.InvalidInput;
                }

                long shiftedGpuOffset = (long)((ulong)arguments[index].GpuOffset << 16);

                gmm.Map(
                    ((ulong)arguments[index].MapOffset << 16) + (ulong)map.Address,
                     (ulong)shiftedGpuOffset,
                     (ulong)arguments[index].Pages     << 16);

                if (shiftedGpuOffset < 0)
                {
                    Logger.Warning?.Print(LogClass.ServiceNv,
                        $"Page 0x{arguments[index].GpuOffset:x16} size 0x{arguments[index].Pages:x16} not allocated!");

                    return NvInternalResult.InvalidInput;
                }
            }

            return NvInternalResult.Success;
        }

        public override void Close() { }

        public static AddressSpaceContext GetAddressSpaceContext(ServiceCtx context)
        {
            return _addressSpaceContextRegistry.GetOrAdd(context.Process, (key) => new AddressSpaceContext(context));
        }
    }
}
