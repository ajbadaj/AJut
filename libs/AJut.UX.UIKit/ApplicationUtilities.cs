namespace AJut.UX
{
    using System;
    using System.IO;
    using AJut.Security;
    using Foundation;

    // ===========[ ApplicationUtilities ]=======================================
    // Standard setup for a UIKit app, taking the same ApplicationSetupConfig the WPF, WinUI3, Android
    // and AppKit setups take: the app data root (AppStorage), crypto obfuscation's seed, logging, and
    // unhandled exception reporting.
    //
    // The app data root is the app container's Library/Application Support, where an iOS app keeps
    // what it writes for itself: it belongs to this app alone and is backed up, unlike Caches or tmp.
    // The container is already the app's own, so no app id is added under it, as on Android. iOS does
    // not create the folder for a new app, so setup makes it. A config's StorageRootOverride is the
    // root exactly as given, the same as on every stack. Its ApplicationStorageRoot and
    // StorageRootIsolation are Windows special folder settings, and are not used here.
    //
    // Unhandled exceptions are reported from two places, the same as on AppKit.
    // AppDomain.UnhandledException sees an exception that unwinds a managed thread. An exception
    // thrown from a UIKit callback or event handler instead leaves managed code through a native
    // frame, and the runtime raises Runtime.MarshalManagedException for it before converting it;
    // AppDomain never sees that one, so it is reported there too. Its marshaling mode is left as the
    // runtime chose, so the handler's answer can't stop it: the report is what this adds.
    public static class ApplicationUtilities
    {
        private static bool g_isSetup;
        private static bool g_blockReentrancy;
        private static ApplicationExceptionProcessor g_onExceptionReceived;

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

            // These static event subscriptions are deliberately for the life of the app: they exist to outlive everything
            // else, so an unhandled exception or the process exiting can still log. The g_isSetup guard keeps them from
            // ever being added twice.
            if (config.OnExceptionReceived != null)
            {
                g_onExceptionReceived = config.OnExceptionReceived;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                ObjCRuntime.Runtime.MarshalManagedException += OnMarshalManagedException;
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

            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            g_isSetup = true;
        }

        /// <summary>
        /// Works out what <see cref="AppDataRoot"/> should be, making the folder: the config's override exactly as given when
        /// it has one, otherwise the app container's Library/Application Support.
        /// </summary>
        public static string DetermineAppDataRoot (ApplicationSetupConfig config)
        {
            if (config.StorageRootOverride != null)
            {
                Directory.CreateDirectory(config.StorageRootOverride);
                return config.StorageRootOverride;
            }

            NSUrl[] supportFolders = NSFileManager.DefaultManager.GetUrls(NSSearchPathDirectory.ApplicationSupportDirectory, NSSearchPathDomain.User);
            string root = supportFolders.Length > 0
                ? supportFolders[0].Path
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support");

            Directory.CreateDirectory(root);
            return root;
        }

        /// <summary>Builds a path under this app's <see cref="AppDataRoot"/> (set by <see cref="RunOnetimeSetup"/>)</summary>
        public static string BuildAppDataProjectPath (params string[] pathParts) => AppStorage.BuildAppDataPath(pathParts);

        // ===========[ Helper Methods ]========================================
        private static void OnProcessExit (object sender, EventArgs e)
        {
            if (Logger.IsEnabled)
            {
                Logger.ForceFlushToFile();
            }
        }

        private static void OnUnhandledException (object sender, UnhandledExceptionEventArgs e)
        {
            if (g_blockReentrancy || (g_onExceptionReceived == null))
            {
                return;
            }

            g_blockReentrancy = true;
            try
            {
                Logger.LogError($"Unhandled exception received: {e.ExceptionObject}");
                if (g_onExceptionReceived(new UnhandledExceptionReport(e.ExceptionObject, e.IsTerminating)) && e.IsTerminating)
                {
                    Logger.LogError("Tried to handle unhandled exception - but termination is moving ahead.");
                }
            }
            catch (Exception handlerFailure)
            {
                Logger.LogError("The unhandled exception handler threw", handlerFailure);
            }
            finally
            {
                g_blockReentrancy = false;
            }
        }

        private static void OnMarshalManagedException (object sender, ObjCRuntime.MarshalManagedExceptionEventArgs e)
        {
            if (g_blockReentrancy || (g_onExceptionReceived == null))
            {
                return;
            }

            g_blockReentrancy = true;
            try
            {
                Logger.LogError($"Unhandled exception received, leaving managed code through a native frame: {e.Exception}");
                g_onExceptionReceived(new UnhandledExceptionReport(e.Exception, null));
            }
            catch (Exception handlerFailure)
            {
                Logger.LogError("The unhandled exception handler threw", handlerFailure);
            }
            finally
            {
                g_blockReentrancy = false;
            }
        }
    }
}
