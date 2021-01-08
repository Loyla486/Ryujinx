﻿using System.IO;
using System.Text;

namespace Ryujinx.HLE.HOS.Applets.SoftwareKeyboard
{
    internal class InlineResponses
    {
        private const uint MaxStrLenUTF8 = 0x7D4;
        private const uint MaxStrLenUTF16 = 0x3EC;

        private static void BeginResponse(InlineKeyboardState state, InlineKeyboardResponse resCode, BinaryWriter writer)
        {
            writer.Write((uint)state);
            writer.Write((uint)resCode);
        }

        private static uint WriteString(string text, BinaryWriter writer, uint maxSize, Encoding encoding)
        {
            // Ensure the text fits in the buffer, but do not straight cut the bytes because
            // this may corrupt the encoding. Search for a cut in the source string that fits.

            byte[] bytes = null;

            for (int maxStr = text.Length; maxStr >= 0; maxStr--)
            {
                // This loop will probably will run only once.
                bytes = encoding.GetBytes(text.Substring(0, maxStr));
                if (bytes.Length <= maxSize)
                    break;
            }

            writer.Write(bytes);
            writer.Seek((int)maxSize - bytes.Length, SeekOrigin.Current);
            writer.Write((uint)text.Length); // String size

            return (uint)text.Length; // Return the cursor position at the end of the text
        }

        private static void WriteStringWithCursor(string text, BinaryWriter writer, uint maxSize, Encoding encoding)
        {
            uint cursor = WriteString(text, writer, maxSize, encoding);

            writer.Write(cursor); // Cursor position
        }

        public static byte[] FinishedInitialize()
        {
            uint resSize = 2 * sizeof(uint) + 0x1;

            using (MemoryStream stream = new MemoryStream(new byte[resSize]))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                BeginResponse(InlineKeyboardState.Ready, InlineKeyboardResponse.FinishedInitialize, writer);
                writer.Write((byte)1); // Data (ignored by the program)

                return stream.ToArray();
            }
        }

        public static byte[] Default()
        {
            uint resSize = 2 * sizeof(uint);

            using (MemoryStream stream = new MemoryStream(new byte[resSize]))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                BeginResponse(InlineKeyboardState.Initializing, InlineKeyboardResponse.Default, writer);

                return stream.ToArray();
            }
        }

        public static byte[] ChangedString(string text)
        {
            uint resSize = 4 * sizeof(uint) + MaxStrLenUTF16;

            using (MemoryStream stream = new MemoryStream(new byte[resSize]))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                BeginResponse(InlineKeyboardState.Initializing, InlineKeyboardResponse.ChangedString, writer);
                WriteStringWithCursor(text, writer, MaxStrLenUTF16, Encoding.Unicode);
                writer.Write((int)0); // ?
                writer.Write((int)0); // ?

                return stream.ToArray();
            }
        }

        public static byte[] MovedCursor(string text)
        {
            uint resSize = 4 * sizeof(uint) + MaxStrLenUTF16;

            using (MemoryStream stream = new MemoryStream(new byte[resSize]))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                BeginResponse(InlineKeyboardState.Initializing, InlineKeyboardResponse.MovedCursor, writer);
                WriteStringWithCursor(text, writer, MaxStrLenUTF16, Encoding.Unicode);

                return stream.ToArray();
            }
        }

        public static byte[] MovedTab(string text)
        {
            // Should be the same as MovedCursor.

            uint resSize = 4 * sizeof(uint) + MaxStrLenUTF16;

            using (MemoryStream stream = new MemoryStream(new byte[resSize]))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                BeginResponse(InlineKeyboardState.Initializing, InlineKeyboardResponse.MovedTab, writer);
                WriteStringWithCursor(text, writer, MaxStrLenUTF16, Encoding.Unicode);

                return stream.ToArray();
            }
        }

        public static byte[] DecidedEnter(string text)
        {
            uint resSize = 3 * sizeof(uint) + MaxStrLenUTF16;

            using (MemoryStream stream = new MemoryStream(new byte[resSize]))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                BeginResponse(InlineKeyboardState.Completed, InlineKeyboardResponse.DecidedEnter, writer);
                WriteString(text, writer, MaxStrLenUTF16, Encoding.Unicode);

                return stream.ToArray();
            }
        }

        public static byte[] DecidedCancel()
        {
            uint resSize = 2 * sizeof(uint);

            using (MemoryStream stream = new MemoryStream(new byte[resSize]))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                BeginResponse(InlineKeyboardState.Completed, InlineKeyboardResponse.DecidedCancel, writer);

                return stream.ToArray();
            }
        }

        public static byte[] ChangedStringUtf8(string text)
        {
            uint resSize = 6 * sizeof(uint) + MaxStrLenUTF8;

            using (MemoryStream stream = new MemoryStream(new byte[resSize]))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                BeginResponse(InlineKeyboardState.DataAvailable, InlineKeyboardResponse.ChangedStringUtf8, writer);
                WriteStringWithCursor(text, writer, MaxStrLenUTF8, Encoding.UTF8);
                writer.Write((int)0); // ?
                writer.Write((int)0); // ?

                return stream.ToArray();
            }
        }

        public static byte[] MovedCursorUtf8(string text)
        {
            uint resSize = 4 * sizeof(uint) + MaxStrLenUTF8;

            using (MemoryStream stream = new MemoryStream(new byte[resSize]))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                BeginResponse(InlineKeyboardState.DataAvailable, InlineKeyboardResponse.MovedCursorUtf8, writer);
                WriteStringWithCursor(text, writer, MaxStrLenUTF8, Encoding.UTF8);

                return stream.ToArray();
            }
        }

        public static byte[] DecidedEnterUtf8(string text)
        {
            uint resSize = 3 * sizeof(uint) + MaxStrLenUTF8;

            using (MemoryStream stream = new MemoryStream(new byte[resSize]))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                BeginResponse(InlineKeyboardState.Completed, InlineKeyboardResponse.DecidedEnterUtf8, writer);
                WriteString(text, writer, MaxStrLenUTF8, Encoding.UTF8);

                return stream.ToArray();
            }
        }

        public static byte[] UnsetCustomizeDic()
        {
            uint resSize = 2 * sizeof(uint);

            using (MemoryStream stream = new MemoryStream(new byte[resSize]))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                BeginResponse(InlineKeyboardState.Initializing, InlineKeyboardResponse.UnsetCustomizeDic, writer);

                return stream.ToArray();
            }
        }

        public static byte[] ReleasedUserWordInfo()
        {
            uint resSize = 2 * sizeof(uint);

            using (MemoryStream stream = new MemoryStream(new byte[resSize]))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                BeginResponse(InlineKeyboardState.Initializing, InlineKeyboardResponse.ReleasedUserWordInfo, writer);

                return stream.ToArray();
            }
        }

        public static byte[] UnsetCustomizedDictionaries()
        {
            uint resSize = 2 * sizeof(uint);

            using (MemoryStream stream = new MemoryStream(new byte[resSize]))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                BeginResponse(InlineKeyboardState.Initializing, InlineKeyboardResponse.UnsetCustomizedDictionaries, writer);

                return stream.ToArray();
            }
        }

        public static byte[] ChangedStringV2(string text)
        {
            uint resSize = 6 * sizeof(uint) + MaxStrLenUTF16 + 0x1;

            using (MemoryStream stream = new MemoryStream(new byte[resSize]))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                BeginResponse(InlineKeyboardState.DataAvailable, InlineKeyboardResponse.ChangedStringV2, writer);
                WriteStringWithCursor(text, writer, MaxStrLenUTF16, Encoding.Unicode);
                writer.Write((int)0); // ?
                writer.Write((int)0); // ?
                writer.Write((byte)0); // Flag == 0

                return stream.ToArray();
            }
        }

        public static byte[] MovedCursorV2(string text)
        {
            uint resSize = 4 * sizeof(uint) + MaxStrLenUTF16 + 0x1;

            using (MemoryStream stream = new MemoryStream(new byte[resSize]))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                BeginResponse(InlineKeyboardState.DataAvailable, InlineKeyboardResponse.MovedCursorV2, writer);
                WriteStringWithCursor(text, writer, MaxStrLenUTF16, Encoding.Unicode);
                writer.Write((byte)0); // Flag == 0

                return stream.ToArray();
            }
        }

        public static byte[] ChangedStringUtf8V2(string text)
        {
            uint resSize = 6 * sizeof(uint) + MaxStrLenUTF8 + 0x1;

            using (MemoryStream stream = new MemoryStream(new byte[resSize]))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                BeginResponse(InlineKeyboardState.DataAvailable, InlineKeyboardResponse.ChangedStringUtf8V2, writer);
                WriteStringWithCursor(text, writer, MaxStrLenUTF8, Encoding.UTF8);
                writer.Write((int)0); // ?
                writer.Write((int)0); // ?
                writer.Write((byte)0); // Flag == 0

                return stream.ToArray();
            }
        }

        public static byte[] MovedCursorUtf8V2(string text)
        {
            uint resSize = 4 * sizeof(uint) + MaxStrLenUTF8 + 0x1;

            using (MemoryStream stream = new MemoryStream(new byte[resSize]))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                BeginResponse(InlineKeyboardState.DataAvailable, InlineKeyboardResponse.MovedCursorUtf8V2, writer);
                WriteStringWithCursor(text, writer, MaxStrLenUTF8, Encoding.UTF8);
                writer.Write((byte)0); // Flag == 0

                return stream.ToArray();
            }
        }
    }
}
