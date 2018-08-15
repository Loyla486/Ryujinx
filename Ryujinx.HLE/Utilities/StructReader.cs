using ChocolArm64.Memory;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.Utilities
{
    class StructReader
    {
        private AMemory Memory;

        public long Position { get; private set; }

        public StructReader(AMemory Memory, long Position)
        {
            this.Memory   = Memory;
            this.Position = Position;
        }

        public T Read<T>() where T : struct
        {
            T Value = AMemoryHelper.Read<T>(Memory, Position);

            Position += Marshal.SizeOf<T>();

            return Value;
        }

        public T[] Read<T>(int Size) where T : struct
        {
            int StructSize = Marshal.SizeOf<T>();

            int Count = Size / StructSize;

            T[] Output = new T[Count];

            for (int Index = 0; Index < Count; Index++)
            {
                Output[Index] = AMemoryHelper.Read<T>(Memory, Position);

                Position += StructSize;
            }

            return Output;
        }
    }
}
