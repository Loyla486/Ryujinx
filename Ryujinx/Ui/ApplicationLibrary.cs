using JsonPrettyPrinterPlus;
using LibHac;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Shim;
using LibHac.FsSystem;
using LibHac.FsSystem.NcaUtils;
using LibHac.Ncm;
using LibHac.Ns;
using LibHac.Spl;
using Ryujinx.Common.Logging;
using Ryujinx.Configuration.System;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.Loaders.Npdm;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Utf8Json;
using Utf8Json.Resolvers;

using RightsId = LibHac.Fs.RightsId;

namespace Ryujinx.Ui
{
    public class ApplicationLibrary
    {
        public static event EventHandler<ApplicationAddedEventArgs>        ApplicationAdded;
        public static event EventHandler<ApplicationCountUpdatedEventArgs> ApplicationCountUpdated;

        private static readonly byte[] _nspIcon = GetResourceBytes("Ryujinx.Ui.assets.NSPIcon.png");
        private static readonly byte[] _xciIcon = GetResourceBytes("Ryujinx.Ui.assets.XCIIcon.png");
        private static readonly byte[] _ncaIcon = GetResourceBytes("Ryujinx.Ui.assets.NCAIcon.png");
        private static readonly byte[] _nroIcon = GetResourceBytes("Ryujinx.Ui.assets.NROIcon.png");
        private static readonly byte[] _nsoIcon = GetResourceBytes("Ryujinx.Ui.assets.NSOIcon.png");

        private static VirtualFileSystem _virtualFileSystem;
        private static Language          _desiredTitleLanguage;
        private static bool              _loadingError;

        public static IEnumerable<string> GetFilesInDirectory(string directory)
        {
            Stack<string> stack = new Stack<string>();
            stack.Push(directory);
            while (stack.Count > 0)
            {
                string dir = stack.Pop();
                string[] content = { };

                try
                {
                    content = Directory.GetFiles(dir, "*");
                }
                catch (UnauthorizedAccessException)
                {
                    Logger.PrintWarning(LogClass.Application, $"Failed to get access to directory: \"{dir}\"");
                }

                if (content.Length > 0)
                {
                    foreach (string file in content)
                        yield return file;
                }

                try
                {
                    content = Directory.GetDirectories(dir);
                }
                catch (UnauthorizedAccessException)
                {
                    Logger.PrintWarning(LogClass.Application, $"Failed to get access to directory: \"{dir}\"");
                }

                if (content.Length > 0)
                {
                    foreach (string subdir in content)
                        stack.Push(subdir);
                }
            }
        }

        public static void ReadControlData(IFileSystem controlFs, Span<byte> outProperty)
        {
            controlFs.OpenFile(out IFile controlFile, "/control.nacp".ToU8Span(), OpenMode.Read).ThrowIfFailure();
            controlFile.Read(out long _, 0, outProperty, ReadOption.None).ThrowIfFailure();
        }

        public static void LoadApplications(List<string> appDirs, VirtualFileSystem virtualFileSystem, Language desiredTitleLanguage)
        {
            int numApplicationsFound  = 0;
            int numApplicationsLoaded = 0;

            _loadingError         = false;
            _virtualFileSystem    = virtualFileSystem;
            _desiredTitleLanguage = desiredTitleLanguage;

            // Builds the applications list with paths to found applications
            List<string> applications = new List<string>();
            foreach (string appDir in appDirs)
            {
                
                if (!Directory.Exists(appDir))
                {
                    Logger.PrintWarning(LogClass.Application, $"The \"game_dirs\" section in \"Config.json\" contains an invalid directory: \"{appDir}\"");

                    continue;
                }

                foreach (string app in GetFilesInDirectory(appDir))
                {
                    if ((Path.GetExtension(app).ToLower() == ".nsp") ||
                        (Path.GetExtension(app).ToLower() == ".pfs0") ||
                        (Path.GetExtension(app).ToLower() == ".xci") ||
                        (Path.GetExtension(app).ToLower() == ".nca") ||
                        (Path.GetExtension(app).ToLower() == ".nro") ||
                        (Path.GetExtension(app).ToLower() == ".nso"))
                    {
                        applications.Add(app);
                        numApplicationsFound++;
                    }
                }
            }

            // Loops through applications list, creating a struct and then firing an event containing the struct for each application
            foreach (string applicationPath in applications)
            {
                double fileSize        = new FileInfo(applicationPath).Length * 0.000000000931;
                string titleName       = "Unknown";
                string titleId         = "0000000000000000";
                string developer       = "Unknown";
                string version         = "0";
                string saveDataPath    = null;
                byte[] applicationIcon = null;
                BlitStruct<ApplicationControlProperty> controlHolder = new BlitStruct<ApplicationControlProperty>(1);

                try
                {
                    using (FileStream file = new FileStream(applicationPath, FileMode.Open, FileAccess.Read))
                    {
                        if ((Path.GetExtension(applicationPath).ToLower() == ".nsp")  ||
                            (Path.GetExtension(applicationPath).ToLower() == ".pfs0") ||
                            (Path.GetExtension(applicationPath).ToLower() == ".xci"))
                        {
                            try
                            {
                                PartitionFileSystem pfs;

                                bool isExeFs = false;

                                if (Path.GetExtension(applicationPath).ToLower() == ".xci")
                                {
                                    Xci xci = new Xci(_virtualFileSystem.KeySet, file.AsStorage());

                                    pfs = xci.OpenPartition(XciPartitionType.Secure);
                                }
                                else
                                {
                                    pfs = new PartitionFileSystem(file.AsStorage());

                                    // If the NSP doesn't have a main NCA, decrement the number of applications found and then continue to the next application.
                                    bool hasMainNca = false;

                                    foreach (DirectoryEntryEx fileEntry in pfs.EnumerateEntries("/", "*"))
                                    {
                                        if (Path.GetExtension(fileEntry.FullPath).ToLower() == ".nca")
                                        {
                                            pfs.OpenFile(out IFile ncaFile, fileEntry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                                            Nca nca       = new Nca(_virtualFileSystem.KeySet, ncaFile.AsStorage());
                                            int dataIndex = Nca.GetSectionIndexFromType(NcaSectionType.Data, NcaContentType.Program);

                                            if (nca.Header.ContentType == NcaContentType.Program && !nca.Header.GetFsHeader(dataIndex).IsPatchSection())
                                            {
                                                hasMainNca = true;

                                                break;
                                            }
                                        }
                                        else if (Path.GetFileNameWithoutExtension(fileEntry.FullPath) == "main")
                                        {
                                            isExeFs = true;
                                        }
                                    }

                                    if (!hasMainNca && !isExeFs)
                                    {
                                        numApplicationsFound--;

                                        continue;
                                    }
                                }

                                if (isExeFs)
                                {
                                    applicationIcon = _nspIcon;

                                    Result result = pfs.OpenFile(out IFile npdmFile, "/main.npdm".ToU8Span(), OpenMode.Read);

                                    if (ResultFs.PathNotFound.Includes(result))
                                    {
                                        Npdm npdm = new Npdm(npdmFile.AsStream());

                                        titleName = npdm.TitleName;
                                        titleId   = npdm.Aci0.TitleId.ToString("x16");
                                    }
                                }
                                else
                                {
                                    // Store the ControlFS in variable called controlFs
                                    GetControlFsAndTitleId(pfs, out IFileSystem controlFs, out titleId);

                                    ReadControlData(controlFs, controlHolder.ByteSpan);

                                    // Creates NACP class from the NACP file
                                    controlFs.OpenFile(out IFile controlNacpFile, "/control.nacp".ToU8Span(), OpenMode.Read).ThrowIfFailure();

                                    Nacp controlData = new Nacp(controlNacpFile.AsStream());

                                    // Get the title name, title ID, developer name and version number from the NACP
                                    version = controlData.DisplayVersion;

                                    GetNameIdDeveloper(controlData, out titleName, out _, out developer);

                                    // Read the icon from the ControlFS and store it as a byte array
                                    try
                                    {
                                        controlFs.OpenFile(out IFile icon, $"/icon_{_desiredTitleLanguage}.dat".ToU8Span(), OpenMode.Read).ThrowIfFailure();

                                        using (MemoryStream stream = new MemoryStream())
                                        {
                                            icon.AsStream().CopyTo(stream);
                                            applicationIcon = stream.ToArray();
                                        }
                                    }
                                    catch (HorizonResultException)
                                    {
                                        foreach (DirectoryEntryEx entry in controlFs.EnumerateEntries("/", "*"))
                                        {
                                            if (entry.Name == "control.nacp")
                                            {
                                                continue;
                                            }

                                            controlFs.OpenFile(out IFile icon, entry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                                            using (MemoryStream stream = new MemoryStream())
                                            {
                                                icon.AsStream().CopyTo(stream);
                                                applicationIcon = stream.ToArray();
                                            }

                                            if (applicationIcon != null)
                                            {
                                                break;
                                            }
                                        }

                                        if (applicationIcon == null)
                                        {
                                            applicationIcon = Path.GetExtension(applicationPath).ToLower() == ".xci" ? _xciIcon : _nspIcon;
                                        }
                                    }
                                }
                            }
                            catch (MissingKeyException exception)
                            {
                                applicationIcon = Path.GetExtension(applicationPath).ToLower() == ".xci" ? _xciIcon : _nspIcon;

                                Logger.PrintWarning(LogClass.Application, $"Your key set is missing a key with the name: {exception.Name}");
                            }
                            catch (InvalidDataException)
                            {
                                applicationIcon = Path.GetExtension(applicationPath).ToLower() == ".xci" ? _xciIcon : _nspIcon;

                                Logger.PrintWarning(LogClass.Application, $"The header key is incorrect or missing and therefore the NCA header content type check has failed. Errored File: {applicationPath}");
                            }
                            catch (Exception exception)
                            {
                                Logger.PrintWarning(LogClass.Application, $"The file encountered was not of a valid type. Errored File: {applicationPath}");
                                Logger.PrintDebug(LogClass.Application, exception.ToString());

                                numApplicationsFound--;
                                _loadingError = true;

                                continue;
                            }
                        }
                        else if (Path.GetExtension(applicationPath).ToLower() == ".nro")
                        {
                            BinaryReader reader = new BinaryReader(file);

                            byte[] Read(long position, int size)
                            {
                                file.Seek(position, SeekOrigin.Begin);

                                return reader.ReadBytes(size);
                            }

                            try
                            {
                                file.Seek(24, SeekOrigin.Begin);

                                int assetOffset = reader.ReadInt32();

                                if (Encoding.ASCII.GetString(Read(assetOffset, 4)) == "ASET")
                                {
                                    byte[] iconSectionInfo = Read(assetOffset + 8, 0x10);

                                    long iconOffset = BitConverter.ToInt64(iconSectionInfo, 0);
                                    long iconSize   = BitConverter.ToInt64(iconSectionInfo, 8);

                                    ulong nacpOffset = reader.ReadUInt64();
                                    ulong nacpSize   = reader.ReadUInt64();

                                    // Reads and stores game icon as byte array
                                    applicationIcon = Read(assetOffset + iconOffset, (int) iconSize);

                                    // Creates memory stream out of byte array which is the NACP
                                    using (MemoryStream stream = new MemoryStream(Read(assetOffset + (int) nacpOffset, (int) nacpSize)))
                                    {
                                        // Creates NACP class from the memory stream
                                        Nacp controlData = new Nacp(stream);

                                        // Get the title name, title ID, developer name and version number from the NACP
                                        version = controlData.DisplayVersion;

                                        GetNameIdDeveloper(controlData, out titleName, out titleId, out developer);
                                    }
                                }
                                else
                                {
                                    applicationIcon = _nroIcon;
                                    titleName       = Path.GetFileNameWithoutExtension(applicationPath);
                                }
                            }
                            catch
                            {
                                Logger.PrintWarning(LogClass.Application, $"The file encountered was not of a valid type. Errored File: {applicationPath}");

                                numApplicationsFound--;

                                continue;
                            }
                        }
                        else if (Path.GetExtension(applicationPath).ToLower() == ".nca")
                        {
                            try
                            {
                                Nca nca       = new Nca(_virtualFileSystem.KeySet, new FileStream(applicationPath, FileMode.Open, FileAccess.Read).AsStorage());
                                int dataIndex = Nca.GetSectionIndexFromType(NcaSectionType.Data, NcaContentType.Program);

                                if (nca.Header.ContentType != NcaContentType.Program || nca.Header.GetFsHeader(dataIndex).IsPatchSection())
                                {
                                    numApplicationsFound--;

                                    continue;
                                }
                            }
                            catch (InvalidDataException)
                            {
                                Logger.PrintWarning(LogClass.Application, $"The NCA header content type check has failed. This is usually because the header key is incorrect or missing. Errored File: {applicationPath}");
                            }
                            catch
                            {
                                Logger.PrintWarning(LogClass.Application, $"The file encountered was not of a valid type. Errored File: {applicationPath}");

                                numApplicationsFound--;
                                _loadingError = true;

                                continue;
                            }

                            applicationIcon = _ncaIcon;
                            titleName       = Path.GetFileNameWithoutExtension(applicationPath);
                        }
                        // If its an NSO we just set defaults
                        else if (Path.GetExtension(applicationPath).ToLower() == ".nso")
                        {
                            applicationIcon = _nsoIcon;
                            titleName       = Path.GetFileNameWithoutExtension(applicationPath);
                        }
                    }
                }
                catch (IOException exception)
                {
                    Logger.PrintWarning(LogClass.Application, exception.Message);

                    numApplicationsFound--;
                    _loadingError = true;

                    continue;
                }

                ApplicationMetadata appMetadata = LoadAndSaveMetaData(titleId);

                if (ulong.TryParse(titleId, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong titleIdNum))
                {
                    SaveDataFilter filter = new SaveDataFilter();
                    filter.SetUserId(new UserId(1, 0));
                    filter.SetProgramId(new TitleId(titleIdNum));

                    Result result = virtualFileSystem.FsClient.FindSaveDataWithFilter(out SaveDataInfo saveDataInfo, SaveDataSpaceId.User, ref filter);

                    if (result.IsSuccess())
                    {
                        saveDataPath = Path.Combine(virtualFileSystem.GetNandPath(), $"user/save/{saveDataInfo.SaveDataId:x16}");
                    }
                }

                ApplicationData data = new ApplicationData()
                {
                    Favorite      = appMetadata.Favorite,
                    Icon          = applicationIcon,
                    TitleName     = titleName,
                    TitleId       = titleId,
                    Developer     = developer,
                    Version       = version,
                    TimePlayed    = ConvertSecondsToReadableString(appMetadata.TimePlayed),
                    LastPlayed    = appMetadata.LastPlayed,
                    FileExtension = Path.GetExtension(applicationPath).ToUpper().Remove(0 ,1),
                    FileSize      = (fileSize < 1) ? (fileSize * 1024).ToString("0.##") + "MB" : fileSize.ToString("0.##") + "GB",
                    Path          = applicationPath,
                    SaveDataPath  = saveDataPath,
                    ControlHolder = controlHolder
                };

                numApplicationsLoaded++;

                OnApplicationAdded(new ApplicationAddedEventArgs()
                {
                    AppData = data
                });

                OnApplicationCountUpdated(new ApplicationCountUpdatedEventArgs()
                {
                    NumAppsFound  = numApplicationsFound,
                    NumAppsLoaded = numApplicationsLoaded
                });
            }

            OnApplicationCountUpdated(new ApplicationCountUpdatedEventArgs()
            {
                NumAppsFound  = numApplicationsFound,
                NumAppsLoaded = numApplicationsLoaded
            });

            if (_loadingError)
            {
                Gtk.Application.Invoke(delegate
                {
                    GtkDialog.CreateErrorDialog("One or more files encountered could not be loaded, check logs for more info.");
                });
            }
        }

        protected static void OnApplicationAdded(ApplicationAddedEventArgs e)
        {
            ApplicationAdded?.Invoke(null, e);
        }

        protected static void OnApplicationCountUpdated(ApplicationCountUpdatedEventArgs e)
        {
            ApplicationCountUpdated?.Invoke(null, e);
        }

        private static byte[] GetResourceBytes(string resourceName)
        {
            Stream resourceStream    = Assembly.GetCallingAssembly().GetManifestResourceStream(resourceName);
            byte[] resourceByteArray = new byte[resourceStream.Length];

            resourceStream.Read(resourceByteArray);

            return resourceByteArray;
        }

        private static void GetControlFsAndTitleId(PartitionFileSystem pfs, out IFileSystem controlFs, out string titleId)
        {
            Nca controlNca = null;

            // Add keys to key set if needed
            foreach (DirectoryEntryEx ticketEntry in pfs.EnumerateEntries("/", "*.tik"))
            {
                Result result = pfs.OpenFile(out IFile ticketFile, ticketEntry.FullPath.ToU8Span(), OpenMode.Read);

                if (result.IsSuccess())
                {
                    Ticket ticket = new Ticket(ticketFile.AsStream());

                    _virtualFileSystem.KeySet.ExternalKeySet.Add(new RightsId(ticket.RightsId), new AccessKey(ticket.GetTitleKey(_virtualFileSystem.KeySet)));
                }
            }

            // Find the Control NCA and store it in variable called controlNca
            foreach (DirectoryEntryEx fileEntry in pfs.EnumerateEntries("/", "*.nca"))
            {
                pfs.OpenFile(out IFile ncaFile, fileEntry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

                Nca nca = new Nca(_virtualFileSystem.KeySet, ncaFile.AsStorage());

                if (nca.Header.ContentType == NcaContentType.Control)
                {
                    controlNca = nca;
                }
            }

            // Return the ControlFS
            controlFs = controlNca?.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);
            titleId = controlNca?.Header.TitleId.ToString("x16");
        }

        internal static ApplicationMetadata LoadAndSaveMetaData(string titleId, Action<ApplicationMetadata> modifyFunction = null)
        {
            string metadataFolder = Path.Combine(_virtualFileSystem.GetBasePath(), "games", titleId, "gui");
            string metadataFile   = Path.Combine(metadataFolder, "metadata.json");

            IJsonFormatterResolver resolver = CompositeResolver.Create(new[] { StandardResolver.AllowPrivateSnakeCase });

            ApplicationMetadata appMetadata;

            if (!File.Exists(metadataFile))
            {
                Directory.CreateDirectory(metadataFolder);

                appMetadata = new ApplicationMetadata
                {
                    Favorite   = false,
                    TimePlayed = 0,
                    LastPlayed = "Never"
                };

                byte[] data = JsonSerializer.Serialize(appMetadata, resolver);
                File.WriteAllText(metadataFile, Encoding.UTF8.GetString(data, 0, data.Length).PrettyPrintJson());
            }

            using (Stream stream = File.OpenRead(metadataFile))
            {
                appMetadata = JsonSerializer.Deserialize<ApplicationMetadata>(stream, resolver);
            }

            if (modifyFunction != null)
            {
                modifyFunction(appMetadata);

                byte[] saveData = JsonSerializer.Serialize(appMetadata, resolver);
                File.WriteAllText(metadataFile, Encoding.UTF8.GetString(saveData, 0, saveData.Length).PrettyPrintJson());
            }

            return appMetadata;
        }

        private static string ConvertSecondsToReadableString(double seconds)
        {
            const int secondsPerMinute = 60;
            const int secondsPerHour   = secondsPerMinute * 60;
            const int secondsPerDay    = secondsPerHour   * 24;

            string readableString;

            if (seconds < secondsPerMinute)
            {
                readableString = $"{seconds}s";
            }
            else if (seconds < secondsPerHour)
            {
                readableString = $"{Math.Round(seconds / secondsPerMinute, 2, MidpointRounding.AwayFromZero)} mins";
            }
            else if (seconds < secondsPerDay)
            {
                readableString = $"{Math.Round(seconds / secondsPerHour, 2, MidpointRounding.AwayFromZero)} hrs";
            }
            else
            {
                readableString = $"{Math.Round(seconds / secondsPerDay, 2, MidpointRounding.AwayFromZero)} days";
            }

            return readableString;
        }

        private static void GetNameIdDeveloper(Nacp controlData, out string titleName, out string titleId, out string developer)
        {
            Enum.TryParse(_desiredTitleLanguage.ToString(), out TitleLanguage desiredTitleLanguage);

            NacpDescription nacpDescription = controlData.Descriptions.ToList().Find(x => x.Language == desiredTitleLanguage);

            if (nacpDescription != null)
            {
                titleName = nacpDescription.Title;
                developer = nacpDescription.Developer;
            }
            else
            {
                titleName = null;
                developer = null;
            }

            if (string.IsNullOrWhiteSpace(titleName))
            {
                titleName = controlData.Descriptions.ToList().Find(x => !string.IsNullOrWhiteSpace(x.Title)).Title;
            }

            if (string.IsNullOrWhiteSpace(developer))
            {
                developer = controlData.Descriptions.ToList().Find(x => !string.IsNullOrWhiteSpace(x.Developer)).Developer;
            }

            if (controlData.PresenceGroupId != 0)
            {
                titleId = controlData.PresenceGroupId.ToString("x16");
            }
            else if (controlData.SaveDataOwnerId != 0)
            {
                titleId = controlData.SaveDataOwnerId.ToString("x16");
            }
            else if (controlData.AddOnContentBaseId != 0)
            {
                titleId = (controlData.AddOnContentBaseId - 0x1000).ToString("x16");
            }
            else
            {
                titleId = "0000000000000000";
            }
        }
    }
}
