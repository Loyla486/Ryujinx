namespace Ryujinx.HLE.HOS.Services.Hid
{
    public enum BatteryCharge : int
    {
        // TODO : Check if these are the correct states
        Percent0 = 0,
        Percent25 = 1,
        Percent50 = 2,
        Percent75 = 3,
        Percent100 = 4
    }
}