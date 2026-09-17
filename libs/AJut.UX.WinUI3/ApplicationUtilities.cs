namespace AJut.UX
{
    using AJut.Security;
    using Microsoft.UI.Xaml;
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;

    public delegate bool ExceptionProcessor(object exceptionObject);
    public static class ApplicationUtilities
    {
        private static bool g_isSetup = false;
        private static bool g_blockReentrancy = false;
        public static string g_sharedProjectName = null;

        public static string ProjectName { get; private set; }
        public static string AppDataRoot { get; private set; }

        /// <summary>
        /// Sets up your application with standard project setup mechanisms including optionally logging, exception processing, and configuration of the <see cref="AppDataRoot"/>
        /// </summary>
        /// <param name="projectName">The name of the project, used for context in logging and potentially elsewhere</param>
        /// <param name="setupLogging">Should logging be setup, will default to a project name specific appdata location</param>
        /// <param name="onExceptionRecieved">Something to handle unhandled exceptions</param>
        /// <param name="ageMaxInDaysToKeepLogs">The max age (in days) to keep logs - this will auto purge logs with this call for all logs older than specified. Pass in -1 to skip log purging (not recommended). Default = 10.</param>
        /// <param name="sharedProjectName">A shared project name so two or more projects can share a root location (ie CoolProj is the shared project name, but individually the projects are: CoolProjClient, CoolProjServer)</param>
        /// <param name="storageRootOverride">Override to the root of logs and your "app data" folder? This will seed the <see cref="AppDataRoot"/> location which is commonly used in establishing app storage info, including in <see cref="BuildAppDataProjectPath"/></param>
        /// <remarks>
        /// This overload seeds crypto obfuscation from <paramref name="sharedProjectName"/> when there is one and
        /// <paramref name="projectName"/> otherwise, which is what <see cref="eCryptoSeedSource.SharedProjectNameFirst"/> does
        /// and what the config overload defaults to - so conversion leaves the seed where it is.
        /// <para>
        /// Either way the seeding happens during this call, so to seed with something else entirely, call
        /// <see cref="AJut.Security.CryptoObfuscation.SeedDefaults"/> yourself once setup has run - the last call wins.
        /// </para>
        /// </remarks>
        [Obsolete("Use the ApplicationSetupConfig overload, which takes the same config type the WPF surface takes.")]
        public static void RunOnetimeSetup(string projectName, Application application, bool setupLogging = true, ExceptionProcessor onExceptionRecieved = null, int ageMaxInDaysToKeepLogs = 10, string sharedProjectName = null, string storageRootOverride = null)
        {
            RunOnetimeSetup(application, new ApplicationSetupConfig(projectName)
            {
                SharedProjectName = sharedProjectName,
                SetupLogging = setupLogging,
                AgeMaxInDaysToKeepLogs = ageMaxInDaysToKeepLogs,
                OnExceptionReceived = onExceptionRecieved == null ? null : _ForwardToProcessor,
                StorageRootOverride = storageRootOverride,
            });

            bool _ForwardToProcessor(UnhandledExceptionReport report)
            {
                return onExceptionRecieved(report.ExceptionObject);
            }
        }

        /// <summary>
        /// Sets up your application with standard project setup mechanisms including optionally logging, exception processing, and configuration of the <see cref="AppDataRoot"/>
        /// </summary>
        /// <param name="application">The application being setup</param>
        /// <param name="config">The setup config, which is the same type the WPF surface takes</param>
        /// <remarks>
        /// Crypto obfuscation is seeded during this call, per <see cref="ApplicationSetupConfig.CryptoSeedSource"/>. To seed
        /// with something else entirely, call <see cref="AJut.Security.CryptoObfuscation.SeedDefaults"/> yourself once setup
        /// has run - the last call wins.
        /// </remarks>
        public static void RunOnetimeSetup(Application application, ApplicationSetupConfig config)
        {
            if (g_isSetup)
            {
                return;
            }

            g_sharedProjectName = config.SharedProjectName;
            ProjectName = config.ProjectName;

            AppDataRoot = DetermineAppDataRoot(config);
            CryptoObfuscation.SeedDefaults(config.DetermineCryptoSeed());

            TypeXT.RegisterSpecialDouble<GridLength>(gl => gl.Value);

            // Static-event subs below are intentionally app-lifetime. The g_isSetup guard
            // prevents re-entry, and these handlers exist precisely to outlive everything
            // else so an unhandled exception or process exit can still log. No -= path is
            // appropriate here - if you find yourself wanting one, add a real shutdown
            // method instead of unhooking these piecemeal.
            if (config.OnExceptionReceived != null)
            {
                AppDomain.CurrentDomain.UnhandledException += _OnHandleException;
                application.UnhandledException += _AppOnUnhandledException;
                NativeCrashHandler.Setup();
            }


            if (config.SetupLogging)
            {
                Logger.CreateAndStartWritingToLogFileIn(EstablishLogsDirectory());
                if (config.AgeMaxInDaysToKeepLogs != -1)
                {
                    PurgeAllLogsOlderThan(TimeSpan.FromDays(config.AgeMaxInDaysToKeepLogs));
                }
            }

            AppDomain.CurrentDomain.ProcessExit += _OnAppExit;
            g_isSetup = true;

            void _OnAppExit(object? sender, EventArgs e)
            {
                if (Logger.IsEnabled)
                {
                    Logger.ForceFlushToFile();
                }
            }
            void _OnHandleException(object sender, System.UnhandledExceptionEventArgs e)
            {
                if (g_blockReentrancy)
                {
                    return;
                }

                g_blockReentrancy = true;
                try
                {
                    Logger.LogError($"Unhandled exception received: {e.ExceptionObject}");
                    if (config.OnExceptionReceived(new UnhandledExceptionReport(e.ExceptionObject, e.IsTerminating)))
                    {
                        if (e.IsTerminating)
                        {
                            Logger.LogError("Tried to handle unhandled exception - but termination is moving ahead.");
                        }
                    }
                }
                catch
                {
                }
                finally
                {
                    g_blockReentrancy = false;
                }
            }
            void _AppOnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
            {
                if (g_blockReentrancy)
                {
                    return;
                }

                g_blockReentrancy = true;
                try
                {
                    Logger.LogError($"Unhandled exception received: {e.Exception}");
                    if (config.OnExceptionReceived(new UnhandledExceptionReport(e.Exception, null)))
                    {
                        e.Handled = true;
                    }
                }
                catch
                {
                }
                finally
                {
                    g_blockReentrancy = false;
                }
            }
        }

        /// <summary>
        /// Manually purge all logs that are outside of the given time span (evaluated by last write time)
        /// </summary>
        public static void PurgeAllLogsOlderThan(TimeSpan age)
        {
            DirectoryInfo logsFolder = new DirectoryInfo(EstablishLogsDirectory());
            foreach (FileInfo file in logsFolder.EnumerateFiles().ToList())
            {
                if (DateTime.Now - file.LastWriteTime > age)
                {
                    file.Delete();
                }
            }
        }

        /// <summary>
        /// Builds a string path for something relative to this application's app data root folder (assumes it was setup via the <see cref="RunOnetimeSetup"/> function).
        /// </summary>
        public static string BuildAppDataProjectPath(params string[] pathParts)
        {
            return Path.Combine(ApplicationUtilities.AppDataRoot, Path.Combine(pathParts));
        }

        /// <summary>
        /// Works out what the <see cref="AppDataRoot"/> should be from the setup config
        /// </summary>
        private static string DetermineAppDataRoot(ApplicationSetupConfig config)
        {
            // An override is the root, exactly as given, so a process handed somebody else's storage root lands on it
            if (config.StorageRootOverride != null)
            {
                return config.StorageRootOverride;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // In packaged windows apps, this will contain the package name - so putting it again is redundant
                //  and packaging is the default for WinUI and since this is a WinUI utility that's what we will assume
                //  allowing the user to override this behavior if they want via the StorageRootOverride
                return Environment.GetFolderPath(config.ApplicationStorageRoot ?? Environment.SpecialFolder.LocalApplicationData);
            }

            // Mac/Linux
            return Path.Combine(
                Environment.GetFolderPath(config.ApplicationStorageRoot ?? Environment.SpecialFolder.ApplicationData),
                config.StorageRootProjectName
            );
        }

        private static string EstablishLogsDirectory()
        {
            string logsDir;
            if (g_sharedProjectName != null)
            {
                logsDir = Path.Combine(AppDataRoot, "Logs", ProjectName);
            }
            else
            {
                logsDir = Path.Combine(AppDataRoot, "Logs");
            }

            Directory.CreateDirectory(logsDir);
            return logsDir;
        }

        private static class NativeCrashHandler
        {
            private const uint EXCEPTION_ACCESS_VIOLATION = 0xC0000005;
            private const uint EXCEPTION_STACK_OVERFLOW = 0xC00000FD;

            [StructLayout(LayoutKind.Sequential)]
            private struct EXCEPTION_RECORD
            {
                public uint ExceptionCode;
                public uint ExceptionFlags;
                public IntPtr ExceptionRecordPtr;
                public IntPtr ExceptionAddress;
                public uint NumberParameters;
                // ExceptionInformation is variable length, we'll read first two manually
                public IntPtr ExceptionInformation0; // 0 = read, 1 = write, 8 = DEP
                public IntPtr ExceptionInformation1; // The address that was accessed
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct EXCEPTION_POINTERS
            {
                public IntPtr ExceptionRecord;
                public IntPtr ContextRecord;
            }

            [DllImport("kernel32.dll")]
            private static extern IntPtr SetUnhandledExceptionFilter(IntPtr lpTopLevelExceptionFilter);

            private delegate int UnhandledExceptionFilterDelegate(IntPtr exceptionPointersPtr);
            private static UnhandledExceptionFilterDelegate m_handler;

            public static void Setup()
            {
                m_handler = OnNativeException;
                SetUnhandledExceptionFilter(Marshal.GetFunctionPointerForDelegate(m_handler));
            }

            private static int OnNativeException(IntPtr exceptionPointersPtr)
            {
                try
                {
                    var message = DecodeExceptionInfo(exceptionPointersPtr);
                    Logger.LogError($"The app will crash due to an unhandled (and likely unhandle-able) lower level crash. Partially decoded crash details:\n{message}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Native crash (failed to decode: {ex.Message})");
                }

                return 0; // EXCEPTION_CONTINUE_SEARCH
            }

            private static string DecodeExceptionInfo(IntPtr exceptionPointersPtr)
            {
                if (exceptionPointersPtr == IntPtr.Zero)
                {
                    return "Native crash (null exception pointers)";
                }

                var pointers = Marshal.PtrToStructure<EXCEPTION_POINTERS>(exceptionPointersPtr);
                if (pointers.ExceptionRecord == IntPtr.Zero)
                {
                    return "Native crash (null exception record)";
                }

                var record = Marshal.PtrToStructure<EXCEPTION_RECORD>(pointers.ExceptionRecord);

                string exceptionType = record.ExceptionCode switch
                {
                    EXCEPTION_ACCESS_VIOLATION => "ACCESS_VIOLATION",
                    EXCEPTION_STACK_OVERFLOW => "STACK_OVERFLOW",
                    _ => $"0x{record.ExceptionCode:X8}"
                };

                string accessDetails = "";
                if (record.ExceptionCode == EXCEPTION_ACCESS_VIOLATION && record.NumberParameters >= 2)
                {
                    string accessType = record.ExceptionInformation0.ToInt64() switch
                    {
                        0 => "reading from",
                        1 => "writing to",
                        8 => "DEP violation at",
                        _ => "accessing"
                    };
                    accessDetails = $" ({accessType} address 0x{record.ExceptionInformation1:X})";
                }

                return $"Native crash: {exceptionType}{accessDetails} at 0x{record.ExceptionAddress:X}";
            }
        }
    }
}
