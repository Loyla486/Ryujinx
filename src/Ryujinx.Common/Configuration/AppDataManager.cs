using Ryujinx.Common.Logging;
using Ryujinx.Common.Utilities;
using System;
using System.IO;

namespace Ryujinx.Common.Configuration
{
    public static class AppDataManager
    {
        private const string DefaultBaseDir = "Ryujinx";
        private const string DefaultPortableDir = "portable";

        // The following 3 are always part of Base Directory
        private const string GamesDir = "games";
        private const string ProfilesDir = "profiles";
        private const string KeysDir = "system";

        public enum LaunchMode
        {
            UserProfile,
            Portable,
            Custom,
        }

        public static LaunchMode Mode { get; private set; }

        public static string BaseDirPath { get; private set; }
        public static string GamesDirPath { get; private set; }
        public static string ProfilesDirPath { get; private set; }
        public static string KeysDirPath { get; private set; }
        public static string KeysDirPathUser { get; }

        public static string LogsDirPath { get; private set; }

        public const string DefaultNandDir = "bis";
        public const string DefaultSdcardDir = "sdcard";
        private const string DefaultModsDir = "mods";

        public static string CustomModsPath { get; set; }
        public static string CustomSdModsPath { get; set; }
        public static string CustomNandPath { get; set; } // TODO: Actually implement this into VFS
        public static string CustomSdCardPath { get; set; } // TODO: Actually implement this into VFS

        static AppDataManager()
        {
            KeysDirPathUser = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch");
        }

        public static void Initialize(string baseDirPath)
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            if (appDataPath.Length == 0)
            {
                appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            string userProfilePath = Path.Combine(appDataPath, DefaultBaseDir);
            string portablePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultPortableDir);

            // On macOS, check for a portable directory next to the app bundle as well.
            if (OperatingSystem.IsMacOS() && !Directory.Exists(portablePath))
            {
                string bundlePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."));
                // Make sure we're actually running within an app bundle.
                if (bundlePath.EndsWith(".app"))
                {
                    portablePath = Path.GetFullPath(Path.Combine(bundlePath, "..", DefaultPortableDir));
                }
            }

            if (Directory.Exists(portablePath))
            {
                BaseDirPath = portablePath;
                Mode = LaunchMode.Portable;
            }
            else
            {
                BaseDirPath = userProfilePath;
                Mode = LaunchMode.UserProfile;
            }

            if (baseDirPath != null && baseDirPath != userProfilePath)
            {
                if (!Directory.Exists(baseDirPath))
                {
                    Logger.Error?.Print(LogClass.Application, $"Custom Data Directory '{baseDirPath}' does not exist. Falling back to {Mode}...");
                }
                else
                {
                    BaseDirPath = baseDirPath;
                    Mode = LaunchMode.Custom;
                }
            }

            BaseDirPath = Path.GetFullPath(BaseDirPath); // convert relative paths

            // NOTE: Moves the Ryujinx folder in `~/.config` to `~/Library/Application Support` if one is found
            // and a Ryujinx folder does not already exist in Application Support.
            // Also creates a symlink from `~/.config/Ryujinx` to `~/Library/Application Support/Ryujinx` to preserve backwards compatibility.
            // This should be removed in the future.
            if (OperatingSystem.IsMacOS() && Mode == LaunchMode.UserProfile)
            {
                string oldConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), DefaultBaseDir);
                if (Path.Exists(oldConfigPath) && !IsPathSymlink(oldConfigPath) && !Path.Exists(BaseDirPath))
                {
                    FileSystemUtils.MoveDirectory(oldConfigPath, BaseDirPath);
                    Directory.CreateSymbolicLink(oldConfigPath, BaseDirPath);
                }
            }

            SetupBasePaths();
        }

        public static string GetOrCreateLogsDir()
        {
            if (Directory.Exists(LogsDirPath))
            {
                return LogsDirPath;
            }

            Logger.Notice.Print(LogClass.Application, "Logging directory not found; attempting to create new logging directory.");
            LogsDirPath = SetUpLogsDir();

            return LogsDirPath;
        }

        private static string SetUpLogsDir()
        {
            string logDir = "";

            if (Mode == LaunchMode.Portable)
            {
                logDir = Path.Combine(BaseDirPath, "Logs");
                try
                {
                    Directory.CreateDirectory(logDir);
                }
                catch
                {
                    Logger.Warning?.Print(LogClass.Application, $"Logging directory could not be created '{logDir}'");

                    return null;
                }
            }
            else
            {
                if (OperatingSystem.IsMacOS())
                {
                    // NOTE: Should evaluate to "~/Library/Logs/Ryujinx/".
                    logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Logs", DefaultBaseDir);
                    try
                    {
                        Directory.CreateDirectory(logDir);
                    }
                    catch
                    {
                        Logger.Warning?.Print(LogClass.Application, $"Logging directory could not be created '{logDir}'");
                        logDir = "";
                    }

                    if (string.IsNullOrEmpty(logDir))
                    {
                        // NOTE: Should evaluate to "~/Library/Application Support/Ryujinx/Logs".
                        logDir = Path.Combine(BaseDirPath, "Logs");

                        try
                        {
                            Directory.CreateDirectory(logDir);
                        }
                        catch
                        {
                            Logger.Warning?.Print(LogClass.Application, $"Logging directory could not be created '{logDir}'");

                            return null;
                        }
                    }
                }
                else if (OperatingSystem.IsWindows())
                {
                    // NOTE: Should evaluate to a "Logs" directory in whatever directory Ryujinx was launched from.
                    logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                    try
                    {
                        Directory.CreateDirectory(logDir);
                    }
                    catch
                    {
                        Logger.Warning?.Print(LogClass.Application, $"Logging directory could not be created '{logDir}'");
                        logDir = "";
                    }

                    if (string.IsNullOrEmpty(logDir))
                    {
                        // NOTE: Should evaluate to "C:\Users\user\AppData\Roaming\Ryujinx\Logs".
                        logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), DefaultBaseDir, "Logs");

                        try
                        {
                            Directory.CreateDirectory(logDir);
                        }
                        catch
                        {
                            Logger.Warning?.Print(LogClass.Application, $"Logging directory could not be created '{logDir}'");

                            return null;
                        }
                    }
                }
                else if (OperatingSystem.IsLinux())
                {
                    // NOTE: Should evaluate to "~/.config/Ryujinx/Logs".
                    logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), DefaultBaseDir, "Logs");

                    try
                    {
                        Directory.CreateDirectory(logDir);
                    }
                    catch
                    {
                        Logger.Warning?.Print(LogClass.Application, $"Logging directory could not be created '{logDir}'");

                        return null;
                    }
                }
            }

            return logDir;
        }

        private static void SetupBasePaths()
        {
            Directory.CreateDirectory(BaseDirPath);
            LogsDirPath = SetUpLogsDir();
            Directory.CreateDirectory(GamesDirPath = Path.Combine(BaseDirPath, GamesDir));
            Directory.CreateDirectory(ProfilesDirPath = Path.Combine(BaseDirPath, ProfilesDir));
            Directory.CreateDirectory(KeysDirPath = Path.Combine(BaseDirPath, KeysDir));
        }

        // Check if existing old baseDirPath is a symlink, to prevent possible errors.
        // Should be removed, when the existence of the old directory isn't checked anymore.
        private static bool IsPathSymlink(string path)
        {
            FileAttributes attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }

        public static string GetModsPath() => CustomModsPath ?? Directory.CreateDirectory(Path.Combine(BaseDirPath, DefaultModsDir)).FullName;
        public static string GetSdModsPath() => CustomSdModsPath ?? Directory.CreateDirectory(Path.Combine(BaseDirPath, DefaultSdcardDir, "atmosphere")).FullName;
    }
}
