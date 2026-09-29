namespace AJut.UX
{
    using AJut.OS.Windows;
    using AJut.Security;
    using Microsoft.UI.Xaml;
    using System;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;

    public delegate bool ExceptionProcessor(object exceptionObject);
    public static class ApplicationUtilities
    {
        // What WinUI3 setup roots app data in when a config does not say: local app data on Windows, roaming elsewhere
        private static readonly Environment.SpecialFolder kDefaultStorageRoot = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Environment.SpecialFolder.LocalApplicationData
            : Environment.SpecialFolder.ApplicationData;

        private static bool g_isSetup = false;
        private static bool g_blockReentrancy = false;
        public static string g_sharedProjectName = null;
        private static eStorageRootIsolation g_storageRootIsolation = eStorageRootIsolation.ProjectFolder;

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
            g_storageRootIsolation = config.StorageRootIsolation;
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

                // Keeps any native crash filter the host set up before this, and runs it first on a crash - see Setup for why
                NativeCrashHandler.Setup();
            }


            if (config.SetupLogging)
            {
                string logsDir = EstablishLogsDirectory();
                Logger.CreateAndStartWritingToLogFileIn(logsDir);
                if (config.AgeMaxInDaysToKeepLogs != -1)
                {
                    PurgeAllLogsOlderThan(TimeSpan.FromDays(config.AgeMaxInDaysToKeepLogs), logsDir);
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
        public static void PurgeAllLogsOlderThan(TimeSpan age) => PurgeAllLogsOlderThan(age, EstablishLogsDirectory());

        /// <summary>
        /// Builds a string path for something relative to this application's app data root folder (assumes it was setup via the <see cref="RunOnetimeSetup"/> function).
        /// </summary>
        public static string BuildAppDataProjectPath(params string[] pathParts)
        {
            return Path.Combine(ApplicationUtilities.AppDataRoot, Path.Combine(pathParts));
        }

        /// <summary>
        /// Works out what the <see cref="AppDataRoot"/> should be from the setup config, ensuring the folder exists either way.
        /// </summary>
        /// <remarks>
        /// Split out of <see cref="RunOnetimeSetup(Application, ApplicationSetupConfig)"/> so the precedence and the verbatim handling can be tested without standing up an <see cref="Application"/>.
        /// </remarks>
        internal static string DetermineAppDataRoot(ApplicationSetupConfig config)
        {
            // An override is the root, exactly as given. Appending the project name the way the special folder path
            //  below does would defeat the entire point of setting one - you would end up in a subfolder of the root
            //  you were handed, and quietly disagree with whoever handed it to you.
            if (config.StorageRootOverride != null)
            {
                Directory.CreateDirectory(config.StorageRootOverride);
                return config.StorageRootOverride;
            }

            Environment.SpecialFolder specialFolder = config.ApplicationStorageRoot ?? kDefaultStorageRoot;

            // Packaging alone does not isolate the special folder. GetFolderPath hands a packaged app the plain shared
            //  folder, and only MSIX write virtualization makes it app specific, so the project folder goes on unless the
            //  config says virtualization is on. Nothing detects that - the config states it.
            if (config.StorageRootIsolation == eStorageRootIsolation.PackageVirtualization)
            {
                return WindowsEnvironmentHelper.EstablishSpecialFolderLocation(specialFolder);
            }

            return WindowsEnvironmentHelper.EstablishSpecialFolderLocation(specialFolder, config.StorageRootProjectName);
        }

        /// <summary>
        /// Works out where logs go under the app data root: a folder per project whenever that root is shared, straight
        /// into Logs otherwise.
        /// </summary>
        internal static string DetermineLogsDirectory(string appDataRoot, string projectName, string sharedProjectName, eStorageRootIsolation storageRootIsolation)
        {
            // Projects sharing a root each get their own logs folder. A virtualized package root is shared by everything
            //  in the package (and by every app on the machine if the setting is wrong), so it gets one too.
            if ((sharedProjectName != null)
                || (storageRootIsolation == eStorageRootIsolation.PackageVirtualization))
            {
                return Path.Combine(appDataRoot, "Logs", projectName);
            }

            return Path.Combine(appDataRoot, "Logs");
        }

        private static string EstablishLogsDirectory()
        {
            string logsDir = DetermineLogsDirectory(AppDataRoot, ProjectName, g_sharedProjectName, g_storageRootIsolation);
            Directory.CreateDirectory(logsDir);
            return logsDir;
        }

        private static void PurgeAllLogsOlderThan(TimeSpan age, string logsDir)
        {
            DirectoryInfo logsFolder = new DirectoryInfo(logsDir);
            foreach (FileInfo file in logsFolder.EnumerateFiles().ToList())
            {
                if (DateTime.Now - file.LastWriteTime > age)
                {
                    file.Delete();
                }
            }
        }

        private static class NativeCrashHandler
        {
            private const uint EXCEPTION_ACCESS_VIOLATION = 0xC0000005;
            private const uint EXCEPTION_STACK_OVERFLOW = 0xC00000FD;
            private const int EXCEPTION_CONTINUE_SEARCH = 0;
            private const int EXCEPTION_CONTINUE_EXECUTION = -1;

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
            private static UnhandledExceptionFilterDelegate g_handler;
            private static IntPtr g_previousFilter;

            public static void Setup()
            {
                g_handler = OnNativeException;

                // A process only gets one of these filters - setting one replaces whatever was there, and hands back what it
                //  replaced. So a filter the host set up before this (a crash reporter writing a dump, say) only ever runs if
                //  this one calls it, and it gets called FIRST: after a native crash the heap may be corrupt, and the host's
                //  filter should get its chance before this does anything that allocates. Anything that sets a filter AFTER
                //  this replaces it, and has to chain to it the same way or this never runs.
                g_previousFilter = SetUnhandledExceptionFilter(Marshal.GetFunctionPointerForDelegate(g_handler));
            }

            private static int OnNativeException(IntPtr exceptionPointersPtr)
            {
                int result = g_previousFilter == IntPtr.Zero ? EXCEPTION_CONTINUE_SEARCH : CallFilter(g_previousFilter, exceptionPointersPtr);
                if (result == EXCEPTION_CONTINUE_EXECUTION)
                {
                    // The earlier filter dealt with it and the app carries on, so there is no crash to report
                    return result;
                }

                try
                {
                    var message = DecodeExceptionInfo(exceptionPointersPtr);
                    Logger.LogError($"The app will crash due to an unhandled (and likely unhandle-able) lower level crash. Partially decoded crash details:\n{message}");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Native crash (failed to decode: {ex.Message})");
                }

                return result;
            }

            /// <summary>
            /// Calls a filter through its raw function pointer. Wrapping the pointer in a delegate breaks when the filter was
            /// itself made from a managed delegate, as a C# host's would be: the runtime hands back that original delegate, its
            /// type isn't ours, and the cast throws. The raw call works whatever made the filter, and allocates nothing.
            /// </summary>
            private static unsafe int CallFilter(IntPtr filter, IntPtr exceptionPointersPtr)
                => ((delegate* unmanaged[Stdcall]<IntPtr, int>)filter)(exceptionPointersPtr);

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
