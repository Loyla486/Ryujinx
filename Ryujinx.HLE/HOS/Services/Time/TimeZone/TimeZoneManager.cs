﻿using LibHac.Fs.NcaUtils;
using Ryujinx.Common.Logging;
using Ryujinx.HLE.FileSystem;
using System;
using System.Collections.ObjectModel;
using LibHac.Fs;
using System.IO;
using System.Collections.Generic;
using TimeZoneConverter.Posix;
using TimeZoneConverter;

using static Ryujinx.HLE.HOS.Services.Time.TimeZoneRule;
using static Ryujinx.HLE.HOS.ErrorCode;

namespace Ryujinx.HLE.HOS.Services.Time.TimeZone
{
    public sealed class TimeZoneManager
    {
        private const long TimeZoneBinaryTitleId = 0x010000000000080E;

        private static TimeZoneManager instance;

        private static object instanceLock = new object();

        private Switch         _device;
        private TimeZoneRule   _myRules;
        private string        _deviceLocationName;
        private string[]      _locationNameCache;

        TimeZoneManager()
        {
            _device = null;

            // Empty rules (UTC)
            _myRules = new TimeZoneRule
            {
                Ats   = new long[TzMaxTimes],
                Types = new byte[TzMaxTimes],
                Ttis  = new TimeTypeInfo[TzMaxTypes],
                Chars = new char[TzCharsArraySize]
            };

            _deviceLocationName = "UTC";
        }

        internal void Initialize(Switch device)
        {
            _device = device;

            InitializeLocationNameCache();
        }

        private void InitializeLocationNameCache()
        {
            if (HasTimeZoneBinaryTitle())
            {
                using (IStorage ncaFileStream = new LocalStorage(_device.FileSystem.SwitchPathToSystemPath(GetTimeZoneBinaryTitleContentPath()), FileAccess.Read, FileMode.Open))
                {
                    Nca nca                 = new Nca(_device.System.KeySet, ncaFileStream);
                    IFileSystem romfs       = nca.OpenFileSystem(NcaSectionType.Data, _device.System.FsIntegrityCheckLevel);
                    Stream binaryListStream = romfs.OpenFile("binaryList.txt", OpenMode.Read).AsStream();

                    StreamReader reader = new StreamReader(binaryListStream);

                    List<string> locationNameList = new List<string>();

                    string locationName;
                    while ((locationName = reader.ReadLine()) != null)
                    {
                        locationNameList.Add(locationName);
                    }

                    _locationNameCache = locationNameList.ToArray();
                }
            }
            else
            {
                ReadOnlyCollection<TimeZoneInfo> timeZoneInfos = TimeZoneInfo.GetSystemTimeZones();
                _locationNameCache = new string[timeZoneInfos.Count];

                int i = 0;

                foreach (TimeZoneInfo timeZoneInfo in timeZoneInfos)
                {
                    bool needConversion = TZConvert.TryWindowsToIana(timeZoneInfo.Id, out string convertedName);
                    if (needConversion)
                    {
                        _locationNameCache[i] = convertedName;
                    }
                    else
                    {
                        _locationNameCache[i] = timeZoneInfo.Id;
                    }
                    i++;
                }

                // As we aren't using the system archive, "UTC" might not exist on the host system.
                // Load from C# TimeZone APIs UTC id.
                string utcId = TimeZoneInfo.Utc.Id;
                bool utcNeedConversion = TZConvert.TryWindowsToIana(utcId, out string utcConvertedName);
                if (utcNeedConversion)
                {
                    utcId = utcConvertedName;
                }

                _deviceLocationName = utcId;
            }
        }

        private bool IsLocationNameValid(string locationName)
        {
            foreach (string cachedLocationName in _locationNameCache)
            {
                if (cachedLocationName.Equals(locationName))
                {
                    return true;
                }
            }
            return false;
        }

        public string GetDeviceLocationName()
        {
            return _deviceLocationName;
        }

        public uint SetDeviceLocationName(string locationName)
        {
            uint resultCode = LoadTimeZoneRules(out TimeZoneRule rules, locationName);

            if (resultCode == 0)
            {
                _myRules            = rules;
                _deviceLocationName = locationName;
            }

            return resultCode;
        }

        public uint LoadLocationNameList(uint index, out string[] outLocationNameArray, uint maxLength)
        {
            List<string> locationNameList = new List<string>();

            for (int i = 0; i < _locationNameCache.Length && i < maxLength; i++)
            {
                if (i < index)
                {
                    continue;
                }

                string locationName = _locationNameCache[i];

                // If the location name is too long, error out.
                if (locationName.Length > 0x24)
                {
                    outLocationNameArray = new string[0];

                    return MakeError(ErrorModule.Time, TimeError.LocationNameTooLong);
                }

                locationNameList.Add(locationName);
            }

            outLocationNameArray = locationNameList.ToArray();

            return 0;
        }

        public uint GetTotalLocationNameCount()
        {
            return (uint)_locationNameCache.Length;
        }

        public string GetTimeZoneBinaryTitleContentPath()
        {
            return _device.System.ContentManager.GetInstalledContentPath(TimeZoneBinaryTitleId, StorageId.NandSystem, ContentType.Data);
        }

        public bool HasTimeZoneBinaryTitle()
        {
            return !string.IsNullOrEmpty(GetTimeZoneBinaryTitleContentPath());
        }

        public uint LoadTimeZoneRules(out TimeZoneRule outRules, string locationName)
        {
            outRules = new TimeZoneRule
            {
                Ats   = new long[TzMaxTimes],
                Types = new byte[TzMaxTimes],
                Ttis  = new TimeTypeInfo[TzMaxTypes],
                Chars = new char[TzCharsArraySize]
            };

            if (!IsLocationNameValid(locationName))
            {
                return MakeError(ErrorModule.Time, TimeError.TimeZoneNotFound);
            }

            if (!HasTimeZoneBinaryTitle())
            {
                // If the user doesn't have the system archives, we generate a POSIX rule string and parse it to generate a incomplete TimeZoneRule
                // TODO: As for now not having system archives is fine, we should enforce the usage of system archives later.
                Logger.PrintWarning(LogClass.ServiceTime, "TimeZoneBinary system archive not found! Time conversions will not be accurate!");
                try
                {
                    TimeZoneInfo info      = TZConvert.GetTimeZoneInfo(locationName);
                    string       posixRule = PosixTimeZone.FromTimeZoneInfo(info);

                    if (!TimeZone.ParsePosixName(posixRule, out outRules))
                    {
                        return MakeError(ErrorModule.Time, TimeError.TimeZoneConversionFailed);
                    }

                    return 0;
                }
                catch (TimeZoneNotFoundException)
                {
                    Logger.PrintWarning(LogClass.ServiceTime, $"Timezone not found for string: {locationName})");

                    return MakeError(ErrorModule.Time, TimeError.TimeZoneNotFound);
                }
            }
            else
            {
                using (IStorage ncaFileStream = new LocalStorage(_device.FileSystem.SwitchPathToSystemPath(GetTimeZoneBinaryTitleContentPath()), FileAccess.Read, FileMode.Open))
                {
                    Nca nca = new Nca(_device.System.KeySet, ncaFileStream);

                    IFileSystem romfs      = nca.OpenFileSystem(NcaSectionType.Data, _device.System.FsIntegrityCheckLevel);
                    Stream      tzIfStream = romfs.OpenFile($"zoneinfo/{locationName}", OpenMode.Read).AsStream();

                    if (!TimeZone.LoadTimeZoneRules(out outRules, tzIfStream))
                    {
                        return MakeError(ErrorModule.Time, TimeError.TimeZoneConversionFailed);
                    }
                }

                return 0;
            }
        }

        public uint ToCalendarTimeWithMyRules(long time, out CalendarInfo calendar)
        {
            return ToCalendarTime(_myRules, time, out calendar);
        }

        public static uint ToCalendarTime(TimeZoneRule rules, long time, out CalendarInfo calendar)
        {
            int error = TimeZone.ToCalendarTime(rules, time, out calendar);

            if (error != 0)
            {
                return MakeError(ErrorModule.Time, error);
            }

            return 0;
        }

        public uint ToPosixTimeWithMyRules(CalendarTime calendarTime, out long posixTime)
        {
            return ToPosixTime(_myRules, calendarTime, out posixTime);
        }

        public static uint ToPosixTime(TimeZoneRule rules, CalendarTime calendarTime, out long posixTime)
        {
            int error = TimeZone.ToPosixTime(rules, calendarTime, out posixTime);

            if (error != 0)
            {
                return MakeError(ErrorModule.Time, error);
            }

            return 0;
        }

        public static TimeZoneManager Instance
        {
            get
            {
                lock (instanceLock)
                {
                    if (instance == null)
                    {
                        instance = new TimeZoneManager();
                    }

                    return instance;
                }
            }
        }
    }
}
