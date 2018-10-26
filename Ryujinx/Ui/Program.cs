﻿using Ryujinx.Audio;
using Ryujinx.Audio.OpenAL;
using Ryujinx.Common.Logging;
using Ryujinx.Graphics.Gal;
using Ryujinx.Graphics.Gal.OpenGL;
using Ryujinx.HLE;
using System;
using System.IO;

namespace Ryujinx
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.Title = "Ryujinx Console";

            IGalRenderer renderer = new OGLRenderer();

            IAalOutput audioOut = new OpenALAudioOut();

            Switch device = new Switch(renderer, audioOut);

            Config.Read(device);

            Logger.Updated += ConsoleLog.Log;

            if (args.Length == 1)
            {
                if (Directory.Exists(args[0]))
                {
                    string[] romFsFiles = Directory.GetFiles(args[0], "*.istorage");

                    if (romFsFiles.Length == 0) romFsFiles = Directory.GetFiles(args[0], "*.romfs");

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

            using (GLScreen screen = new GLScreen(device, renderer))
            {
                screen.MainLoop();

                device.Dispose();
            }

            audioOut.Dispose();
        }
    }
}
