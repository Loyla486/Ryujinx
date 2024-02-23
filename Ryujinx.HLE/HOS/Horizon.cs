using LibHac;
using Ryujinx.HLE.HOS.Font;
using Ryujinx.HLE.HOS.Kernel;
using Ryujinx.HLE.HOS.SystemState;
using Ryujinx.HLE.Loaders.Executables;
using Ryujinx.HLE.Loaders.Npdm;
using Ryujinx.HLE.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

namespace Ryujinx.HLE.HOS
{
    public class Horizon : IDisposable
    {
        internal const int HidSize  = 0x40000;
        internal const int FontSize = 0x1100000;

        private Switch Device;

        private KProcessScheduler Scheduler;

        private ConcurrentDictionary<int, Process> Processes;

        public SystemStateMgr State { get; private set; }

        internal KSharedMemory HidSharedMem  { get; private set; }
        internal KSharedMemory FontSharedMem { get; private set; }

        internal SharedFontManager Font { get; private set; }

        internal KEvent VsyncEvent { get; private set; }

        internal Keyset KeySet { get; private set; }

        public Horizon(Switch Device)
        {
            this.Device = Device;

            Scheduler = new KProcessScheduler(Device.Log);

            Processes = new ConcurrentDictionary<int, Process>();

            State = new SystemStateMgr();

            if (!Device.Memory.Allocator.TryAllocate(HidSize,  out long HidPA) ||
                !Device.Memory.Allocator.TryAllocate(FontSize, out long FontPA))
            {
                throw new InvalidOperationException();
            }

            HidSharedMem  = new KSharedMemory(HidPA, HidSize);
            FontSharedMem = new KSharedMemory(FontPA, FontSize);

            Font = new SharedFontManager(Device, FontSharedMem.PA);

            VsyncEvent = new KEvent();

            LoadKeySet();
        }

        public void LoadCart(string ExeFsDir, string RomFsFile = null)
        {
            if (RomFsFile != null)
            {
                Device.FileSystem.LoadRomFs(RomFsFile);
            }

            string NpdmFileName = Path.Combine(ExeFsDir, "main.npdm");

            Npdm MetaData = null;

            if (File.Exists(NpdmFileName))
            {
                Device.Log.PrintInfo(LogClass.Loader, $"Loading main.npdm...");

                using (FileStream Input = new FileStream(NpdmFileName, FileMode.Open))
                {
                    MetaData = new Npdm(Input);
                }
            }
            else
            {
                Device.Log.PrintWarning(LogClass.Loader, $"NPDM file not found, using default values!");
            }

            Process MainProcess = MakeProcess(MetaData);

            void LoadNso(string FileName)
            {
                foreach (string File in Directory.GetFiles(ExeFsDir, FileName))
                {
                    if (Path.GetExtension(File) != string.Empty)
                    {
                        continue;
                    }

                    Device.Log.PrintInfo(LogClass.Loader, $"Loading {Path.GetFileNameWithoutExtension(File)}...");

                    using (FileStream Input = new FileStream(File, FileMode.Open))
                    {
                        string Name = Path.GetFileNameWithoutExtension(File);

                        Nso Program = new Nso(Input, Name);

                        MainProcess.LoadProgram(Program);
                    }
                }
            }

            if (!MainProcess.MetaData.Is64Bits)
            {
                throw new NotImplementedException("32-bit titles are unsupported!");
            }

            LoadNso("rtld");

            MainProcess.SetEmptyArgs();

            LoadNso("main");
            LoadNso("subsdk*");
            LoadNso("sdk");

            MainProcess.Run();
        }

        public void LoadXci(string XciFile)
        {
            FileStream File = new FileStream(XciFile, FileMode.Open, FileAccess.Read);

            Xci Xci = new Xci(KeySet, File);

            Nca Nca = GetXciMainNca(Xci);

            if (Nca == null)
            {
                Device.Log.PrintError(LogClass.Loader, "Unable to load XCI");

                return;
            }

            LoadNca(Nca);
        }

        private Nca GetXciMainNca(Xci Xci)
        {
            if (Xci.SecurePartition == null)
            {
                throw new InvalidDataException("Could not find XCI secure partition");
            }

            Nca MainNca = null;
            Nca PatchNca = null;

            foreach (PfsFileEntry FileEntry in Xci.SecurePartition.Files.Where(x => x.Name.EndsWith(".nca")))
            {
                Stream NcaStream = Xci.SecurePartition.OpenFile(FileEntry);

                Nca Nca = new Nca(KeySet, NcaStream, true);

                if (Nca.Header.ContentType == ContentType.Program)
                {
                    if (Nca.Sections.Any(x => x?.Type == SectionType.Romfs))
                    {
                        MainNca = Nca;
                    }
                    else if (Nca.Sections.Any(x => x?.Type == SectionType.Bktr))
                    {
                        PatchNca = Nca;
                    }
                }
            }

            if (MainNca == null)
            {
                Device.Log.PrintError(LogClass.Loader, "Could not find an Application NCA in the provided XCI file");
            }

            MainNca.SetBaseNca(PatchNca);

            return MainNca;
        }

        public void LoadNca(string NcaFile)
        {
            FileStream File = new FileStream(NcaFile, FileMode.Open, FileAccess.Read);

            Nca Nca = new Nca(KeySet, File, true);

            LoadNca(Nca);
        }

        public void LoadNsp(string NspFile)
        {
            FileStream File = new FileStream(NspFile, FileMode.Open, FileAccess.Read);

            Pfs Nsp = new Pfs(File);

            PfsFileEntry TicketFile = Nsp.Files.FirstOrDefault(x => x.Name.EndsWith(".tik"));

            // Load title key from the NSP's ticket in case the user doesn't have a title key file
            if (TicketFile != null)
            {
                // todo Change when Ticket(Stream) overload is added
                Ticket Ticket = new Ticket(new BinaryReader(Nsp.OpenFile(TicketFile)));

                KeySet.TitleKeys[Ticket.RightsId] = Ticket.GetTitleKey(KeySet);
            }

            foreach (PfsFileEntry NcaFile in Nsp.Files.Where(x => x.Name.EndsWith(".nca")))
            {
                Nca Nca = new Nca(KeySet, Nsp.OpenFile(NcaFile), true);

                if (Nca.Header.ContentType == ContentType.Program)
                {
                    LoadNca(Nca);

                    return;
                }
            }

            Device.Log.PrintError(LogClass.Loader, "Could not find an Application NCA in the provided NSP file");
        }

        public void LoadNca(Nca Nca)
        {
            NcaSection RomfsSection = Nca.Sections.FirstOrDefault(x => x?.Type == SectionType.Romfs);
            NcaSection ExefsSection = Nca.Sections.FirstOrDefault(x => x?.IsExefs == true);

            if (ExefsSection == null)
            {
                Device.Log.PrintError(LogClass.Loader, "No ExeFS found in NCA");

                return;
            }

            if (RomfsSection == null)
            {
                Device.Log.PrintError(LogClass.Loader, "No RomFS found in NCA");

                return;
            }

            Stream RomfsStream = Nca.OpenSection(RomfsSection.SectionNum, false);
            Device.FileSystem.SetRomFs(RomfsStream);

            Stream ExefsStream = Nca.OpenSection(ExefsSection.SectionNum, false);
            Pfs Exefs = new Pfs(ExefsStream);

            Npdm MetaData = null;

            if (Exefs.FileExists("main.npdm"))
            {
                Device.Log.PrintInfo(LogClass.Loader, "Loading main.npdm...");

                MetaData = new Npdm(Exefs.OpenFile("main.npdm"));
            }
            else
            {
                Device.Log.PrintWarning(LogClass.Loader, $"NPDM file not found, using default values!");
            }

            Process MainProcess = MakeProcess(MetaData);

            void LoadNso(string Filename)
            {
                foreach (PfsFileEntry File in Exefs.Files.Where(x => x.Name.StartsWith(Filename)))
                {
                    if (Path.GetExtension(File.Name) != string.Empty)
                    {
                        continue;
                    }

                    Device.Log.PrintInfo(LogClass.Loader, $"Loading {Filename}...");

                    string Name = Path.GetFileNameWithoutExtension(File.Name);

                    Nso Program = new Nso(Exefs.OpenFile(File), Name);

                    MainProcess.LoadProgram(Program);
                }
            }

            if (!MainProcess.MetaData.Is64Bits)
            {
                throw new NotImplementedException("32-bit titles are unsupported!");
            }

            LoadNso("rtld");

            MainProcess.SetEmptyArgs();

            LoadNso("main");
            LoadNso("subsdk");
            LoadNso("sdk");

            MainProcess.Run();
        }

        public void LoadProgram(string FilePath)
        {
            bool IsNro = Path.GetExtension(FilePath).ToLower() == ".nro";

            string Name = Path.GetFileNameWithoutExtension(FilePath);
            string SwitchFilePath = Device.FileSystem.SystemPathToSwitchPath(FilePath);

            if (IsNro && (SwitchFilePath == null || !SwitchFilePath.StartsWith("sdmc:/")))
            {
                string SwitchPath = $"sdmc:/switch/{Name}{Homebrew.TemporaryNroSuffix}";
                string TempPath = Device.FileSystem.SwitchPathToSystemPath(SwitchPath);

                string SwitchDir = Path.GetDirectoryName(TempPath);

                if (!Directory.Exists(SwitchDir))
                {
                    Directory.CreateDirectory(SwitchDir);
                }

                File.Copy(FilePath, TempPath, true);

                FilePath = TempPath;
            }

            Process MainProcess = MakeProcess();

            using (FileStream Input = new FileStream(FilePath, FileMode.Open))
            {
                MainProcess.LoadProgram(IsNro
                    ? (IExecutable)new Nro(Input, FilePath)
                    : (IExecutable)new Nso(Input, FilePath));
            }

            MainProcess.SetEmptyArgs();
            MainProcess.Run(IsNro);
        }

        public void LoadKeySet()
        {
            string KeyFile        = null;
            string TitleKeyFile   = null;
            string ConsoleKeyFile = null;

            string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            LoadSetAtPath(Path.Combine(Home, ".switch"));
            LoadSetAtPath(Device.FileSystem.GetSystemPath());

            KeySet = ExternalKeys.ReadKeyFile(KeyFile, TitleKeyFile, ConsoleKeyFile);

            void LoadSetAtPath(string BasePath)
            {
                string LocalKeyFile        = Path.Combine(BasePath,    "prod.keys");
                string LocalTitleKeyFile   = Path.Combine(BasePath,   "title.keys");
                string LocalConsoleKeyFile = Path.Combine(BasePath, "console.keys");

                if (File.Exists(LocalKeyFile))
                {
                    KeyFile = LocalKeyFile;
                }

                if (File.Exists(LocalTitleKeyFile))
                {
                    TitleKeyFile = LocalTitleKeyFile;
                }

                if (File.Exists(LocalConsoleKeyFile))
                {
                    ConsoleKeyFile = LocalConsoleKeyFile;
                }
            }
        }

        public void SignalVsync() => VsyncEvent.WaitEvent.Set();

        private Process MakeProcess(Npdm MetaData = null)
        {
            Process Process;

            lock (Processes)
            {
                int ProcessId = 0;

                while (Processes.ContainsKey(ProcessId))
                {
                    ProcessId++;
                }

                Process = new Process(Device, Scheduler, ProcessId, MetaData);

                Processes.TryAdd(ProcessId, Process);
            }

            InitializeProcess(Process);

            return Process;
        }

        private void InitializeProcess(Process Process)
        {
            Process.AppletState.SetFocus(true);
        }

        internal void ExitProcess(int ProcessId)
        {
            if (Processes.TryRemove(ProcessId, out Process Process))
            {
                Process.Dispose();

                if (Processes.Count == 0)
                {
                    Unload();

                    Device.Unload();
                }
            }
        }

        private void Unload()
        {
            VsyncEvent.Dispose();

            Scheduler.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool Disposing)
        {
            if (Disposing)
            {
                foreach (Process Process in Processes.Values)
                {
                    Process.Dispose();
                }
            }
        }
    }
}
