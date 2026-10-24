namespace AJut.UX.Theming
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using AJut.UX.Helpers;

    /// <summary>
    /// AJut's light and dark palettes as data, and their touch variants, for UI toolkits that have no XAML theme
    /// dictionaries to load them from (AppKit, UIKit, Android). The values are WinUI3's: LightThemeColorsBase.xaml,
    /// DarkThemeColorsBase.xaml, LightTouchThemeColorsBase.xaml and DarkTouchThemeColorsBase.xaml in AJut.UX.WinUI3,
    /// which stay the source of truth. A test reads those files and fails on any key here that is missing, extra or
    /// different, so the two cannot drift apart.
    /// </summary>
    /// <remarks>
    /// A toolkit package resolves a key with <c>AJutPalette.For(isDark, isTouch).TryGetColor(key, out ColorArgb color)</c>,
    /// and the key can be anything the WinUI3 theme defines: a palette key (AJut_Color_*, AJut_Brush_*), a control
    /// key (AJut_&lt;Control&gt;_&lt;Concept&gt;, see <see cref="AJutThemeKeys"/>), or one of the stock control keys
    /// the AJut theme overrides (ButtonBackground and the rest). What stays with each toolkit is the unthemed
    /// default for each key in its own color type, the switch that says whether the AJut theme is loaded, working
    /// out whether the UI is light or dark, and whether it is touch focused.
    ///
    /// The touch tables are for touch-first UI, which has no hover: a button has to read as one from its fill alone, so
    /// button surfaces stand further off the panels, a disabled button's surface does not, and button borders come off
    /// (they are transparent, not missing, so nothing shifts). They change only colors, never sizes.
    /// </remarks>
    public static partial class AJutPalette
    {
        private static readonly Table g_light = new Table("Light", BuildLightEntries());
        private static readonly Table g_dark = new Table("Dark", BuildDarkEntries());
        private static readonly Table g_lightTouch = new Table("LightTouch", LayOver(BuildLightEntries(), BuildLightTouchOverrides()));
        private static readonly Table g_darkTouch = new Table("DarkTouch", LayOver(BuildDarkEntries(), BuildDarkTouchOverrides()));

        // ===========[ Properties ]===================================
        /// <summary>The AJut light theme's values.</summary>
        public static Table Light => g_light;

        /// <summary>The AJut dark theme's values.</summary>
        public static Table Dark => g_dark;

        /// <summary>The AJut light theme's values for touch-focused UI: the light values, with enabled button surfaces a neutral step darker than the panels and button borders off.</summary>
        public static Table LightTouch => g_lightTouch;

        /// <summary>The AJut dark theme's values for touch-focused UI: the dark values, with enabled button surfaces lifted, button borders off and text brighter.</summary>
        public static Table DarkTouch => g_darkTouch;

        // ===========[ Public Interface Methods ]===================================
        /// <summary>The dark table when <paramref name="isDark"/>, otherwise the light one.</summary>
        public static Table For (bool isDark) => isDark ? g_dark : g_light;

        /// <summary>The table for a light or dark UI, in its touch variant when <paramref name="isTouch"/>.</summary>
        public static Table For (bool isDark, bool isTouch)
        {
            if (isTouch)
            {
                return isDark ? g_darkTouch : g_lightTouch;
            }

            return isDark ? g_dark : g_light;
        }

        // ===========[ Helper Methods ]===================================
        // A touch palette is its base palette with the touch entries in place of the base's
        private static Dictionary<string, AJutThemeEntry> LayOver (Dictionary<string, AJutThemeEntry> entries, Dictionary<string, AJutThemeEntry> overrides)
        {
            foreach (KeyValuePair<string, AJutThemeEntry> pair in overrides)
            {
                entries[pair.Key] = pair.Value;
            }

            return entries;
        }

        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// One theme's values (light or dark, or either one's touch variant), and the lookup that turns any themed key into what to paint with.
        /// </summary>
        public sealed class Table
        {
            private readonly Dictionary<string, AJutThemeEntry> m_entries;

            // ===========[ Setup/Construction/Teardown ]===================================
            internal Table (string themeName, Dictionary<string, AJutThemeEntry> entries)
            {
                this.ThemeName = themeName;
                m_entries = entries;
            }

            // ===========[ Properties ]===================================
            /// <summary>"Light", "Dark", "LightTouch" or "DarkTouch", for the WinUI3 palette file this table mirrors.</summary>
            public string ThemeName { get; }

            /// <summary>The palette keys this theme defines (AJut_Color_*, AJut_Brush_* and AJut_Double_*), not the control keys that alias them.</summary>
            public IEnumerable<string> Keys => m_entries.Keys;

            // ===========[ Public Interface Methods ]===================================
            /// <summary>
            /// Resolves <paramref name="key"/> to a solid color in this theme, following aliases and applying any brush
            /// opacity on the way to the alpha. Returns false when the key is unknown here, or names a gradient or a
            /// number; the toolkit then paints its unthemed default for the key, the same on every stack.
            /// </summary>
            public bool TryGetColor (string key, out ColorArgb color)
            {
                if (this.TryGetPaint(key, out AJutThemePaint? paint) && !paint.IsGradient)
                {
                    color = paint.Color;
                    return true;
                }

                color = default;
                return false;
            }

            /// <summary>
            /// Resolves <paramref name="key"/> to what it paints in this theme, a solid color or a top to bottom gradient,
            /// following aliases and applying any brush opacity on the way. Returns false when the key is unknown here or
            /// names a number.
            /// </summary>
            public bool TryGetPaint (string key, [NotNullWhen(true)] out AJutThemePaint? paint)
            {
                // Real chains are two or three links (control key, palette brush, palette color). Anything longer is a
                // loop in the data, which the in-step test would also catch; this just stops a lookup from spinning.
                const int kMaxAliasDepth = 8;

                double opacity = 1.0;
                string current = key;
                for (int depth = 0; depth < kMaxAliasDepth; ++depth)
                {
                    if (!this.TryFindEntry(current, out AJutThemeEntry? entry))
                    {
                        break;
                    }

                    opacity *= entry.Opacity;
                    if (entry.AliasKey != null)
                    {
                        current = entry.AliasKey;
                        continue;
                    }

                    if (entry.Paint != null)
                    {
                        paint = entry.Paint.WithOpacity(opacity);
                        return true;
                    }

                    break;
                }

                paint = null;
                return false;
            }

            /// <summary>Resolves one of the palette's numbers (AJut_Double_StandardDisabledOpacity and the like).</summary>
            public bool TryGetDouble (string key, out double value)
            {
                if (m_entries.TryGetValue(key, out AJutThemeEntry? entry) && entry.Number.HasValue)
                {
                    value = entry.Number.Value;
                    return true;
                }

                value = 0.0;
                return false;
            }

            // ===========[ Helper Methods ]===================================
            // A key is this theme's own palette entry, or one of the theme independent control and stock keys.
            private bool TryFindEntry (string key, [NotNullWhen(true)] out AJutThemeEntry? entry)
            {
                return m_entries.TryGetValue(key, out entry)
                    || AJutThemeKeys.TryGetEntry(key, out entry);
            }
        }
    }

    /// <summary>What a themed key paints with: a solid color, or a gradient running from the top edge (offset 0) to the bottom edge (offset 1).</summary>
    public sealed class AJutThemePaint
    {
        // ===========[ Setup/Construction/Teardown ]===================================
        internal AJutThemePaint (ColorArgb color)
        {
            this.Color = color;
            this.GradientStops = Array.Empty<AJutGradientStop>();
        }

        internal AJutThemePaint (IReadOnlyList<AJutGradientStop> gradientStops)
        {
            this.GradientStops = gradientStops;
            this.IsGradient = true;
        }

        // ===========[ Properties ]===================================
        /// <summary>True for a gradient (read <see cref="GradientStops"/>); false for a solid color (read <see cref="Color"/>).</summary>
        public bool IsGradient { get; }

        /// <summary>The color of a solid paint; default for a gradient.</summary>
        public ColorArgb Color { get; }

        /// <summary>A gradient's stops, top to bottom; empty for a solid paint.</summary>
        public IReadOnlyList<AJutGradientStop> GradientStops { get; }

        // ===========[ Public Interface Methods ]===================================
        /// <summary>This paint with every color's alpha scaled by <paramref name="opacity"/>, the way a XAML brush's Opacity applies.</summary>
        public AJutThemePaint WithOpacity (double opacity)
        {
            if (opacity >= 1.0)
            {
                return this;
            }

            if (!this.IsGradient)
            {
                return new AJutThemePaint(_ScaleAlpha(this.Color, opacity));
            }

            var stops = new AJutGradientStop[this.GradientStops.Count];
            for (int index = 0; index < stops.Length; ++index)
            {
                AJutGradientStop stop = this.GradientStops[index];
                stops[index] = new AJutGradientStop(stop.Offset, _ScaleAlpha(stop.Color, opacity));
            }

            return new AJutThemePaint(stops);
        }

        // ===========[ Helper Methods ]===================================
        private static ColorArgb _ScaleAlpha (ColorArgb color, double opacity)
        {
            return color.WithAlpha((byte)Math.Round(color.A * Math.Clamp(opacity, 0.0, 1.0)));
        }
    }

    /// <summary>One stop of a top to bottom gradient: offset 0 is the top edge and 1 the bottom.</summary>
    public readonly record struct AJutGradientStop (double Offset, ColorArgb Color);

    /// <summary>
    /// One themed key's definition, the way the XAML states it: a value (a paint or a number), or an alias to
    /// another key with an opacity applied on top. The palette tables and <see cref="AJutThemeKeys"/> are built
    /// from these, through the static helpers below, which read the same as the XAML they mirror.
    /// </summary>
    internal sealed class AJutThemeEntry
    {
        // ===========[ Setup/Construction/Teardown ]===================================
        private AJutThemeEntry (AJutThemePaint? paint, string? aliasKey, double opacity, double? number)
        {
            this.Paint = paint;
            this.AliasKey = aliasKey;
            this.Opacity = opacity;
            this.Number = number;
        }

        // ===========[ Properties ]===================================
        public AJutThemePaint? Paint { get; }

        public string? AliasKey { get; }

        public double Opacity { get; }

        public double? Number { get; }

        // ===========[ Public Interface Methods ]===================================
        /// <summary>A solid color, from 0xAARRGGBB.</summary>
        public static AJutThemeEntry Solid (uint argb, double opacity = 1.0) => new AJutThemeEntry(new AJutThemePaint(_Unpack(argb)), null, opacity, null);

        /// <summary>A top to bottom gradient.</summary>
        public static AJutThemeEntry Gradient (params AJutGradientStop[] stops) => new AJutThemeEntry(new AJutThemePaint(stops), null, 1.0, null);

        /// <summary>One gradient stop, its color from 0xAARRGGBB.</summary>
        public static AJutGradientStop Stop (double offset, uint argb) => new AJutGradientStop(offset, _Unpack(argb));

        /// <summary>Whatever <paramref name="key"/> resolves to, with <paramref name="opacity"/> applied on top.</summary>
        public static AJutThemeEntry Alias (string key, double opacity = 1.0) => new AJutThemeEntry(null, key, opacity, null);

        /// <summary>A number, such as a standard opacity (an x:Double in the XAML).</summary>
        public static AJutThemeEntry Double (double value) => new AJutThemeEntry(null, null, 1.0, value);

        // ===========[ Helper Methods ]===================================
        private static ColorArgb _Unpack (uint argb) => new ColorArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
    }
}
