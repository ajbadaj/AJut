namespace AJut.Application
{
    using AJut;
    using AJut.Security;
    using System;
    using System.IO;
    using System.Linq;
    using System.Windows;

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
        public static void RunOnetimeSetup(string projectName, bool setupLogging = true, ExceptionProcessor onExceptionRecieved = null, int ageMaxInDaysToKeepLogs = 10)
        {
            if(g_isSetup)
            {
                return;
            }

            ProjectName = projectName;
            AppDataRoot = AppDataHelper.EstablishAppDataLocation(ProjectName);
            CryptoObfuscation.Seed(projectName);

            TypeXT.RegisterSpecialDouble<GridLength>(gl => gl.Value);

            if (setupLogging)
            {
                Logger.SetupLogFile(AppDataHelper.EstablishAppDataLocation(ProjectName, "Logs"));
                PurgeAllLogsOlderThan(TimeSpan.FromDays(ageMaxInDaysToKeepLogs));
            }

            if (onExceptionRecieved != null)
            {
#if WINDOWS_UWP
                Application.Current.UnhandledException += Current_UnhandledException;
#else
                Application.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
#endif
            }

            g_isSetup = true;

#if WINDOWS_UWP
            void Current_UnhandledException (object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
#else
            void Current_DispatcherUnhandledException (object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
#endif
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
        }

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
    }
}
