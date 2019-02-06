﻿using Ryujinx.Audio;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.Gal;
using Ryujinx.Graphics.Gal.OpenGL;
using Ryujinx.HLE;
using System;
using System.IO;

namespace Ryujinx
{
    class Program
    {
        static ILogTarget _fileTarget;
        static ILogTarget _consoleTarget;

        static void Main(string[] args)
        {
            Console.Title = "Ryujinx Console";

            IGalRenderer renderer = new OGLRenderer();

            IAalOutput audioOut = InitializeAudioEngine();

            Switch device = new Switch(renderer, audioOut);

            Config.Read(device);

            _fileTarget = new AsyncLogTargetWrapper(new FileLogTarget("Ryujinx.log"), 1000, AsyncLogTargetOverflowAction.Block);
            _consoleTarget = new AsyncLogTargetWrapper(new ConsoleLogTarget(), 1000, AsyncLogTargetOverflowAction.Block);

            Logger.Updated += _fileTarget.Log;
            Logger.Updated += _consoleTarget.Log;

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            AppDomain.CurrentDomain.ProcessExit        += CurrentDomain_ProcessExit;

            if (args.Length == 1)
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
                        Console.WriteLine("Loading as cart with RomFS.");

                        device.LoadCart(args[0], romFsFiles[0]);
                    }
                    else
                    {
                        Console.WriteLine("Loading as cart WITHOUT RomFS.");

                        device.LoadCart(args[0]);
                    }
                }
                else if (File.Exists(args[0]))
                {
                    switch (Path.GetExtension(args[0]).ToLowerInvariant())
                    {
                        case ".xci":
                            Console.WriteLine("Loading as XCI.");
                            device.LoadXci(args[0]);
                            break;
                        case ".nca":
                            Console.WriteLine("Loading as NCA.");
                            device.LoadNca(args[0]);
                            break;
                        case ".nsp":
                        case ".pfs0":
                            Console.WriteLine("Loading as NSP.");
                            device.LoadNsp(args[0]);
                            break;
                        default:
                            Console.WriteLine("Loading as homebrew.");
                            device.LoadProgram(args[0]);
                            break;
                    }
                }
            }
            else
            {
                Console.WriteLine("Please specify the folder with the NSOs/IStorage or a NSO/NRO.");
            }

            using (GlScreen screen = new GlScreen(device, renderer))
            {
                screen.MainLoop();

                device.Dispose();
            }

            audioOut.Dispose();
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            _fileTarget.Dispose();
            _consoleTarget.Dispose();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;

            Logger.PrintError(LogClass.Emulation, $"Unhandled exception caught: {exception}");

            if (e.IsTerminating)
            {
                _fileTarget.Dispose();
                _consoleTarget.Dispose();
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
