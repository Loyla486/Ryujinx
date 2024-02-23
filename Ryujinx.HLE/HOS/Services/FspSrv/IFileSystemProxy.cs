using LibHac;
using LibHac.IO;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using static Ryujinx.HLE.FileSystem.VirtualFileSystem;
using static Ryujinx.HLE.HOS.ErrorCode;
using static Ryujinx.HLE.Utilities.StringUtils;

namespace Ryujinx.HLE.HOS.Services.FspSrv
{
    class IFileSystemProxy : IpcService
    {
        private Dictionary<int, ServiceProcessRequest> _commands;

        public override IReadOnlyDictionary<int, ServiceProcessRequest> Commands => _commands;

        public IFileSystemProxy()
        {
            _commands = new Dictionary<int, ServiceProcessRequest>
            {
                { 1,    Initialize                               },
                { 8,    OpenFileSystemWithId                     },
                { 11,   OpenBisFileSystem                        },
                { 18,   OpenSdCardFileSystem                     },
                { 51,   OpenSaveDataFileSystem                   },
                { 52,   OpenSaveDataFileSystemBySystemSaveDataId },
                { 200,  OpenDataStorageByCurrentProcess          },
                { 202,  OpenDataStorageByDataId                  },
                { 203,  OpenPatchDataStorageByCurrentProcess     },
                { 1005, GetGlobalAccessLogMode                   }
            };
        }

        // Initialize(u64, pid)
        public long Initialize(ServiceCtx context)
        {
            return 0;
        }

        // OpenFileSystemWithId(nn::fssrv::sf::FileSystemType filesystem_type, nn::ApplicationId tid, buffer<bytes<0x301>, 0x19, 0x301> path) 
        // -> object<nn::fssrv::sf::IFileSystem> contentFs
        public long OpenFileSystemWithId(ServiceCtx context)
        {
            FileSystemType fileSystemType = (FileSystemType)context.RequestData.ReadInt32();
            long           titleId        = context.RequestData.ReadInt64();
            string         switchPath     = ReadUtf8String(context);
            string         fullPath       = context.Device.FileSystem.SwitchPathToSystemPath(switchPath);

            if (!File.Exists(fullPath))
            {
                if (fullPath.Contains("."))
                {
                    return OpenFileSystemFromInternalFile(context, fullPath);
                }

                return MakeError(ErrorModule.Fs, FsErr.PathDoesNotExist);
            }

            FileStream fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
            string     extension  = Path.GetExtension(fullPath);

            if (extension == ".nca")
            {
                return OpenNcaFs(context, fullPath, fileStream.AsStorage());
            }
            else if (extension == ".nsp")
            {
                return OpenNsp(context, fullPath);
            }

            return MakeError(ErrorModule.Fs, FsErr.InvalidInput);
        }

        // OpenBisFileSystem(nn::fssrv::sf::Partition partitionID, buffer<bytes<0x301>, 0x19, 0x301>) -> object<nn::fssrv::sf::IFileSystem> Bis
        public long OpenBisFileSystem(ServiceCtx context)
        {
            int    bisPartitionId  = context.RequestData.ReadInt32();
            string partitionString = ReadUtf8String(context);
            string bisPartitonPath = string.Empty;

            switch (bisPartitionId)
            {
                case 29:
                    bisPartitonPath = SafeNandPath;
                    break;
                case 30:
                case 31:
                    bisPartitonPath = SystemNandPath;
                    break;
                case 32:
                    bisPartitonPath = UserNandPath;
                    break;
                default:
                    return MakeError(ErrorModule.Fs, FsErr.InvalidInput);
            }

            string fullPath = context.Device.FileSystem.GetFullPartitionPath(bisPartitonPath);

            FileSystemProvider fileSystemProvider = new FileSystemProvider(fullPath, context.Device.FileSystem.GetBasePath());

            MakeObject(context, new IFileSystem(fullPath, fileSystemProvider));

            return 0;
        }

        // OpenSdCardFileSystem() -> object<nn::fssrv::sf::IFileSystem>
        public long OpenSdCardFileSystem(ServiceCtx context)
        {
            string sdCardPath = context.Device.FileSystem.GetSdCardPath();

            FileSystemProvider fileSystemProvider = new FileSystemProvider(sdCardPath, context.Device.FileSystem.GetBasePath());

            MakeObject(context, new IFileSystem(sdCardPath, fileSystemProvider));

            return 0;
        }

        // OpenSaveDataFileSystem(u8 save_data_space_id, nn::fssrv::sf::SaveStruct saveStruct) -> object<nn::fssrv::sf::IFileSystem> saveDataFs
        public long OpenSaveDataFileSystem(ServiceCtx context)
        {
            LoadSaveDataFileSystem(context);

            return 0;
        }

        // OpenSaveDataFileSystemBySystemSaveDataId(u8 save_data_space_id, nn::fssrv::sf::SaveStruct saveStruct) -> object<nn::fssrv::sf::IFileSystem> systemSaveDataFs
        public long OpenSaveDataFileSystemBySystemSaveDataId(ServiceCtx context)
        {
            LoadSaveDataFileSystem(context);

            return 0;
        }

        // OpenDataStorageByCurrentProcess() -> object<nn::fssrv::sf::IStorage> dataStorage
        public long OpenDataStorageByCurrentProcess(ServiceCtx context)
        {
            MakeObject(context, new IStorage(context.Device.FileSystem.RomFs));

            return 0;
        }

        // OpenDataStorageByDataId(u8 storageId, nn::ApplicationId tid) -> object<nn::fssrv::sf::IStorage> dataStorage
        public long OpenDataStorageByDataId(ServiceCtx context)
        {
            StorageId storageId = (StorageId)context.RequestData.ReadByte();
            byte[]    padding   = context.RequestData.ReadBytes(7);
            long      titleId   = context.RequestData.ReadInt64();

            ContentType contentType = ContentType.Data;

            StorageId installedStorage =
                context.Device.System.ContentManager.GetInstalledStorage(titleId, contentType, storageId);

            if (installedStorage == StorageId.None)
            {
                contentType = ContentType.AocData;

                installedStorage =
                    context.Device.System.ContentManager.GetInstalledStorage(titleId, contentType, storageId);
            }

            if (installedStorage != StorageId.None)
            {
                string contentPath = context.Device.System.ContentManager.GetInstalledContentPath(titleId, storageId, contentType);
                string installPath = context.Device.FileSystem.SwitchPathToSystemPath(contentPath);

                if (!string.IsNullOrWhiteSpace(installPath))
                {
                    string ncaPath = installPath;

                    if (File.Exists(ncaPath))
                    {
                        LibHac.IO.IStorage ncaStorage   = new FileStream(ncaPath, FileMode.Open, FileAccess.Read).AsStorage();
                        Nca                nca          = new Nca(context.Device.System.KeySet, ncaStorage, false);
                        NcaSection         romfsSection = nca.Sections.FirstOrDefault(x => x?.Type == SectionType.Romfs);
                        Stream             romfsStream  = nca.OpenSection(romfsSection.SectionNum, false, context.Device.System.FsIntegrityCheckLevel, false).AsStream();

                        MakeObject(context, new IStorage(romfsStream));

                        return 0;
                    }
                    else
                    { 
                        throw new FileNotFoundException($"No Nca found in Path `{ncaPath}`.");
                    }
                }
                else
                { 
                    throw new DirectoryNotFoundException($"Path for title id {titleId:x16} on Storage {storageId} was not found in Path {installPath}.");
                }
            }

            throw new FileNotFoundException($"System archive with titleid {titleId:x16} was not found on Storage {storageId}. Found in {installedStorage}.");
        }

        // OpenPatchDataStorageByCurrentProcess() -> object<nn::fssrv::sf::IStorage>
        public long OpenPatchDataStorageByCurrentProcess(ServiceCtx context)
        {
            MakeObject(context, new IStorage(context.Device.FileSystem.RomFs));

            return 0;
        }

        // GetGlobalAccessLogMode() -> u32 logMode
        public long GetGlobalAccessLogMode(ServiceCtx context)
        {
            context.ResponseData.Write(0);

            return 0;
        }

        public void LoadSaveDataFileSystem(ServiceCtx context)
        {
            SaveSpaceId saveSpaceId = (SaveSpaceId)context.RequestData.ReadInt64();

            long titleId = context.RequestData.ReadInt64();

            UInt128 userId = new UInt128(
                context.RequestData.ReadInt64(), 
                context.RequestData.ReadInt64());

            long               saveId             = context.RequestData.ReadInt64();
            SaveDataType       saveDataType       = (SaveDataType)context.RequestData.ReadByte();
            SaveInfo           saveInfo           = new SaveInfo(titleId, saveId, saveDataType, userId, saveSpaceId);
            string             savePath           = context.Device.FileSystem.GetGameSavePath(saveInfo, context);
            FileSystemProvider fileSystemProvider = new FileSystemProvider(savePath, context.Device.FileSystem.GetBasePath());

            MakeObject(context, new IFileSystem(savePath, fileSystemProvider));
        }

        private long OpenNsp(ServiceCtx context, string pfsPath)
        {
            FileStream pfsFile = new FileStream(pfsPath, FileMode.Open, FileAccess.Read);
            Pfs        nsp     = new Pfs(pfsFile.AsStorage());

            ImportTitleKeysFromNsp(nsp, context.Device.System.KeySet);


            IFileSystem nspFileSystem = new IFileSystem(pfsPath, new PFsProvider(nsp));

            MakeObject(context, nspFileSystem);

            return 0;
        }

        private long OpenNcaFs(ServiceCtx context, string ncaPath, LibHac.IO.IStorage ncaStorage)
        {
            Nca nca = new Nca(context.Device.System.KeySet, ncaStorage, false);

            NcaSection romfsSection = nca.Sections.FirstOrDefault(x => x?.Type == SectionType.Romfs);
            NcaSection pfsSection   = nca.Sections.FirstOrDefault(x => x?.Type == SectionType.Pfs0);

            if (romfsSection != null)
            {
                LibHac.IO.IStorage romfsStorage = nca.OpenSection(romfsSection.SectionNum, false, context.Device.System.FsIntegrityCheckLevel, false);
                IFileSystem ncaFileSystem       = new IFileSystem(ncaPath, new RomFsProvider(romfsStorage));

                MakeObject(context, ncaFileSystem);
            }
            else if(pfsSection != null)
            {
                LibHac.IO.IStorage pfsStorage    = nca.OpenSection(pfsSection.SectionNum, false, context.Device.System.FsIntegrityCheckLevel, false);
                Pfs                pfs           = new Pfs(pfsStorage);
                IFileSystem        ncaFileSystem = new IFileSystem(ncaPath, new PFsProvider(pfs));

                MakeObject(context, ncaFileSystem);
            }
            else
            {
                return MakeError(ErrorModule.Fs, FsErr.PartitionNotFound);
            }

            return 0;
        }

        private long OpenFileSystemFromInternalFile(ServiceCtx context, string fullPath)
        {
            DirectoryInfo archivePath = new DirectoryInfo(fullPath).Parent;

            while (string.IsNullOrWhiteSpace(archivePath.Extension))
            {
                archivePath = archivePath.Parent;
            }

            if (archivePath.Extension == ".nsp" && File.Exists(archivePath.FullName))
            {
                FileStream pfsFile = new FileStream(
                    archivePath.FullName.TrimEnd(Path.DirectorySeparatorChar),
                    FileMode.Open,
                    FileAccess.Read);

                Pfs nsp = new Pfs(pfsFile.AsStorage());

                ImportTitleKeysFromNsp(nsp, context.Device.System.KeySet);
                
                string filename = fullPath.Replace(archivePath.FullName, string.Empty).TrimStart('\\');

                if (nsp.FileExists(filename))
                {
                    return OpenNcaFs(context, fullPath, nsp.OpenFile(filename));
                }
            }

            return MakeError(ErrorModule.Fs, FsErr.PathDoesNotExist);
        }

        private void ImportTitleKeysFromNsp(Pfs nsp, Keyset keySet)
        {
            foreach (PfsFileEntry ticketEntry in nsp.Files.Where(x => x.Name.EndsWith(".tik")))
            {
                Ticket ticket = new Ticket(nsp.OpenFile(ticketEntry).AsStream());

                if (!keySet.TitleKeys.ContainsKey(ticket.RightsId))
                {
                    keySet.TitleKeys.Add(ticket.RightsId, ticket.GetTitleKey(keySet));
                }
            }
        }
    }
}