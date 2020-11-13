namespace AJut
{
    using System;
    using System.Diagnostics;
    using System.IO;

    public class Logger : IDisposable
    {
        private static Logger g_LoggerInstance = new Logger();
        
        private string m_logFilePath;
        private StreamWriter m_logFileWriter;
        private FileStream m_logFileStream;
        private bool m_shouldLogToConsole;
        private bool m_isEnabled = true;
        private bool m_flushAfterEach = false;
        private readonly object m_logWritingLock = new object();
        
        private static string kLogFilenameFormat = "log-{0:MM.dd.yyyy-hh.mm.ss}.txt";
        private static string kErrorType = "Error";
        private static string kInfoType = "Info";
        
        private static string kLogFormat = "[{0}] {1:MM.dd.yyyy-hh.mm.ss} |   ";
        private static string kErrorLogFormat = "\r\n[{0}] {1:MM.dd.yyyy-hh.mm.ss} |   ";


        #region ========== Instance Code ==========
        private Logger()
        {
            SetDebugDefaults();
        }

        [Conditional("DEBUG")]
        private void SetDebugDefaults()
        {
            m_shouldLogToConsole = true;
        }

        public static bool ShouldLogToConsole
        {
            get => g_LoggerInstance.m_shouldLogToConsole;
            set => g_LoggerInstance.m_shouldLogToConsole = value;
        }

        public static void Enable()
        {
            g_LoggerInstance.m_isEnabled = true;
        }

        public static void Disable()
        {
            ForceFlush();
            g_LoggerInstance.m_isEnabled = false;
        }

        public static void SetFlushAfterEachLog(bool flushAfterEach)
        {
            g_LoggerInstance.m_flushAfterEach = flushAfterEach;
        }

        private void Startup(string newLogFilePath = null)
        {
            if (newLogFilePath != null)
            {
                m_logFilePath = newLogFilePath;
            }

            m_logFileStream = File.OpenWrite(m_logFilePath);
            m_logFileWriter = new StreamWriter(m_logFileStream);
        }

        private void Shutdown()
        {
            if (m_logFileWriter != null)
            {
                m_logFileWriter.Dispose();
                m_logFileWriter = null;

                // The writer will close the stream, so we can nullify this too
                m_logFileStream = null;
            }

            if (m_logFileStream != null)
            {
                m_logFileStream.Dispose();
                m_logFileStream = null;
            }
        }

        public static string GrabFileTextAndReset ()
        {
            ForceFlush();
            g_LoggerInstance.Shutdown();
            string logFileText = File.ReadAllText(g_LoggerInstance.m_logFilePath);
            g_LoggerInstance.Startup();
            WriteTextUnformattedToLog(logFileText);

            return logFileText;
        }

        public void Dispose()
        {
            this.Shutdown();
        }

        #endregion

        public static string LogFilePath
        {
            get { return g_LoggerInstance.m_logFilePath; }
        }

        public static void SetupLogFile(string dir, bool logToConsoleToo = true)
        {
            if (g_LoggerInstance != null)
            {
                g_LoggerInstance.Dispose();
                g_LoggerInstance = null;
            }


            if (dir != null)
            {
                g_LoggerInstance = new Logger();

                Directory.CreateDirectory(dir);
                g_LoggerInstance.Startup(Path.Combine(dir, String.Format(kLogFilenameFormat, DateTime.Now)));
            }

            g_LoggerInstance.m_shouldLogToConsole = logToConsoleToo;
        }

        public static void ForceFlush()
        {
            lock (g_LoggerInstance.m_logWritingLock)
            {
                // Again, probably paranoid to check both and flush both, but better safe than sorry!
                if (g_LoggerInstance.m_logFileWriter != null && g_LoggerInstance.m_logFileStream != null)
                {
                    g_LoggerInstance.m_logFileWriter.Flush();
                    g_LoggerInstance.m_logFileStream.Flush(true);
                }
            }
        }

        public static void LogInfo(string message)
        {
            Log(kInfoType, true, message);
        }
        public static void LogError(string message)
        {
            Log(kErrorType, true, message);
        }

        public static void LogError(Exception exc)
        {
            Log(kErrorType, true, $"Exception Encountered: {exc}");
        }

        public static void LogError(string message, Exception exc)
        {
            Log(kErrorType, true, $"{message}\nException Encountered: {exc}");
        }

        private static void Log(string type, bool isError, string message)
        {
            if (g_LoggerInstance == null || g_LoggerInstance.m_isEnabled == false)
            {
                return;
            }

            string output = String.Format(isError ? kErrorLogFormat : kLogFormat, type, DateTime.Now) + message;

            if (g_LoggerInstance.m_shouldLogToConsole)
            {
#if WINDOWS_UWP
                Debug.WriteLine(output);
#else
                Console.WriteLine(output);
#endif
            }

            WriteTextUnformattedToLog(output);
        }

        private static void WriteTextUnformattedToLog (string text)
        {
            lock (g_LoggerInstance.m_logWritingLock)
            {
                if (g_LoggerInstance.m_logFileWriter != null)
                {
                    g_LoggerInstance.m_logFileWriter.Write(text);
                }

                if (g_LoggerInstance.m_flushAfterEach)
                {
                    ForceFlush();
                }
            }
        }

    }
}
