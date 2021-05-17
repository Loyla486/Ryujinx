﻿using ARMeilleure.Translation;
using ARMeilleure.Translation.PTC;
using Gdk;
using Gtk;
using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.Configuration;
using Ryujinx.Graphics.GAL;
using Ryujinx.HLE.HOS.Services.Hid;
using Ryujinx.Input;
using Ryujinx.Input.HLE;
using Ryujinx.Ui.Widgets;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Ryujinx.Ui
{
    using Key = Input.Key;
    using Switch = HLE.Switch;

    public abstract class RendererWidgetBase : DrawingArea
    {
        private const int SwitchPanelWidth = 1280;
        private const int SwitchPanelHeight = 720;
        private const int TargetFps = 60;

        public ManualResetEvent WaitEvent { get; set; }
        public NpadManager NpadManager { get; }
        public Switch Device { get; private set; }
        public IRenderer Renderer { get; private set; }

        public static event EventHandler<StatusUpdatedEventArgs> StatusUpdatedEvent;

        private bool _isActive;
        private bool _isStopped;
        private bool _isFocused;

        private double _mouseX;
        private double _mouseY;
        private bool _mousePressed;

        private bool _toggleFullscreen;
        private bool _toggleDockedMode;

        private readonly long _ticksPerFrame;

        private long _ticks = 0;

        private readonly Stopwatch _chrono;

        private KeyboardHotkeyState _prevHotkeyState;

        private readonly ManualResetEvent _exitEvent;

        // Hide Cursor
        const int CursorHideIdleTime = 8; // seconds
        private static readonly Cursor _invisibleCursor = new Cursor(Display.Default, CursorType.BlankCursor);
        private long _lastCursorMoveTime;
        private bool _hideCursorOnIdle;
        private InputManager _inputManager;
        private IKeyboard _keyboardInterface;
        private GraphicsDebugLevel _glLogLevel;
        private string _gpuVendorName;

        private int _windowHeight;
        private int _windowWidth;

        public RendererWidgetBase(InputManager inputManager, GraphicsDebugLevel glLogLevel)
        {
            _inputManager = inputManager;
            NpadManager = _inputManager.CreateNpadManager();
            _keyboardInterface = (IKeyboard)_inputManager.KeyboardDriver.GetGamepad("0");

            WaitEvent = new ManualResetEvent(false);

            _glLogLevel = glLogLevel;

            Destroyed += Renderer_Destroyed;

            _chrono = new Stopwatch();

            _ticksPerFrame = Stopwatch.Frequency / TargetFps;

            AddEvents((int)(EventMask.ButtonPressMask
                          | EventMask.ButtonReleaseMask
                          | EventMask.PointerMotionMask
                          | EventMask.KeyPressMask
                          | EventMask.KeyReleaseMask));

            Shown += Renderer_Shown;

            _exitEvent = new ManualResetEvent(false);

            _hideCursorOnIdle = ConfigurationState.Instance.HideCursorOnIdle;
            _lastCursorMoveTime = Stopwatch.GetTimestamp();

            ConfigurationState.Instance.HideCursorOnIdle.Event += HideCursorStateChanged;
        }

        public abstract void InitializeRenderer();

        public abstract void SwapBuffers();

        public abstract string GetGpuVendorName();

        private void HideCursorStateChanged(object sender, ReactiveEventArgs<bool> state)
        {
            Gtk.Application.Invoke(delegate
            {
                _hideCursorOnIdle = state.NewValue;

                if (_hideCursorOnIdle)
                {
                    _lastCursorMoveTime = Stopwatch.GetTimestamp();
                }
                else
                {
                    Window.Cursor = null;
                }
            });
        }

        private void Parent_FocusOutEvent(object o, Gtk.FocusOutEventArgs args)
        {
            _isFocused = false;
        }

        private void Parent_FocusInEvent(object o, Gtk.FocusInEventArgs args)
        {
            _isFocused = true;
        }

        private void Renderer_Destroyed(object sender, EventArgs e)
        {
            ConfigurationState.Instance.HideCursorOnIdle.Event -= HideCursorStateChanged;

            NpadManager.Dispose();
            Dispose();
        }

        private void Renderer_Shown(object sender, EventArgs e)
        {
            _isFocused = ParentWindow.State.HasFlag(Gdk.WindowState.Focused);
        }

        protected override bool OnButtonPressEvent(EventButton evnt)
        {
            _mouseX = evnt.X;
            _mouseY = evnt.Y;

            if (evnt.Button == 1)
            {
                _mousePressed = true;
            }

            return false;
        }

        protected override bool OnButtonReleaseEvent(EventButton evnt)
        {
            if (evnt.Button == 1)
            {
                _mousePressed = false;
            }

            return false;
        }

        protected override bool OnMotionNotifyEvent(EventMotion evnt)
        {
            if (evnt.Device.InputSource == InputSource.Mouse)
            {
                _mouseX = evnt.X;
                _mouseY = evnt.Y;
            }

            if (_hideCursorOnIdle)
            {
                _lastCursorMoveTime = Stopwatch.GetTimestamp();
            }

            return false;
        }

        protected override void OnGetPreferredHeight(out int minimumHeight, out int naturalHeight)
        {
            Gdk.Monitor monitor = Display.GetMonitorAtWindow(Window);

            // If the monitor is at least 1080p, use the Switch panel size as minimal size.
            if (monitor.Geometry.Height >= 1080)
            {
                minimumHeight = SwitchPanelHeight;
            }
            // Otherwise, we default minimal size to 480p 16:9.
            else
            {
                minimumHeight = 480;
            }

            naturalHeight = minimumHeight;
        }

        protected override void OnGetPreferredWidth(out int minimumWidth, out int naturalWidth)
        {
            Gdk.Monitor monitor = Display.GetMonitorAtWindow(Window);

            // If the monitor is at least 1080p, use the Switch panel size as minimal size.
            if (monitor.Geometry.Height >= 1080)
            {
                minimumWidth = SwitchPanelWidth;
            }
            // Otherwise, we default minimal size to 480p 16:9.
            else
            {
                minimumWidth = 854;
            }

            naturalWidth = minimumWidth;
        }

        protected override bool OnConfigureEvent(EventConfigure evnt)
        {
            bool result = base.OnConfigureEvent(evnt);

            Gdk.Monitor monitor = Display.GetMonitorAtWindow(Window);

            _windowWidth = evnt.Width * monitor.ScaleFactor;
            _windowHeight = evnt.Height * monitor.ScaleFactor;

            Renderer?.Window.SetSize(_windowWidth, _windowHeight);

            return result;
        }

        private void HandleScreenState(KeyboardStateSnapshot keyboard)
        {
            bool toggleFullscreen = keyboard.IsPressed(Key.F11)
                                || ((keyboard.IsPressed(Key.AltLeft)
                                || keyboard.IsPressed(Key.AltRight))
                                && keyboard.IsPressed(Key.Enter))
                                || keyboard.IsPressed(Key.Escape);

            bool fullScreenToggled = ParentWindow.State.HasFlag(Gdk.WindowState.Fullscreen);

            if (toggleFullscreen != _toggleFullscreen)
            {
                if (toggleFullscreen)
                {
                    if (fullScreenToggled)
                    {
                        ParentWindow.Unfullscreen();
                        (Toplevel as MainWindow)?.ToggleExtraWidgets(true);
                    }
                    else
                    {
                        if (keyboard.IsPressed(Key.Escape))
                        {
                            if (!ConfigurationState.Instance.ShowConfirmExit || GtkDialog.CreateExitDialog())
                            {
                                Exit();
                            }
                        }
                        else
                        {
                            ParentWindow.Fullscreen();
                            (Toplevel as MainWindow)?.ToggleExtraWidgets(false);
                        }
                    }
                }
            }

            _toggleFullscreen = toggleFullscreen;

            bool toggleDockedMode = keyboard.IsPressed(Key.F9);

            if (toggleDockedMode != _toggleDockedMode)
            {
                if (toggleDockedMode)
                {
                    ConfigurationState.Instance.System.EnableDockedMode.Value =
                        !ConfigurationState.Instance.System.EnableDockedMode.Value;
                }
            }

            _toggleDockedMode = toggleDockedMode;

            if (_hideCursorOnIdle)
            {
                long cursorMoveDelta = Stopwatch.GetTimestamp() - _lastCursorMoveTime;
                Window.Cursor = (cursorMoveDelta >= CursorHideIdleTime * Stopwatch.Frequency) ? _invisibleCursor : null;
            }
        }

        public void Initialize(Switch device)
        {
            Device = device;
            Renderer = Device.Gpu.Renderer;
            Renderer?.Window.SetSize(_windowWidth, _windowHeight);

            NpadManager.Initialize(device, ConfigurationState.Instance.Hid.InputConfig, ConfigurationState.Instance.Hid.EnableKeyboard);
        }

        public void Render()
        {
            Gtk.Window parent = Toplevel as Gtk.Window;
            parent.Present();

            InitializeRenderer();

            Device.Gpu.Renderer.Initialize(_glLogLevel);

            _gpuVendorName = GetGpuVendorName();

            Device.Gpu.InitializeShaderCache();
            Translator.IsReadyForTranslation.Set();

            while (_isActive)
            {
                if (_isStopped)
                {
                    return;
                }

                _ticks += _chrono.ElapsedTicks;

                _chrono.Restart();

                if (Device.WaitFifo())
                {
                    Device.Statistics.RecordFifoStart();
                    Device.ProcessFrame();
                    Device.Statistics.RecordFifoEnd();
                }

                while (Device.ConsumeFrameAvailable())
                {
                    Device.PresentFrame(SwapBuffers);
                }

                if (_ticks >= _ticksPerFrame)
                {
                    string dockedMode = ConfigurationState.Instance.System.EnableDockedMode ? "Docked" : "Handheld";
                    float scale = Graphics.Gpu.GraphicsConfig.ResScale;
                    if (scale != 1)
                    {
                        dockedMode += $" ({scale}x)";
                    }

                    StatusUpdatedEvent?.Invoke(this, new StatusUpdatedEventArgs(
                        Device.EnableDeviceVsync,
                        dockedMode,
                        ConfigurationState.Instance.Graphics.AspectRatio.Value.ToText(),
                        $"Game: {Device.Statistics.GetGameFrameRate():00.00} FPS",
                        $"FIFO: {Device.Statistics.GetFifoPercent():0.00} %",
                        $"GPU: {_gpuVendorName}"));

                    _ticks = Math.Min(_ticks - _ticksPerFrame, _ticksPerFrame);
                }
            }
        }

        public void Start()
        {
            _chrono.Restart();

            _isActive = true;

            Gtk.Window parent = this.Toplevel as Gtk.Window;

            parent.FocusInEvent += Parent_FocusInEvent;
            parent.FocusOutEvent += Parent_FocusOutEvent;

            Application.Invoke(delegate
            {
                parent.Present();

                string titleNameSection = string.IsNullOrWhiteSpace(Device.Application.TitleName) ? string.Empty
                    : $" - {Device.Application.TitleName}";

                string titleVersionSection = string.IsNullOrWhiteSpace(Device.Application.DisplayVersion) ? string.Empty
                    : $" v{Device.Application.DisplayVersion}";

                string titleIdSection = string.IsNullOrWhiteSpace(Device.Application.TitleIdText) ? string.Empty
                    : $" ({Device.Application.TitleIdText.ToUpper()})";

                string titleArchSection = Device.Application.TitleIs64Bit ? " (64-bit)" : " (32-bit)";

                parent.Title = $"Ryujinx {Program.Version}{titleNameSection}{titleVersionSection}{titleIdSection}{titleArchSection}";
            });

            Thread renderLoopThread = new Thread(Render)
            {
                Name = "GUI.RenderLoop"
            };
            renderLoopThread.Start();

            Thread nvStutterWorkaround = new Thread(NVStutterWorkaround)
            {
                Name = "GUI.NVStutterWorkaround"
            };
            nvStutterWorkaround.Start();

            MainLoop();

            renderLoopThread.Join();
            nvStutterWorkaround.Join();

            Exit();
        }

        public void Exit()
        {
            NpadManager?.Dispose();

            if (_isStopped)
            {
                return;
            }

            _isStopped = true;
            _isActive = false;

            _exitEvent.WaitOne();
            _exitEvent.Dispose();
        }

        private void NVStutterWorkaround()
        {
            while (_isActive)
            {
                // When NVIDIA Threaded Optimization is on, the driver will snapshot all threads in the system whenever the application creates any new ones.
                // The ThreadPool has something called a "GateThread" which terminates itself after some inactivity.
                // However, it immediately starts up again, since the rules regarding when to terminate and when to start differ.
                // This creates a new thread every second or so.
                // The main problem with this is that the thread snapshot can take 70ms, is on the OpenGL thread and will delay rendering any graphics.
                // This is a little over budget on a frame time of 16ms, so creates a large stutter.
                // The solution is to keep the ThreadPool active so that it never has a reason to terminate the GateThread.

                // TODO: This should be removed when the issue with the GateThread is resolved.

                ThreadPool.QueueUserWorkItem((state) => { });
                Thread.Sleep(300);
            }
        }

        public void MainLoop()
        {
            while (_isActive)
            {
                UpdateFrame();

                // Polling becomes expensive if it's not slept
                Thread.Sleep(1);
            }

            _exitEvent.Set();
        }

        private bool UpdateFrame()
        {
            if (!_isActive)
            {
                return true;
            }

            if (_isStopped)
            {
                return false;
            }

            if (_isFocused)
            {
                Gtk.Application.Invoke(delegate
                {
                    KeyboardStateSnapshot keyboard = _keyboardInterface.GetKeyboardStateSnapshot();

                    HandleScreenState(keyboard);

                    if (keyboard.IsPressed(Key.Delete))
                    {
                        if (!ParentWindow.State.HasFlag(WindowState.Fullscreen))
                        {
                            Ptc.Continue();
                        }
                    }
                });
            }

            NpadManager.Update();

            if (_isFocused)
            {
                KeyboardHotkeyState currentHotkeyState = GetHotkeyState();

                if (currentHotkeyState.HasFlag(KeyboardHotkeyState.ToggleVSync) &&
                    !_prevHotkeyState.HasFlag(KeyboardHotkeyState.ToggleVSync))
                {
                    Device.EnableDeviceVsync = !Device.EnableDeviceVsync;
                }

                _prevHotkeyState = currentHotkeyState;
            }

            // Touchscreen
            bool hasTouch = false;

            // Get screen touch position from left mouse click
            // OpenTK always captures mouse events, even if out of focus, so check if window is focused.
            if (_isFocused && _mousePressed)
            {
                float aspectWidth = SwitchPanelHeight * ConfigurationState.Instance.Graphics.AspectRatio.Value.ToFloat();

                int screenWidth = AllocatedWidth;
                int screenHeight = AllocatedHeight;

                if (AllocatedWidth > AllocatedHeight * aspectWidth / SwitchPanelHeight)
                {
                    screenWidth = (int)(AllocatedHeight * aspectWidth) / SwitchPanelHeight;
                }
                else
                {
                    screenHeight = (AllocatedWidth * SwitchPanelHeight) / (int)aspectWidth;
                }

                int startX = (AllocatedWidth - screenWidth) >> 1;
                int startY = (AllocatedHeight - screenHeight) >> 1;

                int endX = startX + screenWidth;
                int endY = startY + screenHeight;

                if (_mouseX >= startX &&
                    _mouseY >= startY &&
                    _mouseX < endX &&
                    _mouseY < endY)
                {
                    int screenMouseX = (int)_mouseX - startX;
                    int screenMouseY = (int)_mouseY - startY;

                    int mX = (screenMouseX * (int)aspectWidth) / screenWidth;
                    int mY = (screenMouseY * SwitchPanelHeight) / screenHeight;

                    TouchPoint currentPoint = new TouchPoint
                    {
                        X = (uint)mX,
                        Y = (uint)mY,

                        // Placeholder values till more data is acquired
                        DiameterX = 10,
                        DiameterY = 10,
                        Angle = 90
                    };

                    hasTouch = true;

                    Device.Hid.Touchscreen.Update(currentPoint);
                }
            }

            if (!hasTouch)
            {
                Device.Hid.Touchscreen.Update();
            }

            Device.Hid.DebugPad.Update();

            return true;
        }


        [Flags]
        private enum KeyboardHotkeyState
        {
            None,
            ToggleVSync
        }

        private KeyboardHotkeyState GetHotkeyState()
        {
            KeyboardHotkeyState state = KeyboardHotkeyState.None;

            if (_keyboardInterface.IsPressed((Key)ConfigurationState.Instance.Hid.Hotkeys.Value.ToggleVsync))
            {
                state |= KeyboardHotkeyState.ToggleVSync;
            }

            return state;
        }
    }
}
