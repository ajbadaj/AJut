namespace AJut.UX.Theming
{
    using System;
    using AJut;
    using AppKit;
    using Foundation;

    // ===========[ AppThemeManager ]============================================
    // Light, dark or follow the OS for an AppKit app, on AJut.UX's AppThemeResolver (the same
    // resolver the WinUI3 and WPF AppThemeManagers wrap). The resolver decides which theme is in
    // effect; this class reads the OS theme, keeps the setting in NSUserDefaults, and applies the
    // result by setting the app's appearance.
    //
    // Applying a theme on AppKit is setting NSApplication.Appearance: aqua for light, darkAqua for
    // dark, or null to let the system decide. Every window and view inherits it, and every
    // ThemedColors color resolves against it, so nothing else needs touching. Under UseSameAsOS
    // the appearance is set to null rather than to the OS's current one: an explicit appearance
    // would pin the app to it, and the app would stop following the OS until restarted.
    //
    // The OS theme is read from the global default AppleInterfaceStyle ("Dark" in dark mode,
    // unset in light mode, and kept current while the system is on Auto), and the system posts
    // the distributed notification AppleInterfaceThemeChangedNotification when it changes. These
    // are read rather than NSApplication.EffectiveAppearance on purpose: the app's effective
    // appearance follows the app's own Appearance once one is set, so it stops saying anything
    // about the OS the moment a Dark or Light choice is applied.
    public sealed class AppThemeManager : IDisposable
    {
        private const string kStoreKey = "AJut_AppThemeConfiguration";

        private readonly OSThemeSource m_osSource;
        private readonly AppThemeResolver m_resolver;

        // ===========[ Setup/Construction/Teardown ]===========================
        /// <summary>
        /// Creates the manager and applies the theme straight away. The setting is loaded from
        /// NSUserDefaults when <paramref name="persistInUserDefaults"/> is set and one was saved, otherwise
        /// <paramref name="fallback"/> is used.
        /// </summary>
        public AppThemeManager (eAppThemeConfiguration fallback = eAppThemeConfiguration.UseSameAsOS, bool persistInUserDefaults = true)
        {
            m_osSource = new OSThemeSource();
            m_resolver = new AppThemeResolver(m_osSource, persistInUserDefaults ? new UserDefaultsStore() : null, fallback);
            m_resolver.ThemeChanged += this.OnThemeChanged;
            m_resolver.PropertyChanged += this.OnResolverPropertyChanged;
            m_resolver.ForceThemeChange(m_resolver.ThemeConfiguration);
        }

        public void Dispose ()
        {
            m_resolver.ThemeChanged -= this.OnThemeChanged;
            m_resolver.PropertyChanged -= this.OnResolverPropertyChanged;
            m_resolver.Dispose();
            m_osSource.Dispose();
        }

        // ===========[ Events ]================================================
        /// <summary>Raised after a new theme has been applied</summary>
        public event EventHandler<EventArgs<eAppTheme>> ThemeChanged;

        private void OnThemeChanged (object sender, EventArgs<eAppTheme> e)
        {
            this.Apply();
            this.ThemeChanged?.Invoke(this, e);
        }

        // A new choice can leave the theme as it was (Dark to UseSameAsOS while the OS is dark), and
        // still has to move the app's appearance: from pinned to darkAqua to following the system
        private void OnResolverPropertyChanged (object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppThemeResolver.ThemeConfiguration))
            {
                this.Apply();
            }
        }

        // ===========[ Properties ]============================================
        /// <summary>The resolver underneath, for binding to its INotifyPropertyChanged properties</summary>
        public AppThemeResolver Resolver => m_resolver;

        /// <summary>The user's choice: dark, light, or follow the OS. Setting it saves it and applies the result.</summary>
        public eAppThemeConfiguration ThemeConfiguration
        {
            get => m_resolver.ThemeConfiguration;
            set => m_resolver.ThemeConfiguration = value;
        }

        /// <summary>The theme in effect</summary>
        public eAppTheme CurrentTheme => m_resolver.CurrentTheme;

        /// <summary>
        /// Whether the AJut palette is painted in its touch variant: lifted button surfaces and no button borders, for
        /// touch with no hover. Off by default. This is <see cref="ThemedColors.IsTouchFocused"/>, which is where AppKit
        /// switches the palette (beside <see cref="ThemedColors.IsThemeLoaded"/>, and with no theme manager needed); it is
        /// here too so an app sets it beside its theme choice, as on WPF and WinUI3.
        /// </summary>
        public bool IsTouchFocused
        {
            get => ThemedColors.IsTouchFocused;
            set => ThemedColors.IsTouchFocused = value;
        }

        // ===========[ Helper Methods ]========================================
        private void Apply ()
        {
            NSApplication app = NSApplication.SharedApplication;
            switch (m_resolver.ThemeConfiguration)
            {
                case eAppThemeConfiguration.Dark:
                    app.Appearance = NSAppearance.GetAppearance(NSAppearance.NameDarkAqua);
                    break;

                case eAppThemeConfiguration.Light:
                    app.Appearance = NSAppearance.GetAppearance(NSAppearance.NameAqua);
                    break;

                default:
                    app.Appearance = null;
                    break;
            }
        }

        // ===========[ Subclasses/structs ]====================================
        private sealed class OSThemeSource : IOSThemeSource, IDisposable
        {
            private const string kInterfaceStyleKey = "AppleInterfaceStyle";
            private const string kThemeChangedNotification = "AppleInterfaceThemeChangedNotification";

            private NSObject m_observerToken;

            public OSThemeSource ()
            {
                m_observerToken = NSDistributedNotificationCenter.DefaultCenter.AddObserver(new NSString(kThemeChangedNotification), this.OnSystemThemeChanged);
            }

            public void Dispose ()
            {
                if (m_observerToken != null)
                {
                    NSDistributedNotificationCenter.DefaultCenter.RemoveObserver(m_observerToken);
                    m_observerToken = null;
                }
            }

            public event EventHandler OSThemeChanged;

            public eAppTheme GetOSTheme ()
            {
                string style = NSUserDefaults.StandardUserDefaults.StringForKey(kInterfaceStyleKey);
                return style == "Dark" ? eAppTheme.Dark : eAppTheme.Light;
            }

            // Distributed notifications arrive on the thread that registered for them, which is the
            // main thread here, as IOSThemeSource asks
            private void OnSystemThemeChanged (NSNotification notification)
            {
                this.OSThemeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private sealed class UserDefaultsStore : IAppThemeConfigurationStore
        {
            public bool TryLoad (out eAppThemeConfiguration configuration)
            {
                string stored = NSUserDefaults.StandardUserDefaults.StringForKey(kStoreKey);
                if (stored != null && Enum.TryParse(stored, out configuration))
                {
                    return true;
                }

                configuration = default;
                return false;
            }

            public void Save (eAppThemeConfiguration configuration)
            {
                NSUserDefaults.StandardUserDefaults.SetString(configuration.ToString(), kStoreKey);
            }
        }
    }
}
