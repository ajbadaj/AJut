namespace AJut.UX.Theming
{
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Xaml;
    using Microsoft.Win32;
    using System;
    using System.ComponentModel;
    using Windows.Storage;
    using Windows.UI.ViewManagement;

    // Which theme is in effect is decided by AJut.UX's AppThemeResolver, shared with every other
    // stack; this class maps WinUI3's ElementTheme and ApplicationTheme onto it, reads the OS theme
    // from the registry, keeps the choice in the app's local settings, and applies a theme by
    // setting RequestedTheme on each window's content.
    public partial class AppThemeManager : NotifyPropertyChanged, IDisposable
    {
        private const string kUserSelectedTheme = "AppThemeManager::UserSelectedTheme";
        private const string kRegistryThemeRootKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string kRegistryIsLightThemeValue = "AppsUseLightTheme";
        private const string kTouchThemeColorsSource = "ms-appx:///AJut.UX.WinUI3/Resources/TouchThemeColors.xaml";

        private Application m_targetApplication;
        private ApplicationTheme m_currentTheme = ApplicationTheme.Dark;
        private WindowManager m_windows;
        private RegistryThemeSource m_osSource;
        private AppThemeResolver m_resolver;
        private bool m_isForcingThemeChange;
        private Uri m_touchThemeColorsSource;
        private ResourceDictionary m_touchThemeColors;
        private bool m_isTouchFocused;

        public event EventHandler<EventArgs<ApplicationTheme>> ThemeChanged;

        /// <param name="touchThemeColorsSource">
        /// The dictionary added when <see cref="IsTouchFocused"/> is on: light and dark theme dictionaries with the touch
        /// palettes. Leave it null for AJut's own (Resources/TouchThemeColors.xaml in AJut.UX.WinUI3); an app that changes
        /// the AJut colors passes its own of the same shape, with its changes laid over the touch palettes.
        /// </param>
        public void Setup(Application target, WindowManager windows, bool persistUserThemeSelection = true, Uri touchThemeColorsSource = null)
        {
            m_windows = windows;
            m_targetApplication = target ?? Application.Current;
            m_touchThemeColorsSource = touchThemeColorsSource ?? new Uri(kTouchThemeColorsSource);

            m_osSource = new RegistryThemeSource();
            m_resolver = new AppThemeResolver(
                m_osSource,
                persistUserThemeSelection ? new LocalSettingsStore(ApplicationData.Current.LocalSettings) : null,
                eAppThemeConfiguration.UseSameAsOS
            );
            m_resolver.PropertyChanged += this.OnResolverPropertyChanged;
            m_resolver.ThemeChanged += this.OnResolverThemeChanged;

            // Set the initial theme. Not persisting, the app's own RequestedTheme is the starting theme.
            if (persistUserThemeSelection)
            {
                this.ForceThemeChange(ToElementTheme(m_resolver.ThemeConfiguration));
            }
            else
            {
                this.SetTheme(m_targetApplication.RequestedTheme);
            }

            // Touch focus turned on before setup takes effect now
            if (m_isTouchFocused)
            {
                this.ApplyTouchThemeColors();
            }
        }

        void IDisposable.Dispose ()
        {
            GC.SuppressFinalize(this);
            if (m_resolver != null)
            {
                m_resolver.PropertyChanged -= this.OnResolverPropertyChanged;
                m_resolver.ThemeChanged -= this.OnResolverThemeChanged;
                m_resolver.Dispose();
                m_resolver = null;
            }

            m_osSource?.Dispose();
            m_osSource = null;
        }

        public ElementTheme[] ThemeConfigurationOptions { get; } = Enum.GetValues<ElementTheme>();

        public ElementTheme ThemeConfiguration
        {
            get => m_resolver != null ? ToElementTheme(m_resolver.ThemeConfiguration) : ElementTheme.Default;
            set
            {
                if (m_resolver != null)
                {
                    m_resolver.ThemeConfiguration = ToConfiguration(value);
                }
            }
        }

        public void ForceThemeChange(ElementTheme theme)
        {
            // The resolver raises its own change notices while forcing; the theme is applied once, below,
            // whether or not it changed, so those are passed over until then
            m_isForcingThemeChange = true;
            try
            {
                m_resolver.ForceThemeChange(ToConfiguration(theme));
            }
            finally
            {
                m_isForcingThemeChange = false;
            }

            this.RaisePropertiesChanged(nameof(ThemeConfiguration));
            this.SetTheme(ToApplicationTheme(m_resolver.CurrentTheme), onlyIfNew: false);
        }

        public ApplicationTheme CurrentTheme => m_currentTheme;

        /// <summary>
        /// Whether the theme is touch focused: the AJut touch palettes in place of the light and dark ones, so buttons
        /// read as buttons without hover (lifted surfaces, no borders). Off by default, and AJut never turns it on by
        /// itself. An app can follow a 2-in-1's touch use by setting this from a <see cref="TouchPreferenceWatcher"/>,
        /// or offer it as a setting.
        /// </summary>
        public bool IsTouchFocused
        {
            get => m_isTouchFocused;
            set
            {
                if (this.SetAndRaiseIfChanged(ref m_isTouchFocused, value) && m_targetApplication != null)
                {
                    this.ApplyTouchThemeColors();
                }
            }
        }

        // Adds the touch theme colors as the application's last merged dictionary, where its keys win over the app's own
        // theme dictionaries, or takes them out
        private void ApplyTouchThemeColors ()
        {
            if (m_isTouchFocused)
            {
                m_touchThemeColors ??= new ResourceDictionary { Source = m_touchThemeColorsSource };
                m_targetApplication.Resources.MergedDictionaries.Remove(m_touchThemeColors);
                m_targetApplication.Resources.MergedDictionaries.Add(m_touchThemeColors);
            }
            else if (m_touchThemeColors != null)
            {
                m_targetApplication.Resources.MergedDictionaries.Remove(m_touchThemeColors);
            }

            // WinUI looks a {ThemeResource} up again only when the element's theme changes, so a dictionary added or
            // removed now would otherwise show on the next light or dark switch and not before. Taking each window's
            // content through the other theme and back makes everything in it look its resources up again. Windows
            // made later look them up when they are first shown, so they need nothing.
            ApplicationTheme otherTheme = (m_currentTheme == ApplicationTheme.Dark) ? ApplicationTheme.Light : ApplicationTheme.Dark;
            foreach (Window window in m_windows)
            {
                this.ApplyTheme(window, otherTheme);
                this.ApplyTheme(window, m_currentTheme);
            }
        }

        private void OnResolverPropertyChanged (object sender, PropertyChangedEventArgs e)
        {
            if (m_isForcingThemeChange || e.PropertyName != nameof(AppThemeResolver.ThemeConfiguration))
            {
                return;
            }

            // A new choice: applied against what this manager has applied, not against the resolver's last theme
            this.RaisePropertiesChanged(nameof(ThemeConfiguration));
            this.SetTheme(ToApplicationTheme(m_resolver.CurrentTheme));
        }

        private void OnResolverThemeChanged (object sender, EventArgs<eAppTheme> e)
        {
            if (!m_isForcingThemeChange)
            {
                this.SetTheme(ToApplicationTheme(e.Value));
            }
        }

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

        private static eAppThemeConfiguration ToConfiguration (ElementTheme theme)
        {
            switch (theme)
            {
                case ElementTheme.Dark: return eAppThemeConfiguration.Dark;
                case ElementTheme.Light: return eAppThemeConfiguration.Light;
                default: return eAppThemeConfiguration.UseSameAsOS;
            }
        }

        private static ElementTheme ToElementTheme (eAppThemeConfiguration configuration)
        {
            switch (configuration)
            {
                case eAppThemeConfiguration.Dark: return ElementTheme.Dark;
                case eAppThemeConfiguration.Light: return ElementTheme.Light;
                default: return ElementTheme.Default;
            }
        }

        private static ApplicationTheme ToApplicationTheme (eAppTheme theme) => theme == eAppTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light;

        // ===========[ Subclasses/structs ]===================================
        // The OS theme from the registry, and a change notice from UISettings.ColorValuesChanged.
        //
        // ColorValuesChanged is raised on a background thread, where DispatcherQueue.GetForCurrentThread()
        // is null, so the enqueue below never runs and a live OS theme change is not followed. That is how
        // this has always behaved, and it is kept as is here; following the OS live is AJU-109's fix.
        private sealed class RegistryThemeSource : IOSThemeSource, IDisposable
        {
            private UISettings m_uiSettings;

            public RegistryThemeSource ()
            {
                m_uiSettings = new UISettings();
                m_uiSettings.ColorValuesChanged += this.OnColorValuesChanged;
            }

            public void Dispose ()
            {
                if (m_uiSettings != null)
                {
                    m_uiSettings.ColorValuesChanged -= this.OnColorValuesChanged;
                    m_uiSettings = null;
                }
            }

            public event EventHandler OSThemeChanged;

            public eAppTheme GetOSTheme () => GetSystemTheme() == ApplicationTheme.Dark ? eAppTheme.Dark : eAppTheme.Light;

            private void OnColorValuesChanged (UISettings sender, object args)
            {
                DispatcherQueue.GetForCurrentThread()?.TryEnqueue(this.RaiseOSThemeChanged);
            }

            private void RaiseOSThemeChanged ()
            {
                this.OSThemeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // The choice in the app's local settings, under the same key and as the same ElementTheme name
        // (Default, Light or Dark) this manager has always written, so a saved choice carries over
        private sealed class LocalSettingsStore : IAppThemeConfigurationStore
        {
            private readonly ApplicationDataContainer m_settings;

            public LocalSettingsStore (ApplicationDataContainer settings)
            {
                m_settings = settings;
            }

            public bool TryLoad (out eAppThemeConfiguration configuration)
            {
                if (m_settings.Values.TryGetValue(kUserSelectedTheme, out object value)
                    && value is string themeConfigStr
                    && Enum.TryParse(themeConfigStr, false, out ElementTheme themeConfig))
                {
                    configuration = ToConfiguration(themeConfig);
                    return true;
                }

                configuration = eAppThemeConfiguration.UseSameAsOS;
                return false;
            }

            public void Save (eAppThemeConfiguration configuration)
            {
                m_settings.Values[kUserSelectedTheme] = ToElementTheme(configuration).ToString();
            }
        }
    }
}
