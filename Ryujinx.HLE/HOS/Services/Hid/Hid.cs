using Ryujinx.Common;
using Ryujinx.HLE.Exceptions;
using System.Runtime.InteropServices;

namespace Ryujinx.HLE.HOS.Services.Hid
{
    public class Hid
    {
        private readonly Switch _device;
        private long _hidMemoryAddress;

        internal ref HidSharedMemory SharedMemory => ref _device.Memory.GetStructRef<HidSharedMemory>(_hidMemoryAddress);
        internal const int SharedMemEntryCount = 17;

        public DebugPadDevice DebugPad;
        public TouchDevice Touchscreen;
        public MouseDevice Mouse;
        public KeyboardDevice Keyboard;
        public NpadDevices Npads;

        static Hid()
        {
            if (Marshal.SizeOf<ShMemDebugPad>() != 0x400)
            {
                throw new InvalidStructLayoutException(typeof(ShMemDebugPad), 0x400);
            }
            if (Marshal.SizeOf<ShMemTouchScreen>() != 0x3000)
            {
                throw new InvalidStructLayoutException(typeof(ShMemTouchScreen), 0x3000);
            }
            if (Marshal.SizeOf<ShMemKeyboard>() != 0x400)
            {
                throw new InvalidStructLayoutException(typeof(ShMemKeyboard), 0x400);
            }
            if (Marshal.SizeOf<ShMemMouse>() != 0x400)
            {
                throw new InvalidStructLayoutException(typeof(ShMemMouse), 0x400);
            }
            if (Marshal.SizeOf<ShMemNpad>() != 0x5000)
            {
                throw new InvalidStructLayoutException(typeof(ShMemNpad), 0x5000);
            }
            if (Marshal.SizeOf<HidSharedMemory>() != Horizon.HidSize)
            {
                throw new InvalidStructLayoutException(typeof(HidSharedMemory), Horizon.HidSize);
            }
        }

        public Hid(in Switch device, long sharedHidMemoryAddress)
        {
            _device = device;
            _hidMemoryAddress = sharedHidMemoryAddress;

            device.Memory.FillWithZeros(sharedHidMemoryAddress, Horizon.HidSize);
        }

        public void InitDevices()
        {
            DebugPad = new DebugPadDevice(_device, true);
            Touchscreen = new TouchDevice(_device, true);
            Mouse = new MouseDevice(_device, false);
            Keyboard = new KeyboardDevice(_device, false);
            Npads = new NpadDevices(_device, true);
        }

        public ControllerKeys UpdateStickButtons(JoystickPosition leftStick, JoystickPosition rightStick)
        {
            ControllerKeys result = 0;

            result |= (leftStick.Dx < 0) ? ControllerKeys.LStickLeft : result;
            result |= (leftStick.Dx > 0) ? ControllerKeys.LStickRight : result;
            result |= (leftStick.Dy < 0) ? ControllerKeys.LStickDown : result;
            result |= (leftStick.Dy > 0) ? ControllerKeys.LStickUp : result;

            result |= (rightStick.Dx < 0) ? ControllerKeys.RStickLeft : result;
            result |= (rightStick.Dx > 0) ? ControllerKeys.RStickRight : result;
            result |= (rightStick.Dy < 0) ? ControllerKeys.RStickDown : result;
            result |= (rightStick.Dy > 0) ? ControllerKeys.RStickUp : result;

            return result;
        }

        internal static ulong GetTimestampTicks()
        {
            return (ulong)PerformanceCounter.ElapsedMilliseconds * 19200;
        }

    }
}
