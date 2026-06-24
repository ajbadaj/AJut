namespace AJut.UX.Helpers
{
    using System;

    // Byte based color math, kept framework agnostic on purpose: WPF and WinUI3 each ship their own
    // Color type, so the real work happens here on A/R/G/B bytes and the per framework layers only
    // convert in and out. The parsing half of this helper lives in AJutColorHelper.cs.
    public static partial class AJutColorHelper
    {
        // 0.179 is the WCAG luminance crossover where white and black give equal contrast. Anchoring the
        // readable foregrounds at pure white / pure black keeps the worst case (a background sitting right
        // on the crossover) at a ~4.58 contrast ratio, so the pick always clears WCAG AA.
        private const double kReadableForegroundLuminanceCrossover = 0.179;
        private static readonly ColorArgb kReadableLight = new ColorArgb(255, 0xFF, 0xFF, 0xFF);
        private static readonly ColorArgb kReadableDark = new ColorArgb(255, 0x00, 0x00, 0x00);

        // ===========[ HSL Conversion ]===================================

        /// <summary>Converts an sRGB color to HSL. Alpha is ignored.</summary>
        public static HslColor RgbToHsl (ColorArgb color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double lightness = (max + min) / 2.0;

            double delta = max - min;
            if (delta == 0.0)
            {
                return new HslColor(0.0, 0.0, lightness);
            }

            double saturation = lightness > 0.5
                ? delta / (2.0 - max - min)
                : delta / (max + min);

            double hue;
            if (max == r)
            {
                hue = ((g - b) / delta) + (g < b ? 6.0 : 0.0);
            }
            else if (max == g)
            {
                hue = ((b - r) / delta) + 2.0;
            }
            else
            {
                hue = ((r - g) / delta) + 4.0;
            }

            hue *= 60.0;
            return new HslColor(hue, saturation, lightness);
        }

        /// <summary>Converts HSL back to sRGB, stamping on the given alpha.</summary>
        public static ColorArgb HslToRgb (HslColor hsl, byte alpha = 255)
        {
            double lightness = Math.Clamp(hsl.Lightness, 0.0, 1.0);
            double saturation = Math.Clamp(hsl.Saturation, 0.0, 1.0);

            if (saturation == 0.0)
            {
                byte gray = _ToByte(lightness);
                return new ColorArgb(alpha, gray, gray, gray);
            }

            double q = lightness < 0.5
                ? lightness * (1.0 + saturation)
                : lightness + saturation - (lightness * saturation);
            double p = (2.0 * lightness) - q;
            double h = hsl.Hue / 360.0;

            byte r = _ToByte(_HueToChannel(p, q, h + (1.0 / 3.0)));
            byte g = _ToByte(_HueToChannel(p, q, h));
            byte b = _ToByte(_HueToChannel(p, q, h - (1.0 / 3.0)));
            return new ColorArgb(alpha, r, g, b);

            static double _HueToChannel (double p, double q, double t)
            {
                if (t < 0.0) { t += 1.0; }
                if (t > 1.0) { t -= 1.0; }
                if (t < (1.0 / 6.0)) { return p + ((q - p) * 6.0 * t); }
                if (t < (1.0 / 2.0)) { return q; }
                if (t < (2.0 / 3.0)) { return p + ((q - p) * ((2.0 / 3.0) - t) * 6.0); }
                return p;
            }
        }

        // ===========[ Luminance / Contrast ]===================================

        /// <summary>WCAG relative luminance in [0, 1] (sRGB linearized). Alpha is ignored.</summary>
        public static double GetRelativeLuminance (ColorArgb color)
        {
            return (0.2126 * _Linearize(color.R))
                 + (0.7152 * _Linearize(color.G))
                 + (0.0722 * _Linearize(color.B));

            static double _Linearize (byte channel)
            {
                double s = channel / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }
        }

        /// <summary>WCAG contrast ratio between two colors, in [1, 21]. Alpha is ignored.</summary>
        public static double GetContrastRatio (ColorArgb first, ColorArgb second)
        {
            double l1 = GetRelativeLuminance(first);
            double l2 = GetRelativeLuminance(second);
            double lighter = Math.Max(l1, l2);
            double darker = Math.Min(l1, l2);
            return (lighter + 0.05) / (darker + 0.05);
        }

        /// <summary>True when a color is dark enough that a light foreground reads better than a dark one.</summary>
        public static bool IsDark (ColorArgb color) => GetRelativeLuminance(color) < kReadableForegroundLuminanceCrossover;

        /// <summary>
        /// Picks a foreground (near white or near black) that reads cleanly on the given background -
        /// whichever side of the WCAG crossover gives the higher contrast.
        /// </summary>
        public static ColorArgb PickReadableForeground (ColorArgb background) => IsDark(background) ? kReadableLight : kReadableDark;

        /// <summary>
        /// Like <see cref="PickReadableForeground(ColorArgb)"/>, but holds onto a preferred polarity so a
        /// background that only drifts slightly across the light/dark crossover does not flip the foreground.
        /// Pass the polarity decided from a base color (e.g. <see cref="IsDark"/> of an accent); the pick only
        /// flips away from it once the background passes the crossover by more than <paramref name="hysteresis"/>.
        /// </summary>
        public static ColorArgb PickReadableForeground (ColorArgb background, bool preferLight, double hysteresis)
        {
            double threshold = preferLight
                ? kReadableForegroundLuminanceCrossover + hysteresis
                : kReadableForegroundLuminanceCrossover - hysteresis;
            return GetRelativeLuminance(background) < threshold ? kReadableLight : kReadableDark;
        }

        // ===========[ Adjustments ]===================================

        /// <summary>Adds <paramref name="delta"/> to the HSL lightness (delta in [-1, 1]), preserving alpha.</summary>
        public static ColorArgb AdjustLightness (ColorArgb color, double delta)
        {
            HslColor hsl = RgbToHsl(color);
            return HslToRgb(hsl with { Lightness = Math.Clamp(hsl.Lightness + delta, 0.0, 1.0) }, color.A);
        }

        public static ColorArgb Lighten (ColorArgb color, double amount) => AdjustLightness(color, amount);
        public static ColorArgb Darken (ColorArgb color, double amount) => AdjustLightness(color, -amount);

        /// <summary>Multiplies HSL saturation by <paramref name="factor"/> (clamped to [0, 1]), preserving alpha.</summary>
        public static ColorArgb ScaleSaturation (ColorArgb color, double factor)
        {
            HslColor hsl = RgbToHsl(color);
            return HslToRgb(hsl with { Saturation = Math.Clamp(hsl.Saturation * factor, 0.0, 1.0) }, color.A);
        }

        /// <summary>Linear blend in sRGB (and alpha): t=0 returns <paramref name="from"/>, t=1 returns <paramref name="to"/>.</summary>
        public static ColorArgb Blend (ColorArgb from, ColorArgb to, double t)
        {
            t = Math.Clamp(t, 0.0, 1.0);
            return new ColorArgb(
                _Lerp(from.A, to.A, t),
                _Lerp(from.R, to.R, t),
                _Lerp(from.G, to.G, t),
                _Lerp(from.B, to.B, t)
            );

            static byte _Lerp (byte a, byte b, double t) => (byte)Math.Round(a + ((b - a) * t));
        }

        private static byte _ToByte (double normalized) => (byte)Math.Clamp((int)Math.Round(normalized * 255.0), 0, 255);
    }

    /// <summary>An A/R/G/B color as bytes - the framework neutral currency for AJut color math.</summary>
    public readonly record struct ColorArgb (byte A, byte R, byte G, byte B)
    {
        public static ColorArgb FromRgb (byte r, byte g, byte b) => new ColorArgb(255, r, g, b);
        public ColorArgb WithAlpha (byte alpha) => this with { A = alpha };
    }

    /// <summary>Hue [0, 360), Saturation [0, 1], Lightness [0, 1].</summary>
    public readonly record struct HslColor (double Hue, double Saturation, double Lightness);
}
