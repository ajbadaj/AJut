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
        // Android's own answer for touch, read before the showroom changes it: an Android UI is touch first
        private static readonly bool kSystemIsTouchFocused = ThemeColors.IsTouchFocused;

        private eThemeMode m_themeMode = eThemeMode.System;
        private bool m_useAJutTheme;
        private bool m_isTouchFocusedChoice = kSystemIsTouchFocused;

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
                    ThemeColors.IsTouchFocused = this.IsTouchFocused;
                    this.RaisePropertiesChanged(nameof(IsTouchFocused), nameof(CanChooseTouch));
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
        public bool IsTouchFocused
        {
            get => (m_themeMode == eThemeMode.System) ? kSystemIsTouchFocused : m_isTouchFocusedChoice;
            set
            {
                if ((m_themeMode == eThemeMode.System) || (m_isTouchFocusedChoice == value))
                {
                    return;
                }

                m_isTouchFocusedChoice = value;
                ThemeColors.IsTouchFocused = value;
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
