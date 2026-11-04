namespace AJut.UX.Theming
{
    using System;
    using System.ComponentModel;
    using AJut;
    using Foundation;
    using UIKit;

    // ===========[ AppThemeManager ]============================================
    // Light, dark or follow the OS for an iOS app, on AJut.UX's AppThemeResolver (the same resolver
    // the WinUI3, WPF and AppKit AppThemeManagers wrap). The resolver decides which theme is in
    // effect; this class reads the OS theme, keeps the choice in NSUserDefaults, and applies the
    // result to the app's windows.
    //
    // Applying a theme on UIKit is setting OverrideUserInterfaceStyle on a window: Dark or Light for
    // a choice, and Unspecified under UseSameAsOS, which lets the window follow the system again.
    // Every view and view controller under a window inherits its style, and every ThemedColors color
    // is a dynamic color that resolves against it, so nothing else needs touching. iOS has no app-wide
    // appearance the way AppKit's NSApplication.Appearance is, and before iOS 17 a scene has no style
    // of its own to set, so the manager sets it on every window of every connected scene, and on each
    // window shown later as it becomes visible, which takes in scenes that connect later. A window
    // becomes visible before it is first drawn, so it never shows a frame in the other style.
    //
    // The OS theme is read from a window scene's traits. A window's override reaches the window and
    // everything under it, never its scene, so the scene keeps saying what the system is set to. iOS
    // posts nothing when the system switches between light and dark. Under UseSameAsOS the windows
    // carry no override and follow a switch at once on their own; CurrentTheme catches up when the app
    // next becomes active. Switching from Control Center or Settings always passes through that, since
    // the app stops being active while either is open.
    //
    // With the AJut theme loaded it also gives each window the AJut accent as its tint, and every
    // UISwitch the AJut on color, through StockControlTint, so the stock controls match AJut's.
    public sealed class AppThemeManager : IDisposable
    {
        private const string kStoreKey = "AJut_AppThemeConfiguration";

        private readonly OSThemeSource m_osSource;
        private readonly AppThemeResolver m_resolver;
        private NSObject m_windowShownObserver;

        // ===========[ Setup/Construction/Teardown ]===========================
        /// <summary>
        /// Creates the manager and applies the theme to every window there is now, and to each one shown after. Make it in
        /// FinishedLaunching and keep it for the life of the app. The choice is loaded from NSUserDefaults when
        /// <paramref name="persistInUserDefaults"/> is set and one was saved, otherwise <paramref name="fallback"/> is used.
        /// </summary>
        public AppThemeManager (eAppThemeConfiguration fallback = eAppThemeConfiguration.UseSameAsOS, bool persistInUserDefaults = true)
        {
            m_osSource = new OSThemeSource();
            m_resolver = new AppThemeResolver(m_osSource, persistInUserDefaults ? new UserDefaultsStore() : null, fallback);
            m_resolver.ThemeChanged += this.OnResolverThemeChanged;
            m_resolver.PropertyChanged += this.OnResolverPropertyChanged;
            m_windowShownObserver = NSNotificationCenter.DefaultCenter.AddObserver(UIWindow.DidBecomeVisibleNotification, this.OnWindowDidBecomeVisible);
            StockControlTint.Apply();
            m_resolver.ForceThemeChange(m_resolver.ThemeConfiguration);
        }

        /// <summary>Stops following the OS and new windows. Windows already given a theme keep it.</summary>
        public void Dispose ()
        {
            if (m_windowShownObserver != null)
            {
                NSNotificationCenter.DefaultCenter.RemoveObserver(m_windowShownObserver);
                m_windowShownObserver = null;
            }

            m_resolver.ThemeChanged -= this.OnResolverThemeChanged;
            m_resolver.PropertyChanged -= this.OnResolverPropertyChanged;
            m_resolver.Dispose();
            m_osSource.Dispose();
        }

        // ===========[ Events ]================================================
        /// <summary>Raised after a new theme has been applied</summary>
        public event EventHandler<EventArgs<eAppTheme>> ThemeChanged;

        private void OnResolverThemeChanged (object sender, EventArgs<eAppTheme> e)
        {
            this.ApplyToAllWindows();
            this.ThemeChanged?.Invoke(this, e);
        }

        // A new choice can leave the theme as it was (Dark to UseSameAsOS while the system is dark), and still has to
        // move the windows: from pinned to dark to following the system
        private void OnResolverPropertyChanged (object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AppThemeResolver.ThemeConfiguration))
            {
                this.ApplyToAllWindows();
            }
        }

        private void OnWindowDidBecomeVisible (NSNotification notification)
        {
            if (notification.Object is UIWindow window)
            {
                this.ApplyTo(window);
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
        /// Whether the AJut palette is painted in its touch variant: lifted button surfaces and no button borders, for touch
        /// with no hover. Off by default, as on every stack. This is <see cref="ThemedColors.UsesTouchVariant"/>, which is
        /// where UIKit switches the palette; it is here too so an app sets it beside its theme choice, as on every other
        /// stack. AJut's UIKit controls take their colors as they are made, so views made before a change keep the palette
        /// they were made with until they are rebuilt.
        /// </summary>
        public bool UsesTouchVariant
        {
            get => ThemedColors.UsesTouchVariant;
            set => ThemedColors.UsesTouchVariant = value;
        }

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Gives <paramref name="window"/> the theme in effect now, and with the AJut theme loaded the AJut accent as its
        /// tint (<see cref="StockControlTint"/>). Every window is given both as it becomes visible, so this is only needed
        /// for a window that should take them before it is shown.
        /// </summary>
        public void ApplyTo (UIWindow window)
        {
            window.OverrideUserInterfaceStyle = ToInterfaceStyle(m_resolver.ThemeConfiguration);
            StockControlTint.ApplyTo(window);
        }

        // ===========[ Helper Methods ]========================================
        private static UIUserInterfaceStyle ToInterfaceStyle (eAppThemeConfiguration configuration)
        {
            switch (configuration)
            {
                case eAppThemeConfiguration.Dark: return UIUserInterfaceStyle.Dark;
                case eAppThemeConfiguration.Light: return UIUserInterfaceStyle.Light;
                default: return UIUserInterfaceStyle.Unspecified;
            }
        }

        private void ApplyToAllWindows ()
        {
            UIUserInterfaceStyle style = ToInterfaceStyle(m_resolver.ThemeConfiguration);
            foreach (UIScene scene in UIApplication.SharedApplication.ConnectedScenes)
            {
                if (scene is UIWindowScene windowScene)
                {
                    foreach (UIWindow window in windowScene.Windows)
                    {
                        window.OverrideUserInterfaceStyle = style;
                        StockControlTint.ApplyTo(window);
                    }
                }
            }
        }

        // ===========[ Subclasses/structs ]====================================
        // The OS theme from a window scene's traits, checked again each time the app becomes active. Before any scene has
        // connected there is no scene to read, so the current traits stand in until the first activation reads one.
        private sealed class OSThemeSource : IOSThemeSource, IDisposable
        {
            private NSObject m_becameActiveObserver;

            public OSThemeSource ()
            {
                m_becameActiveObserver = NSNotificationCenter.DefaultCenter.AddObserver(UIApplication.DidBecomeActiveNotification, this.OnAppDidBecomeActive);
            }

            public void Dispose ()
            {
                if (m_becameActiveObserver != null)
                {
                    NSNotificationCenter.DefaultCenter.RemoveObserver(m_becameActiveObserver);
                    m_becameActiveObserver = null;
                }
            }

            public event EventHandler OSThemeChanged;

            public eAppTheme GetOSTheme ()
            {
                UITraitCollection traits = UITraitCollection.CurrentTraitCollection;
                foreach (UIScene scene in UIApplication.SharedApplication.ConnectedScenes)
                {
                    if (scene is UIWindowScene windowScene)
                    {
                        traits = windowScene.TraitCollection;
                        break;
                    }
                }

                return traits.UserInterfaceStyle == UIUserInterfaceStyle.Dark ? eAppTheme.Dark : eAppTheme.Light;
            }

            // Raised on every activation; the resolver acts only when the theme it reads is not the one in effect.
            // Application notifications are posted on the main thread, as IOSThemeSource asks.
            private void OnAppDidBecomeActive (NSNotification notification)
            {
                this.OSThemeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // The choice under the same NSUserDefaults key AppKit's AppThemeManager uses, written as the choice's name
        private sealed class UserDefaultsStore : IAppThemeConfigurationStore
        {
            public bool TryLoad (out eAppThemeConfiguration configuration)
            {
                string stored = NSUserDefaults.StandardUserDefaults.StringForKey(kStoreKey);
                if ((stored != null)
                    && Enum.TryParse(stored, out configuration)
                    && Enum.IsDefined(configuration))
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
