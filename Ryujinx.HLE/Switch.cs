using LibHac.FsSystem;
using Ryujinx.Audio.Backends.CompatLayer;
using Ryujinx.Audio.Integration;
using Ryujinx.Common;
using Ryujinx.Common.Logging;
using Ryujinx.Configuration;
using Ryujinx.Graphics.GAL;
using Ryujinx.Graphics.Gpu;
using Ryujinx.Graphics.Host1x;
using Ryujinx.Graphics.Nvdec;
using Ryujinx.Graphics.Vic;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.FileSystem.Content;
using Ryujinx.HLE.HOS;
using Ryujinx.HLE.HOS.Services;
using Ryujinx.HLE.HOS.Services.Apm;
using Ryujinx.HLE.HOS.Services.Hid;
using Ryujinx.HLE.HOS.Services.Nv.NvDrvServices;
using Ryujinx.HLE.HOS.SystemState;
using Ryujinx.Memory;
using System;

namespace Ryujinx.HLE
{
    public class Switch : IDisposable
    {
        private MemoryConfiguration _memoryConfiguration;

        public IHardwareDeviceDriver AudioDeviceDriver { get; private set; }

        internal MemoryBlock Memory { get; private set; }

        public GpuContext Gpu { get; private set; }

        internal NvMemoryAllocator MemoryAllocator { get; private set; }

        internal Host1xDevice Host1x { get; }

        public VirtualFileSystem FileSystem { get; private set; }

        public Horizon System { get; private set; }

        public ApplicationLoader Application { get; }

        public PerformanceStatistics Statistics { get; private set; }

        public UserChannelPersistence UserChannelPersistence { get; }

        public Hid Hid { get; private set; }

        public TamperMachine TamperMachine { get; private set; }

        public IHostUiHandler UiHandler { get; set; }

        public bool EnableDeviceVsync { get; set; } = true;

        public Switch(
            VirtualFileSystem fileSystem,
            ContentManager contentManager,
            UserChannelPersistence userChannelPersistence,
            IRenderer renderer,
            IHardwareDeviceDriver audioDeviceDriver,
            MemoryConfiguration memoryConfiguration)
        {
            if (renderer == null)
            {
                throw new ArgumentNullException(nameof(renderer));
            }

            if (audioDeviceDriver == null)
            {
                throw new ArgumentNullException(nameof(audioDeviceDriver));
            }

            if (userChannelPersistence == null)
            {
                throw new ArgumentNullException(nameof(userChannelPersistence));
            }

            UserChannelPersistence = userChannelPersistence;

            _memoryConfiguration = memoryConfiguration;

            AudioDeviceDriver = new CompatLayerHardwareDeviceDriver(audioDeviceDriver);

            Memory = new MemoryBlock(memoryConfiguration.ToDramSize());

            Gpu = new GpuContext(renderer);

            MemoryAllocator = new NvMemoryAllocator();

            Host1x = new Host1xDevice(Gpu.Synchronization);
            var nvdec = new NvdecDevice(Gpu.MemoryManager);
            var vic = new VicDevice(Gpu.MemoryManager);
            Host1x.RegisterDevice(ClassId.Nvdec, nvdec);
            Host1x.RegisterDevice(ClassId.Vic, vic);

            nvdec.FrameDecoded += (FrameDecodedEventArgs e) =>
            {
                // FIXME:
                // Figure out what is causing frame ordering issues on H264.
                // For now this is needed as workaround.
                if (e.CodecId == CodecId.H264)
                {
                    vic.SetSurfaceOverride(e.LumaOffset, e.ChromaOffset, 0);
                }
                else
                {
                    vic.DisableSurfaceOverride();
                }
            };

            FileSystem = fileSystem;

            System = new Horizon(this, contentManager, memoryConfiguration);
            System.InitializeServices();

            Statistics = new PerformanceStatistics();

            Hid = new Hid(this, System.HidBaseAddress);
            Hid.InitDevices();

            Application = new ApplicationLoader(this, fileSystem, contentManager);

            TamperMachine = new TamperMachine();
        }

        public void Initialize()
        {
            System.State.SetLanguage((SystemLanguage)ConfigurationState.Instance.System.Language.Value);

            System.State.SetRegion((RegionCode)ConfigurationState.Instance.System.Region.Value);

            EnableDeviceVsync = ConfigurationState.Instance.Graphics.EnableVsync;

            System.State.DockedMode = ConfigurationState.Instance.System.EnableDockedMode;

            System.PerformanceState.PerformanceMode = System.State.DockedMode ? PerformanceMode.Boost : PerformanceMode.Default;

            System.EnablePtc = ConfigurationState.Instance.System.EnablePtc;

            System.FsIntegrityCheckLevel = GetIntegrityCheckLevel();

            System.GlobalAccessLogMode = ConfigurationState.Instance.System.FsGlobalAccessLogMode;

            ServiceConfiguration.IgnoreMissingServices = ConfigurationState.Instance.System.IgnoreMissingServices;
            ConfigurationState.Instance.System.IgnoreMissingServices.Event += (object _, ReactiveEventArgs<bool> args) =>
            {
                ServiceConfiguration.IgnoreMissingServices = args.NewValue;
            };

            // Configure controllers
            Hid.RefreshInputConfig(ConfigurationState.Instance.Hid.InputConfig.Value);
            ConfigurationState.Instance.Hid.InputConfig.Event += Hid.RefreshInputConfigEvent;

            Logger.Info?.Print(LogClass.Application, $"AudioBackend: {ConfigurationState.Instance.System.AudioBackend.Value}");
            Logger.Info?.Print(LogClass.Application, $"IsDocked: {ConfigurationState.Instance.System.EnableDockedMode.Value}");
            Logger.Info?.Print(LogClass.Application, $"Vsync: {ConfigurationState.Instance.Graphics.EnableVsync.Value}");
            Logger.Info?.Print(LogClass.Application, $"MemoryConfiguration: {_memoryConfiguration}");
        }

        public static IntegrityCheckLevel GetIntegrityCheckLevel()
        {
            return ConfigurationState.Instance.System.EnableFsIntegrityChecks
                ? IntegrityCheckLevel.ErrorOnInvalid
                : IntegrityCheckLevel.None;
        }

        public void LoadCart(string exeFsDir, string romFsFile = null)
        {
            Application.LoadCart(exeFsDir, romFsFile);
        }

        public void LoadXci(string xciFile)
        {
            Application.LoadXci(xciFile);
        }

        public void LoadNca(string ncaFile)
        {
            Application.LoadNca(ncaFile);
        }

        public void LoadNsp(string nspFile)
        {
            Application.LoadNsp(nspFile);
        }

        public void LoadProgram(string fileName)
        {
            Application.LoadProgram(fileName);
        }

        public bool WaitFifo()
        {
            return Gpu.GPFifo.WaitForCommands();
        }

        public void ProcessFrame()
        {
            Gpu.Renderer.PreFrame();

            Gpu.GPFifo.DispatchCalls();
        }

        public bool ConsumeFrameAvailable()
        {
            return Gpu.Window.ConsumeFrameAvailable();
        }

        public void PresentFrame(Action swapBuffersCallback)
        {
            Gpu.Window.Present(swapBuffersCallback);
        }

        public void DisposeGpu()
        {
            Gpu.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                ConfigurationState.Instance.Hid.InputConfig.Event -= Hid.RefreshInputConfigEvent;

                System.Dispose();
                Host1x.Dispose();
                AudioDeviceDriver.Dispose();
                FileSystem.Unload();
                Memory.Dispose();
            }
        }
    }
}
