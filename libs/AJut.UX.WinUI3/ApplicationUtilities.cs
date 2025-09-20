namespace AJut.UX
{
    using AJut.Security;
    using Microsoft.UI.Xaml;
    using System;
    using System.IO;
    using System.Linq;
    using Windows.Storage;

    public delegate bool ExceptionProcessor (object exceptionObject);
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
        /// <param name="applicationStorageRoot">What <see cref="Environment.SpecialFolder"/> do you want to keep things like logs in? This will seed the <see cref="AppDataRoot"/> location which is commonly used in establishing app storage info, including in <see cref="BuildAppDataProjectPath"/></param>
        public static void RunOnetimeSetup (string projectName, Application application, bool setupLogging = true, ExceptionProcessor onExceptionRecieved = null, int ageMaxInDaysToKeepLogs = 10, string sharedProjectName = null, StorageFolder storageRoot = null)
        {
            if (g_isSetup)
            {
                return;
            }

            g_sharedProjectName = sharedProjectName;
            ProjectName = projectName;
            AppDataRoot = (storageRoot ?? ApplicationData.Current.LocalFolder).Path;
            CryptoObfuscation.SeedDefaults(g_sharedProjectName);

            TypeXT.RegisterSpecialDouble<GridLength>(gl => gl.Value);
            
            if (onExceptionRecieved != null)
            {
                AppDomain.CurrentDomain.UnhandledException += _OnHandleException;
                application.UnhandledException += _AppOnUnhandledException;
            }


            if (setupLogging)
            {
                Logger.CreateAndStartWritingToLogFileIn(EstablishLogsDirectory());
                if (ageMaxInDaysToKeepLogs != -1)
                {
                    PurgeAllLogsOlderThan(TimeSpan.FromDays(ageMaxInDaysToKeepLogs));
                }
            }

            AppDomain.CurrentDomain.ProcessExit += _OnAppExit;
            g_isSetup = true;

            void _OnAppExit (object? sender, EventArgs e)
            {
                if (Logger.IsEnabled)
                {
                    Logger.ForceFlushToFile();
                }
            }
            void _OnHandleException (object sender, System.UnhandledExceptionEventArgs e)
            {
                if (g_blockReentrancy)
                {
                    return;
                }

                g_blockReentrancy = true;
                try
                {
                    Logger.LogError($"Unhandled excpetion recieved: {e.ExceptionObject}");
                    if (onExceptionRecieved(e.ExceptionObject))
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
            void _AppOnUnhandledException (object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
            {
                if (g_blockReentrancy)
                {
                    return;
                }

                g_blockReentrancy = true;
                try
                {
                    Logger.LogError($"Unhandled excpetion recieved: {e.Exception}");
                    if (onExceptionRecieved(e.Exception))
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
