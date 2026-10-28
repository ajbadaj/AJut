namespace AJut.UX
{
    using System;
    using AJut.UX.Helpers;
    using UIKit;

    /// <summary>
    /// UIKit conveniences over the framework neutral color math in <see cref="AJutColorHelper"/>. These
    /// translate a <see cref="UIColor"/> in and out of <see cref="ColorArgb"/> so callers do not have to
    /// think in bytes.
    /// </summary>
    public static class ColorMathExtensions
    {
        private static readonly UITraitCollection kLightTraits = UITraitCollection.FromUserInterfaceStyle(UIUserInterfaceStyle.Light);
        private static readonly UITraitCollection kDarkTraits = UITraitCollection.FromUserInterfaceStyle(UIUserInterfaceStyle.Dark);

        /// <summary>
        /// Reads the color's sRGB bytes. A dynamic color (any of the UIKit system colors, or one made by
        /// <see cref="UIColorXT.FromLightDark"/>) is read as whatever it currently resolves to, which is
        /// rarely what you want - use the overload that takes a <see cref="UIUserInterfaceStyle"/> for those.
        /// </summary>
        public static ColorArgb ToColorArgb (this UIColor color)
        {
            color.GetRGBA(out nfloat r, out nfloat g, out nfloat b, out nfloat a);
            return new ColorArgb(_ToByte(a), _ToByte(r), _ToByte(g), _ToByte(b));

            // Extended sRGB components can sit outside 0 to 1 (wide gamut colors), so clamp before scaling.
            static byte _ToByte (nfloat component) => (byte)Math.Round(Math.Clamp((double)component, 0.0, 1.0) * 255.0);
        }

        /// <summary>Resolves a (possibly dynamic) color for the light or dark appearance, then reads its sRGB bytes.</summary>
        public static ColorArgb ToColorArgb (this UIColor color, UIUserInterfaceStyle style)
        {
            UITraitCollection traits = style == UIUserInterfaceStyle.Dark ? kDarkTraits : kLightTraits;
            return color.GetResolvedColor(traits).ToColorArgb();
        }

        public static UIColor ToUIColor (this ColorArgb color) => UIColor.FromRGBA(color.R, color.G, color.B, color.A);

        public static UIColor PickReadableForeground (this UIColor background) => AJutColorHelper.PickReadableForeground(background.ToColorArgb()).ToUIColor();
        public static UIColor Lighten (this UIColor color, double amount) => AJutColorHelper.Lighten(color.ToColorArgb(), amount).ToUIColor();
        public static UIColor Darken (this UIColor color, double amount) => AJutColorHelper.Darken(color.ToColorArgb(), amount).ToUIColor();
        public static double GetRelativeLuminance (this UIColor color) => AJutColorHelper.GetRelativeLuminance(color.ToColorArgb());
        public static double GetContrastRatio (this UIColor first, UIColor second) => AJutColorHelper.GetContrastRatio(first.ToColorArgb(), second.ToColorArgb());
        public static bool IsDark (this UIColor color) => AJutColorHelper.IsDark(color.ToColorArgb());
    }
}
