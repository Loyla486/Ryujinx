using Ryujinx.Memory;
using Ryujinx.Memory.Range;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ryujinx.Graphics.Gpu.Memory
{
    /// <summary>
    /// GPU memory manager.
    /// </summary>
    public class MemoryManager : IWritableBlock, IDisposable
    {
        private const int PtLvl0Bits = 14;
        private const int PtLvl1Bits = 14;
        public  const int PtPageBits = 12;

        private const ulong PtLvl0Size = 1UL << PtLvl0Bits;
        private const ulong PtLvl1Size = 1UL << PtLvl1Bits;
        public  const ulong PageSize   = 1UL << PtPageBits;

        private const ulong PtLvl0Mask = PtLvl0Size - 1;
        private const ulong PtLvl1Mask = PtLvl1Size - 1;
        public  const ulong PageMask   = PageSize   - 1;

        private const int PtLvl0Bit = PtPageBits + PtLvl1Bits;
        private const int PtLvl1Bit = PtPageBits;
        private const int AddressSpaceBits = PtPageBits + PtLvl1Bits + PtLvl0Bits;

        public const ulong PteUnmapped = ulong.MaxValue;

        private readonly ulong[][] _pageTable;
        private readonly GpuContext _context;

        public event EventHandler<UnmapEventArgs> MemoryUnmapped;

        /// <summary>
        /// Physical memory where the virtual memory is mapped into.
        /// </summary>
        internal PhysicalMemory Physical { get; }

        /// <summary>
        /// Cache of GPU buffers, cached by GPU virtual memory regions.
        /// </summary>
        internal VirtualBufferCache VirtualBufferCache { get; }

        /// <summary>
        /// Cache of GPU counters.
        /// </summary>
        internal CounterCache CounterCache { get; }

        /// <summary>
        /// Creates a new instance of the GPU memory manager.
        /// </summary>
        /// <param name="context">GPU context that the memory manager belongs to</param>
        /// <param name="physicalMemory">Physical memory that this memory manager will map into</param>
        internal MemoryManager(GpuContext context, PhysicalMemory physicalMemory)
        {
            _context = context;
            Physical = physicalMemory;
            VirtualBufferCache = new VirtualBufferCache(context, this);
            CounterCache = new CounterCache();
            _pageTable = new ulong[PtLvl0Size][];
            MemoryUnmapped += Physical.TextureCache.MemoryUnmappedHandler;
            MemoryUnmapped += VirtualBufferCache.MemoryUnmappedHandler;
            MemoryUnmapped += CounterCache.MemoryUnmappedHandler;
        }

        /// <summary>
        /// Reads data from GPU mapped memory.
        /// </summary>
        /// <typeparam name="T">Type of the data</typeparam>
        /// <param name="va">GPU virtual address where the data is located</param>
        /// <param name="tracked">True if read tracking is triggered on the memory region</param>
        /// <returns>The data at the specified memory location</returns>
        public T Read<T>(ulong va, bool tracked = false) where T : unmanaged
        {
            int size = Unsafe.SizeOf<T>();

            if (IsContiguous(va, size))
            {
                ulong address = Translate(va);

                if (tracked)
                {
                    return Physical.ReadTracked<T>(address);
                }
                else
                {
                    return Physical.Read<T>(address);
                }
            }
            else
            {
                Span<byte> data = new byte[size];

                ReadImpl(va, data, tracked);

                return MemoryMarshal.Cast<byte, T>(data)[0];
            }
        }

        /// <summary>
        /// Gets a read-only span of data from GPU mapped memory.
        /// </summary>
        /// <param name="va">GPU virtual address where the data is located</param>
        /// <param name="size">Size of the data</param>
        /// <param name="tracked">True if read tracking is triggered on the span</param>
        /// <returns>The span of the data at the specified memory location</returns>
        public ReadOnlySpan<byte> GetSpan(ulong va, int size, bool tracked = false)
        {
            if (IsContiguous(va, size))
            {
                return Physical.GetSpan(Translate(va), size, tracked);
            }
            else
            {
                Span<byte> data = new byte[size];

                ReadImpl(va, data, tracked);

                return data;
            }
        }

        /// <summary>
        /// Gets a read-only span of data from GPU mapped memory, up to the entire range specified,
        /// or the last mapped page if the range is not fully mapped.
        /// </summary>
        /// <param name="va">GPU virtual address where the data is located</param>
        /// <param name="size">Size of the data</param>
        /// <param name="tracked">True if read tracking is triggered on the span</param>
        /// <returns>The span of the data at the specified memory location</returns>
        public ReadOnlySpan<byte> GetSpanMapped(ulong va, int size, bool tracked = false)
        {
            bool isContiguous = true;
            int mappedSize;

            if (ValidateAddress(va) && GetPte(va) != PteUnmapped && Physical.IsMapped(Translate(va)))
            {
                ulong endVa = va + (ulong)size;
                ulong endVaAligned = (endVa + PageMask) & ~PageMask;
                ulong currentVa = va & ~PageMask;

                int pages = (int)((endVaAligned - currentVa) / PageSize);

                for (int page = 0; page < pages - 1; page++)
                {
                    ulong nextVa = currentVa + PageSize;
                    ulong nextPa = Translate(nextVa);

                    if (!ValidateAddress(nextVa) || GetPte(nextVa) == PteUnmapped || !Physical.IsMapped(nextPa))
                    {
                        break;
                    }

                    if (Translate(currentVa) + PageSize != nextPa)
                    {
                        isContiguous = false;
                    }

                    currentVa += PageSize;
                }

                currentVa += PageSize;

                if (currentVa > endVa)
                {
                    currentVa = endVa;
                }

                mappedSize = (int)(currentVa - va);
            }
            else
            {
                return ReadOnlySpan<byte>.Empty;
            }

            if (isContiguous)
            {
                return Physical.GetSpan(Translate(va), mappedSize, tracked);
            }
            else
            {
                Span<byte> data = new byte[mappedSize];

                ReadImpl(va, data, tracked);

                return data;
            }
        }

        /// <summary>
        /// Reads data from a possibly non-contiguous region of GPU mapped memory.
        /// </summary>
        /// <param name="va">GPU virtual address of the data</param>
        /// <param name="data">Span to write the read data into</param>
        /// <param name="tracked">True to enable write tracking on read, false otherwise</param>
        private void ReadImpl(ulong va, Span<byte> data, bool tracked)
        {
            if (data.Length == 0)
            {
                return;
            }

            int offset = 0, size;

            if ((va & PageMask) != 0)
            {
                ulong pa = Translate(va);

                size = Math.Min(data.Length, (int)PageSize - (int)(va & PageMask));

                Physical.GetSpan(pa, size, tracked).CopyTo(data.Slice(0, size));

                offset += size;
            }

            for (; offset < data.Length; offset += size)
            {
                ulong pa = Translate(va + (ulong)offset);

                size = Math.Min(data.Length - offset, (int)PageSize);

                Physical.GetSpan(pa, size, tracked).CopyTo(data.Slice(offset, size));
            }
        }

        /// <summary>
        /// Gets a writable region from GPU mapped memory.
        /// </summary>
        /// <param name="va">Start address of the range</param>
        /// <param name="size">Size in bytes to be range</param>
        /// <param name="tracked">True if write tracking is triggered on the span</param>
        /// <returns>A writable region with the data at the specified memory location</returns>
        public WritableRegion GetWritableRegion(ulong va, int size, bool tracked = false)
        {
            if (IsContiguous(va, size))
            {
                return Physical.GetWritableRegion(Translate(va), size, tracked);
            }
            else
            {
                Memory<byte> memory = new byte[size];

                GetSpan(va, size).CopyTo(memory.Span);

                return new WritableRegion(this, va, memory, tracked);
            }
        }

        /// <summary>
        /// Writes data to GPU mapped memory.
        /// </summary>
        /// <typeparam name="T">Type of the data</typeparam>
        /// <param name="va">GPU virtual address to write the value into</param>
        /// <param name="value">The value to be written</param>
        public void Write<T>(ulong va, T value) where T : unmanaged
        {
            Write(va, MemoryMarshal.Cast<T, byte>(MemoryMarshal.CreateSpan(ref value, 1)));
        }

        /// <summary>
        /// Writes data to GPU mapped memory.
        /// </summary>
        /// <param name="va">GPU virtual address to write the data into</param>
        /// <param name="data">The data to be written</param>
        public void Write(ulong va, ReadOnlySpan<byte> data)
        {
            WriteImpl(va, data, Physical.Write);
        }

        /// <summary>
        /// Writes data to GPU mapped memory, destined for a tracked resource.
        /// </summary>
        /// <param name="va">GPU virtual address to write the data into</param>
        /// <param name="data">The data to be written</param>
        public void WriteTrackedResource(ulong va, ReadOnlySpan<byte> data)
        {
            WriteImpl(va, data, Physical.WriteTrackedResource);
        }

        /// <summary>
        /// Writes data to GPU mapped memory without write tracking.
        /// </summary>
        /// <param name="va">GPU virtual address to write the data into</param>
        /// <param name="data">The data to be written</param>
        public void WriteUntracked(ulong va, ReadOnlySpan<byte> data)
        {
            WriteImpl(va, data, Physical.WriteUntracked);
        }

        private delegate void WriteCallback(ulong address, ReadOnlySpan<byte> data);

        /// <summary>
        /// Writes data to possibly non-contiguous GPU mapped memory.
        /// </summary>
        /// <param name="va">GPU virtual address of the region to write into</param>
        /// <param name="data">Data to be written</param>
        /// <param name="writeCallback">Write callback</param>
        private void WriteImpl(ulong va, ReadOnlySpan<byte> data, WriteCallback writeCallback)
        {
            if (IsContiguous(va, data.Length))
            {
                writeCallback(Translate(va), data);
            }
            else
            {
                int offset = 0, size;

                if ((va & PageMask) != 0)
                {
                    ulong pa = Translate(va);

                    size = Math.Min(data.Length, (int)PageSize - (int)(va & PageMask));

                    writeCallback(pa, data.Slice(0, size));

                    offset += size;
                }

                for (; offset < data.Length; offset += size)
                {
                    ulong pa = Translate(va + (ulong)offset);

                    size = Math.Min(data.Length - offset, (int)PageSize);

                    writeCallback(pa, data.Slice(offset, size));
                }
            }
        }

        /// <summary>
        /// Writes data to GPU mapped memory, stopping at the first unmapped page at the memory region, if any.
        /// </summary>
        /// <param name="va">GPU virtual address to write the data into</param>
        /// <param name="data">The data to be written</param>
        public void WriteMapped(ulong va, ReadOnlySpan<byte> data)
        {
            if (IsContiguous(va, data.Length))
            {
                Physical.Write(Translate(va), data);
            }
            else
            {
                int offset = 0, size;

                if ((va & PageMask) != 0)
                {
                    ulong pa = Translate(va);

                    size = Math.Min(data.Length, (int)PageSize - (int)(va & PageMask));

                    if (pa != PteUnmapped && Physical.IsMapped(pa))
                    {
                        Physical.Write(pa, data.Slice(0, size));
                    }

                    offset += size;
                }

                for (; offset < data.Length; offset += size)
                {
                    ulong pa = Translate(va + (ulong)offset);

                    size = Math.Min(data.Length - offset, (int)PageSize);

                    if (pa != PteUnmapped && Physical.IsMapped(pa))
                    {
                        Physical.Write(pa, data.Slice(offset, size));
                    }
                }
            }
        }

        /// <summary>
        /// Maps a given range of pages to the specified CPU virtual address.
        /// </summary>
        /// <remarks>
        /// All addresses and sizes must be page aligned.
        /// </remarks>
        /// <param name="pa">CPU virtual address to map into</param>
        /// <param name="va">GPU virtual address to be mapped</param>
        /// <param name="size">Size in bytes of the mapping</param>
        /// <param name="kind">Kind of the resource located at the mapping</param>
        public void Map(ulong pa, ulong va, ulong size, PteKind kind)
        {
            lock (_pageTable)
            {
                MemoryUnmapped?.Invoke(this, new UnmapEventArgs(va, size));

                for (ulong offset = 0; offset < size; offset += PageSize)
                {
                    SetPte(va + offset, PackPte(pa + offset, kind));
                }
            }
        }

        /// <summary>
        /// Unmaps a given range of pages at the specified GPU virtual memory region.
        /// </summary>
        /// <param name="va">GPU virtual address to unmap</param>
        /// <param name="size">Size in bytes of the region being unmapped</param>
        public void Unmap(ulong va, ulong size)
        {
            lock (_pageTable)
            {
                // Event handlers are not expected to be thread safe.
                MemoryUnmapped?.Invoke(this, new UnmapEventArgs(va, size));

                for (ulong offset = 0; offset < size; offset += PageSize)
                {
                    SetPte(va + offset, PteUnmapped);
                }
            }
        }

        /// <summary>
        /// Checks if a region of GPU mapped memory is contiguous.
        /// </summary>
        /// <param name="va">GPU virtual address of the region</param>
        /// <param name="size">Size of the region</param>
        /// <returns>True if the region is contiguous, false otherwise</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsContiguous(ulong va, int size)
        {
            if (!ValidateAddress(va) || GetPte(va) == PteUnmapped)
            {
                return false;
            }

            ulong endVa = (va + (ulong)size + PageMask) & ~PageMask;

            va &= ~PageMask;

            int pages = (int)((endVa - va) / PageSize);

            for (int page = 0; page < pages - 1; page++)
            {
                if (!ValidateAddress(va + PageSize) || GetPte(va + PageSize) == PteUnmapped)
                {
                    return false;
                }

                if (Translate(va) + PageSize != Translate(va + PageSize))
                {
                    return false;
                }

                va += PageSize;
            }

            return true;
        }

        /// <summary>
        /// Gets the size of the contiguous and mapped range from the start of the specified memory region.
        /// </summary>
        /// <param name="va">GPU virtual address of the region</param>
        /// <param name="size">Size of the region</param>
        /// <returns>Size of the contiguous and mapped memory in bytetes</returns>
        public ulong GetContiguousMappedSize(ulong va, ulong size)
        {
            if (!ValidateAddress(va) || GetPte(va) == PteUnmapped)
            {
                return 0;
            }

            ulong endVa = (va + size + PageMask) & ~PageMask;

            va &= ~PageMask;
            ulong startVa = va;

            int pages = (int)((endVa - va) / PageSize);

            for (int page = 0; page < pages - 1; page++)
            {
                if (!ValidateAddress(va + PageSize) || GetPte(va + PageSize) == PteUnmapped)
                {
                    break;
                }

                if (Translate(va) + PageSize != Translate(va + PageSize))
                {
                    break;
                }

                va += PageSize;
            }

            return (va - startVa) + PageSize;
        }

        /// <summary>
        /// Gets the physical regions that make up the given virtual address region.
        /// </summary>
        /// <param name="va">Virtual address of the range</param>
        /// <param name="size">Size of the range</param>
        /// <returns>Multi-range with the physical regions</returns>
        public MultiRange GetPhysicalRegions(ulong va, ulong size)
        {
            if (IsContiguous(va, (int)size))
            {
                return new MultiRange(Translate(va), size);
            }

            ulong regionStart = Translate(va);
            ulong regionSize = Math.Min(size, PageSize - (va & PageMask));

            ulong endVa = va + size;
            ulong endVaRounded = (endVa + PageMask) & ~PageMask;

            va &= ~PageMask;

            int pages = (int)((endVaRounded - va) / PageSize);

            var regions = new List<MemoryRange>();

            for (int page = 0; page < pages - 1; page++)
            {
                ulong currPa = Translate(va);
                ulong newPa = Translate(va + PageSize);

                if ((currPa != PteUnmapped || newPa != PteUnmapped) && currPa + PageSize != newPa)
                {
                    regions.Add(new MemoryRange(regionStart, regionSize));
                    regionStart = newPa;
                    regionSize = 0;
                }

                va += PageSize;
                regionSize += Math.Min(endVa - va, PageSize);
            }

            regions.Add(new MemoryRange(regionStart, regionSize));

            return new MultiRange(regions.ToArray());
        }

        /// <summary>
        /// Checks if a given GPU virtual memory range is mapped to the same physical regions
        /// as the specified physical memory multi-range.
        /// </summary>
        /// <param name="range">Physical memory multi-range</param>
        /// <param name="va">GPU virtual memory address</param>
        /// <returns>True if the virtual memory region is mapped into the specified physical one, false otherwise</returns>
        public bool CompareRange(MultiRange range, ulong va)
        {
            va &= ~PageMask;

            for (int i = 0; i < range.Count; i++)
            {
                MemoryRange currentRange = range.GetSubRange(i);

                if (currentRange.Address != PteUnmapped)
                {
                    ulong address = currentRange.Address & ~PageMask;
                    ulong endAddress = (currentRange.EndAddress + PageMask) & ~PageMask;

                    while (address < endAddress)
                    {
                        if (Translate(va) != address)
                        {
                            return false;
                        }

                        va += PageSize;
                        address += PageSize;
                    }
                }
                else
                {
                    ulong endVa = va + (((currentRange.Size) + PageMask) & ~PageMask);

                    while (va < endVa)
                    {
                        if (Translate(va) != PteUnmapped)
                        {
                            return false;
                        }

                        va += PageSize;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Validates a GPU virtual address.
        /// </summary>
        /// <param name="va">Address to validate</param>
        /// <returns>True if the address is valid, false otherwise</returns>
        private static bool ValidateAddress(ulong va)
        {
            return va < (1UL << AddressSpaceBits);
        }

        /// <summary>
        /// Checks if a given page is mapped.
        /// </summary>
        /// <param name="va">GPU virtual address of the page to check</param>
        /// <returns>True if the page is mapped, false otherwise</returns>
        public bool IsMapped(ulong va)
        {
            return Translate(va) != PteUnmapped;
        }

        /// <summary>
        /// Translates a GPU virtual address to a CPU virtual address.
        /// </summary>
        /// <param name="va">GPU virtual address to be translated</param>
        /// <returns>CPU virtual address, or <see cref="PteUnmapped"/> if unmapped</returns>
        public ulong Translate(ulong va)
        {
            if (!ValidateAddress(va))
            {
                return PteUnmapped;
            }

            ulong pte = GetPte(va);

            if (pte == PteUnmapped)
            {
                return PteUnmapped;
            }

            return UnpackPaFromPte(pte) + (va & PageMask);
        }

        /// <summary>
        /// Gets the kind of a given memory page.
        /// This might indicate the type of resource that can be allocated on the page, and also texture tiling.
        /// </summary>
        /// <param name="va">GPU virtual address</param>
        /// <returns>Kind of the memory page</returns>
        public PteKind GetKind(ulong va)
        {
            if (!ValidateAddress(va))
            {
                return PteKind.Invalid;
            }

            ulong pte = GetPte(va);

            if (pte == PteUnmapped)
            {
                return PteKind.Invalid;
            }

            return UnpackKindFromPte(pte);
        }

        /// <summary>
        /// Gets the Page Table entry for a given GPU virtual address.
        /// </summary>
        /// <param name="va">GPU virtual address</param>
        /// <returns>Page table entry (CPU virtual address)</returns>
        private ulong GetPte(ulong va)
        {
            ulong l0 = (va >> PtLvl0Bit) & PtLvl0Mask;
            ulong l1 = (va >> PtLvl1Bit) & PtLvl1Mask;

            if (_pageTable[l0] == null)
            {
                return PteUnmapped;
            }

            return _pageTable[l0][l1];
        }

        /// <summary>
        /// Sets a Page Table entry at a given GPU virtual address.
        /// </summary>
        /// <param name="va">GPU virtual address</param>
        /// <param name="pte">Page table entry (CPU virtual address)</param>
        private void SetPte(ulong va, ulong pte)
        {
            ulong l0 = (va >> PtLvl0Bit) & PtLvl0Mask;
            ulong l1 = (va >> PtLvl1Bit) & PtLvl1Mask;

            if (_pageTable[l0] == null)
            {
                _pageTable[l0] = new ulong[PtLvl1Size];

                for (ulong index = 0; index < PtLvl1Size; index++)
                {
                    _pageTable[l0][index] = PteUnmapped;
                }
            }

            _pageTable[l0][l1] = pte;
        }

        /// <summary>
        /// Creates a page table entry from a physical address and kind.
        /// </summary>
        /// <param name="pa">Physical address</param>
        /// <param name="kind">Kind</param>
        /// <returns>Page table entry</returns>
        private static ulong PackPte(ulong pa, PteKind kind)
        {
            return pa | ((ulong)kind << 56);
        }

        /// <summary>
        /// Unpacks kind from a page table entry.
        /// </summary>
        /// <param name="pte">Page table entry</param>
        /// <returns>Kind</returns>
        private static PteKind UnpackKindFromPte(ulong pte)
        {
            return (PteKind)(pte >> 56);
        }

        /// <summary>
        /// Unpacks physical address from a page table entry.
        /// </summary>
        /// <param name="pte">Page table entry</param>
        /// <returns>Physical address</returns>
        private static ulong UnpackPaFromPte(ulong pte)
        {
            return pte & 0xffffffffffffffUL;
        }

        /// <summary>
        /// Disposes the memory manager.
        /// It's an error to use the memory manager after disposal.
        /// </summary>
        public void Dispose()
        {
            _context.DeferredActions.Enqueue(Destroy);
        }

        /// <summary>
        /// Performs disposal of the host GPU resources cached by the memory manager, that are not shared.
        /// This must only be called from the render thread.
        /// </summary>
        private void Destroy()
        {
            VirtualBufferCache.Dispose();
        }
    }
}