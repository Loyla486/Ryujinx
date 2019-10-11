﻿using System;

namespace Ryujinx.Common
{
    public static class EndianSwap
    {
        public static ushort Swap16(ushort value) => (ushort)(((value >> 8) & 0xff) | (value << 8));

        public static int Swap32(int value)
        {
            uint uintVal = (uint)value;

            return (int)(((uintVal >> 24) & 0x000000ff) |
                         ((uintVal >>  8) & 0x0000ff00) |
                         ((uintVal <<  8) & 0x00ff0000) |
                         ((uintVal << 24) & 0xff000000));
        }

        public static uint FromBigEndianToPlatformEndian(uint value)
        {
            uint result = value;

            if (BitConverter.IsLittleEndian)
            {
                result = (uint)EndianSwap.Swap32((int)result);
            }

            return result;
        }
    }
}
