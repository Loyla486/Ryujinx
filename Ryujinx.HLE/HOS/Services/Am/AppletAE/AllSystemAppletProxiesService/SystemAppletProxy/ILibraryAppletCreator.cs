using Ryujinx.HLE.HOS.Applets;
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
            int appletId          = context.RequestData.ReadInt32();
            int libraryAppletMode = context.RequestData.ReadInt32();

            MakeObject(context, new ILibraryAppletAccessor((AppletId)appletId, context.Device.System));

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
    }
}