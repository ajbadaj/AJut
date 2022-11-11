namespace TheAJutShowRoom
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Windows;
    using AJut;
    using AJut.Text.AJson;
    using AJut.TypeManagement;
    using AJut.UX;
    using AJut.UX.Controls;
    using AJut.UX.Theming;

    public partial class App : Application, INotifyPropertyChanged
    {
        private const string kConfigFileName = ".ajut-showroom-config";
        public static Random kRNG = new Random(DateTime.Now.Millisecond);
        private static bool g_restrictThemeChangeNotification = true;
        private readonly ResourceDictionary?[] m_themeSaves = new ResourceDictionary?[2];
        private bool m_useThemes = true;

        // ==================[ Construction ]========================
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

            App.Navigator = new StackNavFlowController();
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

            AJutShowroomAppConfig config = GetConfig();
            ThemeTracker.ThemeConfiguration = config.Theme;
            base.OnStartup(e);

            this.UseThemes = config.UseThemes;
            ThemeTracker.PropertyChanged += _OnThemeTrackerChanged;

            g_restrictThemeChangeNotification = false;

            App.Navigator.GenerateAndPushDisplay<UI.Pages.LandingPage>();

            void _OnThemeTrackerChanged (object? sender, PropertyChangedEventArgs e)
            {
                SaveConfig();
            }
        }

        // ==================[ Events ]========================
        public event PropertyChangedEventHandler? PropertyChanged;

        // ==================[ Properties ]========================
        public static string AJut_Core_Version { get; }
        public static string AJut_Ux_Wpf_Version { get; }
        public static StackNavFlowController Navigator { get; }

        public static AppThemeManager? ThemeTracker { get; private set; }

        public bool UseThemes
        {
            get => m_useThemes;
            set 
            {
                if (m_useThemes == value)
                {
                    return;
                }

                m_useThemes = value;
                this.SetUseThemes(value);
                App.SaveConfig();
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UseThemes)));
            }
        }

        // ==================[ Private Utilities ]========================
        private static bool UnhandledExceptionProcessor (Exception e)
        {
            Logger.LogError(e);
            var result = MessageBox.Show($"Whoopsie daisies!!!\n\nException Detected: {e.Message}\n\nWould you like to mark it as handled?", "Exception caught", MessageBoxButton.YesNo);
            return result == MessageBoxResult.Yes;
        }

        private async void SetUseThemes (bool useThemes)
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

            SaveConfig();

            if (g_restrictThemeChangeNotification)
            {
                string msg = m_useThemes ? "on" : "off";
                await App.Navigator.StackTopDisplayAdapter.ShowPopover(MessageBoxPopover.Generate($"Themes are now {msg}, this may require restart to take proper effect"));
            }
        }


        private static void SaveConfig ()
        {
            string configFilePath = ApplicationUtilities.BuildAppDataProjectPath(kConfigFileName);

            Json json = JsonHelper.BuildJsonForObject(new AJutShowroomAppConfig
            {
                Theme = ThemeTracker?.ThemeConfiguration ?? eAppThemeConfiguration.UseSameAsOS,
                UseThemes = ((App)App.Current).UseThemes,
            });

            if (!json)
            {
                Logger.LogError($"Failed to build json for showroom config - errors were: {json.GetErrorReport()}");
                return;
            }

            File.Delete(configFilePath);
            File.WriteAllText(configFilePath, json.ToString());
        }

        private static AJutShowroomAppConfig GetConfig ()
        {
            var defaultConfig = new AJutShowroomAppConfig
            {
                Theme = ThemeTracker?.ThemeConfiguration ?? eAppThemeConfiguration.UseSameAsOS,
                UseThemes = true,
            };

            string configFilePath = ApplicationUtilities.BuildAppDataProjectPath(kConfigFileName);
            if (File.Exists(configFilePath))
            {
                Json configJson = JsonHelper.ParseFile(configFilePath);
                if (configJson)
                {
                    return _GetConfig(JsonHelper.BuildObjectForJson<AJutShowroomAppConfig>(configJson));
                }

                Logger.LogError($"Failed to build json for showroom config - errors were: {configJson.GetErrorReport()}");
            }

            return _GetConfig(null);

            AJutShowroomAppConfig _GetConfig (AJutShowroomAppConfig _built)
            {
                if (_built == null)
                {
                    Logger.LogInfo("Due to invalid or missing config, building new default showroom config");
                    return defaultConfig;
                }

                return _built;
            }
        }

        private class AJutShowroomAppConfig
        {
            public eAppThemeConfiguration Theme { get; set; }
            public bool UseThemes { get; set; }
        }
    }
}
