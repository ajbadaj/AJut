namespace TheAJutShowRoom
{
    using System;
    using System.Diagnostics;
    using System.Windows;
    using AJut;
    using AJut.TypeManagement;
    using AJut.UX;
    using AJut.UX.Theming;

    public partial class App : Application
    {
        public static Random kRNG = new Random(DateTime.Now.Millisecond);

        static App ()
        {
            var assembly = typeof(Logger).Assembly;
            var versionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            AJut_Core_Version = versionInfo.ProductVersion?.ToString() ?? "unknown version";
            Logger.LogInfo($"Using AJut.Core version #{AJut_Core_Version}");

            assembly = typeof(ApplicationUtilities).Assembly;
            versionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            AJut_Ux_Wpf_Version = versionInfo.ProductVersion?.ToString() ?? "unknown version";
            Logger.LogInfo($"Using AJut.UX.Wpf version #{AJut_Ux_Wpf_Version}");
        }
        public App ()
        {
            // Go through all types and find type id registrations, this will allow automatic discovery and propogation of type matching
            TypeIdRegistrar.RegisterAllTypeIds(typeof(App).Assembly);

            // Run a one time setup which will establish an appdata location, project name, logging, seed obfuscation, and optionally apply root exception handling
            ApplicationUtilities.RunOnetimeSetup("AJut.ShowRoom", onExceptionRecieved: UnhandledExceptionProcessor);

            // Add an entry to the log so we know we got this far!
            Logger.LogInfo("Starting up AJut Show Room");
        }

        protected override void OnStartup (StartupEventArgs e)
        {
            ThemeTracker = new AppThemeManager();
            ThemeTracker.ThemeConfiguration = eAppThemeConfiguration.UseSameAsOS;
            base.OnStartup(e);
        }

        public static AppThemeManager? ThemeTracker { get; private set; }

        private static bool UnhandledExceptionProcessor (Exception e)
        {
            Logger.LogError(e);
            var result = MessageBox.Show($"Whoopsie daisies!!!\n\nException Detected: {e.Message}\n\nWould you like to mark it as handled?", "Exception caught", MessageBoxButton.YesNo);
            return result == MessageBoxResult.Yes;
        }

        public static string AppDataPath(string pathEnd)
        {
            return System.IO.Path.Combine(ApplicationUtilities.AppDataRoot, pathEnd);
        }

        private readonly ResourceDictionary?[] m_themeSaves = new ResourceDictionary?[2];

        public static void SetUseThemes (bool useThemes)
        {
            ((App)App.Current).DoSetUseThemes(useThemes);
        }

        private void DoSetUseThemes (bool useThemes)
        {
            if (useThemes)
            {
                this.Resources.MergedDictionaries.Insert(0, m_themeSaves[0]);
                this.Resources.MergedDictionaries.Insert(1, m_themeSaves[1]);

                m_themeSaves[0] = null;
                m_themeSaves[1] = null;
            }
            else
            {
                m_themeSaves[0] = this.Resources.MergedDictionaries[0];
                m_themeSaves[1] = this.Resources.MergedDictionaries[1];
                this.Resources.MergedDictionaries.Remove(m_themeSaves[0]);
                this.Resources.MergedDictionaries.Remove(m_themeSaves[1]);
            }
        }

        public static string AJut_Core_Version { get; }
        public static string AJut_Ux_Wpf_Version { get; }
    }
}
