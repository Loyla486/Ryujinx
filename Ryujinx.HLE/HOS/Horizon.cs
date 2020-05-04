using LibHac;
using LibHac.Account;
using LibHac.Bcat;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.FsSystem.NcaUtils;
using LibHac.Ncm;
using LibHac.Ns;
using LibHac.Spl;
using Ryujinx.Common;
using Ryujinx.Common.Configuration;
using Ryujinx.Common.Logging;
using Ryujinx.Configuration;
using Ryujinx.HLE.FileSystem.Content;
using Ryujinx.HLE.HOS.Font;
using Ryujinx.HLE.HOS.Kernel;
using Ryujinx.HLE.HOS.Kernel.Memory;
using Ryujinx.HLE.HOS.Kernel.Process;
using Ryujinx.HLE.HOS.Kernel.Threading;
using Ryujinx.HLE.HOS.Services.Am.AppletAE.AllSystemAppletProxiesService.SystemAppletProxy;
using Ryujinx.HLE.HOS.Services.Arp;
using Ryujinx.HLE.HOS.Services.Mii;
using Ryujinx.HLE.HOS.Services.Nv;
using Ryujinx.HLE.HOS.Services.Nv.NvDrvServices.NvHostCtrl;
using Ryujinx.HLE.HOS.Services.Pcv.Bpc;
using Ryujinx.HLE.HOS.Services.Settings;
using Ryujinx.HLE.HOS.Services.Sm;
using Ryujinx.HLE.HOS.Services.SurfaceFlinger;
using Ryujinx.HLE.HOS.Services.Time.Clock;
using Ryujinx.HLE.HOS.SystemState;
using Ryujinx.HLE.Loaders.Executables;
using Ryujinx.HLE.Loaders.Npdm;
using Ryujinx.HLE.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using static LibHac.Fs.ApplicationSaveDataManagement;

namespace Ryujinx.HLE.HOS
{
    using TimeServiceManager = Services.Time.TimeManager;
    using JsonHelper         = Common.Utilities.JsonHelper;

    public class Horizon : IDisposable
    {
        internal const int HidSize  = 0x40000;
        internal const int FontSize = 0x1100000;
        internal const int IirsSize = 0x8000;
        internal const int TimeSize = 0x1000;

        internal KernelContext KernelContext { get; }

        internal Switch Device { get; private set; }

        internal SurfaceFlinger SurfaceFlinger { get; private set; }

        public SystemStateMgr State { get; private set; }

        internal AppletStateMgr AppletState { get; private set; }

        internal KSharedMemory HidSharedMem  { get; private set; }
        internal KSharedMemory FontSharedMem { get; private set; }
        internal KSharedMemory IirsSharedMem { get; private set; }
        internal SharedFontManager Font { get; private set; }

        internal ContentManager ContentManager { get; private set; }

        internal KEvent VsyncEvent { get; private set; }

        internal KEvent DisplayResolutionChangeEvent { get; private set; }

        public Keyset KeySet => Device.FileSystem.KeySet;

#pragma warning disable CS0649
        private bool _hasStarted;
#pragma warning restore CS0649
        private bool _isDisposed;

        public BlitStruct<ApplicationControlProperty> ControlData { get; set; }

        public string TitleName { get; private set; }

        public ulong  TitleId { get; private set; }
        public string TitleIdText => TitleId.ToString("x16");

        public string TitleVersionString { get; private set; }

        public bool TitleIs64Bit { get; private set; }

        public IntegrityCheckLevel FsIntegrityCheckLevel { get; set; }

        public int GlobalAccessLogMode { get; set; }

        internal ulong HidBaseAddress { get; private set; }

        internal NvHostSyncpt HostSyncpoint { get; private set; }

        internal LibHac.Horizon LibHacHorizonServer { get; private set; }
        internal HorizonClient LibHacHorizonClient { get; private set; }

        public Horizon(Switch device, ContentManager contentManager)
        {
            ControlData = new BlitStruct<ApplicationControlProperty>(1);

            KernelContext = new KernelContext(device, device.Memory);

            Device = device;

            State = new SystemStateMgr();

            // Note: This is not really correct, but with HLE of services, the only memory
            // region used that is used is Application, so we can use the other ones for anything.
            KMemoryRegionManager region = KernelContext.MemoryRegions[(int)MemoryRegion.NvServices];

            ulong hidPa  = region.Address;
            ulong fontPa = region.Address + HidSize;
            ulong iirsPa = region.Address + HidSize + FontSize;
            ulong timePa = region.Address + HidSize + FontSize + IirsSize;

            HidBaseAddress = hidPa - DramMemoryMap.DramBase;

            KPageList hidPageList  = new KPageList();
            KPageList fontPageList = new KPageList();
            KPageList iirsPageList = new KPageList();
            KPageList timePageList = new KPageList();

            hidPageList .AddRange(hidPa,  HidSize  / KMemoryManager.PageSize);
            fontPageList.AddRange(fontPa, FontSize / KMemoryManager.PageSize);
            iirsPageList.AddRange(iirsPa, IirsSize / KMemoryManager.PageSize);
            timePageList.AddRange(timePa, TimeSize / KMemoryManager.PageSize);

            HidSharedMem  = new KSharedMemory(KernelContext, hidPageList,  0, 0, MemoryPermission.Read);
            FontSharedMem = new KSharedMemory(KernelContext, fontPageList, 0, 0, MemoryPermission.Read);
            IirsSharedMem = new KSharedMemory(KernelContext, iirsPageList, 0, 0, MemoryPermission.Read);

            KSharedMemory timeSharedMemory = new KSharedMemory(KernelContext, timePageList, 0, 0, MemoryPermission.Read);

            TimeServiceManager.Instance.Initialize(device, this, timeSharedMemory, timePa - DramMemoryMap.DramBase, TimeSize);

            AppletState = new AppletStateMgr(this);

            AppletState.SetFocus(true);

            Font = new SharedFontManager(device, fontPa - DramMemoryMap.DramBase);

            IUserInterface.InitializePort(this);

            VsyncEvent = new KEvent(KernelContext);

            DisplayResolutionChangeEvent = new KEvent(KernelContext);

            ContentManager = contentManager;

            // TODO: use set:sys (and get external clock source id from settings)
            // TODO: use "time!standard_steady_clock_rtc_update_interval_minutes" and implement a worker thread to be accurate.
            UInt128 clockSourceId = new UInt128(Guid.NewGuid().ToByteArray());
            IRtcManager.GetExternalRtcValue(out ulong rtcValue);

            // We assume the rtc is system time.
            TimeSpanType systemTime = TimeSpanType.FromSeconds((long)rtcValue);

            // Configure and setup internal offset
            TimeSpanType internalOffset = TimeSpanType.FromSeconds(ConfigurationState.Instance.System.SystemTimeOffset);
            
            TimeSpanType systemTimeOffset = new TimeSpanType(systemTime.NanoSeconds + internalOffset.NanoSeconds);

            if (systemTime.IsDaylightSavingTime() && !systemTimeOffset.IsDaylightSavingTime())
            {
                internalOffset = internalOffset.AddSeconds(3600L);
            }
            else if (!systemTime.IsDaylightSavingTime() && systemTimeOffset.IsDaylightSavingTime())
            {
                internalOffset = internalOffset.AddSeconds(-3600L);
            }

            internalOffset = new TimeSpanType(-internalOffset.NanoSeconds);

            // First init the standard steady clock
            TimeServiceManager.Instance.SetupStandardSteadyClock(null, clockSourceId, systemTime, internalOffset, TimeSpanType.Zero, false);
            TimeServiceManager.Instance.SetupStandardLocalSystemClock(null, new SystemClockContext(), systemTime.ToSeconds());

            if (NxSettings.Settings.TryGetValue("time!standard_network_clock_sufficient_accuracy_minutes", out object standardNetworkClockSufficientAccuracyMinutes))
            {
                TimeSpanType standardNetworkClockSufficientAccuracy = new TimeSpanType((int)standardNetworkClockSufficientAccuracyMinutes * 60000000000);

                TimeServiceManager.Instance.SetupStandardNetworkSystemClock(new SystemClockContext(), standardNetworkClockSufficientAccuracy);
            }

            TimeServiceManager.Instance.SetupStandardUserSystemClock(null, false, SteadyClockTimePoint.GetRandom());

            // FIXME: TimeZone shoud be init here but it's actually done in ContentManager

            TimeServiceManager.Instance.SetupEphemeralNetworkSystemClock();

            DatabaseImpl.Instance.InitializeDatabase(device);

            HostSyncpoint = new NvHostSyncpt(device);

            SurfaceFlinger = new SurfaceFlinger(device);

            ConfigurationState.Instance.System.EnableDockedMode.Event += OnDockedModeChange;

            InitLibHacHorizon();
        }

        private void InitLibHacHorizon()
        {
            LibHac.Horizon horizon = new LibHac.Horizon(null, Device.FileSystem.FsServer);

            horizon.CreateHorizonClient(out HorizonClient ryujinxClient).ThrowIfFailure();
            horizon.CreateHorizonClient(out HorizonClient bcatClient).ThrowIfFailure();

            ryujinxClient.Sm.RegisterService(new LibHacIReader(this), "arp:r").ThrowIfFailure();
            new BcatServer(bcatClient);

            LibHacHorizonServer = horizon;
            LibHacHorizonClient = ryujinxClient;
        }

        private void OnDockedModeChange(object sender, ReactiveEventArgs<bool> e)
        {
            if (e.NewValue != State.DockedMode)
            {
                State.DockedMode = e.NewValue;

                AppletState.EnqueueMessage(MessageInfo.OperationModeChanged);
                AppletState.EnqueueMessage(MessageInfo.PerformanceModeChanged);
                SignalDisplayResolutionChange();
            }
        }

        public void LoadCart(string exeFsDir, string romFsFile = null)
        {
            if (romFsFile != null)
            {
                Device.FileSystem.LoadRomFs(romFsFile);
            }

            LocalFileSystem codeFs = new LocalFileSystem(exeFsDir);

            LoadExeFs(codeFs, out _);

            if (TitleId != 0)
            {
                EnsureSaveData(new TitleId(TitleId));
            }
        }

        public void LoadXci(string xciFile)
        {
            FileStream file = new FileStream(xciFile, FileMode.Open, FileAccess.Read);

            Xci xci = new Xci(KeySet, file.AsStorage());

            (Nca mainNca, Nca patchNca, Nca controlNca) = GetXciGameData(xci);

            if (mainNca == null)
            {
                Logger.PrintError(LogClass.Loader, "Unable to load XCI");

                return;
            }

            ContentManager.LoadEntries(Device);

            LoadNca(mainNca, patchNca, controlNca);
        }

        public void LoadKip(string kipFile)
        {
            using (IStorage fs = new LocalStorage(kipFile, FileAccess.Read))
            {
                ProgramLoader.LoadKip(KernelContext, new KipExecutable(fs));
            }
        }

        private (Nca Main, Nca patch, Nca Control) GetXciGameData(Xci xci)
        {
            if (!xci.HasPartition(XciPartitionType.Secure))
            {
                throw new InvalidDataException("Could not find XCI secure partition");
            }

            Nca mainNca    = null;
            Nca patchNca   = null;
            Nca controlNca = null;

            XciPartition securePartition = xci.OpenPartition(XciPartitionType.Secure);

            foreach (DirectoryEntryEx ticketEntry in securePartition.EnumerateEntries("/", "*.tik"))
            {
                Result result = securePartition.OpenFile(out IFile ticketFile, ticketEntry.FullPath.ToU8Span(), OpenMode.Read);

                if (result.IsSuccess())
                {
                    Ticket ticket = new Ticket(ticketFile.AsStream());

                    KeySet.ExternalKeySet.Add(new RightsId(ticket.RightsId), new AccessKey(ticket.GetTitleKey(KeySet)));
                }
            }

            foreach (DirectoryEntryEx fileEntry in securePartition.EnumerateEntries("/", "*.nca"))
            {
                Result result = securePartition.OpenFile(out IFile ncaFile, fileEntry.FullPath.ToU8Span(), OpenMode.Read);
                if (result.IsFailure())
                {
                    continue;
                }

                Nca nca = new Nca(KeySet, ncaFile.AsStorage());

                if (nca.Header.ContentType == NcaContentType.Program)
                {
                    int dataIndex = Nca.GetSectionIndexFromType(NcaSectionType.Data, NcaContentType.Program);

                    if (nca.Header.GetFsHeader(dataIndex).IsPatchSection())
                    {
                        patchNca = nca;
                    }
                    else
                    {
                        mainNca = nca;
                    }
                }
                else if (nca.Header.ContentType == NcaContentType.Control)
                {
                    controlNca = nca;
                }
            }

            if (mainNca == null)
            {
                Logger.PrintError(LogClass.Loader, "Could not find an Application NCA in the provided XCI file");
            }

            if (controlNca != null)
            {
                ReadControlData(controlNca);
            }
            else
            {
                ControlData.ByteSpan.Clear();
            }

            return (mainNca, patchNca, controlNca);
        }

        public void ReadControlData(Nca controlNca)
        {
            IFileSystem controlFs = controlNca.OpenFileSystem(NcaSectionType.Data, FsIntegrityCheckLevel);

            Result result = controlFs.OpenFile(out IFile controlFile, "/control.nacp".ToU8Span(), OpenMode.Read);

            if (result.IsSuccess())
            {
                result = controlFile.Read(out long bytesRead, 0, ControlData.ByteSpan, ReadOption.None);

                if (result.IsSuccess() && bytesRead == ControlData.ByteSpan.Length)
                {
                    TitleName = ControlData.Value
                        .Titles[(int) State.DesiredTitleLanguage].Name.ToString();

                    if (string.IsNullOrWhiteSpace(TitleName))
                    {
                        TitleName = ControlData.Value.Titles.ToArray()
                            .FirstOrDefault(x => x.Name[0] != 0).Name.ToString();
                    }

                    TitleVersionString = ControlData.Value.DisplayVersion.ToString();
                }
            }
            else
            {
                ControlData.ByteSpan.Clear();
            }
        }

        public void LoadNca(string ncaFile)
        {
            FileStream file = new FileStream(ncaFile, FileMode.Open, FileAccess.Read);

            Nca nca = new Nca(KeySet, file.AsStorage(false));

            LoadNca(nca, null, null);
        }

        public void LoadNsp(string nspFile)
        {
            FileStream file = new FileStream(nspFile, FileMode.Open, FileAccess.Read);

            PartitionFileSystem nsp = new PartitionFileSystem(file.AsStorage());

            foreach (DirectoryEntryEx ticketEntry in nsp.EnumerateEntries("/", "*.tik"))
            {
                Result result = nsp.OpenFile(out IFile ticketFile, ticketEntry.FullPath.ToU8Span(), OpenMode.Read);

                if (result.IsSuccess())
                {
                    Ticket ticket = new Ticket(ticketFile.AsStream());

                    KeySet.ExternalKeySet.Add(new RightsId(ticket.RightsId), new AccessKey(ticket.GetTitleKey(KeySet)));
                }
            }

            Nca mainNca    = null;
            Nca patchNca   = null;
            Nca controlNca = null;

            foreach (DirectoryEntryEx fileEntry in nsp.EnumerateEntries("/", "*.nca"))
            {
                nsp.OpenFile(out IFile ncaFile, fileEntry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                Nca nca = new Nca(KeySet, ncaFile.AsStorage());

                if (nca.Header.ContentType == NcaContentType.Program)
                {
                    int dataIndex = Nca.GetSectionIndexFromType(NcaSectionType.Data, NcaContentType.Program);

                    if (nca.Header.GetFsHeader(dataIndex).IsPatchSection())
                    {
                        patchNca = nca;
                    }
                    else
                    {
                        mainNca = nca;
                    }
                }
                else if (nca.Header.ContentType == NcaContentType.Control)
                {
                    controlNca = nca;
                }
            }

            if (mainNca != null)
            {
                LoadNca(mainNca, patchNca, controlNca);

                return;
            }

            // This is not a normal NSP, it's actually a ExeFS as a NSP
            LoadExeFs(nsp, out _);
        }

        public void LoadNca(Nca mainNca, Nca patchNca, Nca controlNca)
        {
            if (mainNca.Header.ContentType != NcaContentType.Program)
            {
                Logger.PrintError(LogClass.Loader, "Selected NCA is not a \"Program\" NCA");

                return;
            }

            IStorage    dataStorage = null;
            IFileSystem codeFs      = null;

            string titleUpdateMetadataPath = System.IO.Path.Combine(Device.FileSystem.GetBasePath(), "games", mainNca.Header.TitleId.ToString("x16"), "updates.json");

            if (File.Exists(titleUpdateMetadataPath))
            {
                string updatePath = JsonHelper.DeserializeFromFile<TitleUpdateMetadata>(titleUpdateMetadataPath).Selected;

                if (File.Exists(updatePath))
                {
                    FileStream file = new FileStream(updatePath, FileMode.Open, FileAccess.Read);
                    PartitionFileSystem nsp = new PartitionFileSystem(file.AsStorage());

                    foreach (DirectoryEntryEx ticketEntry in nsp.EnumerateEntries("/", "*.tik"))
                    {
                        Result result = nsp.OpenFile(out IFile ticketFile, ticketEntry.FullPath.ToU8Span(), OpenMode.Read);

                        if (result.IsSuccess())
                        {
                            Ticket ticket = new Ticket(ticketFile.AsStream());

                            KeySet.ExternalKeySet.Add(new RightsId(ticket.RightsId), new AccessKey(ticket.GetTitleKey(KeySet)));
                        }
                    }

                    foreach (DirectoryEntryEx fileEntry in nsp.EnumerateEntries("/", "*.nca"))
                    {
                        nsp.OpenFile(out IFile ncaFile, fileEntry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                        Nca nca = new Nca(KeySet, ncaFile.AsStorage());

                        if ($"{nca.Header.TitleId.ToString("x16")[..^3]}000" != mainNca.Header.TitleId.ToString("x16"))
                        {
                            break;
                        }

                        if (nca.Header.ContentType == NcaContentType.Program)
                        {
                            patchNca = nca;
                        }
                        else if (nca.Header.ContentType == NcaContentType.Control)
                        {
                            controlNca = nca;
                        }
                    }
                }
            }

            if (patchNca == null)
            {
                if (mainNca.CanOpenSection(NcaSectionType.Data))
                {
                    dataStorage = mainNca.OpenStorage(NcaSectionType.Data, FsIntegrityCheckLevel);
                }

                if (mainNca.CanOpenSection(NcaSectionType.Code))
                {
                    codeFs = mainNca.OpenFileSystem(NcaSectionType.Code, FsIntegrityCheckLevel);
                }
            }
            else
            {
                if (patchNca.CanOpenSection(NcaSectionType.Data))
                {
                    dataStorage = mainNca.OpenStorageWithPatch(patchNca, NcaSectionType.Data, FsIntegrityCheckLevel);
                }

                if (patchNca.CanOpenSection(NcaSectionType.Code))
                {
                    codeFs = mainNca.OpenFileSystemWithPatch(patchNca, NcaSectionType.Code, FsIntegrityCheckLevel);
                }
            }

            if (codeFs == null)
            {
                Logger.PrintError(LogClass.Loader, "No ExeFS found in NCA");

                return;
            }

            if (dataStorage == null)
            {
                Logger.PrintWarning(LogClass.Loader, "No RomFS found in NCA");
            }
            else
            {
                Device.FileSystem.SetRomFs(dataStorage.AsStream(FileAccess.Read));
            }

            LoadExeFs(codeFs, out Npdm metaData);
            
            TitleId      = metaData.Aci0.TitleId;
            TitleIs64Bit = metaData.Is64Bit;

            if (controlNca != null)
            {
                ReadControlData(controlNca);
            }
            else
            {
                ControlData.ByteSpan.Clear();
            }

            if (TitleId != 0)
            {
                EnsureSaveData(new TitleId(TitleId));
            }

            Logger.PrintInfo(LogClass.Loader, $"Application Loaded: {TitleName} v{TitleVersionString} [{TitleIdText}] [{(TitleIs64Bit ? "64-bit" : "32-bit")}]");
        }

        private void LoadExeFs(IFileSystem codeFs, out Npdm metaData)
        {
            Result result = codeFs.OpenFile(out IFile npdmFile, "/main.npdm".ToU8Span(), OpenMode.Read);

            if (ResultFs.PathNotFound.Includes(result))
            {
                Logger.PrintWarning(LogClass.Loader, "NPDM file not found, using default values!");

                metaData = GetDefaultNpdm();
            }
            else
            {
                metaData = new Npdm(npdmFile.AsStream());
            }

            List<IExecutable> staticObjects = new List<IExecutable>();

            void LoadNso(string filename)
            {
                foreach (DirectoryEntryEx file in codeFs.EnumerateEntries("/", $"{filename}*"))
                {
                    if (Path.GetExtension(file.Name) != string.Empty)
                    {
                        continue;
                    }

                    Logger.PrintInfo(LogClass.Loader, $"Loading {file.Name}...");

                    codeFs.OpenFile(out IFile nsoFile, file.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();          
                         
                    NsoExecutable staticObject = new NsoExecutable(nsoFile.AsStorage());

                    staticObjects.Add(staticObject);
                }
            }

            TitleId      = metaData.Aci0.TitleId;
            TitleIs64Bit = metaData.Is64Bit;

            LoadNso("rtld");
            LoadNso("main");
            LoadNso("subsdk");
            LoadNso("sdk");

            ContentManager.LoadEntries(Device);

            ProgramLoader.LoadNsos(KernelContext, metaData, staticObjects.ToArray());
        }

        public void LoadProgram(string filePath)
        {
            Npdm metaData = GetDefaultNpdm();

            bool isNro = Path.GetExtension(filePath).ToLower() == ".nro";


            IExecutable staticObject;

            if (isNro)
            {
                FileStream input = new FileStream(filePath, FileMode.Open);
                NroExecutable obj = new NroExecutable(input);
                staticObject = obj;

                // homebrew NRO can actually have some data after the actual NRO
                if (input.Length > obj.FileSize)
                {
                    input.Position = obj.FileSize;

                    BinaryReader reader = new BinaryReader(input);

                    uint asetMagic = reader.ReadUInt32();

                    if (asetMagic == 0x54455341)
                    {
                        uint asetVersion = reader.ReadUInt32();
                        if (asetVersion == 0)
                        {
                            ulong iconOffset = reader.ReadUInt64();
                            ulong iconSize   = reader.ReadUInt64();

                            ulong nacpOffset = reader.ReadUInt64();
                            ulong nacpSize   = reader.ReadUInt64();

                            ulong romfsOffset = reader.ReadUInt64();
                            ulong romfsSize   = reader.ReadUInt64();

                            if (romfsSize != 0)
                            {
                                Device.FileSystem.SetRomFs(new HomebrewRomFsStream(input, obj.FileSize + (long)romfsOffset));
                            }

                            if (nacpSize != 0)
                            {
                                input.Seek(obj.FileSize + (long)nacpOffset, SeekOrigin.Begin);

                                reader.Read(ControlData.ByteSpan);

                                ref ApplicationControlProperty nacp = ref ControlData.Value;

                                metaData.TitleName = nacp.Titles[(int)State.DesiredTitleLanguage].Name.ToString();

                                if (string.IsNullOrWhiteSpace(metaData.TitleName))
                                {
                                    metaData.TitleName = nacp.Titles.ToArray().FirstOrDefault(x => x.Name[0] != 0).Name.ToString();
                                }

                                if (nacp.PresenceGroupId != 0)
                                {
                                    metaData.Aci0.TitleId = nacp.PresenceGroupId;
                                }
                                else if (nacp.SaveDataOwnerId.Value != 0)
                                {
                                    metaData.Aci0.TitleId = nacp.SaveDataOwnerId.Value;
                                }
                                else if (nacp.AddOnContentBaseId != 0)
                                {
                                    metaData.Aci0.TitleId = nacp.AddOnContentBaseId - 0x1000;
                                }
                                else
                                {
                                    metaData.Aci0.TitleId = 0000000000000000;
                                }
                            }
                        }
                        else
                        {
                            Logger.PrintWarning(LogClass.Loader, $"Unsupported ASET header version found \"{asetVersion}\"");
                        }
                    }
                }
            }
            else
            {
                staticObject = new NsoExecutable(new LocalStorage(filePath, FileAccess.Read));
            }

            ContentManager.LoadEntries(Device);

            TitleName    = metaData.TitleName;
            TitleId      = metaData.Aci0.TitleId;
            TitleIs64Bit = metaData.Is64Bit;

            ProgramLoader.LoadNsos(KernelContext, metaData, new IExecutable[] { staticObject });
        }

        private Npdm GetDefaultNpdm()
        {
            Assembly asm = Assembly.GetCallingAssembly();

            using (Stream npdmStream = asm.GetManifestResourceStream("Ryujinx.HLE.Homebrew.npdm"))
            {
                return new Npdm(npdmStream);
            }
        }

        private Result EnsureSaveData(TitleId titleId)
        {
            Logger.PrintInfo(LogClass.Application, "Ensuring required savedata exists.");

            Uid user = State.Account.LastOpenedUser.UserId.ToLibHacUid();

            ref ApplicationControlProperty control = ref ControlData.Value;

            if (LibHac.Util.IsEmpty(ControlData.ByteSpan))
            {
                // If the current application doesn't have a loaded control property, create a dummy one
                // and set the savedata sizes so a user savedata will be created.
                control = ref new BlitStruct<ApplicationControlProperty>(1).Value;

                // The set sizes don't actually matter as long as they're non-zero because we use directory savedata.
                control.UserAccountSaveDataSize = 0x4000;
                control.UserAccountSaveDataJournalSize = 0x4000;

                Logger.PrintWarning(LogClass.Application,
                    "No control file was found for this game. Using a dummy one instead. This may cause inaccuracies in some games.");
            }

            FileSystemClient fs = Device.FileSystem.FsClient;

            Result rc = fs.EnsureApplicationCacheStorage(out _, titleId, ref control);

            if (rc.IsFailure())
            {
                Logger.PrintError(LogClass.Application, $"Error calling EnsureApplicationCacheStorage. Result code {rc.ToStringWithName()}");
            }

            rc = EnsureApplicationSaveData(fs, out _, titleId, ref control, ref user);

            if (rc.IsFailure())
            {
                Logger.PrintError(LogClass.Application, $"Error calling EnsureApplicationSaveData. Result code {rc.ToStringWithName()}");
            }

            return rc;
        }

        public void SignalDisplayResolutionChange()
        {
            DisplayResolutionChangeEvent.ReadableEvent.Signal();
        }

        public void SignalVsync()
        {
            VsyncEvent.ReadableEvent.Signal();
        }

        public void EnableMultiCoreScheduling()
        {
            if (!_hasStarted)
            {
                KernelContext.Scheduler.MultiCoreScheduling = true;
            }
        }

        public void DisableMultiCoreScheduling()
        {
            if (!_hasStarted)
            {
                KernelContext.Scheduler.MultiCoreScheduling = false;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed && disposing)
            {
                ConfigurationState.Instance.System.EnableDockedMode.Event -= OnDockedModeChange;

                _isDisposed = true;

                SurfaceFlinger.Dispose();

                KProcess terminationProcess = new KProcess(KernelContext);
                KThread terminationThread = new KThread(KernelContext);

                terminationThread.Initialize(0, 0, 0, 3, 0, terminationProcess, ThreadType.Kernel, () =>
                {
                    // Force all threads to exit.
                    lock (KernelContext.Processes)
                    {
                        foreach (KProcess process in KernelContext.Processes.Values)
                        {
                            process.Terminate();
                        }
                    }

                    // Exit ourself now!
                    KernelContext.Scheduler.ExitThread(terminationThread);
                    KernelContext.Scheduler.GetCurrentThread().Exit();
                    KernelContext.Scheduler.RemoveThread(terminationThread);
                });

                terminationThread.Start();

                // Destroy nvservices channels as KThread could be waiting on some user events.
                // This is safe as KThread that are likely to call ioctls are going to be terminated by the post handler hook on the SVC facade.
                INvDrvServices.Destroy();

                // This is needed as the IPC Dummy KThread is also counted in the ThreadCounter.
                KernelContext.ThreadCounter.Signal();

                // It's only safe to release resources once all threads
                // have exited.
                KernelContext.ThreadCounter.Signal();
                KernelContext.ThreadCounter.Wait();

                KernelContext.Dispose();

                Device.Unload();
            }
        }
    }
}