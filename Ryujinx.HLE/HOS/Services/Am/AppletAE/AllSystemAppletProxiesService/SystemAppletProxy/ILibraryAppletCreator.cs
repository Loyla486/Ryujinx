using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Kernel.Memory;
using Ryujinx.HLE.HOS.Services.Am.AppletAE.AllSystemAppletProxiesService.LibraryAppletCreator;

namespace Ryujinx.HLE.HOS.Services.Am.AppletAE.AllSystemAppletProxiesService.SystemAppletProxy
{
    class ILibraryAppletCreator : IpcService
    {
        public ILibraryAppletCreator() { }

        [Command(0)]
        // CreateLibraryApplet(u32, u32) -> object<nn::am::service::ILibraryAppletAccessor>
        public ResultCode CreateLibraryApplet(ServiceCtx context)
        {
            AppletId appletId          = (AppletId)context.RequestData.ReadInt32();
            int      libraryAppletMode = context.RequestData.ReadInt32();

            MakeObject(context, new ILibraryAppletAccessor(appletId, context.Device.System));

            return ResultCode.Success;
        }

        [Command(10)]
        // CreateStorage(u64) -> object<nn::am::service::IStorage>
        public ResultCode CreateStorage(ServiceCtx context)
        {
            long size = context.RequestData.ReadInt64();

            MakeObject(context, new IStorage(new byte[size]));

            return ResultCode.Success;
        }

        [Command(11)]
        // CreateTransferMemoryStorage(b8, u64, handle<copy>) -> object<nn::am::service::IStorage>
        public ResultCode CreateTransferMemoryStorage(ServiceCtx context)
        {
            bool unknown = context.RequestData.ReadBoolean();
            long size    = context.RequestData.ReadInt64();
            int khandle  = context.Request.HandleDesc.ToCopy[0];

            KTransferMemory tm = context.Process.HandleTable.GetObject<KTransferMemory>(khandle);

            if(tm == null)
            {
                Logger.PrintWarning(LogClass.ServiceAm, $"Invalid TransferMemory Handle: {khandle:X}");

                return ResultCode.Success; // TODO: Find correct error code
            }

            var data = new byte[tm.Size];
            context.Memory.Read(tm.Address, data);

            MakeObject(context, new IStorage(data));

            return ResultCode.Success;
        }
    }
}