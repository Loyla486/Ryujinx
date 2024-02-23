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

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            HidControllerButtons CurrentButton = 0;
            HidJoystickPosition LeftJoystick;
            HidJoystickPosition RightJoystick;

            int LeftJoystickDX = 0;
            int LeftJoystickDY = 0;
            int RightJoystickDX = 0;
            int RightJoystickDY = 0;

            if (Keyboard.HasValue)
            {
                KeyboardState Keyboard = this.Keyboard.Value;

                if (Keyboard[Key.Escape]) this.Exit();

                //RightJoystick
                if (Keyboard[(Key)Config.FakeJoyCon.Left.StickUp])    LeftJoystickDY = short.MaxValue;
                if (Keyboard[(Key)Config.FakeJoyCon.Left.StickDown])  LeftJoystickDY = -short.MaxValue;
                if (Keyboard[(Key)Config.FakeJoyCon.Left.StickLeft])  LeftJoystickDX = -short.MaxValue;
                if (Keyboard[(Key)Config.FakeJoyCon.Left.StickRight]) LeftJoystickDX = short.MaxValue;

                //LeftButtons
                if (Keyboard[(Key)Config.FakeJoyCon.Left.StickButton]) CurrentButton |= HidControllerButtons.KEY_LSTICK;
                if (Keyboard[(Key)Config.FakeJoyCon.Left.DPadUp])      CurrentButton |= HidControllerButtons.KEY_DUP;
                if (Keyboard[(Key)Config.FakeJoyCon.Left.DPadDown])    CurrentButton |= HidControllerButtons.KEY_DDOWN;
                if (Keyboard[(Key)Config.FakeJoyCon.Left.DPadLeft])    CurrentButton |= HidControllerButtons.KEY_DLEFT;
                if (Keyboard[(Key)Config.FakeJoyCon.Left.DPadRight])   CurrentButton |= HidControllerButtons.KEY_DRIGHT;
                if (Keyboard[(Key)Config.FakeJoyCon.Left.ButtonMinus]) CurrentButton |= HidControllerButtons.KEY_MINUS;
                if (Keyboard[(Key)Config.FakeJoyCon.Left.ButtonL])     CurrentButton |= HidControllerButtons.KEY_L;
                if (Keyboard[(Key)Config.FakeJoyCon.Left.ButtonZL])    CurrentButton |= HidControllerButtons.KEY_ZL;

                //RightJoystick
                if (Keyboard[(Key)Config.FakeJoyCon.Right.StickUp])    RightJoystickDY = short.MaxValue;
                if (Keyboard[(Key)Config.FakeJoyCon.Right.StickDown])  RightJoystickDY = -short.MaxValue;
                if (Keyboard[(Key)Config.FakeJoyCon.Right.StickLeft])  RightJoystickDX = -short.MaxValue;
                if (Keyboard[(Key)Config.FakeJoyCon.Right.StickRight]) RightJoystickDX = short.MaxValue;

                //RightButtons
                if (Keyboard[(Key)Config.FakeJoyCon.Right.StickButton]) CurrentButton |= HidControllerButtons.KEY_RSTICK;
                if (Keyboard[(Key)Config.FakeJoyCon.Right.ButtonA])     CurrentButton |= HidControllerButtons.KEY_A;
                if (Keyboard[(Key)Config.FakeJoyCon.Right.ButtonB])     CurrentButton |= HidControllerButtons.KEY_B;
                if (Keyboard[(Key)Config.FakeJoyCon.Right.ButtonX])     CurrentButton |= HidControllerButtons.KEY_X;
                if (Keyboard[(Key)Config.FakeJoyCon.Right.ButtonY])     CurrentButton |= HidControllerButtons.KEY_Y;
                if (Keyboard[(Key)Config.FakeJoyCon.Right.ButtonPlus])  CurrentButton |= HidControllerButtons.KEY_PLUS;
                if (Keyboard[(Key)Config.FakeJoyCon.Right.ButtonR])     CurrentButton |= HidControllerButtons.KEY_R;
                if (Keyboard[(Key)Config.FakeJoyCon.Right.ButtonZR])    CurrentButton |= HidControllerButtons.KEY_ZR;
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