namespace AJut.UX.Theming
{
    using System;
    using System.Collections.Generic;
    using AJut.UX.Helpers;
    using Android.Content;
    using Android.Content.Res;
    using Android.Graphics.Drawables;

    /// <summary>
    /// One color an AJut control paints with: the theme key it is known by, and the literal colors it falls back
    /// to with no AJut theme loaded, one for a light UI and one for a dark one. The keys are the WinUI3 resource keys
    /// (the <see cref="AJutThemeKeys"/> constants, or a stock key such as ButtonBackground that AJut's theme
    /// overrides), so a themed Android control paints the same palette entry its WinUI3 twin does.
    /// </summary>
    public readonly record struct ThemedColor (string Key, ColorArgb Light, ColorArgb Dark)
    {
        /// <summary>A themed color from two 0xAARRGGBB literals, which is how the defaults read best in a control.</summary>
        public static ThemedColor FromArgb (string key, uint light, uint dark) => new ThemedColor(key, _Unpack(light), _Unpack(dark));

        private static ColorArgb _Unpack (uint argb) => new ColorArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
    }

    /// <summary>
    /// A themed paint that is a top to bottom gradient by default, such as the checked look of a toggle strip button:
    /// the theme key, and the literal stops for a light and a dark UI with no AJut theme loaded. With the theme loaded,
    /// the key may resolve to a gradient or to a solid color, and either is painted as it comes.
    /// </summary>
    public sealed record ThemedGradient (string Key, IReadOnlyList<AJutGradientStop> Light, IReadOnlyList<AJutGradientStop> Dark)
    {
        /// <summary>One literal stop, its color from 0xAARRGGBB, for building a default.</summary>
        public static AJutGradientStop Stop (double offset, uint argb) => new AJutGradientStop(offset, new ColorArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
    }

    /// <summary>
    /// The one place an AJut Android control turns a <see cref="ThemedColor"/> into the color it paints. With no AJut
    /// theme loaded a control paints its literal defaults, picked by the context's night mode, so it looks sane in
    /// light and dark with nothing set up. With the AJut theme loaded it paints the AJut palette (from
    /// <see cref="AJutPalette"/>, shared with every other toolkit), falling back to the literal for any key the
    /// palette has no solid color for.
    /// </summary>
    public static class ThemeColors
    {
        // ===========[ Properties ]===================================
        /// <summary>
        /// Whether AJut controls paint the AJut palette rather than their plain defaults. Set it before the views are
        /// made, in Application.OnCreate or before an activity's SetContentView. Views read it as they paint, so to
        /// switch it while views are showing, recreate the activity afterward (Activity.Recreate), the same as any
        /// Android theme change.
        /// </summary>
        public static bool IsThemeLoaded { get; set; }

        /// <summary>
        /// Whether the AJut palette is painted in its touch variant (<see cref="AJutPalette.LightTouch"/> or
        /// <see cref="AJutPalette.DarkTouch"/>), where buttons read as buttons from their fill alone, with no hover to
        /// show it and no borders. On by default, since an Android UI is touch first; it only matters while
        /// <see cref="IsThemeLoaded"/>. Set and switched the same way as <see cref="IsThemeLoaded"/>.
        /// </summary>
        public static bool IsTouchFocused { get; set; } = true;

        // ===========[ Public Interface Methods ]===================================
        /// <summary>True when <paramref name="context"/> is showing a dark UI (its configuration's night mode is on).</summary>
        public static bool IsDark (Context context)
        {
            Configuration? configuration = context.Resources?.Configuration;
            return configuration != null
                && ((configuration.UiMode & UiMode.NightMask) == UiMode.NightYes);
        }

        /// <summary>The color to paint for <paramref name="color"/> in <paramref name="context"/>.</summary>
        public static ColorArgb Resolve (Context context, ThemedColor color)
        {
            bool isDark = IsDark(context);
            if (IsThemeLoaded && AJutPalette.For(isDark, IsTouchFocused).TryGetColor(color.Key, out ColorArgb themed))
            {
                return themed;
            }

            return isDark ? color.Dark : color.Light;
        }

        /// <summary>The stops to paint for <paramref name="gradient"/> in <paramref name="context"/>, top to bottom; a themed solid color comes back as one stop.</summary>
        public static IReadOnlyList<AJutGradientStop> Resolve (Context context, ThemedGradient gradient)
        {
            bool isDark = IsDark(context);
            if (IsThemeLoaded && AJutPalette.For(isDark, IsTouchFocused).TryGetPaint(gradient.Key, out AJutThemePaint? themed))
            {
                return themed.IsGradient ? themed.GradientStops : new[] { new AJutGradientStop(0.0, themed.Color) };
            }

            return isDark ? gradient.Dark : gradient.Light;
        }

        /// <summary>A drawable that paints <paramref name="stops"/> top to bottom (or one solid color), with the given corner radii.</summary>
        public static GradientDrawable ToDrawable (IReadOnlyList<AJutGradientStop> stops, float[]? cornerRadii = null)
        {
            var drawable = new GradientDrawable();
            if (stops.Count == 1)
            {
                drawable.SetColor(stops[0].Color.ToAndroidColor());
            }
            else
            {
                drawable.SetOrientation(GradientDrawable.Orientation.TopBottom!);
                var colors = new int[stops.Count];
                var offsets = new float[stops.Count];
                for (int index = 0; index < stops.Count; ++index)
                {
                    colors[index] = stops[index].Color.ToAndroidColor().ToArgb();
                    offsets[index] = (float)stops[index].Offset;
                }

                if (OperatingSystem.IsAndroidVersionAtLeast(29))
                {
                    drawable.SetColors(colors, offsets);
                }
                else
                {
                    // Before Android 10 a gradient drawable spaces its colors evenly; close enough for a button's shading.
                    drawable.SetColors(colors);
                }
            }

            if (cornerRadii != null)
            {
                drawable.SetCornerRadii(cornerRadii);
            }

            return drawable;
        }
    }
}
