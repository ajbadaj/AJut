namespace AJut.UX.Theming
{
    using System.Collections.Generic;
    using AJut.UX.Helpers;
    using UIKit;

    /// <summary>
    /// A themed paint that is a gradient: the key it is known by (the WinUI3 one, such as
    /// AJut_ToggleStripButton_CheckedBackground), and the top to bottom stops it falls back to with no AJut theme
    /// loaded, one set for light and one for dark. With the theme loaded the AJut palette's paint for the key is used
    /// instead, whether that is a gradient or a solid color. It is the gradient counterpart to
    /// <see cref="ThemedColors.Get"/>, for the few keys a single color cannot carry.
    /// </summary>
    public sealed class ThemedGradient
    {
        private readonly IReadOnlyList<AJutGradientStop> m_unthemedLight;
        private readonly IReadOnlyList<AJutGradientStop> m_unthemedDark;

        // ===========[ Setup/Construction/Teardown ]===================================
        public ThemedGradient (string key, IReadOnlyList<AJutGradientStop> unthemedLight, IReadOnlyList<AJutGradientStop> unthemedDark)
        {
            this.Key = key;
            m_unthemedLight = unthemedLight;
            m_unthemedDark = unthemedDark;
        }

        // ===========[ Properties ]===================================
        public string Key { get; }

        // ===========[ Public Interface Methods ]===================================
        /// <summary>A stop at <paramref name="offset"/> (0 is the top) from a 0xAARRGGBB literal, the form the WinUI3 defaults are written in.</summary>
        public static AJutGradientStop Stop (double offset, uint argb)
        {
            return new AJutGradientStop(offset, new ColorArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
        }

        /// <summary>
        /// The stops to paint for the given appearance: the AJut palette's when the theme is loaded and has the key (a
        /// solid palette color comes back as two equal stops), otherwise the unthemed ones.
        /// </summary>
        public IReadOnlyList<AJutGradientStop> Resolve (UIUserInterfaceStyle style)
        {
            AJutThemePaint paint = ThemedColors.GetThemedPaint(this.Key, style);
            if (paint != null)
            {
                return paint.IsGradient
                    ? paint.GradientStops
                    : new[] { new AJutGradientStop(0.0, paint.Color), new AJutGradientStop(1.0, paint.Color) };
            }

            return style == UIUserInterfaceStyle.Dark ? m_unthemedDark : m_unthemedLight;
        }
    }
}
