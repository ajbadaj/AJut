namespace AJut.UX
{
    using AJut.UX.Helpers;
    using Microsoft.UI.Xaml.Media;
    using Windows.UI;

    /// <summary>
    /// WinUI3 conveniences over the framework neutral color math in <see cref="AJutColorHelper"/>. These
    /// just translate a WinUI <see cref="Color"/> in and out of <see cref="ColorArgb"/> so callers do not
    /// have to think in bytes.
    /// </summary>
    public static class ColorMathExtensions
    {
        public static ColorArgb ToColorArgb (this Color color) => new ColorArgb(color.A, color.R, color.G, color.B);
        public static Color ToWindowsColor (this ColorArgb color) => new Color { A = color.A, R = color.R, G = color.G, B = color.B };
        public static SolidColorBrush ToBrush (this ColorArgb color) => new SolidColorBrush(color.ToWindowsColor());

        public static Color PickReadableForeground (this Color background) => AJutColorHelper.PickReadableForeground(background.ToColorArgb()).ToWindowsColor();
        public static Color Lighten (this Color color, double amount) => AJutColorHelper.Lighten(color.ToColorArgb(), amount).ToWindowsColor();
        public static Color Darken (this Color color, double amount) => AJutColorHelper.Darken(color.ToColorArgb(), amount).ToWindowsColor();
        public static double GetRelativeLuminance (this Color color) => AJutColorHelper.GetRelativeLuminance(color.ToColorArgb());
        public static double GetContrastRatio (this Color first, Color second) => AJutColorHelper.GetContrastRatio(first.ToColorArgb(), second.ToColorArgb());
        public static bool IsDark (this Color color) => AJutColorHelper.IsDark(color.ToColorArgb());
    }
}
