namespace AJutShowRoomAndroid
{
    using AJut;
    using AJut.UX.Theming;
    using Android.App;

    /// <summary>
    /// The showroom's own settings, as a model the theme bar binds to. One for the life of the app. Android applies a
    /// theme as views are made, so the activity is recreated after any of these change: by the app's
    /// <see cref="AppThemeManager"/> for a light or dark switch, the way every AJut showroom picks light or dark, and by
    /// the shell for the others.
    /// </summary>
    public sealed class ShowroomSettings : NotifyPropertyChanged
    {
        // AJut's own default for the touch variant, read before the showroom changes it: off, as on every stack
        private static readonly bool kSystemUsesTouchVariant = ThemeColors.UsesTouchVariant;

        private eThemeMode m_themeMode = eThemeMode.System;
        private bool m_useAJutTheme;
        private bool m_usesTouchVariantChoice = kSystemUsesTouchVariant;
        private AppThemeManager? m_theme;

        // ===========[ Properties ]===================================
        public static ShowroomSettings Instance { get; } = new ShowroomSettings();

        /// <summary>
        /// The app's theme manager, made by the first <see cref="ApplyTheme"/> and kept for the life of the app. It keeps
        /// nothing between launches, so a launch's extras always decide the look.
        /// </summary>
        public AppThemeManager? Theme => m_theme;

        /// <summary>Dark or Light forces the app's light or dark; System follows the device's, with AJut's touch default.</summary>
        public eThemeMode ThemeMode
        {
            get => m_themeMode;
            set
            {
                if (this.SetAndRaiseIfChanged(ref m_themeMode, value))
                {
                    ThemeColors.UsesTouchVariant = this.UsesTouchVariant;
                    this.RaisePropertiesChanged(nameof(UsesTouchVariant), nameof(CanChooseTouch));
                    if (m_theme != null)
                    {
                        m_theme.ThemeConfiguration = ToConfiguration(value);
                    }
                }
            }
        }

        /// <summary>Whether AJut controls paint the AJut palette rather than their plain defaults.</summary>
        public bool UseAJutTheme
        {
            get => m_useAJutTheme;
            set
            {
                if (this.SetAndRaiseIfChanged(ref m_useAJutTheme, value))
                {
                    ThemeColors.IsThemeLoaded = value;
                    this.RaisePropertyChanged(nameof(CanChooseTouch));
                }
            }
        }

        /// <summary>
        /// Whether the AJut theme paints its touch variant. Under System it is AJut's default (off); under Dark or Light
        /// it is the choice made here, and setting it while System is picked changes nothing.
        /// </summary>
        public bool UsesTouchVariant
        {
            get => (m_themeMode == eThemeMode.System) ? kSystemUsesTouchVariant : m_usesTouchVariantChoice;
            set
            {
                if ((m_themeMode == eThemeMode.System) || (m_usesTouchVariantChoice == value))
                {
                    return;
                }

                m_usesTouchVariantChoice = value;
                ThemeColors.UsesTouchVariant = value;
                this.RaisePropertyChanged();
            }
        }

        /// <summary>The touch switch only means something with the AJut theme on and a light or dark picked.</summary>
        public bool CanChooseTouch => m_useAJutTheme && (m_themeMode != eThemeMode.System);

        // ===========[ Public Interface Methods ]===================================
        /// <summary>
        /// Gives <paramref name="activity"/> the light or dark picked; call it from the activity's AttachBaseContext. The
        /// first call makes the app's <see cref="Theme"/>.
        /// </summary>
        public void ApplyTheme (Activity activity)
        {
            m_theme ??= new AppThemeManager(activity, ToConfiguration(m_themeMode), persistInSharedPreferences: false);
            m_theme.ApplyTo(activity);
        }

        // ===========[ Helper Methods ]===================================
        private static eAppThemeConfiguration ToConfiguration (eThemeMode mode)
        {
            switch (mode)
            {
                case eThemeMode.Dark: return eAppThemeConfiguration.Dark;
                case eThemeMode.Light: return eAppThemeConfiguration.Light;
                default: return eAppThemeConfiguration.UseSameAsOS;
            }
        }

        // ===========[ Subclasses/structs ]===================================
        /// <summary>The light or dark the showroom shows, in the order the theme bar offers them.</summary>
        public enum eThemeMode
        {
            /// <summary>Dark, whatever the device is set to</summary>
            Dark,

            /// <summary>Light, whatever the device is set to</summary>
            Light,

            /// <summary>The device's own light or dark, with AJut's touch default</summary>
            System,
        }
    }
}
