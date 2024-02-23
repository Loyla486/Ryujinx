using Ryujinx.HLE.HOS.Ipc;
using Ryujinx.HLE.Logging;
using System;
using System.Collections.Generic;
using System.Text;

using static Ryujinx.HLE.HOS.ErrorCode;

namespace Ryujinx.HLE.HOS.Services.Time
{
    class ITimeZoneService : IpcService
    {
        private Dictionary<int, ServiceProcessRequest> m_Commands;

        public override IReadOnlyDictionary<int, ServiceProcessRequest> Commands => m_Commands;

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private TimeZoneInfo TimeZone = TimeZoneInfo.Local;

        public ITimeZoneService()
        {
            m_Commands = new Dictionary<int, ServiceProcessRequest>()
            {
                { 0,   GetDeviceLocationName     },
                { 1,   SetDeviceLocationName     },
                { 2,   GetTotalLocationNameCount },
                { 3,   LoadLocationNameList      },
                { 4,   LoadTimeZoneRule          },
                { 100, ToCalendarTime            },
                { 101, ToCalendarTimeWithMyRule  },
                { 201, ToPosixTime               },
                { 202, ToPosixTimeWithMyRule     }
            };
        }

        public long GetDeviceLocationName(ServiceCtx Context)
        {
            char[] TzName = TimeZone.Id.ToCharArray();

            Context.ResponseData.Write(TzName);

            int Padding = 0x24 - TzName.Length;

            for (int Index = 0; Index < Padding; Index++)
            {
                Context.ResponseData.Write((byte)0);
            }

            return 0;
        }

        public long SetDeviceLocationName(ServiceCtx Context)
        {
            byte[] LocationName = Context.RequestData.ReadBytes(0x24);

            string TzID = Encoding.ASCII.GetString(LocationName).TrimEnd('\0');

            long ResultCode = 0;

            try
            {
                TimeZone = TimeZoneInfo.FindSystemTimeZoneById(TzID);
            }
            catch (TimeZoneNotFoundException)
            {
                ResultCode = MakeError(ErrorModule.Time, 0x3dd);
            }

            return ResultCode;
        }

        public long GetTotalLocationNameCount(ServiceCtx Context)
        {
            Context.ResponseData.Write(TimeZoneInfo.GetSystemTimeZones().Count);

            return 0;
        }

        public long LoadLocationNameList(ServiceCtx Context)
        {
            long BufferPosition = Context.Response.SendBuff[0].Position;
            long BufferSize     = Context.Response.SendBuff[0].Size;

            int Offset = 0;

            foreach (TimeZoneInfo info in TimeZoneInfo.GetSystemTimeZones())
            {
                byte[] TzData = Encoding.ASCII.GetBytes(info.Id);

                Context.Memory.WriteBytes(BufferPosition + Offset, TzData);

                int Padding = 0x24 - TzData.Length;

                for (int Index = 0; Index < Padding; Index++)
                {
                    Context.ResponseData.Write((byte)0);
                }

                Offset += 0x24;
            }

            return 0;
        }

        public long LoadTimeZoneRule(ServiceCtx Context)
        {
            long BufferPosition = Context.Request.ReceiveBuff[0].Position;
            long BufferSize     = Context.Request.ReceiveBuff[0].Size;

            if (BufferSize != 0x4000)
            {
                Context.Device.Log.PrintWarning(LogClass.ServiceTime, $"TimeZoneRule buffer size is 0x{BufferSize:x} (expected 0x4000)");
            }

            long ResultCode = 0;

            byte[] LocationName = Context.RequestData.ReadBytes(0x24);

            string TzID = Encoding.ASCII.GetString(LocationName).TrimEnd('\0');

            // Check if the Time Zone exists, otherwise error out.
            try
            {
                TimeZoneInfo Info = TimeZoneInfo.FindSystemTimeZoneById(TzID);

                byte[] TzData = Encoding.ASCII.GetBytes(Info.Id);

                // FIXME: This is not in ANY cases accurate, but the games don't care about the content of the buffer, they only pass it.
                // TODO: Reverse the TZif2 conversion in PCV to make this match with real hardware.
                Context.Memory.WriteBytes(BufferPosition, TzData);
            }
            catch (TimeZoneNotFoundException)
            {
                Context.Device.Log.PrintWarning(LogClass.ServiceTime, $"Timezone not found for string: {TzID} (len: {TzID.Length})");

                ResultCode = MakeError(ErrorModule.Time, 0x3dd);
            }

            return ResultCode;
        }

        private long ToCalendarTimeWithTz(ServiceCtx Context, long PosixTime, TimeZoneInfo Info)
        {
            DateTime CurrentTime = Epoch.AddSeconds(PosixTime);

            CurrentTime = TimeZoneInfo.ConvertTimeFromUtc(CurrentTime, Info);

            Context.ResponseData.Write((ushort)CurrentTime.Year);
            Context.ResponseData.Write((byte)CurrentTime.Month);
            Context.ResponseData.Write((byte)CurrentTime.Day);
            Context.ResponseData.Write((byte)CurrentTime.Hour);
            Context.ResponseData.Write((byte)CurrentTime.Minute);
            Context.ResponseData.Write((byte)CurrentTime.Second);
            Context.ResponseData.Write((byte)0); //MilliSecond ?
            Context.ResponseData.Write((int)CurrentTime.DayOfWeek);
            Context.ResponseData.Write(CurrentTime.DayOfYear - 1);
            Context.ResponseData.Write(new byte[8]); //TODO: Find out the names used.
            Context.ResponseData.Write((byte)(CurrentTime.IsDaylightSavingTime() ? 1 : 0));
            Context.ResponseData.Write((int)Info.GetUtcOffset(CurrentTime).TotalSeconds);

            return 0;
        }

        public long ToCalendarTime(ServiceCtx Context)
        {
            long PosixTime      = Context.RequestData.ReadInt64();
            long BufferPosition = Context.Request.SendBuff[0].Position;
            long BufferSize     = Context.Request.SendBuff[0].Size;

            if (BufferSize != 0x4000)
            {
                Context.Device.Log.PrintWarning(LogClass.ServiceTime, $"TimeZoneRule buffer size is 0x{BufferSize:x} (expected 0x4000)");
            }

            // TODO: Reverse the TZif2 conversion in PCV to make this match with real hardware.
            byte[] TzData = Context.Memory.ReadBytes(BufferPosition, 0x24);

            string TzID = Encoding.ASCII.GetString(TzData).TrimEnd('\0');

            long ResultCode = 0;

            // Check if the Time Zone exists, otherwise error out.
            try
            {
                TimeZoneInfo Info = TimeZoneInfo.FindSystemTimeZoneById(TzID);

                ResultCode = ToCalendarTimeWithTz(Context, PosixTime, Info);
            }
            catch (TimeZoneNotFoundException)
            {
                Context.Device.Log.PrintWarning(LogClass.ServiceTime, $"Timezone not found for string: {TzID} (len: {TzID.Length})");

                ResultCode = MakeError(ErrorModule.Time, 0x3dd);
            }

            return ResultCode;
        }

        public long ToCalendarTimeWithMyRule(ServiceCtx Context)
        {
            long PosixTime = Context.RequestData.ReadInt64();

            return ToCalendarTimeWithTz(Context, PosixTime, TimeZone);
        }

        public long ToPosixTime(ServiceCtx Context)
        {
            long BufferPosition = Context.Request.SendBuff[0].Position;
            long BufferSize     = Context.Request.SendBuff[0].Size;

            ushort Year   = Context.RequestData.ReadUInt16();
            byte   Month  = Context.RequestData.ReadByte();
            byte   Day    = Context.RequestData.ReadByte();
            byte   Hour   = Context.RequestData.ReadByte();
            byte   Minute = Context.RequestData.ReadByte();
            byte   Second = Context.RequestData.ReadByte();

            DateTime CalendarTime = new DateTime(Year, Month, Day, Hour, Minute, Second);

            if (BufferSize != 0x4000)
            {
                Context.Device.Log.PrintWarning(LogClass.ServiceTime, $"TimeZoneRule buffer size is 0x{BufferSize:x} (expected 0x4000)");
            }

            // TODO: Reverse the TZif2 conversion in PCV to make this match with real hardware.
            byte[] TzData = Context.Memory.ReadBytes(BufferPosition, 0x24);

            string TzID = Encoding.ASCII.GetString(TzData).TrimEnd('\0');

            long ResultCode = 0;

            // Check if the Time Zone exists, otherwise error out.
            try
            {
                TimeZoneInfo Info = TimeZoneInfo.FindSystemTimeZoneById(TzID);

                return ToPosixTimeWithTz(Context, CalendarTime, Info);
            }
            catch (TimeZoneNotFoundException)
            {
                Context.Device.Log.PrintWarning(LogClass.ServiceTime, $"Timezone not found for string: {TzID} (len: {TzID.Length})");

                ResultCode = MakeError(ErrorModule.Time, 0x3dd);
            }

            return ResultCode;
        }

        public long ToPosixTimeWithMyRule(ServiceCtx Context)
        {
            ushort Year   = Context.RequestData.ReadUInt16();
            byte   Month  = Context.RequestData.ReadByte();
            byte   Day    = Context.RequestData.ReadByte();
            byte   Hour   = Context.RequestData.ReadByte();
            byte   Minute = Context.RequestData.ReadByte();
            byte   Second = Context.RequestData.ReadByte();

            DateTime CalendarTime = new DateTime(Year, Month, Day, Hour, Minute, Second, DateTimeKind.Local);

            return ToPosixTimeWithTz(Context, CalendarTime, TimeZone);
        }

        private long ToPosixTimeWithTz(ServiceCtx Context, DateTime CalendarTime, TimeZoneInfo Info)
        {
            DateTime CalenderTimeUTC = TimeZoneInfo.ConvertTimeToUtc(CalendarTime, Info);

            long PosixTime = ((DateTimeOffset)CalenderTimeUTC).ToUnixTimeSeconds();

            long Position = Context.Request.RecvListBuff[0].Position;
            long Size     = Context.Request.RecvListBuff[0].Size;

            Context.Memory.WriteInt64(Position, PosixTime);

            Context.ResponseData.Write(1);

            return 0;
        }
    }
}
