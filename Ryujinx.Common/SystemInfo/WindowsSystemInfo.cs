using Ryujinx.Common.Logging;
using System;
using System.Management;

namespace Ryujinx.Common.SystemInfo
{
    internal class WindowsSysteminfo : SystemInfo
    {
        public override string CpuName { get; }
        public override ulong RamSize { get; }

        public WindowsSysteminfo()
        {
            try
            {
                foreach (ManagementBaseObject mObject in new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor").Get())
                {
                    CpuName = mObject["Name"].ToString();
                }

                foreach (ManagementBaseObject mObject in new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_OperatingSystem").Get())
                {
                    RamSize = ulong.Parse(mObject["TotalVisibleMemorySize"].ToString()) * 1024;
                }
            }
            catch (Exception)
            {
                Logger.PrintError(LogClass.Application, "WMI isn't available, system informations will use default values.");

                CpuName = "Unknown";
            }
        }
    }
}