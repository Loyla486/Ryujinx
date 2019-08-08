﻿using DiscordRPC;
using Ryujinx.Audio;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.Gal;
using Ryujinx.Graphics.Gal.OpenGL;
using Ryujinx.HLE;
using Ryujinx.Profiler;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Ryujinx
{
    class Program
    {
        public static DiscordRpcClient DiscordClient;

        public static RichPresence DiscordPresence;

        public static string ApplicationDirectory => AppDomain.CurrentDomain.BaseDirectory;

        static void Main(string[] args)
        {
            Console.Title = "Ryujinx Console";

            IGalRenderer renderer = new OglRenderer();

            IAalOutput audioOut = InitializeAudioEngine();

            Switch device = new Switch(renderer, audioOut);

            Configuration.Load(Path.Combine(ApplicationDirectory, "Config.jsonc"));
            Configuration.Configure(device);

            Profile.Initialize();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            if (device.System.State.DiscordIntegrationEnabled == true)
            {
                DiscordClient = new DiscordRpcClient("568815339807309834");
                DiscordPresence = new RichPresence
                {
                    Assets = new Assets
                    {
                        LargeImageKey = "ryujinx",
                        LargeImageText = "Ryujinx is an emulator for the Nintendo Switch"
                    }
                };

                DiscordClient.Initialize();
                DiscordClient.SetPresence(DiscordPresence);
            }

            if (args.Length == 0)
            {
                Gtk.Application.Init();

                var resourceNames = Assembly.GetExecutingAssembly().GetManifestResourceNames();
                var app = new Gtk.Application("Ryujinx.Ryujinx", GLib.ApplicationFlags.None);
                app.Register(GLib.Cancellable.Current);

                var win = new MainMenu(device);
                app.AddWindow(win);

                win.Show();

                Gtk.Application.Run();
            }
            else
            {
                if (Directory.Exists(args[0]))
                {
                    string[] romFsFiles = Directory.GetFiles(args[0], "*.istorage");

                    if (romFsFiles.Length == 0)
                    {
                        romFsFiles = Directory.GetFiles(args[0], "*.romfs");
                    }

                    if (romFsFiles.Length > 0)
                    {
                        Logger.PrintInfo(LogClass.Application, "Loading as cart with RomFS.");
                        device.LoadCart(args[0], romFsFiles[0]);
                    }
                    else
                    {
                        Logger.PrintInfo(LogClass.Application, "Loading as cart WITHOUT RomFS.");
                        device.LoadCart(args[0]);
                    }
                }
                else if (File.Exists(args[0]))
                {
                    switch (Path.GetExtension(args[0]).ToLowerInvariant())
                    {
                        case ".xci":
                            Logger.PrintInfo(LogClass.Application, "Loading as XCI.");
                            device.LoadXci(args[0]);
                            break;
                        case ".nca":
                            Logger.PrintInfo(LogClass.Application, "Loading as NCA.");
                            device.LoadNca(args[0]);
                            break;
                        case ".nsp":
                        case ".pfs0":
                            Logger.PrintInfo(LogClass.Application, "Loading as NSP.");
                            device.LoadNsp(args[0]);
                            break;
                        default:
                            Logger.PrintInfo(LogClass.Application, "Loading as homebrew.");
                            device.LoadProgram(args[0]);
                            break;
                    }
                }
                else
                {
                    Logger.PrintWarning(LogClass.Application, "Please specify a valid XCI/NCA/NSP/PFS0/NRO file");
                }
            }

            if (device.System.State.DiscordIntegrationEnabled == true)
            {
                if (File.ReadAllLines(Path.Combine(ApplicationDirectory, "RPsupported.dat")).Contains(device.System.TitleID))
                {
                    DiscordPresence.Assets.LargeImageKey = device.System.TitleID;
                }

                DiscordPresence.Details = $"Playing {device.System.TitleName}";
                DiscordPresence.State = string.IsNullOrWhiteSpace(device.System.TitleID) ? string.Empty : device.System.TitleID.ToUpper();
                DiscordPresence.Assets.LargeImageText = device.System.TitleName;
                DiscordPresence.Assets.SmallImageKey = "ryujinx";
                DiscordPresence.Assets.SmallImageText = "Ryujinx is an emulator for the Nintendo Switch";
                DiscordPresence.Timestamps = new Timestamps(DateTime.UtcNow);

                DiscordClient.SetPresence(DiscordPresence);
            }

            using (GlScreen screen = new GlScreen(device, renderer))
            {
                screen.MainLoop();

                Profile.FinishProfiling();

                device.Dispose();
            }

            audioOut.Dispose();

            Logger.Shutdown();

            DiscordClient.Dispose();
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            Logger.Shutdown();

            DiscordClient.Dispose();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;

            Logger.PrintError(LogClass.Emulation, $"Unhandled exception caught: {exception}");

            if (e.IsTerminating)
            {
                Logger.Shutdown();

                DiscordClient.Dispose();
            }
        }

        /// <summary>
        /// Picks an <see cref="IAalOutput"/> audio output renderer supported on this machine
        /// </summary>
        /// <returns>An <see cref="IAalOutput"/> supported by this machine</returns>
        private static IAalOutput InitializeAudioEngine()
        {
            if (SoundIoAudioOut.IsSupported)
            {
                return new SoundIoAudioOut();
            }
            else if (OpenALAudioOut.IsSupported)
            {
                return new OpenALAudioOut();
            }
            else
            {
                return new DummyAudioOut();
            }
        }
    }
}
