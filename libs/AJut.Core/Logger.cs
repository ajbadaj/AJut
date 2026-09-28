namespace AJut
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Threading;

    /// <summary>
    /// Transmits logged information from any thread to any combination of a log file, the <see cref="Console"/>, the debug <see cref="Trace"/>, and an attached debugger.
    /// </summary>
    public class Logger : IDisposable
    {
        private const string kDefaultLogFilenameFormat = "log-{0:MM.dd.yyyy-hh.mm.ss}.txt";
        private const string kDefaultDateTimeFormat = "MM.dd.yyy-hh.mm.ss";
        private const long kDefaultLogFileSplitSizeBytes = 5L * 1024L * 1024L;
        private const string kSlowWriteReportFormat = "[WARNING] [Logger] Slow log write: {0:F1}ms on thread {1} '{2}' ({3:F1}ms waiting for the write lock, {4:F1}ms writing)";

        // One instance for the life of the process - retargeting the file never replaces it, so settings made before a
        //  log file is started (verbosity, scenarios, formats, output switches) carry through
        private static readonly Logger g_LoggerInstance = new Logger();
        private static readonly LogType kInfoType = new InfoLogType();
        private static readonly LogType kErrorType = new ErrorLogType();
        private static volatile Action<string> g_singleOverrideLogTarget;

        private string m_logFilePath;
        private string m_logFileBasePath;
        private string m_logFileExtension;
        private int m_currentSplitIndex;
        private StreamWriter m_logFileWriter;
        private FileStream m_logFileStream;

        private readonly LogVerbosityManager m_verbosityManager = new LogVerbosityManager();

        private volatile bool m_shouldLogToConsole;
        private volatile bool m_shouldLogToTrace;
        private volatile bool m_shouldLogToDebug;
        private volatile bool m_shouldLogToAttachedDebugger;
        private volatile bool m_isEnabled = true;
        private volatile eLogFlushMode m_flushMode = eLogFlushMode.FlushToDisk;
        private volatile string m_dateTimeFormat = kDefaultDateTimeFormat;
        private long m_slowWriteThresholdTicks;

        private readonly object m_logWritingLock = new object();

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
            //m_shouldLogToDebug = true;
        }

        /// <summary>
        /// The format of the log filename (given a <see cref="DateTime"/>). NOTE: Unlike -ALL- other properties, this will not change an actively running log file session, so set this before <see cref="CreateAndStartWritingToLogFileIn"/> is called.
        /// </summary>
        /// <remarks>
        /// Unlike -ALL- other properties, this will not change an actively running log file session, so set this before <see cref="CreateAndStartWritingToLogFileIn"/> is called.
        /// </remarks>
        public static string LogFilenameFormat { get; set; } = kDefaultLogFilenameFormat;

        /// <summary>
        /// The maximum file size in bytes before the logger splits to a new file. Set to 0 to disable splitting. Default is 5 MB.
        /// </summary>
        public static long LogFileSplitSizeBytes { get; set; } = kDefaultLogFileSplitSizeBytes;

        /// <summary>
        /// Sets a single override log target that supersedes all other outputs (console, trace, debug, file).
        /// When set, the formatted output string is passed to this action and no other target receives it.
        /// Pass null to clear the override and restore normal output routing.
        /// </summary>
        public static void SetSingleOverrideLogTarget (Action<string> target)
        {
            g_singleOverrideLogTarget = target;
        }

        /// <summary>
        /// The verbosity manager that controls what gets logged and supports dynamic scenario-based verbosity raising.
        /// </summary>
        public static LogVerbosityManager VerbosityManager => g_LoggerInstance.m_verbosityManager;

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

        public static bool ShouldLogToDebug
        {
            get => g_LoggerInstance.m_shouldLogToDebug;
            set => g_LoggerInstance.m_shouldLogToDebug = value;
        }

        /// <summary>
        /// Indicates if every line should also go to an attached debugger (the Visual Studio Output window, for instance) - but
        /// only while one is attached, so with no debugger it costs one check a line. Unlike <see cref="ShouldLogToTrace"/>, this
        /// goes straight to the debugger and skips trace listeners entirely. Default is false.
        /// </summary>
        public static bool ShouldLogToAttachedDebugger
        {
            get => g_LoggerInstance.m_shouldLogToAttachedDebugger;
            set => g_LoggerInstance.m_shouldLogToAttachedDebugger = value;
        }

        /// <summary>
        /// How far each line is pushed before the log call returns - see <see cref="eLogFlushMode"/>. Default is
        /// <see cref="eLogFlushMode.FlushToDisk"/>, the safest and the slowest.
        /// </summary>
        /// <remarks>
        /// <see cref="eLogFlushMode.FlushToOS"/> is what most apps want: every line still survives the process crashing, without
        /// each one (and every other thread logging behind it) waiting on the drive.
        /// </remarks>
        public static eLogFlushMode FlushMode
        {
            get => g_LoggerInstance.m_flushMode;
            set => g_LoggerInstance.m_flushMode = value;
        }

        /// <summary>
        /// Indicates if the logger flushes after each line. Setting true means <see cref="eLogFlushMode.FlushToDisk"/>, and false
        /// means <see cref="eLogFlushMode.Buffered"/> (leaving it up to manual calls to <see cref="ForceFlushToFile"/>).
        /// </summary>
        [Obsolete("Use FlushMode, which can also hand each line to the OS without waiting on the disk.")]
        public static bool FlushToFileAfterEach
        {
            get => FlushMode != eLogFlushMode.Buffered;
            set => FlushMode = value ? eLogFlushMode.FlushToDisk : eLogFlushMode.Buffered;
        }

        /// <summary>
        /// When getting a line into the log file takes at least this long - counting both the wait for the write lock and the
        /// write itself - the logger adds a line of its own saying so, at <see cref="eLogVerbosity.Force"/>, with the time split
        /// between the two and the thread it happened on. <see cref="TimeSpan.Zero"/> (the default) turns this off.
        /// </summary>
        /// <remarks>
        /// A thread that spent its time waiting on the lock was stuck behind somebody else's write, which is otherwise invisible
        /// in the log. The report line itself is never timed.
        /// </remarks>
        public static TimeSpan SlowWriteThreshold
        {
            get => TimeSpan.FromTicks(Interlocked.Read(ref g_LoggerInstance.m_slowWriteThresholdTicks));
            set => Interlocked.Exchange(ref g_LoggerInstance.m_slowWriteThresholdTicks, Math.Max(0L, value.Ticks));
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
                m_logFileExtension = Path.GetExtension(newLogFilePath);
                m_logFileBasePath = newLogFilePath.Substring(0, newLogFilePath.Length - m_logFileExtension.Length);
                m_currentSplitIndex = 0;
                m_logFilePath = newLogFilePath;
            }
            else if (m_currentSplitIndex > 0)
            {
                // Split files use base + dash + index (index 0 = original file, no suffix)
                m_logFilePath = $"{m_logFileBasePath}-{m_currentSplitIndex}{m_logFileExtension}";
            }
            // else: m_currentSplitIndex == 0, m_logFilePath is already the original path

            // Append, so reopening a file that already has lines in it carries on after them rather than writing over them
            m_logFileStream = File.Open(m_logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
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
        /// Reads back everything written to the current log file so far. The file stays open throughout, and lines logged
        /// afterward carry on appending to it.
        /// </summary>
        public static string ReadCurrentLogFromDisk ()
        {
            Logger logger = g_LoggerInstance;
            string logFilePath;
            lock (logger.m_logWritingLock)
            {
                // Handing what is buffered to the OS is all a reader needs, reads come through the same OS cache
                logger.m_logFileWriter?.Flush();
                logFilePath = logger.m_logFilePath;
            }

            using (var stream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
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
        /// Sets up the log file for writing, closing whatever log file was open before. Every other setting on the logger
        /// (verbosity and scenarios, formats, flush mode, output switches) carries over untouched.
        /// </summary>
        /// <param name="directoryPath">Path to the directory under which we should create a new log file, or null to close the current file and stop writing to disk</param>
        public static void CreateAndStartWritingToLogFileIn (string directoryPath)
        {
            Logger logger = g_LoggerInstance;
            lock (logger.m_logWritingLock)
            {
                logger.TearDownLogFileStream();
                logger.m_logFilePath = null;

                if (directoryPath != null)
                {
                    Directory.CreateDirectory(directoryPath);
                    string logFileName = AJut.IO.PathHelpers.SanitizeFileName(String.Format(LogFilenameFormat, DateTime.Now));
                    logger.BuildAndSetupLogFileStream(Path.Combine(directoryPath, logFileName));
                }
            }
        }

        /// <summary>
        /// Forces flushing all pending log statements all the way to the physical disk
        /// </summary>
        /// <remarks>
        /// NOTE: Lines already reach the OS after each call unless <see cref="FlushMode"/> is <see cref="eLogFlushMode.Buffered"/>,
        /// which is enough to survive the process dying - this is for surviving the machine going down too
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
        /// Indicates whether an info line at the given verbosity would be logged right now, so a hot path can skip building a
        /// message that would only be thrown away.
        /// </summary>
        /// <remarks>
        /// This only reads <see cref="LogVerbosityManager.EffectiveVerbosity"/>, it never runs scenarios - so don't guard a line
        /// that is itself meant to set off a scenario, since skipping it means the scenario never sees it. Past that it errs on
        /// the side of yes: it can say yes to a line a scenario's <see cref="LogVerbosityScenario.AppliesTo"/> then turns away, but
        /// never no to a line that would have been logged. Errors ignore verbosity, so there is nothing to ask here for those.
        /// </remarks>
        public static bool WouldLog (eLogVerbosity verbosity)
        {
            if (!g_LoggerInstance.m_isEnabled)
            {
                return false;
            }

            if (verbosity == eLogVerbosity.Force)
            {
                return true;
            }

            eLogVerbositySetting effective = g_LoggerInstance.m_verbosityManager.EffectiveVerbosity;
            return effective != eLogVerbositySetting.None && (int)verbosity <= (int)effective;
        }

        /// <summary>
        /// Log information
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="verbosity">The minimum manager verbosity required for this message to appear. Defaults to Normal.</param>
        /// <remarks>
        /// The <see cref="Logger"/> only differentiates between error, and not error - this is to log something that is not an error.
        /// </remarks>
        public static void LogInfo (string message, eLogVerbosity verbosity = eLogVerbosity.Normal)
            => DoLog(kInfoType, message, verbosity);

        /// <summary>
        /// Log information - but only if the target compilation is Debug.
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="verbosity">The minimum manager verbosity required for this message to appear. Defaults to Normal.</param>
        /// <remarks>
        /// The <see cref="Logger"/> only differentiates between error, and not error - this is to log something that is not an error.
        /// </remarks>
        [Conditional("DEBUG")]
        public static void LogDebugInfo (string message, eLogVerbosity verbosity = eLogVerbosity.Normal)
            => DoLog(kInfoType, message, verbosity);

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

        /// <summary>
        /// Puts the logger back the way a fresh process has it: no log file, no override target, every setting at its default,
        /// and no scenarios. The logger is one static instance, so this is how tests keep from leaking settings into each other.
        /// </summary>
        internal static void ResetToDefaults ()
        {
            CreateAndStartWritingToLogFileIn(null);
            g_singleOverrideLogTarget = null;
            LogFilenameFormat = kDefaultLogFilenameFormat;
            LogFileSplitSizeBytes = kDefaultLogFileSplitSizeBytes;

            Logger logger = g_LoggerInstance;
            logger.m_shouldLogToConsole = false;
            logger.m_shouldLogToTrace = false;
            logger.m_shouldLogToDebug = false;
            logger.m_shouldLogToAttachedDebugger = false;
            logger.SetDebugDefaults();
            logger.m_isEnabled = true;
            logger.m_flushMode = eLogFlushMode.FlushToDisk;
            logger.m_dateTimeFormat = kDefaultDateTimeFormat;
            Interlocked.Exchange(ref logger.m_slowWriteThresholdTicks, 0L);
            logger.m_verbosityManager.ResetToDefaults();
        }

        private static void DoLog (LogType logType, string message, eLogVerbosity verbosity = eLogVerbosity.Normal)
        {
            Logger logger = g_LoggerInstance;
            if (!logger.m_isEnabled)
            {
                return;
            }

            // Scenarios evaluate BEFORE the gate check so a message that would be suppressed can still trigger a scenario which
            //  lets that same message through. The level comes back from this line's own pass through the scenarios, so another
            //  thread switching a scenario off in the meantime can't drop the line that switched it on.
            eLogVerbositySetting admitLevel = logger.m_verbosityManager.ProcessLogLine(message, logType.IsError);

            // Force verbosity bypasses all filtering (None gate and verbosity level gate).
            if (verbosity != eLogVerbosity.Force)
            {
                if (admitLevel == eLogVerbositySetting.None)
                {
                    return;
                }
                if (!logType.IsError && (int)verbosity > (int)admitLevel)
                {
                    return;
                }
            }

            logger.Emit(logType, message, isSlowWriteReport: false);
        }

        private void Emit (LogType logType, string message, bool isSlowWriteReport)
        {
            Action<string> overrideTarget = g_singleOverrideLogTarget;
            if (overrideTarget != null)
            {
                overrideTarget(logType.GenerateOutputText(message));
                return;
            }

            bool isTimingWrite = !isSlowWriteReport && Interlocked.Read(ref m_slowWriteThresholdTicks) > 0;
            long lockRequestedAt = isTimingWrite ? Stopwatch.GetTimestamp() : 0;
            long lockAcquiredAt = 0;
            string output;
            lock (m_logWritingLock)
            {
                if (isTimingWrite)
                {
                    lockAcquiredAt = Stopwatch.GetTimestamp();
                }

                // Stamped once the lock is held, so the order lines land in the file is the order of their timestamps
                output = logType.GenerateOutputText(message);
                this.WriteToLogFile(output);
            }

            long writtenAt = isTimingWrite ? Stopwatch.GetTimestamp() : 0;

            this.MirrorOutput(logType, output);

            if (isTimingWrite)
            {
                this.ReportIfWriteWasSlow(lockRequestedAt, lockAcquiredAt, writtenAt);
            }
        }

        /// <summary>
        /// Writes one formatted line to the log file, flushes it as far as <see cref="FlushMode"/> says, and splits to a new
        /// file once this one is big enough. Only call with <see cref="m_logWritingLock"/> held.
        /// </summary>
        private void WriteToLogFile (string text)
        {
            if (m_logFileWriter == null)
            {
                return;
            }

            m_logFileWriter.Write(text);
            switch (m_flushMode)
            {
                case eLogFlushMode.FlushToOS:
                    // StreamWriter.Flush pushes through the FileStream into a completed OS write, which the process dying can't undo
                    m_logFileWriter.Flush();
                    break;

                case eLogFlushMode.FlushToDisk:
                    m_logFileWriter.Flush();
                    m_logFileStream.Flush(true);
                    break;
            }

            if (LogFileSplitSizeBytes > 0 && m_logFileStream.Position >= LogFileSplitSizeBytes)
            {
                ++m_currentSplitIndex;
                this.TearDownLogFileStream();
                this.BuildAndSetupLogFileStream();
            }
        }

        private void MirrorOutput (LogType logType, string output)
        {
            if (m_shouldLogToConsole)
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

            if (m_shouldLogToTrace)
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

            if (m_shouldLogToDebug)
            {
                Debug.WriteLine(output);
            }

            if (m_shouldLogToAttachedDebugger && Debugger.IsAttached)
            {
                Debugger.Log(0, null, output);
            }
        }

        private void ReportIfWriteWasSlow (long lockRequestedAt, long lockAcquiredAt, long writtenAt)
        {
            TimeSpan total = Stopwatch.GetElapsedTime(lockRequestedAt, writtenAt);
            if (total.Ticks < Interlocked.Read(ref m_slowWriteThresholdTicks))
            {
                return;
            }

            Thread thread = Thread.CurrentThread;
            string report = String.Format(
                CultureInfo.InvariantCulture,
                kSlowWriteReportFormat,
                total.TotalMilliseconds,
                thread.ManagedThreadId,
                thread.Name ?? "unnamed",
                Stopwatch.GetElapsedTime(lockRequestedAt, lockAcquiredAt).TotalMilliseconds,
                Stopwatch.GetElapsedTime(lockAcquiredAt, writtenAt).TotalMilliseconds
            );

            // Straight to emit, as a Force line would be, without timing it (so a slow disk can't set off a chain of reports)
            this.Emit(kInfoType, report, isSlowWriteReport: true);
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
