using ChocolArm64.Memory;
using Ryujinx.Audio;
using Ryujinx.Audio.Adpcm;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.HOS.Kernel;
using Ryujinx.HLE.Utilities;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Ryujinx.HLE.HOS.Services.Aud.AudioRenderer
{
    class IAudioRenderer : IpcService, IDisposable
    {
        //This is the amount of samples that are going to be appended
        //each time that RequestUpdateAudioRenderer is called. Ideally,
        //this value shouldn't be neither too small (to avoid the player
        //starving due to running out of samples) or too large (to avoid
        //high latency).
        private const int MixBufferSamplesCount = 960;

        private Dictionary<int, ServiceProcessRequest> m_Commands;

        public override IReadOnlyDictionary<int, ServiceProcessRequest> Commands => m_Commands;

        private KEvent UpdateEvent;

        private MemoryManager Memory;

        private IAalOutput AudioOut;

        private AudioRendererParameter Params;

        private MemoryPoolContext[] MemoryPools;

        private VoiceContext[] Voices;

        private int Track;

        private PlayState PlayState;

        public IAudioRenderer(
            Horizon                System,
            MemoryManager          Memory,
            IAalOutput             AudioOut,
            AudioRendererParameter Params)
        {
            m_Commands = new Dictionary<int, ServiceProcessRequest>()
            {
                { 0, GetSampleRate              },
                { 1, GetSampleCount             },
                { 2, GetMixBufferCount          },
                { 3, GetState                   },
                { 4, RequestUpdateAudioRenderer },
                { 5, StartAudioRenderer         },
                { 6, StopAudioRenderer          },
                { 7, QuerySystemEvent           }
            };

            UpdateEvent = new KEvent(System);

            this.Memory   = Memory;
            this.AudioOut = AudioOut;
            this.Params   = Params;

            Track = AudioOut.OpenTrack(
                AudioConsts.HostSampleRate,
                AudioConsts.HostChannelsCount,
                AudioCallback);

            MemoryPools = CreateArray<MemoryPoolContext>(Params.EffectCount + Params.VoiceCount * 4);

            Voices = CreateArray<VoiceContext>(Params.VoiceCount);

            InitializeAudioOut();

            PlayState = PlayState.Stopped;
        }

        //  GetSampleRate() -> u32
        public long GetSampleRate(ServiceCtx Context)
        {
            Context.ResponseData.Write(Params.SampleRate);

            return 0;
        }

        //  GetSampleCount() -> u32
        public long GetSampleCount(ServiceCtx Context)
        {
            Context.ResponseData.Write(Params.SampleCount);

            return 0;
        }

        // GetMixBufferCount() -> u32
        public long GetMixBufferCount(ServiceCtx Context)
        {
            Context.ResponseData.Write(Params.MixCount);

            return 0;
        }

        // GetState() -> u32
        private long GetState(ServiceCtx Context)
        {
            Context.ResponseData.Write((int)PlayState);

            Logger.PrintStub(LogClass.ServiceAudio, $"Stubbed. Renderer State: {Enum.GetName(typeof(PlayState), PlayState)}");

            return 0;
        }

        private void AudioCallback()
        {
            UpdateEvent.ReadableEvent.Signal();
        }

        private static T[] CreateArray<T>(int Size) where T : new()
        {
            T[] Output = new T[Size];

            for (int Index = 0; Index < Size; Index++)
            {
                Output[Index] = new T();
            }

            return Output;
        }

        private void InitializeAudioOut()
        {
            AppendMixedBuffer(0);
            AppendMixedBuffer(1);
            AppendMixedBuffer(2);

            AudioOut.Start(Track);
        }

        public long RequestUpdateAudioRenderer(ServiceCtx Context)
        {
            long OutputPosition = Context.Request.ReceiveBuff[0].Position;
            long OutputSize     = Context.Request.ReceiveBuff[0].Size;

            MemoryHelper.FillWithZeros(Context.Memory, OutputPosition, (int)OutputSize);

            long InputPosition = Context.Request.SendBuff[0].Position;

            StructReader Reader = new StructReader(Context.Memory, InputPosition);
            StructWriter Writer = new StructWriter(Context.Memory, OutputPosition);

            UpdateDataHeader InputHeader = Reader.Read<UpdateDataHeader>();

            Reader.Read<BehaviorIn>(InputHeader.BehaviorSize);

            MemoryPoolIn[] MemoryPoolsIn = Reader.Read<MemoryPoolIn>(InputHeader.MemoryPoolSize);

            for (int Index = 0; Index < MemoryPoolsIn.Length; Index++)
            {
                MemoryPoolIn MemoryPool = MemoryPoolsIn[Index];

                if (MemoryPool.State == MemoryPoolState.RequestAttach)
                {
                    MemoryPools[Index].OutStatus.State = MemoryPoolState.Attached;
                }
                else if (MemoryPool.State == MemoryPoolState.RequestDetach)
                {
                    MemoryPools[Index].OutStatus.State = MemoryPoolState.Detached;
                }
            }

            Reader.Read<VoiceChannelResourceIn>(InputHeader.VoiceResourceSize);

            VoiceIn[] VoicesIn = Reader.Read<VoiceIn>(InputHeader.VoiceSize);

            for (int Index = 0; Index < VoicesIn.Length; Index++)
            {
                VoiceIn Voice = VoicesIn[Index];

                VoiceContext VoiceCtx = Voices[Index];

                VoiceCtx.SetAcquireState(Voice.Acquired != 0);

                if (Voice.Acquired == 0)
                {
                    continue;
                }

                if (Voice.FirstUpdate != 0)
                {
                    VoiceCtx.AdpcmCtx = GetAdpcmDecoderContext(
                        Voice.AdpcmCoeffsPosition,
                        Voice.AdpcmCoeffsSize);

                    VoiceCtx.SampleFormat  = Voice.SampleFormat;
                    VoiceCtx.SampleRate    = Voice.SampleRate;
                    VoiceCtx.ChannelsCount = Voice.ChannelsCount;

                    VoiceCtx.SetBufferIndex(Voice.BaseWaveBufferIndex);
                }

                VoiceCtx.WaveBuffers[0] = Voice.WaveBuffer0;
                VoiceCtx.WaveBuffers[1] = Voice.WaveBuffer1;
                VoiceCtx.WaveBuffers[2] = Voice.WaveBuffer2;
                VoiceCtx.WaveBuffers[3] = Voice.WaveBuffer3;
                VoiceCtx.Volume         = Voice.Volume;
                VoiceCtx.PlayState      = Voice.PlayState;
            }

            UpdateAudio();

            UpdateDataHeader OutputHeader = new UpdateDataHeader();

            int UpdateHeaderSize = Marshal.SizeOf<UpdateDataHeader>();

            OutputHeader.Revision               = IAudioRendererManager.RevMagic;
            OutputHeader.BehaviorSize           = 0xb0;
            OutputHeader.MemoryPoolSize         = (Params.EffectCount + Params.VoiceCount * 4) * 0x10;
            OutputHeader.VoiceSize              = Params.VoiceCount  * 0x10;
            OutputHeader.EffectSize             = Params.EffectCount * 0x10;
            OutputHeader.SinkSize               = Params.SinkCount   * 0x20;
            OutputHeader.PerformanceManagerSize = 0x10;
            OutputHeader.TotalSize              = UpdateHeaderSize             +
                                                  OutputHeader.BehaviorSize    +
                                                  OutputHeader.MemoryPoolSize +
                                                  OutputHeader.VoiceSize      +
                                                  OutputHeader.EffectSize     +
                                                  OutputHeader.SinkSize       +
                                                  OutputHeader.PerformanceManagerSize;

            Writer.Write(OutputHeader);

            foreach (MemoryPoolContext MemoryPool in MemoryPools)
            {
                Writer.Write(MemoryPool.OutStatus);
            }

            foreach (VoiceContext Voice in Voices)
            {
                Writer.Write(Voice.OutStatus);
            }

            return 0;
        }

        public long StartAudioRenderer(ServiceCtx Context)
        {
            Logger.PrintStub(LogClass.ServiceAudio, "Stubbed.");

            PlayState = PlayState.Playing;

            return 0;
        }

        public long StopAudioRenderer(ServiceCtx Context)
        {
            Logger.PrintStub(LogClass.ServiceAudio, "Stubbed.");

            PlayState = PlayState.Stopped;

            return 0;
        }

        public long QuerySystemEvent(ServiceCtx Context)
        {
            if (Context.Process.HandleTable.GenerateHandle(UpdateEvent.ReadableEvent, out int Handle) != KernelResult.Success)
            {
                throw new InvalidOperationException("Out of handles!");
            }

            Context.Response.HandleDesc = IpcHandleDesc.MakeCopy(Handle);

            return 0;
        }

        private AdpcmDecoderContext GetAdpcmDecoderContext(long Position, long Size)
        {
            if (Size == 0)
            {
                return null;
            }

            AdpcmDecoderContext Context = new AdpcmDecoderContext();

            Context.Coefficients = new short[Size >> 1];

            for (int Offset = 0; Offset < Size; Offset += 2)
            {
                Context.Coefficients[Offset >> 1] = Memory.ReadInt16(Position + Offset);
            }

            return Context;
        }

        private void UpdateAudio()
        {
            long[] Released = AudioOut.GetReleasedBuffers(Track, 2);

            for (int Index = 0; Index < Released.Length; Index++)
            {
                AppendMixedBuffer(Released[Index]);
            }
        }

        private unsafe void AppendMixedBuffer(long Tag)
        {
            int[] MixBuffer = new int[MixBufferSamplesCount * AudioConsts.HostChannelsCount];

            foreach (VoiceContext Voice in Voices)
            {
                if (!Voice.Playing)
                {
                    continue;
                }

                int   OutOffset      = 0;
                int   PendingSamples = MixBufferSamplesCount;
                float Volume         = Voice.Volume;

                while (PendingSamples > 0)
                {
                    int[] Samples = Voice.GetBufferData(Memory, PendingSamples, out int ReturnedSamples);

                    if (ReturnedSamples == 0)
                    {
                        break;
                    }

                    PendingSamples -= ReturnedSamples;

                    for (int Offset = 0; Offset < Samples.Length; Offset++)
                    {
                        MixBuffer[OutOffset++] += (int)(Samples[Offset] * Voice.Volume);
                    }
                }
            }

            AudioOut.AppendBuffer(Track, Tag, GetFinalBuffer(MixBuffer));
        }

        private unsafe static short[] GetFinalBuffer(int[] Buffer)
        {
            short[] Output = new short[Buffer.Length];

            int Offset = 0;

            // Perform Saturation using SSE2 if supported
            if (Sse2.IsSupported)
            {
                fixed (int*   inptr  = Buffer)
                fixed (short* outptr = Output)
                {
                    for (; Offset + 32 <= Buffer.Length; Offset += 32)
                    {
                        // Unroll the loop a little to ensure the CPU pipeline
                        // is always full.
                        Vector128<int> block1A = Sse2.LoadVector128(inptr + Offset + 0);
                        Vector128<int> block1B = Sse2.LoadVector128(inptr + Offset + 4);

                        Vector128<int> block2A = Sse2.LoadVector128(inptr + Offset +  8);
                        Vector128<int> block2B = Sse2.LoadVector128(inptr + Offset + 12);

                        Vector128<int> block3A = Sse2.LoadVector128(inptr + Offset + 16);
                        Vector128<int> block3B = Sse2.LoadVector128(inptr + Offset + 20);

                        Vector128<int> block4A = Sse2.LoadVector128(inptr + Offset + 24);
                        Vector128<int> block4B = Sse2.LoadVector128(inptr + Offset + 28);

                        Vector128<short> output1 = Sse2.PackSignedSaturate(block1A, block1B);
                        Vector128<short> output2 = Sse2.PackSignedSaturate(block2A, block2B);
                        Vector128<short> output3 = Sse2.PackSignedSaturate(block3A, block3B);
                        Vector128<short> output4 = Sse2.PackSignedSaturate(block4A, block4B);

                        Sse2.Store(outptr + Offset +  0, output1);
                        Sse2.Store(outptr + Offset +  8, output2);
                        Sse2.Store(outptr + Offset + 16, output3);
                        Sse2.Store(outptr + Offset + 24, output4);
                    }
                }
            }

            // Process left overs
            for (; Offset < Buffer.Length; Offset++)
            {
                Output[Offset] = DspUtils.Saturate(Buffer[Offset]);
            }

            return Output;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool Disposing)
        {
            if (Disposing)
            {
                AudioOut.CloseTrack(Track);
            }
        }
    }
}
