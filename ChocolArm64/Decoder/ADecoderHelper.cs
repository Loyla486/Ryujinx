using System;

namespace ChocolArm64.Decoder
{
    static class ADecoderHelper
    {
        public struct BitMask
        {
            public long WMask;
            public long TMask;
            public int  Pos;
            public int  Shift;
            public bool IsUndefined;

            public static BitMask Invalid => new BitMask { IsUndefined = true };
        }

        public static BitMask DecodeBitMask(int opCode, bool immediate)
        {
            int immS = (opCode >> 10) & 0x3f;
            int immR = (opCode >> 16) & 0x3f;

            int n  = (opCode >> 22) & 1;
            int sf = (opCode >> 31) & 1;

            int length = ABitUtils.HighestBitSet32((~immS & 0x3f) | (n << 6));

            if (length < 1 || (sf == 0 && n != 0))
            {
                return BitMask.Invalid;
            }

            int size = 1 << length;

            int levels = size - 1;

            int s = immS & levels;
            int r = immR & levels;

            if (immediate && s == levels)
            {
                return BitMask.Invalid;
            }

            long wMask = ABitUtils.FillWithOnes(s + 1);
            long mask = ABitUtils.FillWithOnes(((s - r) & levels) + 1);

            if (r > 0)
            {
                wMask  = ABitUtils.RotateRight(wMask, r, size);
                wMask &= ABitUtils.FillWithOnes(size);
            }

            return new BitMask()
            {
                WMask = ABitUtils.Replicate(wMask, size),
                TMask = ABitUtils.Replicate(mask, size),

                Pos   = immS,
                Shift = immR
            };
        }

        public static long DecodeImm8Float(long imm, int size)
        {
            int e = 0, f = 0;

            switch (size)
            {
                case 0: e =  8; f = 23; break;
                case 1: e = 11; f = 52; break;

                default: throw new ArgumentOutOfRangeException(nameof(size));
            }

            long value = (imm & 0x3f) << f - 4;

            long eBit = (imm >> 6) & 1;
            long sBit = (imm >> 7) & 1;

            if (eBit != 0)
            {
                value |= (1L << e - 3) - 1 << f + 2;
            }

            value |= (eBit ^ 1) << f + e - 1;
            value |=  sBit      << f + e;

            return value;
        }

        public static long DecodeImm26_2(int opCode)
        {
            return ((long)opCode << 38) >> 36;
        }

        public static long DecodeImmS19_2(int opCode)
        {
            return (((long)opCode << 40) >> 43) & ~3;
        }

        public static long DecodeImmS14_2(int opCode)
        {
            return (((long)opCode << 45) >> 48) & ~3;
        }
    }
}