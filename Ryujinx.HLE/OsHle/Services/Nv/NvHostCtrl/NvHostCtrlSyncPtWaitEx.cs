namespace Ryujinx.HLE.OsHle.Services.Nv.NvHostCtrl
{
    struct NvHostCtrlSyncptWaitEx
    {
        public int Id;
        public int Thresh;
        public int Timeout;
        public int Value;
    }
}