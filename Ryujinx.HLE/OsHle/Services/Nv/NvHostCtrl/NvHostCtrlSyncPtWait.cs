namespace Ryujinx.HLE.OsHle.Services.Nv.NvHostCtrl
{
    struct NvHostCtrlSyncptWait
    {
        public int Id;
        public int Thresh;
        public int Timeout;
    }
}