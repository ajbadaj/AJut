namespace AJut.UX.Theming
{
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Xaml;
    using Microsoft.Win32;
    using System;
    using Windows.Storage;
    using Windows.UI.ViewManagement;

    public partial class AppThemeManager : NotifyPropertyChanged, IDisposable
    {
        private const string kPersistentAppSettingsKey = "AppThemeManager::UserSelectedTheme";
        private const string kRegistryThemeRootKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string kRegistryIsLightThemeValue = "AppsUseLightTheme";

        private Application m_targetApplication;
        private ElementTheme m_themeConfiguration = ElementTheme.Default;
        private ApplicationTheme m_currentTheme = ApplicationTheme.Dark;
        private UISettings m_uiSettings;
        private WindowManager m_windows;

        public event EventHandler<EventArgs<ApplicationTheme>> ThemeChanged;
        private ApplicationDataContainer? m_persistedSettings;

        public void Setup(Application target, WindowManager windows, bool persistUserThemeSelection = true)
        {
            m_windows = windows;
            m_targetApplication = target ?? Application.Current;

            // Set the initial theme
            if (persistUserThemeSelection)
            {
                m_persistedSettings = ApplicationData.Current.LocalSettings;
                if (m_persistedSettings.Values.TryGetValue(kPersistentAppSettingsKey, out object value) 
                    && value is string themeConfigStr 
                    && Enum.TryParse(themeConfigStr, false, out ElementTheme themeConfig))
                {
                    m_themeConfiguration = themeConfig;
                    this.ForceThemeChange(themeConfig);
                }
                else
                {
                    m_themeConfiguration = ElementTheme.Default;
                    this.ForceThemeChange(ElementTheme.Default);
                }
            }
            else
            {
                SetTheme(m_targetApplication.RequestedTheme);
            }

            // Begin watching for color changes
            m_uiSettings = new UISettings();
            m_uiSettings.ColorValuesChanged += OnColorValuesChanged;
        }

        void IDisposable.Dispose ()
        {
            GC.SuppressFinalize(this);
            if (m_uiSettings != null)
            {
                m_uiSettings.ColorValuesChanged -= OnColorValuesChanged;
                m_uiSettings = null;
            }
        }

        public ElementTheme[] ThemeConfigurationOptions { get; } = Enum.GetValues<ElementTheme>();

        public ElementTheme ThemeConfiguration
        {
            get => m_themeConfiguration;
            set
            {
                if (this.SetAndRaiseIfChanged(ref m_themeConfiguration, value))
                {
                    this.ApplyUserSelectedTheme(value);
                }
            }
        }

        public void ForceThemeChange(ElementTheme theme)
        {
            m_themeConfiguration = theme;
            this.RaisePropertiesChanged(nameof(ThemeConfiguration));
            this.ApplyUserSelectedTheme(theme, onlyIfNew: false);
        }

        private void ApplyUserSelectedTheme(ElementTheme theme, bool onlyIfNew = true)
        {
            if (m_persistedSettings != null)
            {
                m_persistedSettings.Values[kPersistentAppSettingsKey] = m_themeConfiguration.ToString();
            }

            if (theme == ElementTheme.Default)
            {
                SetTheme(GetSystemTheme(), onlyIfNew);
            }
            else
            {
                SetTheme(theme == ElementTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light, onlyIfNew);
            }
        }

        public ApplicationTheme CurrentTheme => m_currentTheme;

        private void SetTheme (ApplicationTheme theme, bool onlyIfNew = true)
        {
            if (onlyIfNew && m_currentTheme == theme)
            {
                return;
            }

            // Update the WinUI defaults for each content root
            foreach (Window window in m_windows)
            {
                this.ApplyTheme(window, theme);
            }

            m_currentTheme = theme;
            this.ThemeChanged?.Invoke(this, theme);
        }

        private void OnColorValuesChanged (UISettings sender, object args)
        {
            if (this.ThemeConfiguration == ElementTheme.Default)
            {
                // UI thread dispatch required
                DispatcherQueue.GetForCurrentThread()?.TryEnqueue(()=>
                {
                    this.SetTheme(m_targetApplication.RequestedTheme);
                });
            }
        }

        private static ApplicationTheme GetSystemTheme ()
        {
            using (RegistryKey themeRootKey = Registry.CurrentUser.OpenSubKey(kRegistryThemeRootKey, writable: false))
            {
                if (themeRootKey == null)
                {
                    Logger.LogInfo($"Registry info not detected for app os theme. Assuming dark theme.");
                    return ApplicationTheme.Dark;
                }

                object isLightTheCasted = themeRootKey.GetValue(kRegistryIsLightThemeValue, 2);
                return (isLightTheCasted is int isLightThemeInt && isLightThemeInt == 0) ? ApplicationTheme.Dark : ApplicationTheme.Light;
            }

            //var uiSettings = new UISettings();
            //var background = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background);
            //// Heuristic: dark background means dark theme
            //return background.R < 128 && background.G < 128 && background.B < 128
            //    ? ApplicationTheme.Dark
            //    : ApplicationTheme.Light;
        }

        public void ApplyTheme(Window window)
        {
            this.ApplyTheme(window, m_currentTheme);
        }

        public void ApplyTheme(Window window, ApplicationTheme theme)
        {
            if (window.Content is FrameworkElement fe)
            {
                var newRequestedTheme = theme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
                while (fe != null)
                {
                    fe.RequestedTheme = newRequestedTheme;
                    fe = fe.GetFirstParentOf<FrameworkElement>();
                }
            }
        }
    }
}
