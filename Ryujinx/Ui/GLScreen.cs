using OpenTK;
using OpenTK.Graphics;
using OpenTK.Input;
using Ryujinx.Graphics.Gal;
using Ryujinx.HLE;
using Ryujinx.HLE.Input;
using System;

namespace Ryujinx
{
    public class GLScreen : GameWindow
    {
        private const int TouchScreenWidth  = 1280;
        private const int TouchScreenHeight = 720;

        private const float TouchScreenRatioX = (float)TouchScreenWidth  / TouchScreenHeight;
        private const float TouchScreenRatioY = (float)TouchScreenHeight / TouchScreenWidth;

        private Switch Ns;

        private IGalRenderer Renderer;

        private KeyboardState? Keyboard = null;

        private MouseState? Mouse = null;

        public GLScreen(Switch Ns, IGalRenderer Renderer)
            : base(1280, 720,
            new GraphicsMode(), "Ryujinx", 0,
            DisplayDevice.Default, 3, 3,
            GraphicsContextFlags.ForwardCompatible)
        {
            this.Ns       = Ns;
            this.Renderer = Renderer;

            Location = new Point(
                (DisplayDevice.Default.Width  / 2) - (Width  / 2),
                (DisplayDevice.Default.Height / 2) - (Height / 2));
        }

        protected override void OnLoad(EventArgs e)
        {
            VSync = VSyncMode.On;

            Renderer.FrameBuffer.SetWindowSize(Width, Height);
        }
        
        private bool IsGamePadButtonPressedFromString(GamePadState GamePad, string Button)
        {
            if (Button == "LTrigger" || Button == "RTrigger")
            {
                return GetGamePadTriggerFromString(GamePad, Button) >= Config.GamePadTriggerThreshold;
            }
            else
            {
                return (GetGamePadButtonFromString(GamePad, Button) == ButtonState.Pressed);
            }
        }

        private ButtonState GetGamePadButtonFromString(GamePadState GamePad, string Button) //Please make this prettier if you can.
        {
            ButtonState Result = GamePad.Buttons.A;

            switch (Button)
            {
                case "A":
                    Result = GamePad.Buttons.A;
                    break;
                case "B":
                    Result = GamePad.Buttons.B;
                    break;
                case "X":
                    Result = GamePad.Buttons.X;
                    break;
                case "Y":
                    Result = GamePad.Buttons.Y;
                    break;
                case "LStick":
                    Result = GamePad.Buttons.LeftStick;
                    break;
                case "RStick":
                    Result = GamePad.Buttons.RightStick;
                    break;
                case "LShoulder":
                    Result = GamePad.Buttons.LeftShoulder;
                    break;
                case "RShoulder":
                    Result = GamePad.Buttons.RightShoulder;
                    break;
                case "DPadUp":
                    Result = GamePad.DPad.Up;
                    break;
                case "DPadDown":
                    Result = GamePad.DPad.Down;
                    break;
                case "DPadLeft":
                    Result = GamePad.DPad.Left;
                    break;
                case "DPadRight":
                    Result = GamePad.DPad.Right;
                    break;
                case "Start":
                    Result = GamePad.Buttons.Start;
                    break;
                case "Back":
                    Result = GamePad.Buttons.Back;
                    break;
                default:
                    Console.Error.WriteLine("Invalid Button Mapping \"" + Button + "\"!  Defaulting to Button A.");
                    break;
            }

            return Result;
        }

        private float GetGamePadTriggerFromString(GamePadState GamePad, string Trigger)
        {
            float Result = 0;

            switch (Trigger)
            {
                case "LTrigger":
                    Result = GamePad.Triggers.Left;
                    break;
                case "RTrigger":
                    Result = GamePad.Triggers.Right;
                    break;
                default:
                    Console.Error.WriteLine("Invalid Trigger Mapping \"" + Trigger + "\"!  Defaulting to 0.");
                    break;
            }

            return Result;
        }

        private Vector2 GetJoystickAxisFromString(GamePadState GamePad, string Joystick)
        {
            Vector2 Result = new Vector2(0, 0);

            switch (Joystick)
            {
                case "LJoystick":
                    Result = GamePad.ThumbSticks.Left;
                    break;
                case "RJoystick":
                    Result = new Vector2(-GamePad.ThumbSticks.Right.Y, -GamePad.ThumbSticks.Right.X);
                    break;
                default:
                    Console.Error.WriteLine("Invalid Joystick Axis \"" + Joystick + "\"!  Defaulting the Vector2 to 0, 0.");
                    break;
            }

            return Result;
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            HidControllerButtons CurrentButton = 0;
            HidJoystickPosition LeftJoystick;
            HidJoystickPosition RightJoystick;

            int LeftJoystickDX = 0;
            int LeftJoystickDY = 0;
            int RightJoystickDX = 0;
            int RightJoystickDY = 0;
            float AnalogStickDeadzone = Config.GamePadDeadzone;

            //Keyboard Input
            if (Keyboard.HasValue)
            {
                KeyboardState Keyboard = this.Keyboard.Value;

                if (Keyboard[Key.Escape]) this.Exit();

                //LeftJoystick
                if (Keyboard[(Key)Config.JoyConKeyboard.Left.StickUp])    LeftJoystickDY = short.MaxValue;
                if (Keyboard[(Key)Config.JoyConKeyboard.Left.StickDown])  LeftJoystickDY = -short.MaxValue;
                if (Keyboard[(Key)Config.JoyConKeyboard.Left.StickLeft])  LeftJoystickDX = -short.MaxValue;
                if (Keyboard[(Key)Config.JoyConKeyboard.Left.StickRight]) LeftJoystickDX = short.MaxValue;

                //LeftButtons
                if (Keyboard[(Key)Config.JoyConKeyboard.Left.StickButton]) CurrentButton |= HidControllerButtons.KEY_LSTICK;
                if (Keyboard[(Key)Config.JoyConKeyboard.Left.DPadUp])      CurrentButton |= HidControllerButtons.KEY_DUP;
                if (Keyboard[(Key)Config.JoyConKeyboard.Left.DPadDown])    CurrentButton |= HidControllerButtons.KEY_DDOWN;
                if (Keyboard[(Key)Config.JoyConKeyboard.Left.DPadLeft])    CurrentButton |= HidControllerButtons.KEY_DLEFT;
                if (Keyboard[(Key)Config.JoyConKeyboard.Left.DPadRight])   CurrentButton |= HidControllerButtons.KEY_DRIGHT;
                if (Keyboard[(Key)Config.JoyConKeyboard.Left.ButtonMinus]) CurrentButton |= HidControllerButtons.KEY_MINUS;
                if (Keyboard[(Key)Config.JoyConKeyboard.Left.ButtonL])     CurrentButton |= HidControllerButtons.KEY_L;
                if (Keyboard[(Key)Config.JoyConKeyboard.Left.ButtonZL])    CurrentButton |= HidControllerButtons.KEY_ZL;

                //RightJoystick
                if (Keyboard[(Key)Config.JoyConKeyboard.Right.StickUp])    RightJoystickDY = short.MaxValue;
                if (Keyboard[(Key)Config.JoyConKeyboard.Right.StickDown])  RightJoystickDY = -short.MaxValue;
                if (Keyboard[(Key)Config.JoyConKeyboard.Right.StickLeft])  RightJoystickDX = -short.MaxValue;
                if (Keyboard[(Key)Config.JoyConKeyboard.Right.StickRight]) RightJoystickDX = short.MaxValue;

                //RightButtons
                if (Keyboard[(Key)Config.JoyConKeyboard.Right.StickButton]) CurrentButton |= HidControllerButtons.KEY_RSTICK;
                if (Keyboard[(Key)Config.JoyConKeyboard.Right.ButtonA])     CurrentButton |= HidControllerButtons.KEY_A;
                if (Keyboard[(Key)Config.JoyConKeyboard.Right.ButtonB])     CurrentButton |= HidControllerButtons.KEY_B;
                if (Keyboard[(Key)Config.JoyConKeyboard.Right.ButtonX])     CurrentButton |= HidControllerButtons.KEY_X;
                if (Keyboard[(Key)Config.JoyConKeyboard.Right.ButtonY])     CurrentButton |= HidControllerButtons.KEY_Y;
                if (Keyboard[(Key)Config.JoyConKeyboard.Right.ButtonPlus])  CurrentButton |= HidControllerButtons.KEY_PLUS;
                if (Keyboard[(Key)Config.JoyConKeyboard.Right.ButtonR])     CurrentButton |= HidControllerButtons.KEY_R;
                if (Keyboard[(Key)Config.JoyConKeyboard.Right.ButtonZR])    CurrentButton |= HidControllerButtons.KEY_ZR;
            }

            //Controller Input
            if (Config.GamePadEnable)
            {
                GamePadState GamePad = OpenTK.Input.GamePad.GetState(Config.GamePadIndex);

                //LeftButtons
                if (IsGamePadButtonPressedFromString(GamePad, Config.JoyConController.Left.DPadUp))       CurrentButton |= HidControllerButtons.KEY_DUP;
                if (IsGamePadButtonPressedFromString(GamePad, Config.JoyConController.Left.DPadDown))     CurrentButton |= HidControllerButtons.KEY_DDOWN;
                if (IsGamePadButtonPressedFromString(GamePad, Config.JoyConController.Left.DPadLeft))     CurrentButton |= HidControllerButtons.KEY_DLEFT;
                if (IsGamePadButtonPressedFromString(GamePad, Config.JoyConController.Left.DPadRight))    CurrentButton |= HidControllerButtons.KEY_DRIGHT;
                if (IsGamePadButtonPressedFromString(GamePad, Config.JoyConController.Left.StickButton))  CurrentButton |= HidControllerButtons.KEY_LSTICK;
                if (IsGamePadButtonPressedFromString(GamePad, Config.JoyConController.Left.ButtonMinus))  CurrentButton |= HidControllerButtons.KEY_MINUS;
                if (IsGamePadButtonPressedFromString(GamePad, Config.JoyConController.Left.ButtonL))      CurrentButton |= HidControllerButtons.KEY_L;
                if (IsGamePadButtonPressedFromString(GamePad, Config.JoyConController.Left.ButtonZL))     CurrentButton |= HidControllerButtons.KEY_ZL;

                //RightButtons
                if (IsGamePadButtonPressedFromString(GamePad, Config.JoyConController.Right.ButtonA))     CurrentButton |= HidControllerButtons.KEY_A;
                if (IsGamePadButtonPressedFromString(GamePad, Config.JoyConController.Right.ButtonB))     CurrentButton |= HidControllerButtons.KEY_B;
                if (IsGamePadButtonPressedFromString(GamePad, Config.JoyConController.Right.ButtonX))     CurrentButton |= HidControllerButtons.KEY_X;
                if (IsGamePadButtonPressedFromString(GamePad, Config.JoyConController.Right.ButtonY))     CurrentButton |= HidControllerButtons.KEY_Y;
                if (IsGamePadButtonPressedFromString(GamePad, Config.JoyConController.Right.StickButton)) CurrentButton |= HidControllerButtons.KEY_RSTICK;
                if (IsGamePadButtonPressedFromString(GamePad, Config.JoyConController.Right.ButtonPlus))  CurrentButton |= HidControllerButtons.KEY_PLUS;
                if (IsGamePadButtonPressedFromString(GamePad, Config.JoyConController.Right.ButtonR))     CurrentButton |= HidControllerButtons.KEY_R;
                if (IsGamePadButtonPressedFromString(GamePad, Config.JoyConController.Right.ButtonZR))    CurrentButton |= HidControllerButtons.KEY_ZR;

                //LeftJoystick
                if (GetJoystickAxisFromString(GamePad, Config.JoyConController.Left.Stick).X >= AnalogStickDeadzone
                 || GetJoystickAxisFromString(GamePad, Config.JoyConController.Left.Stick).X <= -AnalogStickDeadzone)
                    LeftJoystickDX = (int)(GetJoystickAxisFromString(GamePad, Config.JoyConController.Left.Stick).X * short.MaxValue);

                if (GetJoystickAxisFromString(GamePad, Config.JoyConController.Left.Stick).Y >= AnalogStickDeadzone
                 || GetJoystickAxisFromString(GamePad, Config.JoyConController.Left.Stick).Y <= -AnalogStickDeadzone)
                    LeftJoystickDY = (int)(GetJoystickAxisFromString(GamePad, Config.JoyConController.Left.Stick).Y * short.MaxValue);

                //RightJoystick
                if (GetJoystickAxisFromString(GamePad, Config.JoyConController.Right.Stick).X >= AnalogStickDeadzone
                 || GetJoystickAxisFromString(GamePad, Config.JoyConController.Right.Stick).X <= -AnalogStickDeadzone)
                    RightJoystickDX = (int)(GetJoystickAxisFromString(GamePad, Config.JoyConController.Right.Stick).X * short.MaxValue);

                if (GetJoystickAxisFromString(GamePad, Config.JoyConController.Right.Stick).Y >= AnalogStickDeadzone
                 || GetJoystickAxisFromString(GamePad, Config.JoyConController.Right.Stick).Y <= -AnalogStickDeadzone)
                    RightJoystickDY = (int)(GetJoystickAxisFromString(GamePad, Config.JoyConController.Right.Stick).Y * short.MaxValue);
            }

            LeftJoystick = new HidJoystickPosition
            {
                DX = LeftJoystickDX,
                DY = LeftJoystickDY
            };

            RightJoystick = new HidJoystickPosition
            {
                DX = RightJoystickDX,
                DY = RightJoystickDY
            };

            bool HasTouch = false;

            //Get screen touch position from left mouse click
            //OpenTK always captures mouse events, even if out of focus, so check if window is focused.
            if (Focused && Mouse?.LeftButton == ButtonState.Pressed)
            {
                MouseState Mouse = this.Mouse.Value;

                int ScrnWidth  = Width;
                int ScrnHeight = Height;

                if (Width > Height * TouchScreenRatioX)
                {
                    ScrnWidth = (int)(Height * TouchScreenRatioX);
                }
                else
                {
                    ScrnHeight = (int)(Width * TouchScreenRatioY);
                }

                int StartX = (Width  - ScrnWidth)  >> 1;
                int StartY = (Height - ScrnHeight) >> 1;

                int EndX = StartX + ScrnWidth;
                int EndY = StartY + ScrnHeight;

                if (Mouse.X >= StartX &&
                    Mouse.Y >= StartY &&
                    Mouse.X <  EndX   &&
                    Mouse.Y <  EndY)
                {
                    int ScrnMouseX = Mouse.X - StartX;
                    int ScrnMouseY = Mouse.Y - StartY;

                    int MX = (int)(((float)ScrnMouseX / ScrnWidth)  * TouchScreenWidth);
                    int MY = (int)(((float)ScrnMouseY / ScrnHeight) * TouchScreenHeight);

                    HidTouchPoint CurrentPoint = new HidTouchPoint
                    {
                        X = MX,
                        Y = MY,

                        //Placeholder values till more data is acquired
                        DiameterX = 10,
                        DiameterY = 10,
                        Angle     = 90
                    };

                    HasTouch = true;

                    Ns.Hid.SetTouchPoints(CurrentPoint);
                }
            }

            if (!HasTouch)
            {
                Ns.Hid.SetTouchPoints();
            }

            Ns.Hid.SetJoyconButton(
                HidControllerId.CONTROLLER_HANDHELD,
                HidControllerLayouts.Handheld_Joined,
                CurrentButton,
                LeftJoystick,
                RightJoystick);

            Ns.Hid.SetJoyconButton(
                HidControllerId.CONTROLLER_HANDHELD,
                HidControllerLayouts.Main,
                CurrentButton,
                LeftJoystick,
                RightJoystick);

            Ns.ProcessFrame();

            Renderer.RunActions();
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            Renderer.FrameBuffer.Render();

            Ns.Statistics.RecordSystemFrameTime();

            double HostFps = Ns.Statistics.GetSystemFrameRate();
            double GameFps = Ns.Statistics.GetGameFrameRate();

            Title = $"Ryujinx | Host FPS: {HostFps:0.0} | Game FPS: {GameFps:0.0}";

            SwapBuffers();

            Ns.Os.SignalVsync();
        }

        protected override void OnResize(EventArgs e)
        {
            Renderer.FrameBuffer.SetWindowSize(Width, Height);
        }

        protected override void OnKeyDown(KeyboardKeyEventArgs e)
        {
            Keyboard = e.Keyboard;
        }

        protected override void OnKeyUp(KeyboardKeyEventArgs e)
        {
            Keyboard = e.Keyboard;
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            Mouse = e.Mouse;
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            Mouse = e.Mouse;
        }

        protected override void OnMouseMove(MouseMoveEventArgs e)
        {
            Mouse = e.Mouse;
        }
    }
}