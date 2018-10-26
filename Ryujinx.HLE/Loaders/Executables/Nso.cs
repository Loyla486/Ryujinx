using Ryujinx.HLE.Loaders.Compression;
using System;
using System.IO;

namespace Ryujinx.HLE.Loaders.Executables
{
    internal class Nso : IExecutable
    {
        public string FilePath { get; private set; }

        public byte[] Text { get; private set; }
        public byte[] Ro   { get; private set; }
        public byte[] Data { get; private set; }

        public int Mod0Offset { get; private set; }
        public int TextOffset { get; private set; }
        public int RoOffset   { get; private set; }
        public int DataOffset { get; private set; }
        public int BssSize    { get; private set; }

        public long SourceAddress { get; private set; }
        public long BssAddress    { get; private set; }

        [Flags]
        private enum NsoFlags
        {
            IsTextCompressed = 1 << 0,
            IsRoCompressed   = 1 << 1,
            IsDataCompressed = 1 << 2,
            HasTextHash      = 1 << 3,
            HasRoHash        = 1 << 4,
            HasDataHash      = 1 << 5
        }

        public Nso(Stream input, string filePath)
        {
            FilePath = filePath;

            SourceAddress = 0;
            BssAddress    = 0;

            BinaryReader reader = new BinaryReader(input);

            input.Seek(0, SeekOrigin.Begin);

            int nsoMagic      = reader.ReadInt32();
            int version       = reader.ReadInt32();
            int reserved      = reader.ReadInt32();
            int flagsMsk      = reader.ReadInt32();
            int textOffset    = reader.ReadInt32();
            int textMemOffset = reader.ReadInt32();
            int textDecSize   = reader.ReadInt32();
            int modNameOffset = reader.ReadInt32();
            int roOffset      = reader.ReadInt32();
            int roMemOffset   = reader.ReadInt32();
            int roDecSize     = reader.ReadInt32();
            int modNameSize   = reader.ReadInt32();
            int dataOffset    = reader.ReadInt32();
            int dataMemOffset = reader.ReadInt32();
            int dataDecSize   = reader.ReadInt32();
            int bssSize       = reader.ReadInt32();

            byte[] buildId = reader.ReadBytes(0x20);

            int textSize = reader.ReadInt32();
            int roSize   = reader.ReadInt32();
            int dataSize = reader.ReadInt32();

            input.Seek(0x24, SeekOrigin.Current);

            int dynStrOffset = reader.ReadInt32();
            int dynStrSize   = reader.ReadInt32();
            int dynSymOffset = reader.ReadInt32();
            int dynSymSize   = reader.ReadInt32();

            byte[] textHash = reader.ReadBytes(0x20);
            byte[] roHash   = reader.ReadBytes(0x20);
            byte[] dataHash = reader.ReadBytes(0x20);

            NsoFlags flags = (NsoFlags)flagsMsk;

            TextOffset = textMemOffset;
            RoOffset   = roMemOffset;
            DataOffset = dataMemOffset;
            BssSize    = bssSize;

            //Text segment
            input.Seek(textOffset, SeekOrigin.Begin);

            Text = reader.ReadBytes(textSize);

            if (flags.HasFlag(NsoFlags.IsTextCompressed) || true) Text = Lz4.Decompress(Text, textDecSize);

            //Read-only data segment
            input.Seek(roOffset, SeekOrigin.Begin);

            Ro = reader.ReadBytes(roSize);

            if (flags.HasFlag(NsoFlags.IsRoCompressed) || true) Ro = Lz4.Decompress(Ro, roDecSize);

            //Data segment
            input.Seek(dataOffset, SeekOrigin.Begin);

            Data = reader.ReadBytes(dataSize);

            if (flags.HasFlag(NsoFlags.IsDataCompressed) || true) Data = Lz4.Decompress(Data, dataDecSize);

            using (MemoryStream textMs = new MemoryStream(Text))
            {
                BinaryReader textReader = new BinaryReader(textMs);

                textMs.Seek(4, SeekOrigin.Begin);

                Mod0Offset = textReader.ReadInt32();
            }
        }
    }
}