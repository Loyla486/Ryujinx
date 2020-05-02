namespace Ryujinx.HLE.HOS.Services.Account
{
    enum ResultCode
    {
        ModuleId       = 124,
        ErrorCodeShift = 9,

        Success = 0,

        NullArgument                         = (20  << ErrorCodeShift) | ModuleId,
        InvalidArgument                      = (22  << ErrorCodeShift) | ModuleId,
        NullInputBuffer                      = (30  << ErrorCodeShift) | ModuleId,
        InvalidInputBufferSize               = (31  << ErrorCodeShift) | ModuleId,
        InvalidInputBuffer                   = (32  << ErrorCodeShift) | ModuleId,
        ApplicationLaunchPropertyAlreadyInit = (41  << ErrorCodeShift) | ModuleId,
        InternetRequestDenied                = (59 << ErrorCodeShift)  | ModuleId,
        UserNotFound                         = (100 << ErrorCodeShift) | ModuleId,
        NullObject                           = (302 << ErrorCodeShift) | ModuleId,
        UnknownError1                        = (341 << ErrorCodeShift) | ModuleId
    }
}
