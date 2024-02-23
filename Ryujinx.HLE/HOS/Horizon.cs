using LibHac;
using LibHac.IO;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.FileSystem.Content;
using Ryujinx.HLE.HOS.Font;
using Ryujinx.HLE.HOS.Kernel.Common;
using Ryujinx.HLE.HOS.Kernel.Memory;
using Ryujinx.HLE.HOS.Kernel.Process;
using Ryujinx.HLE.HOS.Kernel.Threading;
using Ryujinx.HLE.HOS.Services.Sm;
using Ryujinx.HLE.HOS.SystemState;
using Ryujinx.HLE.Loaders.Executables;
using Ryujinx.HLE.Loaders.Npdm;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

using NxStaticObject = Ryujinx.HLE.Loaders.Executables.NxStaticObject;

namespace Ryujinx.HLE.HOS
{
    public class Horizon : IDisposable
    {
        internal const int InitialKipId     = 1;
        internal const int InitialProcessId = 0x51;

        internal const int HidSize  = 0x40000;
        internal const int FontSize = 0x1100000;
        internal const int IirsSize = 0x8000;

        private const int MemoryBlockAllocatorSize = 0x2710;

        private const ulong UserSlabHeapBase     = DramMemoryMap.SlabHeapBase;
        private const ulong UserSlabHeapItemSize = KMemoryManager.PageSize;
        private const ulong UserSlabHeapSize     = 0x3de000;

        internal long PrivilegedProcessLowestId  { get; set; } = 1;
        internal long PrivilegedProcessHighestId { get; set; } = 8;

        internal Switch Device { get; private set; }

        public SystemStateMgr State { get; private set; }

        internal bool KernelInitialized { get; private set; }

        internal KResourceLimit ResourceLimit { get; private set; }

        internal KMemoryRegionManager[] MemoryRegions { get; private set; }

        internal KMemoryBlockAllocator LargeMemoryBlockAllocator { get; private set; }
        internal KMemoryBlockAllocator SmallMemoryBlockAllocator { get; private set; }

        internal KSlabHeap UserSlabHeapPages { get; private set; }

        internal KCriticalSection CriticalSection { get; private set; }

        internal KScheduler Scheduler { get; private set; }

        internal KTimeManager TimeManager { get; private set; }

        internal KSynchronization Synchronization { get; private set; }

        internal KContextIdManager ContextIdManager { get; private set; }

        private long _kipId;
        private long _processId;
        private long _threadUid;

        internal CountdownEvent ThreadCounter;

        internal SortedDictionary<long, KProcess> Processes;

        internal ConcurrentDictionary<string, KAutoObject> AutoObjectNames;

        internal bool EnableVersionChecks { get; private set; }

        internal AppletStateMgr AppletState { get; private set; }

        internal KSharedMemory HidSharedMem  { get; private set; }
        internal KSharedMemory FontSharedMem { get; private set; }
        internal KSharedMemory IirsSharedMem { get; private set; }

        internal SharedFontManager Font { get; private set; }

        internal ContentManager ContentManager { get; private set; }

        internal KEvent VsyncEvent { get; private set; }

        internal Keyset KeySet { get; private set; }

        private bool _hasStarted;

        public Nacp ControlData { get; set; }

        public string CurrentTitle { get; private set; }

        public IntegrityCheckLevel FsIntegrityCheckLevel { get; set; }

        internal long HidBaseAddress { get; private set; }

        public Horizon(Switch device)
        {
            ControlData = new Nacp();

            Device = device;

            State = new SystemStateMgr();

            ResourceLimit = new KResourceLimit(this);

            KernelInit.InitializeResourceLimit(ResourceLimit);

            MemoryRegions = KernelInit.GetMemoryRegions();

            LargeMemoryBlockAllocator = new KMemoryBlockAllocator(MemoryBlockAllocatorSize * 2);
            SmallMemoryBlockAllocator = new KMemoryBlockAllocator(MemoryBlockAllocatorSize);

            UserSlabHeapPages = new KSlabHeap(
                UserSlabHeapBase,
                UserSlabHeapItemSize,
                UserSlabHeapSize);

            CriticalSection = new KCriticalSection(this);

            Scheduler = new KScheduler(this);

            TimeManager = new KTimeManager();

            Synchronization = new KSynchronization(this);

            ContextIdManager = new KContextIdManager();

            _kipId     = InitialKipId;
            _processId = InitialProcessId;

            Scheduler.StartAutoPreemptionThread();

            KernelInitialized = true;

            ThreadCounter = new CountdownEvent(1);

            Processes = new SortedDictionary<long, KProcess>();

            AutoObjectNames = new ConcurrentDictionary<string, KAutoObject>();

            //Note: This is not really correct, but with HLE of services, the only memory
            //region used that is used is Application, so we can use the other ones for anything.
            KMemoryRegionManager region = MemoryRegions[(int)MemoryRegion.NvServices];

            ulong hidPa  = region.Address;
            ulong fontPa = region.Address + HidSize;
            ulong iirsPa = region.Address + HidSize + FontSize;

            HidBaseAddress = (long)(hidPa - DramMemoryMap.DramBase);

            KPageList hidPageList  = new KPageList();
            KPageList fontPageList = new KPageList();
            KPageList iirsPageList = new KPageList();

            hidPageList .AddRange(hidPa,  HidSize  / KMemoryManager.PageSize);
            fontPageList.AddRange(fontPa, FontSize / KMemoryManager.PageSize);
            iirsPageList.AddRange(iirsPa, IirsSize / KMemoryManager.PageSize);

            HidSharedMem  = new KSharedMemory(this, hidPageList,  0, 0, MemoryPermission.Read);
            FontSharedMem = new KSharedMemory(this, fontPageList, 0, 0, MemoryPermission.Read);
            IirsSharedMem = new KSharedMemory(this, iirsPageList, 0, 0, MemoryPermission.Read);

            AppletState = new AppletStateMgr(this);

            AppletState.SetFocus(true);

            Font = new SharedFontManager(device, (long)(fontPa - DramMemoryMap.DramBase));

            IUserInterface.InitializePort(this);

            VsyncEvent = new KEvent(this);

            LoadKeySet();

            ContentManager = new ContentManager(device);
        }

        public void LoadCart(string exeFsDir, string romFsFile = null)
        {
            if (romFsFile != null)
            {
                Device.FileSystem.LoadRomFs(romFsFile);
            }

            string npdmFileName = Path.Combine(exeFsDir, "main.npdm");

            Npdm metaData = null;

            if (File.Exists(npdmFileName))
            {
                Logger.PrintInfo(LogClass.Loader, $"Loading main.npdm...");

                using (FileStream input = new FileStream(npdmFileName, FileMode.Open))
                {
                    metaData = new Npdm(input);
                }
            }
            else
            {
                Logger.PrintWarning(LogClass.Loader, $"NPDM file not found, using default values!");

                metaData = GetDefaultNpdm();
            }

            List<IExecutable> staticObjects = new List<IExecutable>();

            void LoadNso(string searchPattern)
            {
                foreach (string file in Directory.GetFiles(exeFsDir, searchPattern))
                {
                    if (Path.GetExtension(file) != string.Empty)
                    {
                        continue;
                    }

                    Logger.PrintInfo(LogClass.Loader, $"Loading {Path.GetFileNameWithoutExtension(file)}...");

                    using (FileStream input = new FileStream(file, FileMode.Open))
                    {
                        NxStaticObject staticObject = new NxStaticObject(input);

                        staticObjects.Add(staticObject);
                    }
                }
            }

            CurrentTitle = metaData.Aci0.TitleId.ToString("x16");

            LoadNso("rtld");
            LoadNso("main");
            LoadNso("subsdk*");
            LoadNso("sdk");

            ContentManager.LoadEntries();

            ProgramLoader.LoadStaticObjects(this, metaData, staticObjects.ToArray());
        }

        public void LoadXci(string xciFile)
        {
            FileStream file = new FileStream(xciFile, FileMode.Open, FileAccess.Read);

            Xci xci = new Xci(KeySet, file.AsStorage());

            (Nca mainNca, Nca controlNca) = GetXciGameData(xci);

            if (mainNca == null)
            {
                Logger.PrintError(LogClass.Loader, "Unable to load XCI");

                return;
            }

            ContentManager.LoadEntries();

            LoadNca(mainNca, controlNca);
        }

        public void LoadKip(string kipFile)
        {
            using (FileStream fs = new FileStream(kipFile, FileMode.Open))
            {
                ProgramLoader.LoadKernelInitalProcess(this, new KernelInitialProcess(fs));
            }
        }

        private (Nca Main, Nca Control) GetXciGameData(Xci xci)
        {
            if (xci.SecurePartition == null)
            {
                throw new InvalidDataException("Could not find XCI secure partition");
            }

            Nca mainNca    = null;
            Nca patchNca   = null;
            Nca controlNca = null;

            foreach (PfsFileEntry ticketEntry in xci.SecurePartition.Files.Where(x => x.Name.EndsWith(".tik")))
            {
                Ticket ticket = new Ticket(xci.SecurePartition.OpenFile(ticketEntry).AsStream());

                if (!KeySet.TitleKeys.ContainsKey(ticket.RightsId))
                {
                    KeySet.TitleKeys.Add(ticket.RightsId, ticket.GetTitleKey(KeySet));
                }
            }

            foreach (PfsFileEntry fileEntry in xci.SecurePartition.Files.Where(x => x.Name.EndsWith(".nca")))
            {
                IStorage ncaStorage = xci.SecurePartition.OpenFile(fileEntry);

                Nca nca = new Nca(KeySet, ncaStorage, true);

                if (nca.Header.ContentType == ContentType.Program)
                {
                    if (nca.Sections.Any(x => x?.Type == SectionType.Romfs))
                    {
                        mainNca = nca;
                    }
                    else if (nca.Sections.Any(x => x?.Type == SectionType.Bktr))
                    {
                        patchNca = nca;
                    }
                }
                else if (nca.Header.ContentType == ContentType.Control)
                {
                    controlNca = nca;
                }
            }

            if (mainNca == null)
            {
                Logger.PrintError(LogClass.Loader, "Could not find an Application NCA in the provided XCI file");
            }

            mainNca.SetBaseNca(patchNca);

            if (controlNca != null)
            {
                ReadControlData(controlNca);
            }

            if (patchNca != null)
            {
                patchNca.SetBaseNca(mainNca);

                return (patchNca, controlNca);
            }

            return (mainNca, controlNca);
        }

        public void ReadControlData(Nca controlNca)
        {
            Romfs controlRomfs = new Romfs(controlNca.OpenSection(0, false, FsIntegrityCheckLevel, true));

            IStorage controlFile = controlRomfs.OpenFile("/control.nacp");

            ControlData = new Nacp(controlFile.AsStream());
        }

        public void LoadNca(string ncaFile)
        {
            FileStream file = new FileStream(ncaFile, FileMode.Open, FileAccess.Read);

            Nca nca = new Nca(KeySet, file.AsStorage(false), false);

            LoadNca(nca, null);
        }

        public void LoadNsp(string nspFile)
        {
            FileStream file = new FileStream(nspFile, FileMode.Open, FileAccess.Read);

            Pfs nsp = new Pfs(file.AsStorage(false));

            foreach (PfsFileEntry ticketEntry in nsp.Files.Where(x => x.Name.EndsWith(".tik")))
            {
                Ticket ticket = new Ticket(nsp.OpenFile(ticketEntry).AsStream());

                if (!KeySet.TitleKeys.ContainsKey(ticket.RightsId))
                {
                    KeySet.TitleKeys.Add(ticket.RightsId, ticket.GetTitleKey(KeySet));
                }
            }

            Nca mainNca    = null;
            Nca controlNca = null;

            foreach (PfsFileEntry ncaFile in nsp.Files.Where(x => x.Name.EndsWith(".nca")))
            {
                Nca nca = new Nca(KeySet, nsp.OpenFile(ncaFile), true);

                if (nca.Header.ContentType == ContentType.Program)
                {
                    mainNca = nca;
                }
                else if (nca.Header.ContentType == ContentType.Control)
                {
                    controlNca = nca;
                }
            }

            if (mainNca != null)
            {
                LoadNca(mainNca, controlNca);

                return;
            }

            // This is not a normal NSP, it's actually a ExeFS as a NSP
            Npdm metaData = null;

            PfsFileEntry npdmFile = nsp.Files.FirstOrDefault(x => x.Name.Equals("main.npdm"));

            if (npdmFile != null)
            {
                Logger.PrintInfo(LogClass.Loader, $"Loading main.npdm...");

                metaData = new Npdm(nsp.OpenFile(npdmFile).AsStream());
            }
            else
            {
                Logger.PrintWarning(LogClass.Loader, $"NPDM file not found, using default values!");

                metaData = GetDefaultNpdm();
            }

            List<IExecutable> staticObjects = new List<IExecutable>();

            void LoadNso(string searchPattern)
            {
                PfsFileEntry entry = nsp.Files.FirstOrDefault(x => x.Name.Equals(searchPattern));

                if (entry != null)
                {
                    Logger.PrintInfo(LogClass.Loader, $"Loading {entry.Name}...");

                    NxStaticObject staticObject = new NxStaticObject(nsp.OpenFile(entry).AsStream());

                    staticObjects.Add(staticObject);
                }
            }

            CurrentTitle = metaData.Aci0.TitleId.ToString("x16");

            LoadNso("rtld");
            LoadNso("main");
            LoadNso("subsdk*");
            LoadNso("sdk");

            ContentManager.LoadEntries();

            if (staticObjects.Count == 0)
            {
                Logger.PrintError(LogClass.Loader, "Could not find an Application NCA in the provided NSP file");
            }
            else
            {
                ProgramLoader.LoadStaticObjects(this, metaData, staticObjects.ToArray());
            }
        }

        public void LoadNca(Nca mainNca, Nca controlNca)
        {
            if (mainNca.Header.ContentType != ContentType.Program)
            {
                Logger.PrintError(LogClass.Loader, "Selected NCA is not a \"Program\" NCA");

                return;
            }

            IStorage romfsStorage = mainNca.OpenSection(ProgramPartitionType.Data, false, FsIntegrityCheckLevel, false);
            IStorage exefsStorage = mainNca.OpenSection(ProgramPartitionType.Code, false, FsIntegrityCheckLevel, true);

            if (exefsStorage == null)
            {
                Logger.PrintError(LogClass.Loader, "No ExeFS found in NCA");

                return;
            }

            if (romfsStorage == null)
            {
                Logger.PrintWarning(LogClass.Loader, "No RomFS found in NCA");
            }
            else
            {
                Device.FileSystem.SetRomFs(romfsStorage.AsStream(false));
            }

            Pfs exefs = new Pfs(exefsStorage);

            Npdm metaData = null;

            if (exefs.FileExists("main.npdm"))
            {
                Logger.PrintInfo(LogClass.Loader, "Loading main.npdm...");

                metaData = new Npdm(exefs.OpenFile("main.npdm").AsStream());
            }
            else
            {
                Logger.PrintWarning(LogClass.Loader, $"NPDM file not found, using default values!");

                metaData = GetDefaultNpdm();
            }

            List<IExecutable> staticObjects = new List<IExecutable>();

            void LoadNso(string filename)
            {
                foreach (PfsFileEntry file in exefs.Files.Where(x => x.Name.StartsWith(filename)))
                {
                    if (Path.GetExtension(file.Name) != string.Empty)
                    {
                        continue;
                    }

                    Logger.PrintInfo(LogClass.Loader, $"Loading {filename}...");

                    NxStaticObject staticObject = new NxStaticObject(exefs.OpenFile(file).AsStream());

                    staticObjects.Add(staticObject);
                }
            }

            Nacp ReadControlData()
            {
                Romfs controlRomfs = new Romfs(controlNca.OpenSection(0, false, FsIntegrityCheckLevel, true));

                IStorage controlFile = controlRomfs.OpenFile("/control.nacp");

                Nacp controlData = new Nacp(controlFile.AsStream());

                CurrentTitle = controlData.Descriptions[(int)State.DesiredTitleLanguage].Title;

                if (string.IsNullOrWhiteSpace(CurrentTitle))
                {
                    CurrentTitle = controlData.Descriptions.ToList().Find(x => !string.IsNullOrWhiteSpace(x.Title)).Title;
                }

                return controlData;
            }

            if (controlNca != null)
            {
                ReadControlData();
            }
            else
            {
                CurrentTitle = metaData.Aci0.TitleId.ToString("x16");
            }

            LoadNso("rtld");
            LoadNso("main");
            LoadNso("subsdk");
            LoadNso("sdk");

            ContentManager.LoadEntries();

            ProgramLoader.LoadStaticObjects(this, metaData, staticObjects.ToArray());
        }

        public void LoadProgram(string filePath)
        {
            Npdm metaData = GetDefaultNpdm();

            bool isNro = Path.GetExtension(filePath).ToLower() == ".nro";

            FileStream input = new FileStream(filePath, FileMode.Open);

            IExecutable staticObject;

            if (isNro)
            {
                NxRelocatableObject obj = new NxRelocatableObject(input);
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
                            ulong iconSize = reader.ReadUInt64();

                            ulong nacpOffset = reader.ReadUInt64();
                            ulong nacpSize = reader.ReadUInt64();

                            ulong romfsOffset = reader.ReadUInt64();
                            ulong romfsSize = reader.ReadUInt64();

                            if (romfsSize != 0)
                            {
                                Device.FileSystem.SetRomFs(new HomebrewRomFsStream(input, obj.FileSize + (long)romfsOffset));
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
                staticObject = new NxStaticObject(input);
            }

            ContentManager.LoadEntries();

            ProgramLoader.LoadStaticObjects(this, metaData, new IExecutable[] { staticObject });
        }

        private Npdm GetDefaultNpdm()
        {
            Assembly asm = Assembly.GetCallingAssembly();

            using (Stream npdmStream = asm.GetManifestResourceStream("Ryujinx.HLE.Homebrew.npdm"))
            {
                return new Npdm(npdmStream);
            }
        }

        public void LoadKeySet()
        {
            string keyFile        = null;
            string titleKeyFile   = null;
            string consoleKeyFile = null;

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            LoadSetAtPath(Path.Combine(home, ".switch"));
            LoadSetAtPath(Device.FileSystem.GetSystemPath());

            KeySet = ExternalKeys.ReadKeyFile(keyFile, titleKeyFile, consoleKeyFile);

            void LoadSetAtPath(string basePath)
            {
                string localKeyFile        = Path.Combine(basePath,    "prod.keys");
                string localTitleKeyFile   = Path.Combine(basePath,   "title.keys");
                string localConsoleKeyFile = Path.Combine(basePath, "console.keys");

                if (File.Exists(localKeyFile))
                {
                    keyFile = localKeyFile;
                }

                if (File.Exists(localTitleKeyFile))
                {
                    titleKeyFile = localTitleKeyFile;
                }

                if (File.Exists(localConsoleKeyFile))
                {
                    consoleKeyFile = localConsoleKeyFile;
                }
            }
        }

        public void SignalVsync()
        {
            VsyncEvent.ReadableEvent.Signal();
        }

        internal long GetThreadUid()
        {
            return Interlocked.Increment(ref _threadUid) - 1;
        }

        internal long GetKipId()
        {
            return Interlocked.Increment(ref _kipId) - 1;
        }

        internal long GetProcessId()
        {
            return Interlocked.Increment(ref _processId) - 1;
        }

        public void EnableMultiCoreScheduling()
        {
            if (!_hasStarted)
            {
                Scheduler.MultiCoreScheduling = true;
            }
        }

        public void DisableMultiCoreScheduling()
        {
            if (!_hasStarted)
            {
                Scheduler.MultiCoreScheduling = false;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                //Force all threads to exit.
                lock (Processes)
                {
                    foreach (KProcess process in Processes.Values)
                    {
                        process.StopAllThreads();
                    }
                }

                //It's only safe to release resources once all threads
                //have exited.
                ThreadCounter.Signal();
                ThreadCounter.Wait();

                Scheduler.Dispose();

                TimeManager.Dispose();

                Device.Unload();
            }
        }
    }
}
