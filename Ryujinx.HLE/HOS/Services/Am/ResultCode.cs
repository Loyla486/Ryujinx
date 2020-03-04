namespace Ryujinx.HLE.HOS.Services.Am
{
    enum ResultCode
    {
        ModuleId       = 128,
        ErrorCodeShift = 9,

        Success = 0,

        NotAvailable           = (2   << ErrorCodeShift) | ModuleId,
        NoMessages             = (3   << ErrorCodeShift) | ModuleId,
        ObjectInvalid          = (500 << ErrorCodeShift) | ModuleId,
        OutOfBounds            = (503 << ErrorCodeShift) | ModuleId,
        InvalidParameters      = (506 << ErrorCodeShift) | ModuleId,
        UnbalancedFatalSection = (512 << ErrorCodeShift) | ModuleId,
        NullObject             = (518 << ErrorCodeShift) | ModuleId
    }
}