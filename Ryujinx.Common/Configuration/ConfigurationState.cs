﻿using Ryujinx.Common;
using Ryujinx.Common.Configuration.Hid;
using Ryujinx.Common.Logging;
using Ryujinx.Configuration.Hid;
using Ryujinx.Configuration.System;
using Ryujinx.Configuration.Ui;
using System;
using System.Collections.Generic;

namespace Ryujinx.Configuration
{
    public class ConfigurationState
    {
        /// <summary>
        /// UI configuration section
        /// </summary>
        public class UiSection
        {
            public class Columns
            {
                public ReactiveObject<bool> FavColumn        { get; private set; }
                public ReactiveObject<bool> IconColumn       { get; private set; }
                public ReactiveObject<bool> AppColumn        { get; private set; }
                public ReactiveObject<bool> DevColumn        { get; private set; }
                public ReactiveObject<bool> VersionColumn    { get; private set; }
                public ReactiveObject<bool> TimePlayedColumn { get; private set; }
                public ReactiveObject<bool> LastPlayedColumn { get; private set; } 
                public ReactiveObject<bool> FileExtColumn    { get; private set; }
                public ReactiveObject<bool> FileSizeColumn   { get; private set; }
                public ReactiveObject<bool> PathColumn       { get; private set; }

                public Columns()
                {
                    FavColumn        = new ReactiveObject<bool>();
                    IconColumn       = new ReactiveObject<bool>();
                    AppColumn        = new ReactiveObject<bool>();
                    DevColumn        = new ReactiveObject<bool>();
                    VersionColumn    = new ReactiveObject<bool>();
                    TimePlayedColumn = new ReactiveObject<bool>();
                    LastPlayedColumn = new ReactiveObject<bool>();
                    FileExtColumn    = new ReactiveObject<bool>();
                    FileSizeColumn   = new ReactiveObject<bool>();
                    PathColumn       = new ReactiveObject<bool>();
                }
            }

            public class ColumnSortSettings
            {
                public ReactiveObject<int>  SortColumnId  { get; private set; }
                public ReactiveObject<bool> SortAscending { get; private set; }

                public ColumnSortSettings()
                {
                    SortColumnId  = new ReactiveObject<int>();
                    SortAscending = new ReactiveObject<bool>();
                }
            }

            /// <summary>
            /// Used to toggle columns in the GUI
            /// </summary>
            public Columns GuiColumns { get; private set; }

            /// <summary>
            /// Used to configure column sort settings in the GUI
            /// </summary>
            public ColumnSortSettings ColumnSort { get; private set; }

            /// <summary>
            /// A list of directories containing games to be used to load games into the games list
            /// </summary>
            public ReactiveObject<List<string>> GameDirs { get; private set; }

            /// <summary>
            /// Enable or disable custom themes in the GUI
            /// </summary>
            public ReactiveObject<bool> EnableCustomTheme { get; private set; }

            /// <summary>
            /// Path to custom GUI theme
            /// </summary>
            public ReactiveObject<string> CustomThemePath { get; private set; }

            public UiSection()
            {
                GuiColumns        = new Columns();
                ColumnSort        = new ColumnSortSettings();
                GameDirs          = new ReactiveObject<List<string>>();
                EnableCustomTheme = new ReactiveObject<bool>();
                CustomThemePath   = new ReactiveObject<string>();
            }
        }

        /// <summary>
        /// Logger configuration section
        /// </summary>
        public class LoggerSection
        {
            /// <summary>
            /// Enables printing debug log messages
            /// </summary>
            public ReactiveObject<bool> EnableDebug { get; private set; }

            /// <summary>
            /// Enables printing stub log messages
            /// </summary>
            public ReactiveObject<bool> EnableStub { get; private set; }

            /// <summary>
            /// Enables printing info log messages
            /// </summary>
            public ReactiveObject<bool> EnableInfo { get; private set; }

            /// <summary>
            /// Enables printing warning log messages
            /// </summary>
            public ReactiveObject<bool> EnableWarn { get; private set; }

            /// <summary>
            /// Enables printing error log messages
            /// </summary>
            public ReactiveObject<bool> EnableError { get; private set; }

            /// <summary>
            /// Enables printing guest log messages
            /// </summary>
            public ReactiveObject<bool> EnableGuest { get; private set; }

            /// <summary>
            /// Enables printing FS access log messages
            /// </summary>
            public ReactiveObject<bool> EnableFsAccessLog { get; private set; }

            /// <summary>
            /// Controls which log messages are written to the log targets
            /// </summary>
            public ReactiveObject<LogClass[]> FilteredClasses { get; private set; }

            /// <summary>
            /// Enables or disables logging to a file on disk
            /// </summary>
            public ReactiveObject<bool> EnableFileLog { get; private set; }

            public LoggerSection()
            {
                EnableDebug       = new ReactiveObject<bool>();
                EnableStub        = new ReactiveObject<bool>();
                EnableInfo        = new ReactiveObject<bool>();
                EnableWarn        = new ReactiveObject<bool>();
                EnableError       = new ReactiveObject<bool>();
                EnableGuest       = new ReactiveObject<bool>();
                EnableFsAccessLog = new ReactiveObject<bool>();
                FilteredClasses   = new ReactiveObject<LogClass[]>();
                EnableFileLog     = new ReactiveObject<bool>();
            }
        }

        /// <summary>
        /// System configuration section
        /// </summary>
        public class SystemSection
        {
            /// <summary>
            /// Change System Language
            /// </summary>
            public ReactiveObject<Language> Language { get; private set; }

            /// <summary>
            /// Change System Region
            /// </summary>
            public ReactiveObject<Region> Region { get; private set; }

            /// <summary>
            /// Change System TimeZone
            /// </summary>
            public ReactiveObject<string> TimeZone { get; private set; }

            /// <summary>
            /// System Time Offset in Seconds
            /// </summary>
            public ReactiveObject<long> SystemTimeOffset { get; private set; }

            /// <summary>
            /// Enables or disables Docked Mode
            /// </summary>
            public ReactiveObject<bool> EnableDockedMode { get; private set; }

            /// <summary>
            /// Enables or disables multi-core scheduling of threads
            /// </summary>
            public ReactiveObject<bool> EnableMulticoreScheduling { get; private set; }

            /// <summary>
            /// Enables or disables profiled translation cache persistency
            /// </summary>
            public ReactiveObject<bool> EnablePtc { get; private set; }

            /// <summary>
            /// Enables integrity checks on Game content files
            /// </summary>
            public ReactiveObject<bool> EnableFsIntegrityChecks { get; private set; }

            /// <summary>
            /// Enables FS access log output to the console. Possible modes are 0-3
            /// </summary>
            public ReactiveObject<int> FsGlobalAccessLogMode { get; private set; }

            /// <summary>
            /// The selected audio backend
            /// </summary>
            public ReactiveObject<AudioBackend> AudioBackend { get; private set; }

            /// <summary>
            /// Enable or disable ignoring missing services
            /// </summary>
            public ReactiveObject<bool> IgnoreMissingServices { get; private set; }

            public SystemSection()
            {
                Language                  = new ReactiveObject<Language>();
                Region                    = new ReactiveObject<Region>();
                TimeZone                  = new ReactiveObject<string>();
                SystemTimeOffset          = new ReactiveObject<long>();
                EnableDockedMode          = new ReactiveObject<bool>();
                EnableMulticoreScheduling = new ReactiveObject<bool>();
                EnablePtc                 = new ReactiveObject<bool>();
                EnableFsIntegrityChecks   = new ReactiveObject<bool>();
                FsGlobalAccessLogMode     = new ReactiveObject<int>();
                AudioBackend              = new ReactiveObject<AudioBackend>();
                IgnoreMissingServices     = new ReactiveObject<bool>();
            }
        }

        /// <summary>
        /// Hid configuration section
        /// </summary>
        public class HidSection
        {
            /// <summary>
            /// Enable or disable keyboard support (Independent from controllers binding)
            /// </summary>
            public ReactiveObject<bool> EnableKeyboard { get; private set; }

            /// <summary>
            /// Hotkey Keyboard Bindings
            /// </summary>
            public ReactiveObject<KeyboardHotkeys> Hotkeys { get; private set; }

            /// <summary>
            /// Input device configuration.
            /// NOTE: This ReactiveObject won't issue an event when the List has elements added or removed.
            /// TODO: Implement a ReactiveList class.
            /// </summary>
            public ReactiveObject<List<InputConfig>> InputConfig { get; private set; }

            public HidSection()
            {
                EnableKeyboard = new ReactiveObject<bool>();
                Hotkeys        = new ReactiveObject<KeyboardHotkeys>();
                InputConfig    = new ReactiveObject<List<InputConfig>>();
            }
        }

        /// <summary>
        /// Graphics configuration section
        /// </summary>
        public class GraphicsSection
        {
            /// <summary>
            /// Max Anisotropy. Values range from 0 - 16. Set to -1 to let the game decide.
            /// </summary>
            public ReactiveObject<float> MaxAnisotropy { get; private set; }

            /// <summary>
            /// Resolution Scale. An integer scale applied to applicable render targets. Values 1-4, or -1 to use a custom floating point scale instead.
            /// </summary>
            public ReactiveObject<int> ResScale { get; private set; }

            /// <summary>
            /// Custom Resolution Scale. A custom floating point scale applied to applicable render targets. Only active when Resolution Scale is -1.
            /// </summary>
            public ReactiveObject<float> ResScaleCustom { get; private set; }

            /// <summary>
            /// Dumps shaders in this local directory
            /// </summary>
            public ReactiveObject<string> ShadersDumpPath { get; private set; }

            /// <summary>
            /// Enables or disables Vertical Sync
            /// </summary>
            public ReactiveObject<bool> EnableVsync { get; private set; }

            public GraphicsSection()
            {
                ResScale        = new ReactiveObject<int>();
                ResScaleCustom  = new ReactiveObject<float>();
                MaxAnisotropy   = new ReactiveObject<float>();
                ShadersDumpPath = new ReactiveObject<string>();
                EnableVsync     = new ReactiveObject<bool>();
            }
        }

        /// <summary>
        /// The default configuration instance
        /// </summary>
        public static ConfigurationState Instance { get; private set; }

        /// <summary>
        /// The Ui section
        /// </summary>
        public UiSection Ui { get; private set; }

        /// <summary>
        /// The Logger section
        /// </summary>
        public LoggerSection Logger { get; private set; }

        /// <summary>
        /// The System section
        /// </summary>
        public SystemSection System { get; private set; }

        /// <summary>
        /// The Graphics section
        /// </summary>
        public GraphicsSection Graphics { get; private set; }

        /// <summary>
        /// The Hid section
        /// </summary>
        public HidSection Hid { get; private set; }

        /// <summary>
        /// Enables or disables Discord Rich Presence
        /// </summary>
        public ReactiveObject<bool> EnableDiscordIntegration { get; private set; }

        private ConfigurationState()
        {
            Ui                       = new UiSection();
            Logger                   = new LoggerSection();
            System                   = new SystemSection();
            Graphics                 = new GraphicsSection();
            Hid                      = new HidSection();
            EnableDiscordIntegration = new ReactiveObject<bool>();
        }

        public ConfigurationFileFormat ToFileFormat()
        {
            List<ControllerConfig> controllerConfigList = new List<ControllerConfig>();
            List<KeyboardConfig>   keyboardConfigList   = new List<KeyboardConfig>();

            foreach (InputConfig inputConfig in Hid.InputConfig.Value)
            {
                if (inputConfig is ControllerConfig controllerConfig)
                {
                    controllerConfigList.Add(controllerConfig);
                }
                else if (inputConfig is KeyboardConfig keyboardConfig)
                {
                    keyboardConfigList.Add(keyboardConfig);
                }
            }

            ConfigurationFileFormat configurationFile = new ConfigurationFileFormat
            {
                Version                   = ConfigurationFileFormat.CurrentVersion,
                ResScale                  = Graphics.ResScale,
                ResScaleCustom            = Graphics.ResScaleCustom,
                MaxAnisotropy             = Graphics.MaxAnisotropy,
                GraphicsShadersDumpPath   = Graphics.ShadersDumpPath,
                LoggingEnableDebug        = Logger.EnableDebug,
                LoggingEnableStub         = Logger.EnableStub,
                LoggingEnableInfo         = Logger.EnableInfo,
                LoggingEnableWarn         = Logger.EnableWarn,
                LoggingEnableError        = Logger.EnableError,
                LoggingEnableGuest        = Logger.EnableGuest,
                LoggingEnableFsAccessLog  = Logger.EnableFsAccessLog,
                LoggingFilteredClasses    = Logger.FilteredClasses,
                EnableFileLog             = Logger.EnableFileLog,
                SystemLanguage            = System.Language,
                SystemRegion              = System.Region,
                SystemTimeZone            = System.TimeZone,
                SystemTimeOffset          = System.SystemTimeOffset,
                DockedMode                = System.EnableDockedMode,
                EnableDiscordIntegration  = EnableDiscordIntegration,
                EnableVsync               = Graphics.EnableVsync,
                EnableMulticoreScheduling = System.EnableMulticoreScheduling,
                EnablePtc                 = System.EnablePtc,
                EnableFsIntegrityChecks   = System.EnableFsIntegrityChecks,
                FsGlobalAccessLogMode     = System.FsGlobalAccessLogMode,
                AudioBackend              = System.AudioBackend,
                IgnoreMissingServices     = System.IgnoreMissingServices,
                GuiColumns                = new GuiColumns
                {
                    FavColumn        = Ui.GuiColumns.FavColumn,
                    IconColumn       = Ui.GuiColumns.IconColumn,
                    AppColumn        = Ui.GuiColumns.AppColumn,
                    DevColumn        = Ui.GuiColumns.DevColumn,
                    VersionColumn    = Ui.GuiColumns.VersionColumn,
                    TimePlayedColumn = Ui.GuiColumns.TimePlayedColumn,
                    LastPlayedColumn = Ui.GuiColumns.LastPlayedColumn,
                    FileExtColumn    = Ui.GuiColumns.FileExtColumn,
                    FileSizeColumn   = Ui.GuiColumns.FileSizeColumn,
                    PathColumn       = Ui.GuiColumns.PathColumn,
                },
                ColumnSort                = new ColumnSort
                {
                    SortColumnId  = Ui.ColumnSort.SortColumnId,
                    SortAscending = Ui.ColumnSort.SortAscending
                },
                GameDirs                  = Ui.GameDirs,
                EnableCustomTheme         = Ui.EnableCustomTheme,
                CustomThemePath           = Ui.CustomThemePath,
                EnableKeyboard            = Hid.EnableKeyboard,
                Hotkeys                   = Hid.Hotkeys,
                KeyboardConfig            = keyboardConfigList,
                ControllerConfig          = controllerConfigList
            };

            return configurationFile;
        }

        public void LoadDefault()
        {
            Graphics.ResScale.Value                = 1;
            Graphics.ResScaleCustom.Value          = 1.0f;
            Graphics.MaxAnisotropy.Value           = -1;
            Graphics.ShadersDumpPath.Value         = "";
            Logger.EnableDebug.Value               = false;
            Logger.EnableStub.Value                = true;
            Logger.EnableInfo.Value                = true;
            Logger.EnableWarn.Value                = true;
            Logger.EnableError.Value               = true;
            Logger.EnableGuest.Value               = true;
            Logger.EnableFsAccessLog.Value         = false;
            Logger.FilteredClasses.Value           = new LogClass[] { };
            Logger.EnableFileLog.Value             = true;
            System.Language.Value                  = Language.AmericanEnglish;
            System.Region.Value                    = Region.USA;
            System.TimeZone.Value                  = "UTC";
            System.SystemTimeOffset.Value          = 0;
            System.EnableDockedMode.Value          = false;
            EnableDiscordIntegration.Value         = true;
            Graphics.EnableVsync.Value             = true;
            System.EnableMulticoreScheduling.Value = true;
            System.EnablePtc.Value                 = false;
            System.EnableFsIntegrityChecks.Value   = true;
            System.FsGlobalAccessLogMode.Value     = 0;
            System.AudioBackend.Value              = AudioBackend.OpenAl;
            System.IgnoreMissingServices.Value     = false;
            Ui.GuiColumns.FavColumn.Value          = true;
            Ui.GuiColumns.IconColumn.Value         = true;
            Ui.GuiColumns.AppColumn.Value          = true;
            Ui.GuiColumns.DevColumn.Value          = true;
            Ui.GuiColumns.VersionColumn.Value      = true;
            Ui.GuiColumns.TimePlayedColumn.Value   = true;
            Ui.GuiColumns.LastPlayedColumn.Value   = true;
            Ui.GuiColumns.FileExtColumn.Value      = true;
            Ui.GuiColumns.FileSizeColumn.Value     = true;
            Ui.GuiColumns.PathColumn.Value         = true;
            Ui.ColumnSort.SortColumnId.Value       = 0;
            Ui.ColumnSort.SortAscending.Value      = false;
            Ui.GameDirs.Value                      = new List<string>();
            Ui.EnableCustomTheme.Value             = false;
            Ui.CustomThemePath.Value               = "";
            Hid.EnableKeyboard.Value               = false;
            
            Hid.Hotkeys.Value = new KeyboardHotkeys
            {
                ToggleVsync = Key.Tab
            };

            Hid.InputConfig.Value = new List<InputConfig>
            {
                new KeyboardConfig
                {
                    Index          = 0,
                    ControllerType = ControllerType.JoyconPair,
                    PlayerIndex    = PlayerIndex.Player1,
                    LeftJoycon     = new NpadKeyboardLeft
                    {
                        StickUp     = Key.W,
                        StickDown   = Key.S,
                        StickLeft   = Key.A,
                        StickRight  = Key.D,
                        StickButton = Key.F,
                        DPadUp      = Key.Up,
                        DPadDown    = Key.Down,
                        DPadLeft    = Key.Left,
                        DPadRight   = Key.Right,
                        ButtonMinus = Key.Minus,
                        ButtonL     = Key.E,
                        ButtonZl    = Key.Q,
                        ButtonSl    = Key.Home,
                        ButtonSr    = Key.End
                    },
                    RightJoycon    = new NpadKeyboardRight
                    {
                        StickUp     = Key.I,
                        StickDown   = Key.K,
                        StickLeft   = Key.J,
                        StickRight  = Key.L,
                        StickButton = Key.H,
                        ButtonA     = Key.Z,
                        ButtonB     = Key.X,
                        ButtonX     = Key.C,
                        ButtonY     = Key.V,
                        ButtonPlus  = Key.Plus,
                        ButtonR     = Key.U,
                        ButtonZr    = Key.O,
                        ButtonSl    = Key.PageUp,
                        ButtonSr    = Key.PageDown
                    }
                }
            };
        }

        public void Load(ConfigurationFileFormat configurationFileFormat, string configurationFilePath)
        {
            bool configurationFileUpdated = false;

            if (configurationFileFormat.Version < 0 || configurationFileFormat.Version > ConfigurationFileFormat.CurrentVersion)
            {
                Common.Logging.Logger.PrintWarning(LogClass.Application, $"Unsupported configuration version {configurationFileFormat.Version}, loading default.");

                LoadDefault();

                return;
            }

            if (configurationFileFormat.Version < 2)
            {
                Common.Logging.Logger.PrintWarning(LogClass.Application, $"Outdated configuration version {configurationFileFormat.Version}, migrating to version 2.");

                configurationFileFormat.SystemRegion = Region.USA;

                configurationFileUpdated = true;
            }

            if (configurationFileFormat.Version < 3)
            {
                Common.Logging.Logger.PrintWarning(LogClass.Application, $"Outdated configuration version {configurationFileFormat.Version}, migrating to version 3.");

                configurationFileFormat.SystemTimeZone = "UTC";

                configurationFileUpdated = true;
            }

            if (configurationFileFormat.Version < 4)
            {
                Common.Logging.Logger.PrintWarning(LogClass.Application, $"Outdated configuration version {configurationFileFormat.Version}, migrating to version 4.");

                configurationFileFormat.MaxAnisotropy = -1;

                configurationFileUpdated = true;
            }

            if (configurationFileFormat.Version < 5)
            {
                Common.Logging.Logger.PrintWarning(LogClass.Application, $"Outdated configuration version {configurationFileFormat.Version}, migrating to version 5.");

                configurationFileFormat.SystemTimeOffset = 0;

                configurationFileUpdated = true;
            }

            if (configurationFileFormat.Version < 6)
            {
                Common.Logging.Logger.PrintWarning(LogClass.Application, $"Outdated configuration version {configurationFileFormat.Version}, migrating to version 6.");

                configurationFileFormat.ControllerConfig = new List<ControllerConfig>();
                configurationFileFormat.KeyboardConfig   = new List<KeyboardConfig>
                {
                    new KeyboardConfig
                    {
                        Index          = 0,
                        ControllerType = ControllerType.JoyconPair,
                        PlayerIndex    = PlayerIndex.Player1,
                        LeftJoycon     = new NpadKeyboardLeft
                        {
                            StickUp     = Key.W,
                            StickDown   = Key.S,
                            StickLeft   = Key.A,
                            StickRight  = Key.D,
                            StickButton = Key.F,
                            DPadUp      = Key.Up,
                            DPadDown    = Key.Down,
                            DPadLeft    = Key.Left,
                            DPadRight   = Key.Right,
                            ButtonMinus = Key.Minus,
                            ButtonL     = Key.E,
                            ButtonZl    = Key.Q,
                            ButtonSl    = Key.Unbound,
                            ButtonSr    = Key.Unbound
                        },
                        RightJoycon    = new NpadKeyboardRight
                        {
                            StickUp     = Key.I,
                            StickDown   = Key.K,
                            StickLeft   = Key.J,
                            StickRight  = Key.L,
                            StickButton = Key.H,
                            ButtonA     = Key.Z,
                            ButtonB     = Key.X,
                            ButtonX     = Key.C,
                            ButtonY     = Key.V,
                            ButtonPlus  = Key.Plus,
                            ButtonR     = Key.U,
                            ButtonZr    = Key.O,
                            ButtonSl    = Key.Unbound,
                            ButtonSr    = Key.Unbound
                        }
                    }
                };

                configurationFileUpdated = true;
            }

            // Only needed for version 6 configurations.
            if (configurationFileFormat.Version == 6)
            {
                Common.Logging.Logger.PrintWarning(LogClass.Application, $"Outdated configuration version {configurationFileFormat.Version}, migrating to version 7.");

                for (int i = 0; i < configurationFileFormat.KeyboardConfig.Count; i++)
                {
                    if (configurationFileFormat.KeyboardConfig[i].Index != KeyboardConfig.AllKeyboardsIndex)
                    {
                        configurationFileFormat.KeyboardConfig[i].Index++;
                    }
                }
            }

            if (configurationFileFormat.Version < 8)
            {
                Common.Logging.Logger.PrintWarning(LogClass.Application, $"Outdated configuration version {configurationFileFormat.Version}, migrating to version 8.");

                configurationFileFormat.EnablePtc = false;

                configurationFileUpdated = true;
            }

            if (configurationFileFormat.Version < 9)
            {
                Common.Logging.Logger.PrintWarning(LogClass.Application, $"Outdated configuration version {configurationFileFormat.Version}, migrating to version 9.");

                configurationFileFormat.ColumnSort = new ColumnSort
                {
                    SortColumnId  = 0,
                    SortAscending = false
                };

                configurationFileFormat.Hotkeys = new KeyboardHotkeys
                {
                    ToggleVsync = Key.Tab
                };

                configurationFileUpdated = true;
            }

            if (configurationFileFormat.Version < 10)
            {
                Common.Logging.Logger.PrintWarning(LogClass.Application, $"Outdated configuration version {configurationFileFormat.Version}, migrating to version 10.");

                configurationFileFormat.AudioBackend = AudioBackend.OpenAl;

                configurationFileUpdated = true;
            }

            if (configurationFileFormat.Version < 11)
            {
                Common.Logging.Logger.PrintWarning(LogClass.Application, $"Outdated configuration version {configurationFileFormat.Version}, migrating to version 11.");

                configurationFileFormat.ResScale = 1;
                configurationFileFormat.ResScaleCustom = 1.0f;

                configurationFileUpdated = true;
            }

            List<InputConfig> inputConfig = new List<InputConfig>();
            inputConfig.AddRange(configurationFileFormat.ControllerConfig);
            inputConfig.AddRange(configurationFileFormat.KeyboardConfig);

            Graphics.ResScale.Value                = configurationFileFormat.ResScale;
            Graphics.ResScaleCustom.Value          = configurationFileFormat.ResScaleCustom;
            Graphics.MaxAnisotropy.Value           = configurationFileFormat.MaxAnisotropy;
            Graphics.ShadersDumpPath.Value         = configurationFileFormat.GraphicsShadersDumpPath;
            Logger.EnableDebug.Value               = configurationFileFormat.LoggingEnableDebug;
            Logger.EnableStub.Value                = configurationFileFormat.LoggingEnableStub;
            Logger.EnableInfo.Value                = configurationFileFormat.LoggingEnableInfo;
            Logger.EnableWarn.Value                = configurationFileFormat.LoggingEnableWarn;
            Logger.EnableError.Value               = configurationFileFormat.LoggingEnableError;
            Logger.EnableGuest.Value               = configurationFileFormat.LoggingEnableGuest;
            Logger.EnableFsAccessLog.Value         = configurationFileFormat.LoggingEnableFsAccessLog;
            Logger.FilteredClasses.Value           = configurationFileFormat.LoggingFilteredClasses;
            Logger.EnableFileLog.Value             = configurationFileFormat.EnableFileLog;
            System.Language.Value                  = configurationFileFormat.SystemLanguage;
            System.Region.Value                    = configurationFileFormat.SystemRegion;
            System.TimeZone.Value                  = configurationFileFormat.SystemTimeZone;
            System.SystemTimeOffset.Value          = configurationFileFormat.SystemTimeOffset;
            System.EnableDockedMode.Value          = configurationFileFormat.DockedMode;
            EnableDiscordIntegration.Value         = configurationFileFormat.EnableDiscordIntegration;
            Graphics.EnableVsync.Value             = configurationFileFormat.EnableVsync;
            System.EnableMulticoreScheduling.Value = configurationFileFormat.EnableMulticoreScheduling;
            System.EnablePtc.Value                 = configurationFileFormat.EnablePtc;
            System.EnableFsIntegrityChecks.Value   = configurationFileFormat.EnableFsIntegrityChecks;
            System.FsGlobalAccessLogMode.Value     = configurationFileFormat.FsGlobalAccessLogMode;
            System.AudioBackend.Value              = configurationFileFormat.AudioBackend;
            System.IgnoreMissingServices.Value     = configurationFileFormat.IgnoreMissingServices;
            Ui.GuiColumns.FavColumn.Value          = configurationFileFormat.GuiColumns.FavColumn;
            Ui.GuiColumns.IconColumn.Value         = configurationFileFormat.GuiColumns.IconColumn;
            Ui.GuiColumns.AppColumn.Value          = configurationFileFormat.GuiColumns.AppColumn;
            Ui.GuiColumns.DevColumn.Value          = configurationFileFormat.GuiColumns.DevColumn;
            Ui.GuiColumns.VersionColumn.Value      = configurationFileFormat.GuiColumns.VersionColumn;
            Ui.GuiColumns.TimePlayedColumn.Value   = configurationFileFormat.GuiColumns.TimePlayedColumn;
            Ui.GuiColumns.LastPlayedColumn.Value   = configurationFileFormat.GuiColumns.LastPlayedColumn;
            Ui.GuiColumns.FileExtColumn.Value      = configurationFileFormat.GuiColumns.FileExtColumn;
            Ui.GuiColumns.FileSizeColumn.Value     = configurationFileFormat.GuiColumns.FileSizeColumn;
            Ui.GuiColumns.PathColumn.Value         = configurationFileFormat.GuiColumns.PathColumn;
            Ui.ColumnSort.SortColumnId.Value       = configurationFileFormat.ColumnSort.SortColumnId;
            Ui.ColumnSort.SortAscending.Value      = configurationFileFormat.ColumnSort.SortAscending;
            Ui.GameDirs.Value                      = configurationFileFormat.GameDirs;
            Ui.EnableCustomTheme.Value             = configurationFileFormat.EnableCustomTheme;
            Ui.CustomThemePath.Value               = configurationFileFormat.CustomThemePath;
            Hid.EnableKeyboard.Value               = configurationFileFormat.EnableKeyboard;
            Hid.Hotkeys.Value                      = configurationFileFormat.Hotkeys;
            Hid.InputConfig.Value                  = inputConfig;

            if (configurationFileUpdated)
            {
                ToFileFormat().SaveConfig(configurationFilePath);

                Common.Logging.Logger.PrintWarning(LogClass.Application, "Configuration file has been updated!");
            }
        }

        public static void Initialize()
        {
            if (Instance != null)
            {
                throw new InvalidOperationException("Configuration is already initialized");
            }

            Instance = new ConfigurationState();
        }
    }
}
