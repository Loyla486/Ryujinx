using Ryujinx.HLE.HOS.Ipc;
using System.Collections.Generic;

namespace Ryujinx.HLE.HOS.Services.Aud
{
    internal class IHardwareOpusDecoderManager : IpcService
    {
        private Dictionary<int, ServiceProcessRequest> _commands;

        public override IReadOnlyDictionary<int, ServiceProcessRequest> Commands => _commands;

        public IHardwareOpusDecoderManager()
        {
            _commands = new Dictionary<int, ServiceProcessRequest>()
            {
                { 0, Initialize        },
                { 1, GetWorkBufferSize }
            };
        }

        public long Initialize(ServiceCtx context)
        {
            int sampleRate    = context.RequestData.ReadInt32();
            int channelsCount = context.RequestData.ReadInt32();

            MakeObject(context, new IHardwareOpusDecoder(sampleRate, channelsCount));

            return 0;
        }

        public long GetWorkBufferSize(ServiceCtx context)
        {
            //Note: The sample rate is ignored because it is fixed to 48KHz.
            int sampleRate    = context.RequestData.ReadInt32();
            int channelsCount = context.RequestData.ReadInt32();

            context.ResponseData.Write(GetOpusDecoderSize(channelsCount));

            return 0;
        }

        private static int GetOpusDecoderSize(int channelsCount)
        {
            const int silkDecoderSize = 0x2198;

            if (channelsCount < 1 || channelsCount > 2)
            {
                return 0;
            }

            int celtDecoderSize = GetCeltDecoderSize(channelsCount);

            int opusDecoderSize = (channelsCount * 0x800 + 0x4807) & -0x800 | 0x50;

            return opusDecoderSize + silkDecoderSize + celtDecoderSize;
        }

        private static int GetCeltDecoderSize(int channelsCount)
        {
            const int decodeBufferSize = 0x2030;
            const int celtDecoderSize  = 0x58;
            const int celtSigSize      = 0x4;
            const int overlap          = 120;
            const int eBandsCount      = 21;

            return (decodeBufferSize + overlap * 4) * channelsCount +
                    eBandsCount * 16 +
                    celtDecoderSize +
                    celtSigSize;
        }
    }
}
