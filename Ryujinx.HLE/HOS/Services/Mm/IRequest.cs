using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Ipc;
using System.Collections.Generic;

namespace Ryujinx.HLE.HOS.Services.Mm
{
    class IRequest : IpcService
    {
        private Dictionary<int, ServiceProcessRequest> _commands;

        public override IReadOnlyDictionary<int, ServiceProcessRequest> Commands => _commands;

        public IRequest()
        {
            _commands = new Dictionary<int, ServiceProcessRequest>()
            {
                { 0, InitializeOld },
                { 1, FinalizeOld   },
                { 2, SetAndWaitOld },
                { 3, GetOld        },
                { 4, Initialize    },
                { 5, Finalize      },
                { 6, SetAndWait    },
                { 7, Get           }
            };
        }

        // InitializeOld(u32, u32, u32)
        public long InitializeOld(ServiceCtx context)
        {
            int unknown0 = context.RequestData.ReadInt32();
            int unknown1 = context.RequestData.ReadInt32();
            int unknown2 = context.RequestData.ReadInt32();

            Logger.PrintStub(LogClass.ServiceMm, $"Stubbed. Unknown0: {unknown0} - " +
                                                 $"Unknown1: {unknown1} - Unknown2: {unknown2}");

            return 0;
        }

        // FinalizeOld(u32)
        public long FinalizeOld(ServiceCtx context)
        {
            context.Device.Gpu.UninitializeVideoDecoder();

            Logger.PrintStub(LogClass.ServiceMm, "Stubbed.");

            return 0;
        }

        // SetAndWaitOld(u32, u32, u32)
        public long SetAndWaitOld(ServiceCtx context)
        {
            int unknown0 = context.RequestData.ReadInt32();
            int unknown1 = context.RequestData.ReadInt32();
            int unknown2 = context.RequestData.ReadInt32();

            Logger.PrintStub(LogClass.ServiceMm, $"Stubbed. Unknown0: {unknown0} - " +
                                                 $"Unknown1: {unknown1} - Unknown2: {unknown2}");
            return 0;
        }

        // GetOld(u32) -> u32
        public long GetOld(ServiceCtx context)
        {
            int unknown0 = context.RequestData.ReadInt32();

            Logger.PrintStub(LogClass.ServiceMm, $"Stubbed. Unknown0: {unknown0}");

            context.ResponseData.Write(0);

            return 0;
        }

        // Initialize()
        public long Initialize(ServiceCtx context)
        {
            Logger.PrintStub(LogClass.ServiceMm, "Stubbed.");

            return 0;
        }

        // Finalize(u32)
        public long Finalize(ServiceCtx context)
        {
            context.Device.Gpu.UninitializeVideoDecoder();

            Logger.PrintStub(LogClass.ServiceMm, "Stubbed.");

            return 0;
        }

        // SetAndWait(u32, u32, u32)
        public long SetAndWait(ServiceCtx context)
        {
            int unknown0 = context.RequestData.ReadInt32();
            int unknown1 = context.RequestData.ReadInt32();
            int unknown2 = context.RequestData.ReadInt32();

            Logger.PrintStub(LogClass.ServiceMm, $"Stubbed. Unknown0: {unknown0} - " +
                                                 $"Unknown1: {unknown1} - Unknown2: {unknown2}");

            return 0;
        }

        // Get(u32) -> u32
        public long Get(ServiceCtx context)
        {
            int unknown0 = context.RequestData.ReadInt32();

            Logger.PrintStub(LogClass.ServiceMm, $"Stubbed. Unknown0: {unknown0}");

            context.ResponseData.Write(0);

            return 0;
        }
    }
}