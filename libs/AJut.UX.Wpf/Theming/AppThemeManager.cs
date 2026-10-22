namespace AJut.UX.Theming
{
    using System;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Win32;

    /// <summary>
    /// A theme management object to use in your App, to set manually or to provide user bindable options for app configuration
    /// </summary>
    /// <remarks>
    /// Which theme is in effect is decided by AJut.UX's <see cref="AppThemeResolver"/>, shared with every other stack.
    /// This class reads the OS theme from the registry, and applies a theme by swapping the app's theme colors dictionary.
    /// </remarks>
    public class AppThemeManager : NotifyPropertyChanged, IDisposable
    {
        private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(AppThemeManager));
        private const string kRegistryThemeRootKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string kRegistryIsLightThemeValue = "AppsUseLightTheme";

        private readonly Application m_targetApplication;
        private readonly int m_themeColorsInsertionIndex;
        private readonly RegistryThemeSource m_osSource;
        private readonly AppThemeResolver m_resolver;
        private ResourceDictionary m_lightThemeColors;
        private ResourceDictionary m_darkThemeColors;

        // Note: Using these loaders instead of a Lazy because we can't override the lazy's value and we might already start with the value loaded
        private Func<ResourceDictionary> m_lightThemeLoader;
        private Func<ResourceDictionary> m_darkThemeLoader;

        // The theme whose colors are loaded now. The resolver decides which theme is in effect; this is what has been applied.
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

            // The starting choice is whichever theme's colors the app already has loaded, dark when it is neither
            eAppThemeConfiguration startingConfiguration;
            bool isStartingThemeLoaded = true;
            ResourceDictionary currentThemeColors = m_targetApplication.Resources.MergedDictionaries[m_themeColorsInsertionIndex];
            if (currentThemeColors.Source == lightThemeXamlUri)
            {
                m_lightThemeColors = currentThemeColors;
                m_currentTheme = eAppTheme.Light;
                startingConfiguration = eAppThemeConfiguration.Light;
            }
            else if (currentThemeColors.Source == darkThemeXamlUri)
            {
                m_darkThemeColors = currentThemeColors;
                m_currentTheme = eAppTheme.Dark;
                startingConfiguration = eAppThemeConfiguration.Dark;
            }
            else
            {
                m_darkThemeColors = m_darkThemeLoader();
                startingConfiguration = eAppThemeConfiguration.Dark;
                isStartingThemeLoaded = false;
            }

            m_osSource = new RegistryThemeSource();
            m_resolver = new AppThemeResolver(m_osSource, store: null, fallback: startingConfiguration);
            m_resolver.ThemeChanged += this.OnResolverThemeChanged;
            m_resolver.PropertyChanged += this.OnResolverPropertyChanged;

            if (!isStartingThemeLoaded)
            {
                this.AlterApplicationTheme(eAppTheme.Dark);
            }
        }

        public void Dispose ()
        {
            m_resolver.ThemeChanged -= this.OnResolverThemeChanged;
            m_resolver.PropertyChanged -= this.OnResolverPropertyChanged;
            m_resolver.Dispose();
            m_osSource.Dispose();
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
            get => m_resolver.ThemeConfiguration;
            set => m_resolver.ThemeConfiguration = value;
        }

        /// <summary>
        /// The current app theme, this is readonly - to alter the theme set the ThemeConfiguration instead
        /// </summary>
        public eAppTheme CurrentTheme => m_currentTheme;

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

                object isLightTheCasted = themeRootKey.GetValue(kRegistryIsLightThemeValue);
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
            this.RaisePropertiesChanged(nameof(CurrentTheme));

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

        private void OnResolverThemeChanged (object sender, EventArgs<eAppTheme> e)
        {
            this.AlterApplicationTheme(e.Value);
        }

        private void OnResolverPropertyChanged (object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppThemeResolver.ThemeConfiguration))
            {
                this.RaisePropertiesChanged(nameof(ThemeConfiguration));
            }
        }

        // =========================[ Utility Classes ]=============================
        // The OS theme from the registry, and its changes from SystemEvents.UserPreferenceChanged. SystemEvents raises
        // on the thread that first used it when that thread pumps messages, which in a WPF app is the UI thread.
        private sealed class RegistryThemeSource : IOSThemeSource, IDisposable
        {
            public RegistryThemeSource ()
            {
                try
                {
                    SystemEvents.UserPreferenceChanged += this.OnUserPreferenceChanged;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Theming error - starting up OS user preference monitoring is failing (likely a machine problem)", ex);
                }
            }

            public void Dispose ()
            {
                try
                {
                    SystemEvents.UserPreferenceChanged -= this.OnUserPreferenceChanged;
                }
                catch (Exception ex)
                {
                    Logger.LogError("Theming error - stopping OS user preference monitoring is failing (likely a machine problem)", ex);
                }

                this.OSThemeChanged = null;
            }

            public event EventHandler OSThemeChanged;

            public eAppTheme GetOSTheme () => GetWindowsUserAppThemeSetting();

            private void OnUserPreferenceChanged (object sender, UserPreferenceChangedEventArgs e)
            {
                this.OSThemeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

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
