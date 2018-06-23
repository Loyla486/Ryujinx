using Ryujinx.Graphics.Gal;
using Ryujinx.HLE.Gpu;
using Ryujinx.HLE.Logging;
using Ryujinx.HLE.OsHle.Handles;
using Ryujinx.HLE.OsHle.Services.Nv.NvMap;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using static Ryujinx.HLE.OsHle.Services.Android.Parcel;

namespace Ryujinx.HLE.OsHle.Services.Android
{
    class NvFlinger : IDisposable
    {
        private delegate long ServiceProcessParcel(ServiceCtx Context, BinaryReader ParcelReader);

        private Dictionary<(string, int), ServiceProcessParcel> Commands;

        private KEvent ReleaseEvent;

        private IGalRenderer Renderer;

        private const int BufferQueueCount = 0x40;
        private const int BufferQueueMask  = BufferQueueCount - 1;

        [Flags]
        private enum HalTransform
        {
            FlipX     = 1 << 0,
            FlipY     = 1 << 1,
            Rotate90  = 1 << 2
        }

        private enum BufferState
        {
            Free,
            Dequeued,
            Queued,
            Acquired
        }

        private struct Rect
        {
            public int Top;
            public int Left;
            public int Right;
            public int Bottom;
        }

        private struct BufferEntry
        {
            public BufferState State;

            public HalTransform Transform;

            public Rect Crop;

            public GbpBuffer Data;
        }

        private BufferEntry[] BufferQueue;

        private ManualResetEvent WaitBufferFree;

        private bool Disposed;

        public NvFlinger(IGalRenderer Renderer, KEvent ReleaseEvent)
        {
            Commands = new Dictionary<(string, int), ServiceProcessParcel>()
            {
                { ("android.gui.IGraphicBufferProducer", 0x1), GbpRequestBuffer  },
                { ("android.gui.IGraphicBufferProducer", 0x3), GbpDequeueBuffer  },
                { ("android.gui.IGraphicBufferProducer", 0x4), GbpDetachBuffer   },
                { ("android.gui.IGraphicBufferProducer", 0x7), GbpQueueBuffer    },
                { ("android.gui.IGraphicBufferProducer", 0x8), GbpCancelBuffer   },
                { ("android.gui.IGraphicBufferProducer", 0x9), GbpQuery          },
                { ("android.gui.IGraphicBufferProducer", 0xa), GbpConnect        },
                { ("android.gui.IGraphicBufferProducer", 0xb), GbpDisconnect     },
                { ("android.gui.IGraphicBufferProducer", 0xe), GbpPreallocBuffer }
            };

            this.Renderer     = Renderer;
            this.ReleaseEvent = ReleaseEvent;

            BufferQueue = new BufferEntry[0x40];

            WaitBufferFree = new ManualResetEvent(false);
        }

        public long ProcessParcelRequest(ServiceCtx Context, byte[] ParcelData, int Code)
        {
            using (MemoryStream MS = new MemoryStream(ParcelData))
            {
                BinaryReader Reader = new BinaryReader(MS);

                MS.Seek(4, SeekOrigin.Current);

                int StrSize = Reader.ReadInt32();

                string InterfaceName = Encoding.Unicode.GetString(Reader.ReadBytes(StrSize * 2));

                long Remainder = MS.Position & 0xf;

                if (Remainder != 0)
                {
                    MS.Seek(0x10 - Remainder, SeekOrigin.Current);
                }

                MS.Seek(0x50, SeekOrigin.Begin);

                if (Commands.TryGetValue((InterfaceName, Code), out ServiceProcessParcel ProcReq))
                {
                    Context.Ns.Log.PrintDebug(LogClass.ServiceVi, $"{InterfaceName} {ProcReq.Method.Name}");

                    return ProcReq(Context, Reader);
                }
                else
                {
                    throw new NotImplementedException($"{InterfaceName} {Code}");
                }
            }
        }

        private long GbpRequestBuffer(ServiceCtx Context, BinaryReader ParcelReader)
        {
            int Slot = ParcelReader.ReadInt32();

            using (MemoryStream MS = new MemoryStream())
            {
                BinaryWriter Writer = new BinaryWriter(MS);

                BufferEntry Entry = BufferQueue[Slot];

                int  BufferCount = 1; //?
                long BufferSize  = Entry.Data.Size;

                Writer.Write(BufferCount);
                Writer.Write(BufferSize);

                Entry.Data.Write(Writer);

                Writer.Write(0);

                return MakeReplyParcel(Context, MS.ToArray());
            }
        }

        private long GbpDequeueBuffer(ServiceCtx Context, BinaryReader ParcelReader)
        {
            //TODO: Errors.
            int Format        = ParcelReader.ReadInt32();
            int Width         = ParcelReader.ReadInt32();
            int Height        = ParcelReader.ReadInt32();
            int GetTimestamps = ParcelReader.ReadInt32();
            int Usage         = ParcelReader.ReadInt32();

            int Slot = GetFreeSlotBlocking(Width, Height);

            return MakeReplyParcel(Context, Slot, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        private long GbpQueueBuffer(ServiceCtx Context, BinaryReader ParcelReader)
        {
            Context.Ns.Statistics.RecordGameFrameTime();

            //TODO: Errors.
            int Slot            = ParcelReader.ReadInt32();
            int Unknown4        = ParcelReader.ReadInt32();
            int Unknown8        = ParcelReader.ReadInt32();
            int Unknownc        = ParcelReader.ReadInt32();
            int Timestamp       = ParcelReader.ReadInt32();
            int IsAutoTimestamp = ParcelReader.ReadInt32();
            int CropTop         = ParcelReader.ReadInt32();
            int CropLeft        = ParcelReader.ReadInt32();
            int CropRight       = ParcelReader.ReadInt32();
            int CropBottom      = ParcelReader.ReadInt32();
            int ScalingMode     = ParcelReader.ReadInt32();
            int Transform       = ParcelReader.ReadInt32();
            int StickyTransform = ParcelReader.ReadInt32();
            int Unknown34       = ParcelReader.ReadInt32();
            int Unknown38       = ParcelReader.ReadInt32();
            int IsFenceValid    = ParcelReader.ReadInt32();
            int Fence0Id        = ParcelReader.ReadInt32();
            int Fence0Value     = ParcelReader.ReadInt32();
            int Fence1Id        = ParcelReader.ReadInt32();
            int Fence1Value     = ParcelReader.ReadInt32();

            BufferQueue[Slot].Transform = (HalTransform)Transform;

            BufferQueue[Slot].Crop.Top    = CropTop;
            BufferQueue[Slot].Crop.Left   = CropLeft;
            BufferQueue[Slot].Crop.Right  = CropRight;
            BufferQueue[Slot].Crop.Bottom = CropBottom;

            BufferQueue[Slot].State = BufferState.Queued;

            SendFrameBuffer(Context, Slot);

            return MakeReplyParcel(Context, 1280, 720, 0, 0, 0);
        }

        private long GbpDetachBuffer(ServiceCtx Context, BinaryReader ParcelReader)
        {
            return MakeReplyParcel(Context, 0);
        }

        private long GbpCancelBuffer(ServiceCtx Context, BinaryReader ParcelReader)
        {
            //TODO: Errors.
            int Slot = ParcelReader.ReadInt32();

            BufferQueue[Slot].State = BufferState.Free;

            return MakeReplyParcel(Context, 0);
        }

        private long GbpQuery(ServiceCtx Context, BinaryReader ParcelReader)
        {
            return MakeReplyParcel(Context, 0, 0);
        }

        private long GbpConnect(ServiceCtx Context, BinaryReader ParcelReader)
        {
            return MakeReplyParcel(Context, 1280, 720, 0, 0, 0);
        }

        private long GbpDisconnect(ServiceCtx Context, BinaryReader ParcelReader)
        {
            return MakeReplyParcel(Context, 0);
        }

        private long GbpPreallocBuffer(ServiceCtx Context, BinaryReader ParcelReader)
        {
            int Slot = ParcelReader.ReadInt32();

            int BufferCount = ParcelReader.ReadInt32();

            if (BufferCount > 0)
            {
                long BufferSize = ParcelReader.ReadInt64();

                BufferQueue[Slot].State = BufferState.Free;

                BufferQueue[Slot].Data = new GbpBuffer(ParcelReader);
            }

            return MakeReplyParcel(Context, 0);
        }

        private long MakeReplyParcel(ServiceCtx Context, params int[] Ints)
        {
            using (MemoryStream MS = new MemoryStream())
            {
                BinaryWriter Writer = new BinaryWriter(MS);

                foreach (int Int in Ints)
                {
                    Writer.Write(Int);
                }

                return MakeReplyParcel(Context, MS.ToArray());
            }
        }

        private long MakeReplyParcel(ServiceCtx Context, byte[] Data)
        {
            long ReplyPos  = Context.Request.ReceiveBuff[0].Position;
            long ReplySize = Context.Request.ReceiveBuff[0].Size;

            byte[] Reply = MakeParcel(Data, new byte[0]);

            Context.Memory.WriteBytes(ReplyPos, Reply);

            return 0;
        }

        private void SendFrameBuffer(ServiceCtx Context, int Slot)
        {
            int FbWidth  = 1280;
            int FbHeight = 720;

            int NvMapHandle  = BitConverter.ToInt32(BufferQueue[Slot].Data.RawData, 0x4c);
            int BufferOffset = BitConverter.ToInt32(BufferQueue[Slot].Data.RawData, 0x50);

            NvMapHandle Map = NvMapIoctl.GetNvMap(Context, NvMapHandle);;

            long FbAddr = Map.Address + BufferOffset;

            BufferQueue[Slot].State = BufferState.Acquired;

            Rect Crop = BufferQueue[Slot].Crop;

            int RealWidth  = FbWidth;
            int RealHeight = FbHeight;

            float XSign = BufferQueue[Slot].Transform.HasFlag(HalTransform.FlipX) ? -1 : 1;
            float YSign = BufferQueue[Slot].Transform.HasFlag(HalTransform.FlipY) ? -1 : 1;

            float ScaleX = 1;
            float ScaleY = 1;

            float OffsX = 0;
            float OffsY = 0;

            if (Crop.Right  != 0 &&
                Crop.Bottom != 0)
            {
                //Who knows if this is right, I was never good with math...
                RealWidth  = Crop.Right  - Crop.Left;
                RealHeight = Crop.Bottom - Crop.Top;

                if (BufferQueue[Slot].Transform.HasFlag(HalTransform.Rotate90))
                {
                    ScaleY = (float)FbHeight / RealHeight;
                    ScaleX = (float)FbWidth  / RealWidth;

                    OffsY = ((-(float)Crop.Left / Crop.Right)  + ScaleX - 1) * -XSign;
                    OffsX = ((-(float)Crop.Top  / Crop.Bottom) + ScaleY - 1) * -YSign;
                }
                else
                {
                    ScaleX = (float)FbWidth  / RealWidth;
                    ScaleY = (float)FbHeight / RealHeight;

                    OffsX = ((-(float)Crop.Left / Crop.Right)  + ScaleX - 1) *  XSign;
                    OffsY = ((-(float)Crop.Top  / Crop.Bottom) + ScaleY - 1) * -YSign;
                }
            }

            ScaleX *= XSign;
            ScaleY *= YSign;

            float Rotate = 0;

            if (BufferQueue[Slot].Transform.HasFlag(HalTransform.Rotate90))
            {
                Rotate = -MathF.PI * 0.5f;
            }

            Renderer.QueueAction(() => Renderer.FrameBuffer.SetTransform(ScaleX, ScaleY, Rotate, OffsX, OffsY));

            //TODO: Support double buffering here aswell, it is broken for GPU
            //frame buffers because it seems to be completely out of sync.
            if (Context.Ns.Gpu.Engine3d.IsFrameBufferPosition(FbAddr))
            {
                //Frame buffer is rendered to by the GPU, we can just
                //bind the frame buffer texture, it's not necessary to read anything.
                Renderer.QueueAction(() => Renderer.FrameBuffer.Set(FbAddr));
            }
            else
            {
                //Frame buffer is not set on the GPU registers, in this case
                //assume that the app is manually writing to it.
                Texture Texture = new Texture(FbAddr, FbWidth, FbHeight);

                byte[] Data = TextureReader.Read(Context.Memory, Texture);

                Renderer.QueueAction(() => Renderer.FrameBuffer.Set(Data, FbWidth, FbHeight));
            }

            Context.Ns.Gpu.Renderer.QueueAction(() => ReleaseBuffer(Slot));
        }

        private void ReleaseBuffer(int Slot)
        {
            BufferQueue[Slot].State = BufferState.Free;

            ReleaseEvent.WaitEvent.Set();

            lock (WaitBufferFree)
            {
                WaitBufferFree.Set();
            }
        }

        private int GetFreeSlotBlocking(int Width, int Height)
        {
            int Slot;

            do
            {
                lock (WaitBufferFree)
                {
                    if ((Slot = GetFreeSlot(Width, Height)) != -1)
                    {
                        break;
                    }

                    if (Disposed)
                    {
                        break;
                    }

                    WaitBufferFree.Reset();
                }

                WaitBufferFree.WaitOne();
            }
            while (!Disposed);

            return Slot;
        }

        private int GetFreeSlot(int Width, int Height)
        {
            lock (BufferQueue)
            {
                for (int Slot = 0; Slot < BufferQueue.Length; Slot++)
                {
                    if (BufferQueue[Slot].State != BufferState.Free)
                    {
                        continue;
                    }

                    GbpBuffer Data = BufferQueue[Slot].Data;

                    if (Data.Width  == Width &&
                        Data.Height == Height)
                    {
                        BufferQueue[Slot].State = BufferState.Dequeued;

                        return Slot;
                    }
                }
            }

            return -1;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool Disposing)
        {
            if (Disposing && !Disposed)
            {
                Disposed = true;

                lock (WaitBufferFree)
                {
                    WaitBufferFree.Set();
                }

                WaitBufferFree.Dispose();
            }
        }
    }
}