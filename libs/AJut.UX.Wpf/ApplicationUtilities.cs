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

        public static string ProjectName { get; private set; }
        public static string AppDataRoot { get; private set; }

        /// <summary>
        /// Will setup your project with logging & an exception processor
        /// </summary>
        /// <param name="projectName">The name of the project, used for context in logging and potentially elsewhere</param>
        /// <param name="setupLogging">Should logging be setup, will default to a project name specific appdata location</param>
        /// <param name="onExceptionRecieved">Something to handle unhandled exceptions</param>
        public static void RunOnetimeSetup (string projectName, bool setupLogging = true, ExceptionProcessor onExceptionRecieved = null, int ageMaxInDaysToKeepLogs = 10)
        {
            if (g_isSetup)
            {
                return;
            }

            ProjectName = projectName;
            AppDataRoot = AppDataHelper.EstablishAppDataLocation(ProjectName);
            CryptoObfuscation.Seed(projectName);

            TypeXT.RegisterSpecialDouble<GridLength>(gl => gl.Value);
            Application.Current.Exit += _OnAppExit;

            if (setupLogging)
            {
                Logger.CreateAndStartWritingToLogFileIn(AppDataHelper.EstablishAppDataLocation(ProjectName, "Logs"));
                PurgeAllLogsOlderThan(TimeSpan.FromDays(ageMaxInDaysToKeepLogs));
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
        /// Manually purge all logs that are outside of the given time span
        /// </summary>
        public static void PurgeAllLogsOlderThan (TimeSpan age)
        {
            DateTime olderThan = DateTime.Now - age;
            DirectoryInfo logsFolder = new DirectoryInfo(AppDataHelper.EstablishAppDataLocation(ProjectName, "Logs"));
            foreach (FileInfo file in logsFolder.EnumerateFiles().ToList())
            {
                if (DateTime.Now - file.CreationTime > age)
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
    }
}
