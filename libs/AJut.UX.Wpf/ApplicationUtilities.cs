namespace AJut.UX
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Windows;
    using AJut;
    using AJut.Security;

    public static class ApplicationUtilities
    {
        // What WPF setup roots app data in when a config does not say: local, never roaming, the same as WinUI3. Before
        //  AJut 2.0 it was roaming (ApplicationData), which a config asks for to keep reading what an app stored there.
        private const Environment.SpecialFolder kDefaultStorageRoot = Environment.SpecialFolder.LocalApplicationData;

        private static bool g_isSetup = false;
        private static bool g_blockReentrancy = false;
        public static string g_sharedProjectName = null;
        public static string ProjectName { get; private set; }
        /// <summary>The folder this app may write to, kept in <see cref="AppStorage.AppDataRoot"/> and set by setup</summary>
        public static string AppDataRoot => AppStorage.AppDataRoot;

        /// <summary>
        /// Sets up your application with standard project setup mechanisms including optionally logging, exception processing, and configuration of the <see cref="AppDataRoot"/>
        /// </summary>
        /// <param name="config">The setup config, which is the same type the WinUI3 surface takes</param>
        /// <remarks>
        /// Crypto obfuscation is seeded during this call, per <see cref="ApplicationSetupConfig.CryptoSeedSource"/>. To seed
        /// with something else entirely, call <see cref="AJut.Security.CryptoObfuscation.SeedDefaults"/> yourself once setup
        /// has run - the last call wins.
        /// </remarks>
        public static void RunOnetimeSetup (ApplicationSetupConfig config)
        {
            if (g_isSetup)
            {
                return;
            }

            // Worked out before anything is established, since it is the one input that can be rejected
            string cryptoSeed = DetermineCryptoSeed(config);

            g_sharedProjectName = config.SharedProjectName;
            ProjectName = config.ProjectName;
            AppStorage.SetAppDataRoot(DetermineAppDataRoot(config));
            CryptoObfuscation.SeedDefaults(cryptoSeed);

            TypeXT.RegisterSpecialDouble<GridLength>(gl => gl.Value);
            Application.Current.Exit += _OnAppExit;

            if (config.SetupLogging)
            {
                string logsDir = DetermineLogsDirectory(AppDataRoot, ProjectName, g_sharedProjectName, config.StorageRootIsolation);
                Directory.CreateDirectory(logsDir);
                Logger.CreateAndStartWritingToLogFileIn(logsDir);
                if (config.AgeMaxInDaysToKeepLogs != -1)
                {
                    PurgeAllLogsOlderThan(TimeSpan.FromDays(config.AgeMaxInDaysToKeepLogs), logsDir);
                }
            }

            if (config.OnExceptionReceived != null)
            {
                Application.Current.DispatcherUnhandledException += _OnDispatcherUnhandledException;
            }

            g_isSetup = true;

            void _OnDispatcherUnhandledException (object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
            {
                if (g_blockReentrancy)
                {
                    return;
                }

                g_blockReentrancy = true;
                try
                {
                    // Whether this one takes the process down is up to the processor's answer, so there is nothing honest to report yet
                    if (config.OnExceptionReceived(new UnhandledExceptionReport(e.Exception, null)))
                    {
                        e.Handled = true;
                    }
                    else
                    {
                        throw e.Exception;
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
            void _OnAppExit (object sender, ExitEventArgs e)
            {
                if (Logger.IsEnabled)
                {
                    Logger.ForceFlushToFile();
                }
            }
        }

        /// <summary>
        /// Manually purge all logs that are outside of the given time span (evaluated by last write time)
        /// </summary>
        public static void PurgeAllLogsOlderThan (TimeSpan age)
        {
            // Nothing to purge if the logger never started
            string logsDir = Path.GetDirectoryName(Logger.LogFilePath);
            if (logsDir != null)
            {
                PurgeAllLogsOlderThan(age, logsDir);
            }
        }

        /// <summary>
        /// Builds a string path for something relative to this application's app data root folder (assumes it was setup via the <see cref="RunOnetimeSetup"/> function).
        /// </summary>
        public static string BuildAppDataProjectPath (params string[] pathParts)
        {
            return Path.Combine(ApplicationUtilities.AppDataRoot, Path.Combine(pathParts));
        }

        /// <summary>
        /// Works out what the <see cref="AppDataRoot"/> should be from the setup config, ensuring the folder exists either way.
        /// </summary>
        /// <remarks>
        /// Split out of <see cref="RunOnetimeSetup(ApplicationSetupConfig)"/> so the precedence and the verbatim handling can be tested without standing up an <see cref="Application"/>.
        /// </remarks>
        internal static string DetermineAppDataRoot (ApplicationSetupConfig config)
            => AppStorage.DetermineSpecialFolderRoot(config, kDefaultStorageRoot);

        /// <summary>
        /// Works out where logs go under the app data root: a folder per project whenever that root is shared, straight
        /// into Logs otherwise.
        /// </summary>
        internal static string DetermineLogsDirectory (string appDataRoot, string projectName, string sharedProjectName, eStorageRootIsolation storageRootIsolation)
            => AppStorage.DetermineLogsDirectory(appDataRoot, projectName, sharedProjectName, storageRootIsolation);

        /// <summary>
        /// Works out what to seed crypto obfuscation with, refusing to guess in the one case where guessing costs somebody their data.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a shared project name is set and the config did not say which seed source to use</exception>
        internal static string DetermineCryptoSeed (ApplicationSetupConfig config)
        {
            // WPF seeds from the project name alone while WinUI3 seeds from the shared project name when there is one,
            //  so two projects sharing a root can hold obfuscated data the other cannot read, with nothing to say so.
            //  Converging is the right end state, but it changes the key for anyone who already wrote data under a
            //  shared project name, and that break would be just as quiet as the one it fixes. So the callers it can
            //  actually hurt - the ones with a shared project name - get asked instead of guessed at. Everyone else
            //  resolves to the same string either way and never sees this.
            if (config.CryptoSeedSource == null
                && config.SharedProjectName != null)
            {
                throw new InvalidOperationException(
                    $"Setup for '{config.ProjectName}' needs {nameof(ApplicationSetupConfig)}.{nameof(ApplicationSetupConfig.CryptoSeedSource)} set, because it passed a shared project name. "
                    + $"WPF has always seeded crypto obfuscation from the project name alone, WinUI3 seeds it from the shared project name, and those are different keys - so choosing for you would either strand obfuscated data this project has already written, or leave it unreadable to a project sharing the same root. "
                    + $"Use {nameof(eCryptoSeedSource)}.{nameof(eCryptoSeedSource.ProjectNameOnly)} to keep what WPF has always done, or {nameof(eCryptoSeedSource)}.{nameof(eCryptoSeedSource.SharedProjectNameFirst)} to match WinUI3 (recommended for anything new, or anything sharing a root across the two stacks). "
                    + $"If neither is what you want, pick either one and call {nameof(CryptoObfuscation)}.{nameof(CryptoObfuscation.SeedDefaults)} yourself once setup has run."
                );
            }

            return config.DetermineCryptoSeed();
        }

        private static void PurgeAllLogsOlderThan (TimeSpan age, string logsDir)
            => AppStorage.PurgeLogsOlderThan(age, logsDir);
    }
}
