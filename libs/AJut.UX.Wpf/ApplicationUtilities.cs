﻿namespace AJut.UX
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
        public static void RunOnetimeSetup (string projectName, bool setupLogging = true, ExceptionProcessor onExceptionRecieved = null, int ageMaxInDaysToKeepLogs = 10, string sharedProjectName = null, Environment.SpecialFolder applicationStorageRoot = Environment.SpecialFolder.ApplicationData)
        {
            if (g_isSetup)
            {
                return;
            }

            g_sharedProjectName = sharedProjectName;
            ProjectName = projectName;
            AppDataRoot = WindowsEnvironmentHelper.EstablishSpecialFolderLocation(applicationStorageRoot, sharedProjectName ?? projectName);
            CryptoObfuscation.SeedDefaults(projectName);

            TypeXT.RegisterSpecialDouble<GridLength>(gl => gl.Value);
            Application.Current.Exit += _OnAppExit;

            if (setupLogging)
            {
                Logger.CreateAndStartWritingToLogFileIn(EstablishLogsDirectory());
                if (ageMaxInDaysToKeepLogs != -1)
                {
                    PurgeAllLogsOlderThan(TimeSpan.FromDays(ageMaxInDaysToKeepLogs));
                }
            }

            if (onExceptionRecieved != null)
            {
                Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
            }

            g_isSetup = true;

            void Current_DispatcherUnhandledException (object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
            {
                if (g_blockReentrancy)
                {
                    return;
                }

                g_blockReentrancy = true;
                try
                {
                    if (onExceptionRecieved(e.Exception))
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
