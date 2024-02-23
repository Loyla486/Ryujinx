using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Ryujinx.Common.Logging
{
    public static class Logger
    {
        private static Stopwatch m_Time;

        private static readonly bool[] m_EnabledLevels;
        private static readonly bool[] m_EnabledClasses;

        public static event EventHandler<LogEventArgs> Updated;

        public static bool EnableFileLog { get; set; }

        static Logger()
        {
            m_EnabledLevels  = new bool[Enum.GetNames(typeof(LogLevel)).Length];
            m_EnabledClasses = new bool[Enum.GetNames(typeof(LogClass)).Length];

            m_EnabledLevels[(int)LogLevel.Stub]    = true;
            m_EnabledLevels[(int)LogLevel.Info]    = true;
            m_EnabledLevels[(int)LogLevel.Warning] = true;
            m_EnabledLevels[(int)LogLevel.Error]   = true;

            for (int index = 0; index < m_EnabledClasses.Length; index++)
            {
                m_EnabledClasses[index] = true;
            }

            m_Time = Stopwatch.StartNew();
        }

        public static void SetEnable(LogLevel logLevel, bool enabled)
        {
            m_EnabledLevels[(int)logLevel] = enabled;
        }

        public static void SetEnable(LogClass logClass, bool enabled)
        {
            m_EnabledClasses[(int)logClass] = enabled;
        }

        public static void PrintDebug(LogClass logClass, string message, [CallerMemberName] string caller = "")
        {
            Print(LogLevel.Debug, logClass, GetFormattedMessage(logClass, message, caller));
        }

        public static void PrintInfo(LogClass logClass, string message, [CallerMemberName] string Caller = "")
        {
            Print(LogLevel.Info, logClass, GetFormattedMessage(logClass, message, Caller));
        }

        public static void PrintWarning(LogClass logClass, string message, [CallerMemberName] string Caller = "")
        {
            Print(LogLevel.Warning, logClass, GetFormattedMessage(logClass, message, Caller));
        }

        public static void PrintError(LogClass logClass, string message, [CallerMemberName] string Caller = "")
        {
            Print(LogLevel.Error, logClass, GetFormattedMessage(logClass, message, Caller));
        }

        public static void PrintStub(LogClass logClass, string message = "", [CallerMemberName] string caller = "")
        {
            Print(LogLevel.Stub, logClass, GetFormattedMessage(logClass, "Stubbed. " + message, caller));
        }

        public static void PrintStub<T>(LogClass logClass, T obj, [CallerMemberName] string caller = "")
        {
            Print(LogLevel.Stub, logClass, GetFormattedMessage(logClass, "Stubbed.", caller), obj);
        }

        public static void PrintStub<T>(LogClass logClass, string message, T obj, [CallerMemberName] string caller = "")
        {
            Print(LogLevel.Stub, logClass, GetFormattedMessage(logClass, "Stubbed. " + message, caller), obj);
        }

        private static void Print(LogLevel logLevel, LogClass logClass, string message)
        {
            if (m_EnabledLevels[(int)logLevel] && m_EnabledClasses[(int)logClass])
            {
                Updated?.Invoke(null, new LogEventArgs(logLevel, m_Time.Elapsed, Thread.CurrentThread.ManagedThreadId, message));
            }
        }

        private static void Print(LogLevel logLevel, LogClass logClass, string message, object data)
        {
            if (m_EnabledLevels[(int)logLevel] && m_EnabledClasses[(int)logClass])
            {
                Updated?.Invoke(null, new LogEventArgs(logLevel, m_Time.Elapsed, Thread.CurrentThread.ManagedThreadId, message, data));
            }
        }

        private static string GetFormattedMessage(LogClass Class, string Message, string Caller)
        {
            return $"{Class} {Caller}: {Message}";
        }
    }
}