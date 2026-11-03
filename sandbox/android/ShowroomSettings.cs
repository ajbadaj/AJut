namespace AJutShowRoomAndroid
{
    using AJut;
    using AJut.UX.Theming;

    /// <summary>
    /// The showroom's own settings, as a model the theme bar binds to. One for the life of the app. Android applies a
    /// theme as views are made, so the shell recreates the activity after any of these change.
    /// </summary>
    public sealed class ShowroomSettings : NotifyPropertyChanged
    {
        // AJut's own default for the touch variant, read before the showroom changes it: off, as on every stack
        private static readonly bool kSystemUsesTouchVariant = ThemeColors.UsesTouchVariant;

        private eThemeMode m_themeMode = eThemeMode.System;
        private bool m_useAJutTheme;
        private bool m_usesTouchVariantChoice = kSystemUsesTouchVariant;

        // ===========[ Properties ]===================================
        public static ShowroomSettings Instance { get; } = new ShowroomSettings();

        /// <summary>Dark or Light forces the app's light or dark; System follows the device's, and Android's touch answer.</summary>
        public eThemeMode ThemeMode
        {
            get => m_themeMode;
            set
            {
                if (this.SetAndRaiseIfChanged(ref m_themeMode, value))
                {
                    ThemeColors.UsesTouchVariant = this.UsesTouchVariant;
                    this.RaisePropertiesChanged(nameof(UsesTouchVariant), nameof(CanChooseTouch));
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
        /// Whether the AJut theme paints its touch variant. Under System it is Android's answer (on); under Dark or
        /// Light it is the choice made here, and setting it while System is picked changes nothing.
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

        // ===========[ Subclasses/structs ]===================================
        /// <summary>The light or dark the showroom shows, in the order the theme bar offers them.</summary>
        public enum eThemeMode
        {
            /// <summary>Dark, whatever the device is set to</summary>
            Dark,

            /// <summary>Light, whatever the device is set to</summary>
            Light,

            /// <summary>The device's own light or dark, and Android's touch answer</summary>
            System,
        }
    }
}
