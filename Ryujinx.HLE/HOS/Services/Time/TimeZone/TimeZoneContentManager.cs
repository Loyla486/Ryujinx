﻿using LibHac;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.FsSystem.NcaUtils;
using Ryujinx.Common.Logging;
using Ryujinx.Configuration;
using Ryujinx.HLE.Exceptions;
using Ryujinx.HLE.FileSystem;
using Ryujinx.HLE.FileSystem.Content;
using Ryujinx.HLE.HOS.Services.Time.Clock;
using Ryujinx.HLE.Utilities;
using System.Collections.Generic;
using System.IO;

using static Ryujinx.HLE.HOS.Services.Time.TimeZone.TimeZoneRule;

namespace Ryujinx.HLE.HOS.Services.Time.TimeZone
{
    public class TimeZoneContentManager
    {
        private const long TimeZoneBinaryTitleId = 0x010000000000080E;

        private readonly string TimeZoneSystemTitleMissingErrorMessage = "TimeZoneBinary system title not found! TimeZone conversions will not work, provide the system archive to fix this error. (See https://github.com/Ryujinx/Ryujinx#requirements for more information)";

        private VirtualFileSystem   _virtualFileSystem;
        private IntegrityCheckLevel _fsIntegrityCheckLevel;
        private ContentManager      _contentManager;

        public string[] LocationNameCache { get; private set; }

        internal TimeZoneManager Manager { get; private set; }

        public TimeZoneContentManager()
        {
            Manager = new TimeZoneManager();
        }

        public void InitializeInstance(VirtualFileSystem virtualFileSystem, ContentManager contentManager, IntegrityCheckLevel fsIntegrityCheckLevel)
        {
            _virtualFileSystem     = virtualFileSystem;
            _contentManager        = contentManager;
            _fsIntegrityCheckLevel = fsIntegrityCheckLevel;

            InitializeLocationNameCache();
        }

        public string SanityCheckDeviceLocationName()
        {
            string locationName = ConfigurationState.Instance.System.TimeZone;

            if (IsLocationNameValid(locationName))
            {
                return locationName;
            }

            Logger.PrintWarning(LogClass.ServiceTime, $"Invalid device TimeZone {locationName}, switching back to UTC");

            ConfigurationState.Instance.System.TimeZone.Value = "UTC";

            return "UTC";
        }

        internal void Initialize(TimeManager timeManager, Switch device)
        {
            InitializeInstance(device.FileSystem, device.System.ContentManager, device.System.FsIntegrityCheckLevel);

            SteadyClockTimePoint timeZoneUpdatedTimePoint = timeManager.StandardSteadyClock.GetCurrentTimePoint(null);

            string deviceLocationName = SanityCheckDeviceLocationName();

            ResultCode result = GetTimeZoneBinary(deviceLocationName, out Stream timeZoneBinaryStream, out LocalStorage ncaFile);

            if (result == ResultCode.Success)
            {
                // TODO: Read TimeZoneVersion from sysarchive.
                timeManager.SetupTimeZoneManager(deviceLocationName, timeZoneUpdatedTimePoint, (uint)LocationNameCache.Length, new UInt128(), timeZoneBinaryStream);

                ncaFile.Dispose();
            }
            else
            {
                // In the case the user don't have the timezone system archive, we just mark the manager as initialized.
                Manager.MarkInitialized();
            }
        }

        private void InitializeLocationNameCache()
        {
            if (HasTimeZoneBinaryTitle())
            {
                using (IStorage ncaFileStream = new LocalStorage(_virtualFileSystem.SwitchPathToSystemPath(GetTimeZoneBinaryTitleContentPath()), FileAccess.Read, FileMode.Open))
                {
                    Nca         nca              = new Nca(_virtualFileSystem.KeySet, ncaFileStream);
                    IFileSystem romfs            = nca.OpenFileSystem(NcaSectionType.Data, _fsIntegrityCheckLevel);

                    romfs.OpenFile(out IFile binaryListFile, "/binaryList.txt".ToU8Span(), OpenMode.Read).ThrowIfFailure();

                    StreamReader reader = new StreamReader(binaryListFile.AsStream());

                    List<string> locationNameList = new List<string>();

                    string locationName;
                    while ((locationName = reader.ReadLine()) != null)
                    {
                        locationNameList.Add(locationName);
                    }

                    LocationNameCache = locationNameList.ToArray();
                }
            }
            else
            {
                LocationNameCache = new string[] { "UTC" };

                Logger.PrintError(LogClass.ServiceTime, TimeZoneSystemTitleMissingErrorMessage);
            }
        }

        private bool IsLocationNameValid(string locationName)
        {
            foreach (string cachedLocationName in LocationNameCache)
            {
                if (cachedLocationName.Equals(locationName))
                {
                    return true;
                }
            }

            return false;
        }

        public ResultCode SetDeviceLocationName(string locationName)
        {
            ResultCode result = GetTimeZoneBinary(locationName, out Stream timeZoneBinaryStream, out LocalStorage ncaFile);

            if (result == ResultCode.Success)
            {
                result = Manager.SetDeviceLocationNameWithTimeZoneRule(locationName, timeZoneBinaryStream);

                ncaFile.Dispose();
            }

            return result;
        }

        public ResultCode LoadLocationNameList(uint index, out string[] outLocationNameArray, uint maxLength)
        {
            List<string> locationNameList = new List<string>();

            for (int i = 0; i < LocationNameCache.Length && i < maxLength; i++)
            {
                if (i < index)
                {
                    continue;
                }

                string locationName = LocationNameCache[i];

                // If the location name is too long, error out.
                if (locationName.Length > 0x24)
                {
                    outLocationNameArray = new string[0];

                    return ResultCode.LocationNameTooLong;
                }

                locationNameList.Add(locationName);
            }

            outLocationNameArray = locationNameList.ToArray();

            return ResultCode.Success;
        }

        public string GetTimeZoneBinaryTitleContentPath()
        {
            return _contentManager.GetInstalledContentPath(TimeZoneBinaryTitleId, StorageId.NandSystem, NcaContentType.Data);
        }

        public bool HasTimeZoneBinaryTitle()
        {
            return !string.IsNullOrEmpty(GetTimeZoneBinaryTitleContentPath());
        }

        internal ResultCode GetTimeZoneBinary(string locationName, out Stream timeZoneBinaryStream, out LocalStorage ncaFile)
        {
            timeZoneBinaryStream = null;
            ncaFile              = null;

            if (!HasTimeZoneBinaryTitle() || !IsLocationNameValid(locationName))
            {
                return ResultCode.TimeZoneNotFound;
            }

            ncaFile = new LocalStorage(_virtualFileSystem.SwitchPathToSystemPath(GetTimeZoneBinaryTitleContentPath()), FileAccess.Read, FileMode.Open);

            Nca         nca   = new Nca(_virtualFileSystem.KeySet, ncaFile);
            IFileSystem romfs = nca.OpenFileSystem(NcaSectionType.Data, _fsIntegrityCheckLevel);

            Result result = romfs.OpenFile(out IFile timeZoneBinaryFile, $"/zoneinfo/{locationName}".ToU8Span(), OpenMode.Read);

            timeZoneBinaryStream = timeZoneBinaryFile.AsStream();

            return (ResultCode)result.Value;
        }

        internal ResultCode LoadTimeZoneRule(out TimeZoneRule outRules, string locationName)
        {
            outRules = new TimeZoneRule
            {
                Ats   = new long[TzMaxTimes],
                Types = new byte[TzMaxTimes],
                Ttis  = new TimeTypeInfo[TzMaxTypes],
                Chars = new char[TzCharsArraySize]
            };

            if (!HasTimeZoneBinaryTitle())
            {
                throw new InvalidSystemResourceException(TimeZoneSystemTitleMissingErrorMessage);
            }

            ResultCode result = GetTimeZoneBinary(locationName, out Stream timeZoneBinaryStream, out LocalStorage ncaFile);

            if (result == ResultCode.Success)
            {
                result = Manager.ParseTimeZoneRuleBinary(out outRules, timeZoneBinaryStream);

                ncaFile.Dispose();
            }

            return result;
        }
    }
}