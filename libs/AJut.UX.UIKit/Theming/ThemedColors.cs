namespace AJut.UX.Theming
{
    using System.Collections.Generic;
    using AJut.UX.Helpers;
    using UIKit;

    /// <summary>
    /// The one place an AJut UIKit control gets a color it paints with, by the same key the WinUI3 theme uses for it
    /// (an AJut_&lt;Control&gt;_&lt;Concept&gt; key, or a stock key such as ButtonBackground that the AJut theme
    /// overrides). With no theme loaded a key gives its unthemed default, a UIKit system color that follows light and
    /// dark on its own. With the AJut theme loaded it gives the AJut palette's value, from AJut.UX's
    /// <see cref="AJutPalette"/>, joined into one dynamic color from the light and dark themes so it too follows the
    /// appearance with nothing to refresh.
    /// </summary>
    /// <remarks>
    /// This is UIKit's counterpart to WinUI3's three-file pattern: <see cref="ThemedColorKeys"/> holds the unthemed
    /// defaults (AJutCustomControlStylingDefaults.xaml), and the palette and its key aliases come from AJut.UX
    /// (AJutThemeMapAliasing.xaml and the color base files), so a key added to the theme once reaches every stack.
    /// </remarks>
    public static class ThemedColors
    {
        private static readonly Dictionary<string, UIColor> g_unthemedDefaults = new Dictionary<string, UIColor>();
        private static readonly Dictionary<string, UIColor> g_themedCache = new Dictionary<string, UIColor>();
        private static bool g_isThemeLoaded;

        // ===========[ Setup/Construction/Teardown ]===================================
        static ThemedColors ()
        {
            ThemedColorKeys.RegisterAll();
        }

        // ===========[ Properties ]===================================
        /// <summary>
        /// Whether the AJut theme is loaded. Set it once at startup, before the first AJut control is made, the way a
        /// WinUI3 app merges the theme in App.xaml: a control reads its colors when it is made, so controls that already
        /// exist keep the colors they were made with. To switch while running, set it and rebuild the views.
        /// </summary>
        public static bool IsThemeLoaded
        {
            get => g_isThemeLoaded;
            set => g_isThemeLoaded = value;
        }

        // ===========[ Public Interface Methods ]===================================
        /// <summary>
        /// The color to paint <paramref name="key"/> with: the AJut palette's value when the theme is loaded and the
        /// palette has the key, otherwise the key's unthemed default. Null when neither applies, which for some keys is
        /// the default on purpose: the caller then uses the view's TintColor, the way UIKit's own controls do.
        /// </summary>
        public static UIColor Get (string key)
        {
            if (g_isThemeLoaded && TryGetThemed(key, out UIColor themed))
            {
                return themed;
            }

            return g_unthemedDefaults.TryGetValue(key, out UIColor unthemed) ? unthemed : null;
        }

        /// <summary>The AJut palette's paint for <paramref name="key"/> in the given appearance (a solid color or a gradient), or null without the theme or the key.</summary>
        public static AJutThemePaint GetThemedPaint (string key, UIUserInterfaceStyle style)
        {
            if (g_isThemeLoaded && AJutPalette.For(style == UIUserInterfaceStyle.Dark).TryGetPaint(key, out AJutThemePaint paint))
            {
                return paint;
            }

            return null;
        }

        /// <summary>Sets the color <paramref name="key"/> gives with no theme loaded. Null means "use the view's TintColor".</summary>
        public static void SetUnthemedDefault (string key, UIColor color)
        {
            g_unthemedDefaults[key] = color;
        }

        // ===========[ Helper Methods ]===================================
        // A key the palette has in one theme but not the other uses its unthemed default for the missing appearance,
        // the same fallback every stack uses. A key with no default (one that follows the tint) borrows the other
        // theme's value instead, since there is no view here to read a tint from.
        private static bool TryGetThemed (string key, out UIColor themed)
        {
            if (g_themedCache.TryGetValue(key, out themed))
            {
                return true;
            }

            bool hasLight = AJutPalette.Light.TryGetColor(key, out ColorArgb light);
            bool hasDark = AJutPalette.Dark.TryGetColor(key, out ColorArgb dark);
            if (!hasLight && !hasDark)
            {
                themed = null;
                return false;
            }

            g_unthemedDefaults.TryGetValue(key, out UIColor unthemed);
            UIColor lightColor = hasLight ? light.ToUIColor() : (unthemed ?? dark.ToUIColor());
            UIColor darkColor = hasDark ? dark.ToUIColor() : (unthemed ?? light.ToUIColor());
            themed = UIColorXT.FromLightDark(lightColor, darkColor);
            g_themedCache[key] = themed;
            return true;
        }
    }
}
