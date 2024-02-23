using ChocolArm64.Instructions;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading;

using static ChocolArm64.Memory.CompareExchange128;
using static ChocolArm64.Memory.MemoryManagement;

namespace ChocolArm64.Memory
{
    public unsafe class MemoryManager : IMemory, IDisposable
    {
        public const int PageBits = 12;
        public const int PageSize = 1 << PageBits;
        public const int PageMask = PageSize - 1;

        private const long PteFlagNotModified = 1;

        internal const long PteFlagsMask = 7;

        public IntPtr Ram { get; private set; }

        private byte* _ramPtr;

        private IntPtr _pageTable;

        internal IntPtr PageTable => _pageTable;

        internal int PtLevelBits { get; }
        internal int PtLevelSize { get; }
        internal int PtLevelMask { get; }

        public bool HasWriteWatchSupport => MemoryManagement.HasWriteWatchSupport;

        public int  AddressSpaceBits { get; }
        public long AddressSpaceSize { get; }

        public MemoryManager(
            IntPtr ram,
            int    addressSpaceBits = 48,
            bool   useFlatPageTable = false)
        {
            Ram = ram;

            _ramPtr = (byte*)ram;

            AddressSpaceBits = addressSpaceBits;
            AddressSpaceSize = 1L << addressSpaceBits;

            // When flat page table is requested, we use a single
            // array for the mappings of the entire address space.
            // This has better performance, but also high memory usage.
            // The multi level page table uses 9 bits per level, so
            // the memory usage is lower, but the performance is also
            // lower, since each address translation requires multiple reads.
            if (useFlatPageTable)
            {
                PtLevelBits = addressSpaceBits - PageBits;
            }
            else
            {
                PtLevelBits = 9;
            }

            PtLevelSize = 1 << PtLevelBits;
            PtLevelMask = PtLevelSize - 1;

            _pageTable = Allocate((ulong)(PtLevelSize * IntPtr.Size));
        }

        public void Map(long va, long pa, long size)
        {
            SetPtEntries(va, _ramPtr + pa, size);
        }

        public void Unmap(long position, long size)
        {
            SetPtEntries(position, null, size);
        }

        public bool IsMapped(long position)
        {
            return Translate(position) != IntPtr.Zero;
        }

        public long GetPhysicalAddress(long virtualAddress)
        {
            byte* ptr = (byte*)Translate(virtualAddress);

            return (long)(ptr - _ramPtr);
        }

        private IntPtr Translate(long position)
        {
            if (!IsValidPosition(position))
            {
                return IntPtr.Zero;
            }

            byte* ptr = GetPtEntry(position);

            ulong ptrUlong = (ulong)ptr;

            if ((ptrUlong & PteFlagsMask) != 0)
            {
                ptrUlong &= ~(ulong)PteFlagsMask;

                ptr = (byte*)ptrUlong;
            }

            return new IntPtr(ptr + (position & PageMask));
        }

        private IntPtr TranslateWrite(long position)
        {
            if (!IsValidPosition(position))
            {
                return IntPtr.Zero;
            }

            byte* ptr = GetPtEntry(position);

            ulong ptrUlong = (ulong)ptr;

            if ((ptrUlong & PteFlagsMask) != 0)
            {
                if ((ptrUlong & PteFlagNotModified) != 0)
                {
                    ClearPtEntryFlag(position, PteFlagNotModified);
                }

                ptrUlong &= ~(ulong)PteFlagsMask;

                ptr = (byte*)ptrUlong;
            }

            return new IntPtr(ptr + (position & PageMask));
        }

        private byte* GetPtEntry(long position)
        {
            return *(byte**)GetPtPtr(position);
        }

        private void SetPtEntries(long va, byte* ptr, long size)
        {
            long endPosition = (va + size + PageMask) & ~PageMask;

            while ((ulong)va < (ulong)endPosition)
            {
                SetPtEntry(va, ptr);

                va += PageSize;

                if (ptr != null)
                {
                    ptr += PageSize;
                }
            }
        }

        private void SetPtEntry(long position, byte* ptr)
        {
            *(byte**)GetPtPtr(position) = ptr;
        }

        private void SetPtEntryFlag(long position, long flag)
        {
            ModifyPtEntryFlag(position, flag, setFlag: true);
        }

        private void ClearPtEntryFlag(long position, long flag)
        {
            ModifyPtEntryFlag(position, flag, setFlag: false);
        }

        private void ModifyPtEntryFlag(long position, long flag, bool setFlag)
        {
            IntPtr* pt = (IntPtr*)_pageTable;

            while (true)
            {
                IntPtr* ptPtr = GetPtPtr(position);

                IntPtr old = *ptPtr;

                long modified = old.ToInt64();

                if (setFlag)
                {
                    modified |= flag;
                }
                else
                {
                    modified &= ~flag;
                }

                IntPtr origValue = Interlocked.CompareExchange(ref *ptPtr, new IntPtr(modified), old);

                if (origValue == old)
                {
                    break;
                }
            }
        }

        private IntPtr* GetPtPtr(long position)
        {
            if (!IsValidPosition(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            IntPtr nextPtr = _pageTable;

            IntPtr* ptePtr = null;

            int bit = PageBits;

            while (true)
            {
                long index = (position >> bit) & PtLevelMask;

                ptePtr = &((IntPtr*)nextPtr)[index];

                bit += PtLevelBits;

                if (bit >= AddressSpaceBits)
                {
                    break;
                }

                nextPtr = *ptePtr;

                if (nextPtr == IntPtr.Zero)
                {
                    // Entry does not yet exist, allocate a new one.
                    IntPtr newPtr = Allocate((ulong)(PtLevelSize * IntPtr.Size));

                    // Try to swap the current pointer (should be zero), with the allocated one.
                    nextPtr = Interlocked.Exchange(ref *ptePtr, newPtr);

                    // If the old pointer is not null, then another thread already has set it.
                    if (nextPtr != IntPtr.Zero)
                    {
                        Free(newPtr);
                    }
                    else
                    {
                        nextPtr = newPtr;
                    }
                }
            }

            return ptePtr;
        }

        public bool IsRegionModified(long position, long size)
        {
            if (!HasWriteWatchSupport)
            {
                return IsRegionModifiedFallback(position, size);
            }

            IntPtr address = Translate(position);

            IntPtr baseAddr     = address;
            IntPtr expectedAddr = address;

            long pendingPages = 0;

            long pages = size / PageSize;

            bool modified = false;

            bool IsAnyPageModified()
            {
                IntPtr pendingSize = new IntPtr(pendingPages * PageSize);

                IntPtr[] addresses = new IntPtr[pendingPages];

                bool result = GetModifiedPages(baseAddr, pendingSize, addresses, out ulong count);

                if (result)
                {
                    return count != 0;
                }
                else
                {
                    return true;
                }
            }

            while (pages-- > 0)
            {
                if (address != expectedAddr)
                {
                    modified |= IsAnyPageModified();

                    baseAddr = address;

                    pendingPages = 0;
                }

                expectedAddr = address + PageSize;

                pendingPages++;

                if (pages == 0)
                {
                    break;
                }

                position += PageSize;

                address = Translate(position);
            }

            if (pendingPages != 0)
            {
                modified |= IsAnyPageModified();
            }

            return modified;
        }

        private unsafe bool IsRegionModifiedFallback(long position, long size)
        {
            long endAddr = (position + size + PageMask) & ~PageMask;

            bool modified = false;

            while ((ulong)position < (ulong)endAddr)
            {
                if (IsValidPosition(position))
                {
                    byte* ptr = ((byte**)_pageTable)[position >> PageBits];

                    ulong ptrUlong = (ulong)ptr;

                    if ((ptrUlong & PteFlagNotModified) == 0)
                    {
                        modified = true;

                        SetPtEntryFlag(position, PteFlagNotModified);
                    }
                }
                else
                {
                    modified = true;
                }

                position += PageSize;
            }

            return modified;
        }

        public bool TryGetHostAddress(long position, long size, out IntPtr ptr)
        {
            if (IsContiguous(position, size))
            {
                ptr = (IntPtr)Translate(position);

                return true;
            }

            ptr = IntPtr.Zero;

            return false;
        }

        private bool IsContiguous(long position, long size)
        {
            long endPos = position + size;

            position &= ~PageMask;

            long expectedPa = GetPhysicalAddress(position);

            while ((ulong)position < (ulong)endPos)
            {
                long pa = GetPhysicalAddress(position);

                if (pa != expectedPa)
                {
                    return false;
                }

                position   += PageSize;
                expectedPa += PageSize;
            }

            return true;
        }

        public bool IsValidPosition(long position)
        {
            return (ulong)position < (ulong)AddressSpaceSize;
        }

        internal bool AtomicCompareExchange2xInt32(
            long position,
            int  expectedLow,
            int  expectedHigh,
            int  desiredLow,
            int  desiredHigh)
        {
            long expected = (uint)expectedLow;
            long desired  = (uint)desiredLow;

            expected |= (long)expectedHigh << 32;
            desired  |= (long)desiredHigh  << 32;

            return AtomicCompareExchangeInt64(position, expected, desired);
        }

        internal bool AtomicCompareExchangeInt128(
            long  position,
            ulong expectedLow,
            ulong expectedHigh,
            ulong desiredLow,
            ulong desiredHigh)
        {
            if ((position & 0xf) != 0)
            {
                AbortWithAlignmentFault(position);
            }

            IntPtr ptr = TranslateWrite(position);

            return InterlockedCompareExchange128(ptr, expectedLow, expectedHigh, desiredLow, desiredHigh);
        }

        internal Vector128<float> AtomicReadInt128(long position)
        {
            if ((position & 0xf) != 0)
            {
                AbortWithAlignmentFault(position);
            }

            IntPtr ptr = Translate(position);

            InterlockedRead128(ptr, out ulong low, out ulong high);

            Vector128<float> vector = default(Vector128<float>);

            vector = VectorHelper.VectorInsertInt(low,  vector, 0, 3);
            vector = VectorHelper.VectorInsertInt(high, vector, 1, 3);

            return vector;
        }

        public bool AtomicCompareExchangeByte(long position, byte expected, byte desired)
        {
            int* ptr = (int*)Translate(position);

            int currentValue = *ptr;

            int expected32 = (currentValue & ~byte.MaxValue) | expected;
            int desired32  = (currentValue & ~byte.MaxValue) | desired;

            return Interlocked.CompareExchange(ref *ptr, desired32, expected32) == expected32;
        }

        public bool AtomicCompareExchangeInt16(long position, short expected, short desired)
        {
            if ((position & 1) != 0)
            {
                AbortWithAlignmentFault(position);
            }

            int* ptr = (int*)Translate(position);

            int currentValue = *ptr;

            int expected32 = (currentValue & ~ushort.MaxValue) | (ushort)expected;
            int desired32  = (currentValue & ~ushort.MaxValue) | (ushort)desired;

            return Interlocked.CompareExchange(ref *ptr, desired32, expected32) == expected32;
        }

        public bool AtomicCompareExchangeInt32(long position, int expected, int desired)
        {
            if ((position & 3) != 0)
            {
                AbortWithAlignmentFault(position);
            }

            int* ptr = (int*)TranslateWrite(position);

            return Interlocked.CompareExchange(ref *ptr, desired, expected) == expected;
        }

        public bool AtomicCompareExchangeInt64(long position, long expected, long desired)
        {
            if ((position & 7) != 0)
            {
                AbortWithAlignmentFault(position);
            }

            long* ptr = (long*)TranslateWrite(position);

            return Interlocked.CompareExchange(ref *ptr, desired, expected) == expected;
        }

        public int AtomicIncrementInt32(long position)
        {
            if ((position & 3) != 0)
            {
                AbortWithAlignmentFault(position);
            }

            int* ptr = (int*)TranslateWrite(position);

            return Interlocked.Increment(ref *ptr);
        }

        public int AtomicDecrementInt32(long position)
        {
            if ((position & 3) != 0)
            {
                AbortWithAlignmentFault(position);
            }

            int* ptr = (int*)TranslateWrite(position);

            return Interlocked.Decrement(ref *ptr);
        }

        private void AbortWithAlignmentFault(long position)
        {
            // TODO: Abort mode and exception support on the CPU.
            throw new InvalidOperationException($"Tried to compare exchange a misaligned address 0x{position:X16}.");
        }

        public sbyte ReadSByte(long position)
        {
            return (sbyte)ReadByte(position);
        }

        public short ReadInt16(long position)
        {
            return (short)ReadUInt16(position);
        }

        public int ReadInt32(long position)
        {
            return (int)ReadUInt32(position);
        }

        public long ReadInt64(long position)
        {
            return (long)ReadUInt64(position);
        }

        public byte ReadByte(long position)
        {
            return *((byte*)Translate(position));
        }

        public ushort ReadUInt16(long position)
        {
            if ((position & 1) == 0)
            {
                return *((ushort*)Translate(position));
            }
            else
            {
                return (ushort)(ReadByte(position + 0) << 0 |
                                ReadByte(position + 1) << 8);
            }
        }

        public uint ReadUInt32(long position)
        {
            if ((position & 3) == 0)
            {
                return *((uint*)Translate(position));
            }
            else
            {
                return (uint)(ReadUInt16(position + 0) << 0 |
                              ReadUInt16(position + 2) << 16);
            }
        }

        public ulong ReadUInt64(long position)
        {
            if ((position & 7) == 0)
            {
                return *((ulong*)Translate(position));
            }
            else
            {
                return (ulong)ReadUInt32(position + 0) << 0 |
                       (ulong)ReadUInt32(position + 4) << 32;
            }
        }

        public Vector128<float> ReadVector8(long position)
        {
            if (Sse2.IsSupported)
            {
                return Sse.StaticCast<byte, float>(Sse2.SetVector128(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, ReadByte(position)));
            }
            else
            {
                Vector128<float> value = VectorHelper.VectorSingleZero();

                value = VectorHelper.VectorInsertInt(ReadByte(position), value, 0, 0);

                return value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> ReadVector16(long position)
        {
            if (Sse2.IsSupported && (position & 1) == 0)
            {
                return Sse.StaticCast<ushort, float>(Sse2.Insert(Sse2.SetZeroVector128<ushort>(), ReadUInt16(position), 0));
            }
            else
            {
                Vector128<float> value = VectorHelper.VectorSingleZero();

                value = VectorHelper.VectorInsertInt(ReadUInt16(position), value, 0, 1);

                return value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> ReadVector32(long position)
        {
            if (Sse.IsSupported && (position & 3) == 0)
            {
                return Sse.LoadScalarVector128((float*)Translate(position));
            }
            else
            {
                Vector128<float> value = VectorHelper.VectorSingleZero();

                value = VectorHelper.VectorInsertInt(ReadUInt32(position), value, 0, 2);

                return value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> ReadVector64(long position)
        {
            if (Sse2.IsSupported && (position & 7) == 0)
            {
                return Sse.StaticCast<double, float>(Sse2.LoadScalarVector128((double*)Translate(position)));
            }
            else
            {
                Vector128<float> value = VectorHelper.VectorSingleZero();

                value = VectorHelper.VectorInsertInt(ReadUInt64(position), value, 0, 3);

                return value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector128<float> ReadVector128(long position)
        {
            if (Sse.IsSupported && (position & 15) == 0)
            {
                return Sse.LoadVector128((float*)Translate(position));
            }
            else
            {
                Vector128<float> value = VectorHelper.VectorSingleZero();

                value = VectorHelper.VectorInsertInt(ReadUInt64(position + 0), value, 0, 3);
                value = VectorHelper.VectorInsertInt(ReadUInt64(position + 8), value, 1, 3);

                return value;
            }
        }

        public byte[] ReadBytes(long position, long size)
        {
            long endAddr = position + size;

            if ((ulong)size > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            if ((ulong)endAddr < (ulong)position)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            byte[] data = new byte[size];

            int offset = 0;

            while ((ulong)position < (ulong)endAddr)
            {
                long pageLimit = (position + PageSize) & ~(long)PageMask;

                if ((ulong)pageLimit > (ulong)endAddr)
                {
                    pageLimit = endAddr;
                }

                int copySize = (int)(pageLimit - position);

                Marshal.Copy(Translate(position), data, offset, copySize);

                position += copySize;
                offset   += copySize;
            }

            return data;
        }

        public void ReadBytes(long position, byte[] data, int startIndex, int size)
        {
            // Note: This will be moved later.
            long endAddr = position + size;

            if ((ulong)size > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }

            if ((ulong)endAddr < (ulong)position)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            int offset = startIndex;

            while ((ulong)position < (ulong)endAddr)
            {
                long pageLimit = (position + PageSize) & ~(long)PageMask;

                if ((ulong)pageLimit > (ulong)endAddr)
                {
                    pageLimit = endAddr;
                }

                int copySize = (int)(pageLimit - position);

                Marshal.Copy(Translate(position), data, offset, copySize);

                position += copySize;
                offset   += copySize;
            }
        }

        public void WriteSByte(long position, sbyte value)
        {
            WriteByte(position, (byte)value);
        }

        public void WriteInt16(long position, short value)
        {
            WriteUInt16(position, (ushort)value);
        }

        public void WriteInt32(long position, int value)
        {
            WriteUInt32(position, (uint)value);
        }

        public void WriteInt64(long position, long value)
        {
            WriteUInt64(position, (ulong)value);
        }

        public void WriteByte(long position, byte value)
        {
            *((byte*)TranslateWrite(position)) = value;
        }

        public void WriteUInt16(long position, ushort value)
        {
            if ((position & 1) == 0)
            {
                *((ushort*)TranslateWrite(position)) = value;
            }
            else
            {
                WriteByte(position + 0, (byte)(value >> 0));
                WriteByte(position + 1, (byte)(value >> 8));
            }
        }

        public void WriteUInt32(long position, uint value)
        {
            if ((position & 3) == 0)
            {
                *((uint*)TranslateWrite(position)) = value;
            }
            else
            {
                WriteUInt16(position + 0, (ushort)(value >> 0));
                WriteUInt16(position + 2, (ushort)(value >> 16));
            }
        }

        public void WriteUInt64(long position, ulong value)
        {
            if ((position & 7) == 0)
            {
                *((ulong*)TranslateWrite(position)) = value;
            }
            else
            {
                WriteUInt32(position + 0, (uint)(value >> 0));
                WriteUInt32(position + 4, (uint)(value >> 32));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector8(long position, Vector128<float> value)
        {
            if (Sse41.IsSupported)
            {
                WriteByte(position, Sse41.Extract(Sse.StaticCast<float, byte>(value), 0));
            }
            else if (Sse2.IsSupported)
            {
                WriteByte(position, (byte)Sse2.Extract(Sse.StaticCast<float, ushort>(value), 0));
            }
            else
            {
                WriteByte(position, (byte)VectorHelper.VectorExtractIntZx(value, 0, 0));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector16(long position, Vector128<float> value)
        {
            if (Sse2.IsSupported)
            {
                WriteUInt16(position, Sse2.Extract(Sse.StaticCast<float, ushort>(value), 0));
            }
            else
            {
                WriteUInt16(position, (ushort)VectorHelper.VectorExtractIntZx(value, 0, 1));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector32(long position, Vector128<float> value)
        {
            if (Sse.IsSupported && (position & 3) == 0)
            {
                Sse.StoreScalar((float*)TranslateWrite(position), value);
            }
            else
            {
                WriteUInt32(position, (uint)VectorHelper.VectorExtractIntZx(value, 0, 2));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector64(long position, Vector128<float> value)
        {
            if (Sse2.IsSupported && (position & 7) == 0)
            {
                Sse2.StoreScalar((double*)TranslateWrite(position), Sse.StaticCast<float, double>(value));
            }
            else
            {
                WriteUInt64(position, VectorHelper.VectorExtractIntZx(value, 0, 3));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVector128(long position, Vector128<float> value)
        {
            if (Sse.IsSupported && (position & 15) == 0)
            {
                Sse.Store((float*)TranslateWrite(position), value);
            }
            else
            {
                WriteUInt64(position + 0, VectorHelper.VectorExtractIntZx(value, 0, 3));
                WriteUInt64(position + 8, VectorHelper.VectorExtractIntZx(value, 1, 3));
            }
        }

        public void WriteBytes(long position, byte[] data)
        {
            long endAddr = position + data.Length;

            if ((ulong)endAddr < (ulong)position)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            int offset = 0;

            while ((ulong)position < (ulong)endAddr)
            {
                long pageLimit = (position + PageSize) & ~(long)PageMask;

                if ((ulong)pageLimit > (ulong)endAddr)
                {
                    pageLimit = endAddr;
                }

                int copySize = (int)(pageLimit - position);

                Marshal.Copy(data, offset, TranslateWrite(position), copySize);

                position += copySize;
                offset   += copySize;
            }
        }

        public void WriteBytes(long position, byte[] data, int startIndex, int size)
        {
            // Note: This will be moved later.
            long endAddr = position + size;

            if ((ulong)endAddr < (ulong)position)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            int offset = startIndex;

            while ((ulong)position < (ulong)endAddr)
            {
                long pageLimit = (position + PageSize) & ~(long)PageMask;

                if ((ulong)pageLimit > (ulong)endAddr)
                {
                    pageLimit = endAddr;
                }

                int copySize = (int)(pageLimit - position);

                Marshal.Copy(data, offset, Translate(position), copySize);

                position += copySize;
                offset   += copySize;
            }
        }

        public void CopyBytes(long src, long dst, long size)
        {
            // Note: This will be moved later.
            if (IsContiguous(src, size) &&
                IsContiguous(dst, size))
            {
                byte* srcPtr = (byte*)Translate(src);
                byte* dstPtr = (byte*)Translate(dst);

                Buffer.MemoryCopy(srcPtr, dstPtr, size, size);
            }
            else
            {
                WriteBytes(dst, ReadBytes(src, size));
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            IntPtr ptr = Interlocked.Exchange(ref _pageTable, IntPtr.Zero);

            if (ptr != IntPtr.Zero)
            {
                FreePageTableEntry(ptr, PageBits);
            }
        }

        private void FreePageTableEntry(IntPtr ptr, int levelBitEnd)
        {
            levelBitEnd += PtLevelBits;

            if (levelBitEnd >= AddressSpaceBits)
            {
                Free(ptr);

                return;
            }

            for (int index = 0; index < PtLevelSize; index++)
            {
                IntPtr ptePtr = ((IntPtr*)ptr)[index];

                if (ptePtr != IntPtr.Zero)
                {
                    FreePageTableEntry(ptePtr, levelBitEnd);
                }
            }

            Free(ptr);
        }
    }
}