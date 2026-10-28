namespace AJut.UX.Theming
{
    using System;

    /// <summary>
    /// Where an <see cref="AppThemeResolver"/> reads the OS's light or dark setting. Each UI toolkit has one: the
    /// registry on Windows, the system appearance on macOS.
    /// </summary>
    public interface IOSThemeSource
    {
        /// <summary>The theme the OS is set to now</summary>
        eAppTheme GetOSTheme ();

        /// <summary>Raised when the OS theme changes, on the app's main thread. A source that hears about it on another thread marshals first.</summary>
        event EventHandler OSThemeChanged;
    }

    /// <summary>Somewhere an <see cref="AppThemeResolver"/> keeps the user's theme choice between runs</summary>
    public interface IAppThemeConfigurationStore
    {
        /// <summary>The saved choice; false when nothing usable was saved</summary>
        bool TryLoad (out eAppThemeConfiguration configuration);

        /// <summary>Saves the choice for the next run</summary>
        void Save (eAppThemeConfiguration configuration);
    }

    // ===========[ AppThemeResolver ]===========================================
    // The part of an AppThemeManager that is the same on every toolkit: which theme the user chose
    // (dark, light, or follow the OS), keeping that choice, and working out the theme in effect
    // from it. The toolkit's AppThemeManager reads the OS through an IOSThemeSource, keeps the
    // choice through an IAppThemeConfigurationStore, and applies whatever theme this settles on.
    //
    // An OS theme change is only acted on while the choice is UseSameAsOS; under Dark or Light the
    // OS can do what it likes.
    public sealed class AppThemeResolver : NotifyPropertyChanged, IDisposable
    {
        private readonly IOSThemeSource m_osSource;
        private readonly IAppThemeConfigurationStore? m_store;
        private eAppThemeConfiguration m_themeConfiguration;
        private eAppTheme m_currentTheme;
        private bool m_isDisposed;

        // ===========[ Setup/Construction/Teardown ]===========================
        /// <summary>
        /// Starts from the choice <paramref name="store"/> has saved, or <paramref name="fallback"/> when there is no
        /// store or nothing in it, and works out the theme in effect. Nothing is raised: there is nothing listening yet.
        /// </summary>
        public AppThemeResolver (IOSThemeSource osSource, IAppThemeConfigurationStore? store = null, eAppThemeConfiguration fallback = eAppThemeConfiguration.UseSameAsOS)
        {
            m_osSource = osSource ?? throw new ArgumentNullException(nameof(osSource));
            m_store = store;
            m_themeConfiguration = (store != null && store.TryLoad(out eAppThemeConfiguration saved)) ? saved : fallback;
            m_currentTheme = this.ResolveTheme(m_themeConfiguration);
            m_osSource.OSThemeChanged += this.OnOSThemeChanged;
        }

        /// <summary>Stops following the OS. Safe to call more than once.</summary>
        public void Dispose ()
        {
            if (m_isDisposed)
            {
                return;
            }

            m_isDisposed = true;
            m_osSource.OSThemeChanged -= this.OnOSThemeChanged;
        }

        // ===========[ Events ]================================================
        /// <summary>Raised when the theme in effect changes, and on every <see cref="ForceThemeChange"/>. The value is the theme now in effect.</summary>
        public event EventHandler<EventArgs<eAppTheme>>? ThemeChanged;

        private void OnOSThemeChanged (object? sender, EventArgs e)
        {
            if (m_themeConfiguration == eAppThemeConfiguration.UseSameAsOS)
            {
                this.FollowOSTheme(m_osSource.GetOSTheme());
            }
        }

        // ===========[ Properties ]============================================
        /// <summary>The user's choice. Setting a new one saves it, then moves the theme in effect to match.</summary>
        public eAppThemeConfiguration ThemeConfiguration
        {
            get => m_themeConfiguration;
            set
            {
                if (m_themeConfiguration == value)
                {
                    return;
                }

                m_themeConfiguration = value;
                m_store?.Save(value);
                this.ApplyConfiguration(alwaysRaiseThemeChanged: false);
            }
        }

        /// <summary>The theme in effect: the choice itself, or the OS's theme under UseSameAsOS</summary>
        public eAppTheme CurrentTheme => m_currentTheme;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Sets the choice and saves it, then raises <see cref="ThemeChanged"/> whether or not the theme in effect changed,
        /// so a toolkit can apply the theme on start up or to windows that missed the last change.
        /// </summary>
        public void ForceThemeChange (eAppThemeConfiguration configuration)
        {
            m_themeConfiguration = configuration;
            m_store?.Save(configuration);
            this.ApplyConfiguration(alwaysRaiseThemeChanged: true);
        }

        // ===========[ Helper Methods ]========================================
        private eAppTheme ResolveTheme (eAppThemeConfiguration configuration)
        {
            switch (configuration)
            {
                case eAppThemeConfiguration.Dark: return eAppTheme.Dark;
                case eAppThemeConfiguration.Light: return eAppTheme.Light;
                default: return m_osSource.GetOSTheme();
            }
        }

        // Everything is settled before anything is raised, so a listener for either notice reads the new
        // choice and the new theme together
        private void ApplyConfiguration (bool alwaysRaiseThemeChanged)
        {
            eAppTheme resolved = this.ResolveTheme(m_themeConfiguration);
            bool themeChanged = resolved != m_currentTheme;
            m_currentTheme = resolved;

            this.RaisePropertiesChanged(nameof(ThemeConfiguration));
            if (themeChanged)
            {
                this.RaisePropertiesChanged(nameof(CurrentTheme));
            }

            if (themeChanged || alwaysRaiseThemeChanged)
            {
                this.ThemeChanged?.Invoke(this, new EventArgs<eAppTheme>(resolved));
            }
        }

        private void FollowOSTheme (eAppTheme osTheme)
        {
            if (osTheme == m_currentTheme)
            {
                return;
            }

            m_currentTheme = osTheme;
            this.RaisePropertiesChanged(nameof(CurrentTheme));
            this.ThemeChanged?.Invoke(this, new EventArgs<eAppTheme>(osTheme));
        }
    }
}
