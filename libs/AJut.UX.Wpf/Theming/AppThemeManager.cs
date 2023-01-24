namespace AJut.UX.Theming
{
    using System;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Win32;

    public enum eAppTheme { Dark, Light }
    public enum eAppThemeConfiguration { Dark, Light, UseSameAsOS }

    /// <summary>
    /// A theme management object to use in your App, to set manually or to provide user bindable options for app configuration
    /// </summary>
    public class AppThemeManager : NotifyPropertyChanged, IDisposable
    {
        private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(AppThemeManager));
        private const string kRegistryThemeRootKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string kRegistryIsLightThemeValue = "AppsUseLightTheme";

        private readonly Application m_targetApplication;
        private readonly int m_themeColorsInsertionIndex;
        private ResourceDictionary m_lightThemeColors;
        private ResourceDictionary m_darkThemeColors;

        // Note: Using these loaders instead of a Lazy because we can't override the lazy's value and we might already start with the value loaded
        private Func<ResourceDictionary> m_lightThemeLoader;
        private Func<ResourceDictionary> m_darkThemeLoader;

        private eAppThemeConfiguration m_themeConfiguration;
        private eAppTheme m_currentTheme;

        // =========================[ Construction / Setup ]=============================

        /// <summary>
        /// Construct an AppThemeManager instance
        /// </summary>
        /// <param name="lightThemeXamlUriPath">The xaml uri for the light theme, usually in the format of: "/$AppName;component/Themes/LightThemeColors.xaml" - if the entry assembly is the target, and the path lives in Themes/LightThemeColors.xaml you can leave this null</param>
        /// <param name="darkThemeXamlUriPath">The xaml uri for the dark theme, usually in the format of: "/$AppName;component/Themes/DarkThemeColors.xaml" - if the entry assembly is the target, and the path lives in Themes/DarkThemeColors.xaml you can leave this null</param>
        /// <param name="themeColorsInsertionIndex">At what index do we insert the current theme color xaml, default is index zero</param>
        public AppThemeManager (Application target = null, string lightThemeXamlUriPath = null, string darkThemeXamlUriPath = null, int themeColorsInsertionIndex = 0)
        {
            m_targetApplication = target ?? Application.Current;
            m_themeColorsInsertionIndex = themeColorsInsertionIndex;

            var lightThemeXamlUri = new Uri(lightThemeXamlUriPath ?? $"Themes/LightThemeColors.xaml", UriKind.Relative);
            var darkThemeXamlUri = new Uri(darkThemeXamlUriPath ?? $"Themes/DarkThemeColors.xaml", UriKind.Relative);

            this.RunDebugAssertions();

            m_lightThemeLoader = () => Application.LoadComponent(lightThemeXamlUri) as ResourceDictionary;
            m_darkThemeLoader = () => Application.LoadComponent(darkThemeXamlUri) as ResourceDictionary;

            ResourceDictionary currentThemeColors = m_targetApplication.Resources.MergedDictionaries[m_themeColorsInsertionIndex];
            if (currentThemeColors.Source == lightThemeXamlUri)
            {
                m_lightThemeColors = currentThemeColors;
                m_currentTheme = eAppTheme.Light;
                m_themeConfiguration = eAppThemeConfiguration.Light;
            }
            else if (currentThemeColors.Source == darkThemeXamlUri)
            {
                m_darkThemeColors = currentThemeColors;
                m_currentTheme = eAppTheme.Dark;
                m_themeConfiguration = eAppThemeConfiguration.Dark;
            }
            else
            {
                m_darkThemeColors = m_darkThemeLoader();
                m_themeConfiguration = eAppThemeConfiguration.Dark;
                AlterApplicationTheme(eAppTheme.Dark);
            }
        }

        public void Dispose ()
        {
            this.StopWatchingOSForUserThemeChange();
            m_lightThemeColors = null;
            m_darkThemeColors = null;
            m_lightThemeLoader = null;
            m_darkThemeLoader = null;
        }

        [Conditional("DEBUG")]
        private void RunDebugAssertions ()
        {
            Debug.Assert(m_targetApplication?.Resources?.MergedDictionaries != null, $"{nameof(AppThemeManager)}: Target application or resource dictionary setup is invalid");
        }


        // =========================[ Attached Properties ]=============================

        // Adding a window border glow brush for theming the way it's done in ajut ThemedControlStylesBase
        public static DependencyProperty WindowBorderGlowBrushProperty = APUtils.Register(GetWindowBorderGlowBrush, SetWindowBorderGlowBrush);
        public static Brush GetWindowBorderGlowBrush (DependencyObject obj) => (Brush)obj.GetValue(WindowBorderGlowBrushProperty);
        public static void SetWindowBorderGlowBrush (DependencyObject obj, Brush value) => obj.SetValue(WindowBorderGlowBrushProperty, value);

        // =========================[ Bindable Properties ]=============================

        /// <summary>
        /// How the user want's the theme set
        /// </summary>
        public eAppThemeConfiguration ThemeConfiguration
        {
            get => m_themeConfiguration;
            set
            {
                if (this.SetAndRaiseIfChanged(ref m_themeConfiguration, value))
                {
                    switch (value)
                    {
                        case eAppThemeConfiguration.UseSameAsOS:
                            this.AlterApplicationTheme(GetWindowsUserAppThemeSetting());
                            this.EnsureOsMonitoringUnderwayForUserThemeChange();
                            break;

                        case eAppThemeConfiguration.Light:
                            this.AlterApplicationTheme(eAppTheme.Light);
                            break;

                        case eAppThemeConfiguration.Dark:
                            this.AlterApplicationTheme(eAppTheme.Dark);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// The current app theme, this is readonly - to alter the theme set the ThemeConfiguration instead
        /// </summary>
        public eAppTheme CurrentTheme
        {
            get => m_currentTheme;
            private set => this.SetAndRaiseIfChanged(ref m_currentTheme, value);
        }

        /// <summary>
        /// Flags that control the theme from a global xaml perspective
        /// </summary>
        public static Flags GlobalBindingFlags { get; } = new Flags();

        // =========================[ Utility Methods ]=============================

        public static eAppTheme GetWindowsUserAppThemeSetting ()
        {
            using (RegistryKey themeRootKey = Registry.CurrentUser.OpenSubKey(kRegistryThemeRootKey))
            {
                if (themeRootKey == null)
                {
                    Logger.LogInfo($"Registry info not detected for app os theme. Assuming dark theme.");
                    return eAppTheme.Dark;
                }

                object? isLightTheCasted = themeRootKey.GetValue(kRegistryIsLightThemeValue);
                return (isLightTheCasted is int isLightThemeInt && isLightThemeInt == 0) ? eAppTheme.Dark : eAppTheme.Light;
            }
        }

        private void AlterApplicationTheme (eAppTheme theme)
        {
            if (theme == m_currentTheme)
            {
                Logger.LogInfo($"Request to change theme to {theme} ignored, theme is already {theme}");
                return;
            }

            Logger.LogInfo($"Changing theme to: {theme}");
            m_targetApplication.Resources.MergedDictionaries.RemoveAt(m_themeColorsInsertionIndex);
            m_targetApplication.Resources.MergedDictionaries.Insert(0, theme == eAppTheme.Dark ? _GetDarkThemeColors() : _GetLightThemeColors());
            m_currentTheme = theme;

            ResourceDictionary _GetLightThemeColors ()
            {
                if (m_lightThemeColors != null)
                {
                    return m_lightThemeColors;
                }

                m_lightThemeColors = m_lightThemeLoader();
                return m_lightThemeColors;
            }

            ResourceDictionary _GetDarkThemeColors ()
            {
                if (m_darkThemeColors != null)
                {
                    return m_darkThemeColors;
                }

                m_darkThemeColors = m_darkThemeLoader();
                return m_darkThemeColors;
            }
        }

        private void EnsureOsMonitoringUnderwayForUserThemeChange ()
        {
            try
            {
                SystemEvents.UserPreferenceChanged -= this.OnUserPreferenceChanged;
                SystemEvents.UserPreferenceChanged += this.OnUserPreferenceChanged;
            }
            catch (Exception ex)
            {
                Logger.LogError("Theming error - starting up OS user preference monitoring is failing (likely a machine problem)", ex);
            }
        }

        private void StopWatchingOSForUserThemeChange ()
        {
            try
            {
                SystemEvents.UserPreferenceChanged -= this.OnUserPreferenceChanged;
            }
            catch (Exception ex)
            {
                Logger.LogError("Theming error - stopping OS user preference monitoring is failing (likely a machine problem)", ex);
            }
        }

        private void OnUserPreferenceChanged (object sender, UserPreferenceChangedEventArgs e)
        {
            if (m_themeConfiguration == eAppThemeConfiguration.UseSameAsOS)
            {
                this.AlterApplicationTheme(GetWindowsUserAppThemeSetting());
            }
        }

        // =========================[ Utility Classes ]=============================
        public class Flags : NotifyPropertyChanged
        {
            private bool m_listItemsShowHover = true;
            public bool ListItemsShowHover
            {
                get => m_listItemsShowHover;
                set => this.SetAndRaiseIfChanged(ref m_listItemsShowHover, value);
            }
        }
    }
}

namespace AJut.UX.Themeing
{
    using System;
    using System.Windows;

    [Obsolete("This was a naming error, please use AppThemeManager from AJut.Ux.Theming (not this class from AJut.UX.Themeing) - this version will be removed in future updates")]
    public enum eAppTheme { Dark, Light }

    [Obsolete("This was a naming error, please use AppThemeManager from AJut.Ux.Theming (not this class from AJut.UX.Themeing) - this version will be removed in future updates")]
    public enum eAppThemeConfiguration { Dark, Light, UseSameAsOS }

    [Obsolete("This was a naming error, please use AppThemeManager from AJut.Ux.Theming (not this class from AJut.UX.Themeing) - this version will be removed in future updates")]
    public class AppThemeManager : Theming.AppThemeManager
    {
        public AppThemeManager (Application target = null, string lightThemeXamlUriPath = null, string darkThemeXamlUriPath = null, int themeColorsInsertionIndex = 0)
            : base(target, lightThemeXamlUriPath, darkThemeXamlUriPath, themeColorsInsertionIndex)
        {

        }

    }
}
