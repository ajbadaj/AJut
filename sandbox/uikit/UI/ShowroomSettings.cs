namespace AJutShowRoomUIKit.UI
{
    using AJut.UX.Theming;
    using UIKit;

    /// <summary>
    /// The look the theme bar picks, kept here rather than on the bar, since a theme or touch switch rebuilds the bar
    /// along with every other view. The AJut theme itself is <see cref="ThemedColors.IsThemeLoaded"/>.
    /// </summary>
    public static class ShowroomSettings
    {
        private const string kPanelBackgroundKey = "AJut_Brush_PanelBackground";

        // iOS's own answer for touch, read before the showroom changes it: an iOS UI is touch first
        private static readonly bool kSystemIsTouchFocused = ThemedColors.IsTouchFocused;

        private static eThemeMode g_themeMode = eThemeMode.System;
        private static bool g_touchChoice = kSystemIsTouchFocused;

        // ===========[ Properties ]===================================
        /// <summary>
        /// Dark or Light forces the showroom window's light or dark; System follows the device's, and takes iOS's answer
        /// for touch.
        /// </summary>
        public static eThemeMode ThemeMode
        {
            get => g_themeMode;
            set
            {
                g_themeMode = value;
                ThemedColors.IsTouchFocused = IsTouchFocused;
            }
        }

        /// <summary>
        /// Whether the AJut theme paints its touch variant. Under System it is iOS's answer (on); under Dark or Light it
        /// is the choice made on the bar, and setting it while System is picked changes nothing.
        /// </summary>
        public static bool IsTouchFocused
        {
            get => (g_themeMode == eThemeMode.System) ? kSystemIsTouchFocused : g_touchChoice;
            set
            {
                if (g_themeMode == eThemeMode.System)
                {
                    return;
                }

                g_touchChoice = value;
                ThemedColors.IsTouchFocused = value;
            }
        }

        /// <summary>The touch switch only means something with the AJut theme loaded and a light or dark picked.</summary>
        public static bool CanChooseTouch => ThemedColors.IsThemeLoaded && (g_themeMode != eThemeMode.System);

        /// <summary>
        /// What the pages and the theme bar sit on: the AJut panel background with the theme loaded, the way a themed
        /// WinUI3 window does, so the controls are judged against the surface the theme puts them on. Without the theme
        /// (the palette key has no unthemed default) it is the system background.
        /// </summary>
        public static UIColor PageBackground => ThemedColors.Get(kPanelBackgroundKey) ?? UIColor.SystemBackground;

        /// <summary>The window's interface style for <see cref="ThemeMode"/>.</summary>
        public static UIUserInterfaceStyle InterfaceStyle => g_themeMode switch
        {
            eThemeMode.Dark => UIUserInterfaceStyle.Dark,
            eThemeMode.Light => UIUserInterfaceStyle.Light,
            _ => UIUserInterfaceStyle.Unspecified,
        };

        // ===========[ Subclasses/structs ]===================================
        /// <summary>The light or dark the showroom shows, in the order the theme bar offers them.</summary>
        public enum eThemeMode
        {
            /// <summary>Dark, whatever the device is set to</summary>
            Dark,

            /// <summary>Light, whatever the device is set to</summary>
            Light,

            /// <summary>The device's own light or dark, and iOS's answer for touch</summary>
            System,
        }
    }
}
