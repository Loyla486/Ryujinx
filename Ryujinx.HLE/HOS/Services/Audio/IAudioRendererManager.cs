using Ryujinx.Audio;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Services.Audio.AudioRendererManager;
using Ryujinx.HLE.HOS.Services.Audio.AudioRendererManager.Types;
using Ryujinx.HLE.Utilities;

namespace Ryujinx.HLE.HOS.Services.Audio
{
    [Service("audren:u")]
    class IAudioRendererManager : IpcService
    {
        private const int Rev0Magic = ('R' << 0)  |
                                      ('E' << 8)  |
                                      ('V' << 16) |
                                      ('0' << 24);

        private const int Rev = 5;

        public const int RevMagic = Rev0Magic + (Rev << 24);

        public IAudioRendererManager(ServiceCtx context) { }

        [Command(0)]
        // OpenAudioRenderer(nn::audio::detail::AudioRendererParameterInternal, u64, nn::applet::AppletResourceUserId, pid, handle<copy>, handle<copy>)
        // -> object<nn::audio::detail::IAudioRenderer>
        public ResultCode OpenAudioRenderer(ServiceCtx context)
        {
            IAalOutput audioOut = context.Device.AudioOut;

            AudioRendererParameter Params = GetAudioRendererParameter(context);

            MakeObject(context, new IAudioRenderer(
                context.Device.System,
                context.Memory,
                audioOut,
                Params));

            return ResultCode.Success;
        }

        [Command(1)]
        // GetWorkBufferSize(nn::audio::detail::AudioRendererParameterInternal) -> u64
        public ResultCode GetAudioRendererWorkBufferSize(ServiceCtx context)
        {
            AudioRendererParameter Params = GetAudioRendererParameter(context);

            int revision = (Params.Revision - Rev0Magic) >> 24;

            if (revision <= Rev)
            {
                bool isSplitterSupported                  = revision >= 3;
                bool isVariadicCommandBufferSizeSupported = revision >= 5;
                
                long size;

                size  = IntUtils.AlignUp(Params.Unknown8 * 4, 64);
                size += Params.MixCount * 0x400;
                size += (Params.MixCount + 1) * 0x940;
                size += Params.VoiceCount * 0x3F0;
                size += IntUtils.AlignUp((Params.MixCount + 1) * 8, 16);
                size += IntUtils.AlignUp(Params.VoiceCount * 8, 16);
                size += IntUtils.AlignUp(
                    ((Params.SinkCount + Params.MixCount) * 0x3C0 + Params.SampleCount * 4) *
                    (Params.Unknown8 + 6), 64);
                size += (Params.SinkCount + Params.MixCount) * 0x2C0;
                size += (Params.EffectCount + Params.VoiceCount * 4) * 0x30 + 0x50;

                if (isSplitterSupported)
                {
                    size += IntUtils.AlignUp((
                        NodeStatesGetWorkBufferSize(Params.MixCount + 1) +
                        EdgeMatrixGetWorkBufferSize(Params.MixCount + 1)), 16);

                    size += Params.SplitterDestinationDataCount * 0xE0;
                    size += Params.SplitterCount * 0x20;
                    size += IntUtils.AlignUp(Params.SplitterDestinationDataCount * 4, 16);
                }

                size = Params.EffectCount * 0x4C0 +
                       Params.SinkCount * 0x170 +
                       Params.VoiceCount * 0x100 +
                       IntUtils.AlignUp(size, 64) + 0x40;

                if (Params.PerformanceManagerCount >= 1)
                {
                    size += (((Params.EffectCount +
                               Params.SinkCount +
                               Params.VoiceCount +
                               Params.MixCount + 1) * 16 + 0x658) *
                               (Params.PerformanceManagerCount + 1) + 0x13F) & ~0x3FL;
                }

                if (isVariadicCommandBufferSizeSupported)
                {
                    size += Params.EffectCount * 0x840 + 
                            Params.MixCount * 0x5A38 +
                            Params.SinkCount * 0x148 +
                            Params.SplitterDestinationDataCount * 0x540 +
                            Params.VoiceCount * (Params.SplitterCount * 0x68 + 0x2E0) +
                            ((Params.VoiceCount + Params.MixCount + Params.EffectCount + Params.SinkCount + 0x65) << 6) + 0x3F8 + 0x7E;
                }
                else
                {
                    size += 0x1807E;
                }

                size = size & ~0xFFFL;

                context.ResponseData.Write(size);

                Logger.PrintDebug(LogClass.ServiceAudio, $"WorkBufferSize is 0x{size:x16}.");

                return ResultCode.Success;
            }
            else
            {
                context.ResponseData.Write(0L);

                Logger.PrintWarning(LogClass.ServiceAudio, $"Library Revision 0x{Params.Revision:x8} is not supported!");

                return ResultCode.UnsupportedRevision;
            }
        }

        private AudioRendererParameter GetAudioRendererParameter(ServiceCtx context)
        {
            AudioRendererParameter Params = new AudioRendererParameter
            {
                SampleRate                   = context.RequestData.ReadInt32(),
                SampleCount                  = context.RequestData.ReadInt32(),
                Unknown8                     = context.RequestData.ReadInt32(),
                MixCount                     = context.RequestData.ReadInt32(),
                VoiceCount                   = context.RequestData.ReadInt32(),
                SinkCount                    = context.RequestData.ReadInt32(),
                EffectCount                  = context.RequestData.ReadInt32(),
                PerformanceManagerCount      = context.RequestData.ReadInt32(),
                VoiceDropEnable              = context.RequestData.ReadInt32(),
                SplitterCount                = context.RequestData.ReadInt32(),
                SplitterDestinationDataCount = context.RequestData.ReadInt32(),
                Unknown2C                    = context.RequestData.ReadInt32(),
                Revision                     = context.RequestData.ReadInt32()
            };

            return Params;
        }

        private static int NodeStatesGetWorkBufferSize(int value)
        {
            int result = IntUtils.AlignUp(value, 64);

            if (result < 0)
            {
                result |= 7;
            }

            return 4 * (value * value) + 0x12 * value + 2 * (result / 8);
        }

        private static int EdgeMatrixGetWorkBufferSize(int value)
        {
            int result = IntUtils.AlignUp(value * value, 64);

            if (result < 0)
            {
                result |= 7;
            }

            return result / 8;
        }

        [Command(2)]
        // GetAudioDeviceService(nn::applet::AppletResourceUserId) -> object<nn::audio::detail::IAudioDevice>
        public ResultCode GetAudioDeviceService(ServiceCtx context)
        {
            long appletResourceUserId = context.RequestData.ReadInt64();

            MakeObject(context, new IAudioDevice(context.Device.System));

            return ResultCode.Success;
        }

        [Command(4)] // 4.0.0+
        // GetAudioDeviceServiceWithRevisionInfo(nn::applet::AppletResourceUserId, u32) -> object<nn::audio::detail::IAudioDevice>
        public ResultCode GetAudioDeviceServiceWithRevisionInfo(ServiceCtx context)
        {
            long appletResourceUserId = context.RequestData.ReadInt64();
            int  revisionInfo         = context.RequestData.ReadInt32();

            Logger.PrintStub(LogClass.ServiceAudio, new { appletResourceUserId, revisionInfo });

            return GetAudioDeviceService(context);
        }
    }
}
