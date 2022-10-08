namespace AJut
{
    using System;
    using System.Diagnostics;
    using System.IO;

    /// <summary>
    /// Transmits logged infomration from any thread to any combination of a log file, the <see cref="Console"/>, the debug <see cref="Trace"/>.
    /// </summary>
    public class Logger : IDisposable
    {
        private static Logger g_LoggerInstance = new Logger();
        private static readonly LogType kInfoType = new InfoLogType();
        private static readonly LogType kErrorType = new ErrorLogType();

        private string m_logFilePath;
        private StreamWriter m_logFileWriter;
        private FileStream m_logFileStream;

        private volatile bool m_shouldLogToConsole;
        private volatile bool m_shouldLogToTrace;
        private volatile bool m_isEnabled = true;
        private volatile bool m_flushToFileAfterEach = true;
        private volatile string m_dateTimeFormat = "MM.dd.yyy-hh.mm.ss";

        private readonly object m_logWritingLock = new object();

        private static string kLogFilenameFormat = "log-{0:MM.dd.yyyy-hh.mm.ss}.txt";


        #region ========== Instance Code ==========
        private Logger ()
        {
            SetDebugDefaults();
        }

        /// <summary>
        /// Set the debug defaults (only called in Debug)
        /// </summary>
        [Conditional("DEBUG")]
        private void SetDebugDefaults ()
        {
            m_shouldLogToConsole = true;
            m_shouldLogToTrace = true;
        }

        /// <summary>
        /// Indicates if calls to <see cref="Logger"/> should additionally direct to <see cref="Console"/> (default to true in debug, false otherwise)
        /// </summary>
        public static bool ShouldLogToConsole
        {
            get => g_LoggerInstance.m_shouldLogToConsole;
            set => g_LoggerInstance.m_shouldLogToConsole = value;
        }

        /// <summary>
        /// Indicates if calls to <see cref="Logger"/> should additionally direct to <see cref="Trace"/> (default to true in debug, false otherwise)
        /// </summary>
        public static bool ShouldLogToTrace
        {
            get => g_LoggerInstance.m_shouldLogToTrace;
            set => g_LoggerInstance.m_shouldLogToTrace = value;
        }

        /// <summary>
        /// Indicates if the logger should force flush to file after each call (true) or leave it up manual calls to <see cref="ForceFlushToFile"/> (false). Default is true.
        /// </summary>
        public static bool FlushToFileAfterEach
        {
            get => g_LoggerInstance.m_flushToFileAfterEach;
            set => g_LoggerInstance.m_flushToFileAfterEach = value;
        }

        /// <summary>
        /// The format used when adding in date time to log statements
        /// </summary>
        public static string DateTimeFormat
        {
            get => g_LoggerInstance.m_dateTimeFormat;
            set => g_LoggerInstance.m_dateTimeFormat = value;
        }

        /// <summary>
        /// Indicates if the logger is currently enabled (if false, log info/error calls to <see cref="Logger"/> do nothing).
        /// </summary>
        public static bool IsEnabled => g_LoggerInstance.m_isEnabled;

        /// <summary>
        /// Enables logging
        /// </summary>
        public static void Enable ()
        {
            g_LoggerInstance.m_isEnabled = true;
        }

        /// <summary>
        /// Disables logging
        /// </summary>
        public static void Disable ()
        {
            ForceFlushToFile();
            g_LoggerInstance.m_isEnabled = false;
        }

        private void BuildAndSetupLogFileStream (string newLogFilePath = null)
        {
            if (newLogFilePath != null)
            {
                m_logFilePath = newLogFilePath;
            }

            m_logFileStream = File.Open(m_logFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            m_logFileWriter = new StreamWriter(m_logFileStream);
        }

        private void TearDownLogFileStream ()
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

        /// <summary>
        /// Tears down the file locks and reads log file stream, sets the log file stream back up, and returns the text
        /// </summary>
        public static string ReadCurrentLogFromDisk ()
        {
            ForceFlushToFile();
            g_LoggerInstance.TearDownLogFileStream();
            string logFileText = File.ReadAllText(g_LoggerInstance.m_logFilePath);
            g_LoggerInstance.BuildAndSetupLogFileStream();

            return logFileText;
        }

        /// <summary>
        /// Disposes of the logger instance
        /// </summary>
        public void Dispose ()
        {
            this.TearDownLogFileStream();
        }

        #endregion

        /// <summary>
        /// The file path that the logger is currently writing to
        /// </summary>
        public static string LogFilePath => g_LoggerInstance.m_logFilePath;

        /// <summary>
        /// Sets up the log file for writing
        /// </summary>
        /// <param name="directoryPath">Path to the directory under which we should create a new log file</param>
        public static void CreateAndStartWritingToLogFileIn (string directoryPath)
        {
            g_LoggerInstance.TearDownLogFileStream();
            if (directoryPath != null)
            {
                g_LoggerInstance = new Logger();

                Directory.CreateDirectory(directoryPath);
                g_LoggerInstance.BuildAndSetupLogFileStream(Path.Combine(directoryPath, String.Format(kLogFilenameFormat, DateTime.Now)));
            }
        }

        /// <summary>
        /// Forces flushing all pending log statements to the log file
        /// </summary>
        /// <remarks>
        /// NOTE: This will happen automatically if you have set <see cref="FlushToFileAfterEach"/>
        /// </remarks>
        public static void ForceFlushToFile ()
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

        /// <summary>
        /// Log information
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <remarks>
        /// The <see cref="Logger"/> only differentiates between error, and not error - this is to log something that is not an error.
        /// </remarks>
        public static void LogInfo (string message)
        {
            DoLog(kInfoType, message);
        }

        /// <summary>
        /// Log information - but only if the target compilation is Debug.
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <remarks>
        /// The <see cref="Logger"/> only differentiates between error, and not error - this is to log something that is not an error.
        /// </remarks>
        [Conditional("DEBUG")]
        public static void LogDebugInfo (string message)
        {
            DoLog(kInfoType, message);
        }

        /// <summary>
        /// Log error
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <remarks>
        /// The <see cref="Logger"/> only differentiates between error, and not error - this is to log something that *is* an error.
        /// </remarks>
        public static void LogError (string message)
        {
            DoLog(kErrorType, message);
        }

        /// <summary>
        /// Log an <see cref="Exception"/> as, using it's message as the log error text
        /// </summary>
        /// <param name="exc">The exception to log</param>
        /// <remarks>
        /// The <see cref="Logger"/> only differentiates between error, and not error - this is to log something that *is* an error.
        /// </remarks>
        public static void LogError (Exception exc)
        {
            DoLog(kErrorType, $"Exception Encountered: {exc}");
        }

        /// <summary>
        /// Log an error using a message, and text from an <see cref="Exception"/>
        /// </summary>
        /// <param name="message">The error message to log</param>
        /// <param name="exc">The exception to log</param>
        /// <remarks>
        /// The <see cref="Logger"/> only differentiates between error, and not error - this is to log something that *is* an error.
        /// </remarks>
        public static void LogError (string message, Exception exc)
        {
            DoLog(kErrorType, $"{message}\nException Encountered: {exc}");
        }

        private static void DoLog (LogType logType, string message)
        {
            if (g_LoggerInstance == null || g_LoggerInstance.m_isEnabled == false)
            {
                return;
            }

            string output = logType.GenerateOutputText(message);

            if (ShouldLogToConsole)
            {
                if (logType.IsError)
                {
                    Console.Error.WriteLine(output);
                }
                else
                {
                    Console.Out.WriteLine(output);
                }
            }

            if (ShouldLogToTrace)
            {
                if (logType.IsError)
                {
                    Trace.TraceError(output);
                }
                else
                {
                    Trace.WriteLine(output);
                }
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

                if (g_LoggerInstance.m_flushToFileAfterEach)
                {
                    ForceFlushToFile();
                }
            }
        }


        private abstract class LogType 
        {
            public virtual bool IsError { get; } = false;
            public abstract string GenerateOutputText (string message);
        };

        private class InfoLogType : LogType
        {
            public override string GenerateOutputText (string message) => $"\r\n[Info] {DateTime.Now.ToString(Logger.DateTimeFormat)} |   {message}";
        }

        private class ErrorLogType : LogType
        {
            public override bool IsError { get; } = true;
            public override string GenerateOutputText (string message) => $"\r\n[Error] {DateTime.Now.ToString(Logger.DateTimeFormat)} |   {message}";
        }
    }
}
