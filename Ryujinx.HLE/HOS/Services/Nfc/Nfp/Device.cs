﻿using Ryujinx.HLE.HOS.Kernel.Threading;
using Ryujinx.HLE.HOS.Services.Hid;
using Ryujinx.HLE.Input;

namespace Ryujinx.HLE.HOS.Services.Nfc.Nfp
{
    class Device
    {
        public KEvent ActivateEvent;
        public int    ActivateEventHandle = 0;

        public KEvent DeactivateEvent;
        public int    DeactivateEventHandle = 0;

        public DeviceState State = DeviceState.Unavailable;

        public HidControllerId Handle;
        public NpadIdType      NpadIdType;
    }
}