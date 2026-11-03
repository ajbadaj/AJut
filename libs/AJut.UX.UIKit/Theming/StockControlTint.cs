namespace AJut.UX.Theming
{
    using UIKit;

    /// <summary>
    /// Gives iOS's own controls the AJut palette: the AJut accent as each window's tint, which every tint-driven stock
    /// control takes (system buttons, sliders, progress bars, text carets and selection), and the AJut on color for every
    /// UISwitch. The colors are ThemedColors' dynamic colors, so they follow light and dark with nothing to refresh.
    /// </summary>
    /// <remarks>
    /// <see cref="AppThemeManager"/> runs it as it starts and gives every window it themes the accent; an app runs
    /// <see cref="Apply"/> again after turning the AJut theme or its touch palettes on or off, then rebuilds its views, as
    /// AJut's own UIKit controls need. A switch takes its appearance as it enters a window. A switch's thumb and its off
    /// track keep the system's look, since UIKit has no public way to color an off track. With no AJut theme loaded the
    /// switch goes back to the system's color, and a window's tint is left as the app set it.
    /// </remarks>
    public static class StockControlTint
    {
        // ===========[ Properties ]===================================
        /// <summary>The accent stock controls take with the AJut theme loaded; null without it, which leaves the system's.</summary>
        public static UIColor Accent => ThemedColors.Get(MobileLook.StockControls.Accent);

        // ===========[ Public Interface Methods ]===================================
        /// <summary>Gives every UISwitch made from now on the AJut on color, or the system's with no theme loaded</summary>
        public static void Apply ()
        {
            UISwitch.Appearance.OnTintColor = ThemedColors.Get(MobileLook.StockControls.SwitchTrackOn);
        }

        /// <summary>Gives <paramref name="window"/> the AJut accent as its tint, with the AJut theme loaded</summary>
        public static void ApplyTo (UIWindow window)
        {
            UIColor accent = Accent;
            if (accent != null)
            {
                window.TintColor = accent;
            }
        }
    }
}
