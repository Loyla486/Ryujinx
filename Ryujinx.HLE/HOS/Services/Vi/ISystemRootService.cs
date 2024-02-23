using Ryujinx.HLE.HOS.Services.Vi.RootService;

namespace Ryujinx.HLE.HOS.Services.Vi
{
    [Service("vi:s")]
    class ISystemRootService : IpcService
    {
        public ISystemRootService(ServiceCtx context) { }

        [Command(1)]
        // GetDisplayService(u32) -> object<nn::visrv::sf::IApplicationDisplayService>
        public ResultCode GetDisplayService(ServiceCtx context)
        {
            int serviceType = context.RequestData.ReadInt32();

            MakeObject(context, new IApplicationDisplayService());

            return ResultCode.Success;
        }
    }
}