using ChocolArm64.Memory;
using Ryujinx.Graphics.Gal;
using System.Collections.Concurrent;

namespace Ryujinx.Core.Gpu
{
    public class NvGpuVmm : IAMemory, IGalMemory
    {
        public const long AddrSize = 1L << 40;

        private const int  PTLvl0Bits = 14;
        private const int  PTLvl1Bits = 14;
        private const int  PTPageBits = 12;

        private const int  PTLvl0Size = 1 << PTLvl0Bits;
        private const int  PTLvl1Size = 1 << PTLvl1Bits;
        public  const int  PageSize   = 1 << PTPageBits;

        private const int  PTLvl0Mask = PTLvl0Size - 1;
        private const int  PTLvl1Mask = PTLvl1Size - 1;
        public  const int  PageMask   = PageSize   - 1;

        private const int  PTLvl0Bit = PTPageBits + PTLvl1Bits;
        private const int  PTLvl1Bit = PTPageBits;

        public AMemory Memory { get; private set; }

        private struct MappedMemory
        {
            public long Size;

            public MappedMemory(long Size)
            {
                this.Size = Size;
            }
        }

        private ConcurrentDictionary<long, MappedMemory> Maps;

        private const long PteUnmapped = -1;
        private const long PteReserved = -2;

        private long[][] PageTable;

        public NvGpuVmm(AMemory Memory)
        {
            this.Memory = Memory;

            Maps = new ConcurrentDictionary<long, MappedMemory>();

            PageTable = new long[PTLvl0Size][];
        }

        public long Map(long PA, long VA, long Size)
        {
            lock (PageTable)
            {
                for (long Offset = 0; Offset < Size; Offset += PageSize)
                {
                    if (GetPte(VA + Offset) != PteReserved)
                    {
                        return Map(PA, Size);
                    }
                }

                for (long Offset = 0; Offset < Size; Offset += PageSize)
                {
                    SetPte(VA + Offset, PA + Offset);
                }
            }

            return VA;
        }

        public long Map(long PA, long Size)
        {
            lock (PageTable)
            {
                long VA = GetFreePosition(Size);

                if (VA != -1)
                {
                    MappedMemory Map = new MappedMemory(Size);

                    Maps.AddOrUpdate(VA, Map, (Key, Old) => Map);

                    for (long Offset = 0; Offset < Size; Offset += PageSize)
                    {
                        SetPte(VA + Offset, PA + Offset);
                    }
                }

                return VA;
            }
        }

        public bool Unmap(long VA)
        {
            if (Maps.TryRemove(VA, out MappedMemory Map))
            {
                Free(VA, Map.Size);

                return true;
            }

            return false;
        }

        public long Reserve(long VA, long Size, long Align)
        {
            lock (PageTable)
            {
                for (long Offset = 0; Offset < Size; Offset += PageSize)
                {
                    if (IsPageInUse(VA + Offset))
                    {
                        return Reserve(Size, Align);
                    }
                }

                for (long Offset = 0; Offset < Size; Offset += PageSize)
                {
                    SetPte(VA + Offset, PteReserved);
                }
            }

            return VA;
        }

        public long Reserve(long Size, long Align)
        {
            lock (PageTable)
            {
                long Position = GetFreePosition(Size, Align);

                if (Position != -1)
                {
                    for (long Offset = 0; Offset < Size; Offset += PageSize)
                    {
                        SetPte(Position + Offset, PteReserved);
                    }
                }

                return Position;
            }
        }

        public void Free(long VA, long Size)
        {
            lock (PageTable)
            {
                for (long Offset = 0; Offset < Size; Offset += PageSize)
                {
                    SetPte(VA + Offset, PteUnmapped);
                }
            }
        }

        private long GetFreePosition(long Size, long Align = 1)
        {
            long Position = 0;
            long FreeSize = 0;

            if (Align < 1)
            {
                Align = 1;
            }

            Align = (Align + PageMask) & ~PageMask;

            while (Position + FreeSize < AddrSize)
            {
                if (!IsPageInUse(Position + FreeSize))
                {
                    FreeSize += PageSize;

                    if (FreeSize >= Size)
                    {
                        return Position;
                    }
                }
                else
                {
                    Position += FreeSize + PageSize;
                    FreeSize  = 0;

                    long Remainder = Position % Align;

                    if (Remainder != 0)
                    {
                        Position = (Position - Remainder) + Align;
                    }
                }
            }

            return -1;
        }

        public long GetPhysicalAddress(long VA)
        {
            long BasePos = GetPte(VA);

            if (BasePos < 0)
            {
                return -1;
            }

            return BasePos + (VA & PageMask);
        }

        public bool IsRegionFree(long VA, long Size)
        {
            for (long Offset = 0; Offset < Size; Offset += PageSize)
            {
                if (IsPageInUse(VA + Offset))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsPageInUse(long VA)
        {
            if (VA >> PTLvl0Bits + PTLvl1Bits + PTPageBits != 0)
            {
                return false;
            }

            long L0 = (VA >> PTLvl0Bit) & PTLvl0Mask;
            long L1 = (VA >> PTLvl1Bit) & PTLvl1Mask;

            if (PageTable[L0] == null)
            {
                return false;
            }

            return PageTable[L0][L1] != PteUnmapped;
        }

        private long GetPte(long Position)
        {
            long L0 = (Position >> PTLvl0Bit) & PTLvl0Mask;
            long L1 = (Position >> PTLvl1Bit) & PTLvl1Mask;

            if (PageTable[L0] == null)
            {
                return -1;
            }

            return PageTable[L0][L1];
        }

        private void SetPte(long Position, long TgtAddr)
        {
            long L0 = (Position >> PTLvl0Bit) & PTLvl0Mask;
            long L1 = (Position >> PTLvl1Bit) & PTLvl1Mask;

            if (PageTable[L0] == null)
            {
                PageTable[L0] = new long[PTLvl1Size];

                for (int Index = 0; Index < PTLvl1Size; Index++)
                {
                    PageTable[L0][Index] = PteUnmapped;
                }
            }

            PageTable[L0][L1] = TgtAddr;
        }

        public byte ReadByte(long Position)
        {
            Position = GetPhysicalAddress(Position);

            return Memory.ReadByte(Position);
        }

        public ushort ReadUInt16(long Position)
        {
            Position = GetPhysicalAddress(Position);

            return Memory.ReadUInt16(Position);
        }

        public uint ReadUInt32(long Position)
        {
            Position = GetPhysicalAddress(Position);

            return Memory.ReadUInt32(Position);
        }

        public ulong ReadUInt64(long Position)
        {
            Position = GetPhysicalAddress(Position);

            return Memory.ReadUInt64(Position);
        }

        public sbyte ReadSByte(long Position)
        {
            Position = GetPhysicalAddress(Position);

            return Memory.ReadSByte(Position);
        }

        public short ReadInt16(long Position)
        {
            Position = GetPhysicalAddress(Position);

            return Memory.ReadInt16(Position);
        }

        public int ReadInt32(long Position)
        {
            Position = GetPhysicalAddress(Position);

            return Memory.ReadInt32(Position);
        }

        public long ReadInt64(long Position)
        {
            Position = GetPhysicalAddress(Position);

            return Memory.ReadInt64(Position);
        }

        public byte[] ReadBytes(long Position, long Size)
        {
            Position = GetPhysicalAddress(Position);

            return AMemoryHelper.ReadBytes(Memory, Position, Size);
        }

        public void WriteByte(long Position, byte Value)
        {
            Position = GetPhysicalAddress(Position);

            Memory.WriteByte(Position, Value);
        }

        public void WriteUInt16(long Position, ushort Value)
        {
            Position = GetPhysicalAddress(Position);

            Memory.WriteUInt16(Position, Value);
        }

        public void WriteUInt32(long Position, uint Value)
        {
            Position = GetPhysicalAddress(Position);

            Memory.WriteUInt32(Position, Value);
        }

        public void WriteUInt64(long Position, ulong Value)
        {
            Position = GetPhysicalAddress(Position);

            Memory.WriteUInt64(Position, Value);
        }

        public void WriteSByte(long Position, sbyte Value)
        {
            Position = GetPhysicalAddress(Position);

            Memory.WriteSByte(Position, Value);
        }

        public void WriteInt16(long Position, short Value)
        {
            Position = GetPhysicalAddress(Position);

            Memory.WriteInt16(Position, Value);
        }

        public void WriteInt32(long Position, int Value)
        {
            Position = GetPhysicalAddress(Position);

            Memory.WriteInt32(Position, Value);
        }

        public void WriteInt64(long Position, long Value)
        {
            Position = GetPhysicalAddress(Position);

            Memory.WriteInt64(Position, Value);
        }

        public void WriteBytes(long Position, byte[] Data)
        {
            Position = GetPhysicalAddress(Position);

            AMemoryHelper.WriteBytes(Memory, Position, Data);
        }
    }
}