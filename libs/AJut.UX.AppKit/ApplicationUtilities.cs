namespace AJut.UX
{
    using System;
    using System.IO;
    using AJut.IO;
    using AJut.Security;
    using Foundation;

    // ===========[ ApplicationUtilities ]=======================================
    // Standard setup for an AppKit app, taking the same ApplicationSetupConfig the WPF and WinUI3
    // setups take: the app data root (AppStorage), crypto obfuscation's seed, logging, and
    // unhandled exception reporting.
    //
    // The app data root is the macOS one, ~/Library/Application Support/<app id>, where a Mac app
    // keeps what it writes for itself (a sandboxed app's Application Support is inside its
    // container, so the same code lands in the right place there too). The app id is the config's
    // SharedProjectName when it has one, so projects that share a root on Windows share it here too,
    // otherwise the app's bundle identifier, and the project name for an executable with no bundle.
    // A config's StorageRootOverride is the root exactly as given, the same as on every stack. Its
    // ApplicationStorageRoot and StorageRootIsolation are Windows special folder settings, and are
    // not used here, as on Android.
    //
    // Unhandled exceptions are reported from two places. AppDomain.UnhandledException sees an
    // exception that unwinds a managed thread. An exception thrown from an AppKit callback or event
    // handler instead leaves managed code through a native frame, and the runtime raises
    // Runtime.MarshalManagedException for it before converting it; AppDomain never sees that one, so
    // it is reported there too. Its marshaling mode is left as the runtime chose, so the handler's
    // answer can't stop it: the report is what this adds.
    public static class ApplicationUtilities
    {
        private static bool g_isSetup;
        private static bool g_blockReentrancy;

        // ===========[ Properties ]============================================
        /// <summary>The project name setup was run with</summary>
        public static string ProjectName { get; private set; }

        /// <summary>The shared project name setup was run with, or null</summary>
        public static string SharedProjectName { get; private set; }

        /// <summary>The folder this app may write to, kept in <see cref="AppStorage.AppDataRoot"/> and set by setup</summary>
        public static string AppDataRoot => AppStorage.AppDataRoot;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Sets up the app: the <see cref="AppDataRoot"/>, crypto obfuscation's seed, logging (when the config asks for it),
        /// and unhandled exception reporting (when the config gives a processor). Runs once; later calls do nothing.
        /// </summary>
        /// <remarks>
        /// Crypto obfuscation is seeded during this call, per <see cref="ApplicationSetupConfig.CryptoSeedSource"/>. To seed
        /// with something else entirely, call <see cref="CryptoObfuscation.SeedDefaults"/> yourself once setup has run; the
        /// last call wins.
        /// </remarks>
        public static void RunOnetimeSetup (ApplicationSetupConfig config)
        {
            if (g_isSetup)
            {
                return;
            }

            ProjectName = config.ProjectName;
            SharedProjectName = config.SharedProjectName;

            AppStorage.SetAppDataRoot(DetermineAppDataRoot(config));
            CryptoObfuscation.SeedDefaults(config.DetermineCryptoSeed());

            // These static event subscriptions are deliberately for the life of the app: they exist to
            // outlive everything else, so an unhandled exception or the process exiting can still log.
            // The g_isSetup guard keeps them from ever being added twice.
            if (config.OnExceptionReceived != null)
            {
                AppDomain.CurrentDomain.UnhandledException += _OnUnhandledException;
                ObjCRuntime.Runtime.MarshalManagedException += _OnMarshalManagedException;
            }

            if (config.SetupLogging)
            {
                string logsDir = AppStorage.DetermineLogsDirectory(AppDataRoot, ProjectName, SharedProjectName, config.StorageRootIsolation);
                Directory.CreateDirectory(logsDir);
                Logger.CreateAndStartWritingToLogFileIn(logsDir);
                if (config.AgeMaxInDaysToKeepLogs != -1)
                {
                    AppStorage.PurgeLogsOlderThan(TimeSpan.FromDays(config.AgeMaxInDaysToKeepLogs), logsDir);
                }
            }

            AppDomain.CurrentDomain.ProcessExit += _OnProcessExit;
            g_isSetup = true;

            void _OnProcessExit (object sender, EventArgs e)
            {
                if (Logger.IsEnabled)
                {
                    Logger.ForceFlushToFile();
                }
            }

            void _OnUnhandledException (object sender, UnhandledExceptionEventArgs e)
            {
                if (g_blockReentrancy)
                {
                    return;
                }

                g_blockReentrancy = true;
                try
                {
                    Logger.LogError($"Unhandled exception received: {e.ExceptionObject}");
                    if (config.OnExceptionReceived(new UnhandledExceptionReport(e.ExceptionObject, e.IsTerminating)) && e.IsTerminating)
                    {
                        Logger.LogError("Tried to handle unhandled exception - but termination is moving ahead.");
                    }
                }
                catch
                {
                    // Reporting an exception must never be the thing that throws
                }
                finally
                {
                    g_blockReentrancy = false;
                }
            }

            void _OnMarshalManagedException (object sender, ObjCRuntime.MarshalManagedExceptionEventArgs e)
            {
                if (g_blockReentrancy)
                {
                    return;
                }

                g_blockReentrancy = true;
                try
                {
                    Logger.LogError($"Unhandled exception received, leaving managed code through a native frame: {e.Exception}");
                    config.OnExceptionReceived(new UnhandledExceptionReport(e.Exception, null));
                }
                catch
                {
                    // Reporting an exception must never be the thing that throws
                }
                finally
                {
                    g_blockReentrancy = false;
                }
            }
        }

        /// <summary>Builds a path under this app's <see cref="AppDataRoot"/> (set by <see cref="RunOnetimeSetup"/>)</summary>
        public static string BuildAppDataProjectPath (params string[] pathParts) => AppStorage.BuildAppDataPath(pathParts);

        /// <summary>Deletes every log older than <paramref name="age"/> (by last write time), when logging has started</summary>
        public static void PurgeAllLogsOlderThan (TimeSpan age)
        {
            string logsDir = Path.GetDirectoryName(Logger.LogFilePath);
            if (logsDir != null)
            {
                AppStorage.PurgeLogsOlderThan(age, logsDir);
            }
        }

        // ===========[ Helper Methods ]========================================
        // The config's override exactly as given, otherwise the macOS Application Support folder with the app's id
        // appended. The folder is made either way.
        internal static string DetermineAppDataRoot (ApplicationSetupConfig config)
        {
            if (config.StorageRootOverride != null)
            {
                Directory.CreateDirectory(config.StorageRootOverride);
                return config.StorageRootOverride;
            }

            NSUrl[] supportFolders = NSFileManager.DefaultManager.GetUrls(NSSearchPathDirectory.ApplicationSupportDirectory, NSSearchPathDomain.User);
            string supportFolder = supportFolders.Length > 0
                ? supportFolders[0].Path
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support");

            string appId = config.SharedProjectName ?? NSBundle.MainBundle.BundleIdentifier ?? config.ProjectName;
            string root = Path.Combine(supportFolder, PathHelpers.SanitizeFileName(appId));
            Directory.CreateDirectory(root);
            return root;
        }
    }
}
