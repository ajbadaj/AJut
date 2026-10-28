namespace AJut.UX
{
    using System;
    using System.IO;
    using AJut.Security;
    using Android.Content;
    using Android.Runtime;

    /// <summary>
    /// One-time app setup for Android, the same as the WPF and WinUI3 packages' ApplicationUtilities take the same
    /// <see cref="ApplicationSetupConfig"/>: the app data root, crypto obfuscation's seed, unhandled exception
    /// handling, and logging.
    /// </summary>
    public static class ApplicationUtilities
    {
        private static bool g_isSetup;
        private static bool g_blockReentrancy;
        private static ApplicationExceptionProcessor? g_onExceptionReceived;

        // ===========[ Properties ]===================================
        /// <summary>The project name setup was given, used for context in logging; null until setup runs</summary>
        public static string? ProjectName { get; private set; }

        /// <summary>The folder this app may write to, kept in <see cref="AppStorage.AppDataRoot"/> and set by setup</summary>
        public static string? AppDataRoot => AppStorage.AppDataRoot;

        // ===========[ Public Interface Methods ]===================================
        /// <summary>
        /// Sets up the app: the <see cref="AppDataRoot"/> (the app's private files folder, see
        /// <see cref="DetermineAppDataRoot"/>), crypto obfuscation's seed, unhandled exception handling when the config
        /// has a handler, and logging into a Logs folder under the root when the config asks for it. Runs once; later
        /// calls do nothing, so it is safe to call from an activity's OnCreate, which runs again on every recreate.
        /// </summary>
        /// <param name="context">Any context of the app; its application context is the one kept</param>
        /// <param name="config">The setup config, the same type the WPF and WinUI3 setup take</param>
        /// <remarks>
        /// The config's ApplicationStorageRoot and StorageRootIsolation are Windows special folder settings, and are not
        /// used here: an Android app's files folder is its own already.
        /// </remarks>
        public static void RunOnetimeSetup (Context context, ApplicationSetupConfig config)
        {
            if (g_isSetup)
            {
                return;
            }

            ProjectName = config.ProjectName;
            AppStorage.SetAppDataRoot(DetermineAppDataRoot(context, config));
            CryptoObfuscation.SeedDefaults(config.DetermineCryptoSeed());

            // These subscriptions are intentionally app-lifetime: the g_isSetup guard prevents a second set, and they
            // exist to outlive everything else so an unhandled exception can still be logged and handled.
            if (config.OnExceptionReceived != null)
            {
                g_onExceptionReceived = config.OnExceptionReceived;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                AndroidEnvironment.UnhandledExceptionRaiser += OnAndroidUnhandledException;
            }

            if (config.SetupLogging)
            {
                string logsDirectory = AppStorage.DetermineLogsDirectory(AppStorage.AppDataRoot!, config.ProjectName, config.SharedProjectName, config.StorageRootIsolation);
                Directory.CreateDirectory(logsDirectory);
                Logger.CreateAndStartWritingToLogFileIn(logsDirectory);
                if (config.AgeMaxInDaysToKeepLogs != -1)
                {
                    AppStorage.PurgeLogsOlderThan(TimeSpan.FromDays(config.AgeMaxInDaysToKeepLogs), logsDirectory);
                }
            }

            g_isSetup = true;
        }

        /// <summary>
        /// Works out what <see cref="AppDataRoot"/> should be, ensuring the folder exists: the config's override exactly
        /// as given when it has one, otherwise the app's private files folder (Context.FilesDir), which only this app can
        /// read or write and which lives as long as the app is installed.
        /// </summary>
        public static string DetermineAppDataRoot (Context context, ApplicationSetupConfig config)
        {
            // An override is the root as given, the same as on WPF and WinUI3, so it agrees with whoever handed it over
            string root = config.StorageRootOverride ?? context.ApplicationContext!.FilesDir!.AbsolutePath;
            Directory.CreateDirectory(root);
            return root;
        }

        /// <summary>A path under <see cref="AppDataRoot"/>, from the given parts (setup must have run)</summary>
        public static string BuildAppDataProjectPath (params string[] pathParts) => AppStorage.BuildAppDataPath(pathParts);

        // ===========[ Helper Methods ]===================================
        private static void OnUnhandledException (object? sender, UnhandledExceptionEventArgs e)
        {
            if (g_blockReentrancy || (g_onExceptionReceived == null))
            {
                return;
            }

            g_blockReentrancy = true;
            try
            {
                Logger.LogError($"Unhandled exception received: {e.ExceptionObject}");
                g_onExceptionReceived(new UnhandledExceptionReport(e.ExceptionObject, e.IsTerminating));
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

        // Android's own hook sees an exception before the runtime tears the app down, and can stop that by marking it
        // handled, which is what a true from the handler asks for
        private static void OnAndroidUnhandledException (object? sender, RaiseThrowableEventArgs e)
        {
            if (g_blockReentrancy || (g_onExceptionReceived == null))
            {
                return;
            }

            g_blockReentrancy = true;
            try
            {
                Logger.LogError($"Unhandled exception received: {e.Exception}");
                if (g_onExceptionReceived(new UnhandledExceptionReport(e.Exception, null)))
                {
                    e.Handled = true;
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
    }
}
