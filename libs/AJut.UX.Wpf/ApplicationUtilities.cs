namespace AJut.UX
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Windows;
    using AJut;
    using AJut.OS.Windows;
    using AJut.Security;

    public delegate bool ExceptionProcessor (Exception e);
    public static class ApplicationUtilities
    {
        // What WPF setup has always rooted app data in, used when a config does not say
        private const Environment.SpecialFolder kDefaultStorageRoot = Environment.SpecialFolder.ApplicationData;

        private static bool g_isSetup = false;
        private static bool g_blockReentrancy = false;
        public static string g_sharedProjectName = null;

        public static string ProjectName { get; private set; }
        public static string AppDataRoot { get; private set; }

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
            AppDataRoot = DetermineAppDataRoot(config);
            CryptoObfuscation.SeedDefaults(cryptoSeed);

            TypeXT.RegisterSpecialDouble<GridLength>(gl => gl.Value);
            Application.Current.Exit += _OnAppExit;

            if (config.SetupLogging)
            {
                Logger.CreateAndStartWritingToLogFileIn(EstablishLogsDirectory());
                if (config.AgeMaxInDaysToKeepLogs != -1)
                {
                    PurgeAllLogsOlderThan(TimeSpan.FromDays(config.AgeMaxInDaysToKeepLogs));
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
        /// Sets up your application with standard project setup mechanisms including optionally logging, exception processing, and configuration of the <see cref="AppDataRoot"/>
        /// </summary>
        /// <param name="projectName">The name of the project, used for context in logging and potentially elsewhere</param>
        /// <param name="setupLogging">Should logging be setup, will default to a project name specific appdata location</param>
        /// <param name="onExceptionRecieved">Something to handle unhandled exceptions</param>
        /// <param name="ageMaxInDaysToKeepLogs">The max age (in days) to keep logs - this will auto purge logs with this call for all logs older than specified. Pass in -1 to skip log purging (not recommended). Default = 10.</param>
        /// <param name="sharedProjectName">A shared project name so two or more projects can share a root location (ie CoolProj is the shared project name, but individually the projects are: CoolProjClient, CoolProjServer)</param>
        /// <param name="applicationStorageRoot">What <see cref="Environment.SpecialFolder"/> do you want to keep things like logs in? This will seed the <see cref="AppDataRoot"/> location which is commonly used in establishing app storage info, including in <see cref="BuildAppDataProjectPath"/>. Ignored entirely if a <paramref name="storageRootOverride"/> is passed in.</param>
        /// <param name="storageRootOverride">The exact folder to use as the <see cref="AppDataRoot"/>. This wins over <paramref name="applicationStorageRoot"/>, and unlike that parameter it is taken verbatim - no project name is appended to it. That is deliberate: the reason to pass this is to land on a storage root somebody else already picked (say a packaged parent app whose child process you are), and appending a project name would put you next to that root instead of on it.</param>
        /// <remarks>
        /// What this overload does with crypto obfuscation, since it is the part that does not survive a careless conversion:
        /// it seeds from <paramref name="projectName"/> alone, ignoring <paramref name="sharedProjectName"/> entirely. To keep
        /// that exactly as it is, convert to the config overload with <see cref="ApplicationSetupConfig.CryptoSeedSource"/> set
        /// to <see cref="eCryptoSeedSource.ProjectNameOnly"/>.
        /// <para>
        /// Either way the seeding happens during this call, so to seed with something else entirely, call
        /// <see cref="AJut.Security.CryptoObfuscation.SeedDefaults"/> yourself once setup has run - the last call wins.
        /// </para>
        /// </remarks>
        [Obsolete("Use the ApplicationSetupConfig overload, which takes the same config type the WinUI3 surface takes. Mind the crypto obfuscation seed on the way across: this overload seeds from projectName alone and ignores sharedProjectName, so to keep that exactly, set ApplicationSetupConfig.CryptoSeedSource to eCryptoSeedSource.ProjectNameOnly.")]
        public static void RunOnetimeSetup (string projectName, bool setupLogging = true, ExceptionProcessor onExceptionRecieved = null, int ageMaxInDaysToKeepLogs = 10, string sharedProjectName = null, Environment.SpecialFolder applicationStorageRoot = Environment.SpecialFolder.ApplicationData, string storageRootOverride = null)
        {
            RunOnetimeSetup(new ApplicationSetupConfig(projectName)
            {
                SharedProjectName = sharedProjectName,
                SetupLogging = setupLogging,
                AgeMaxInDaysToKeepLogs = ageMaxInDaysToKeepLogs,
                OnExceptionReceived = onExceptionRecieved == null ? null : _ForwardToProcessor,
                ApplicationStorageRoot = applicationStorageRoot,
                StorageRootOverride = storageRootOverride,

                // This overload has always seeded from the project name alone, shared project name or not, and anything
                //  calling it may well have obfuscated data written under that key. Moving it would leave that data
                //  unreadable without a word, so the seed stays put and the config overload is where the choice lives.
                CryptoSeedSource = eCryptoSeedSource.ProjectNameOnly,
            });

            bool _ForwardToProcessor (UnhandledExceptionReport report)
            {
                return onExceptionRecieved(report.Exception);
            }
        }

        /// <summary>
        /// Manually purge all logs that are outside of the given time span (evaluated by last write time)
        /// </summary>
        public static void PurgeAllLogsOlderThan (TimeSpan age)
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
        {
            // An override is the root, exactly as given. Appending the project name the way the special folder path
            //  below does would defeat the entire point of setting one - you would end up in a subfolder of the root
            //  you were handed, and quietly disagree with whoever handed it to you.
            if (config.StorageRootOverride != null)
            {
                Directory.CreateDirectory(config.StorageRootOverride);
                return config.StorageRootOverride;
            }

            return WindowsEnvironmentHelper.EstablishSpecialFolderLocation(config.ApplicationStorageRoot ?? kDefaultStorageRoot, config.StorageRootProjectName);
        }

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

        private static string EstablishLogsDirectory ()
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
    }
}
