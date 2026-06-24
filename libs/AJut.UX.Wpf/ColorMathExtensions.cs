namespace AJut.UX
{
    using AJut.UX.Helpers;
    using System.Windows.Media;

    /// <summary>
    /// WPF conveniences over the framework neutral color math in <see cref="AJutColorHelper"/>. These just
    /// translate a WPF <see cref="Color"/> in and out of <see cref="ColorArgb"/> so callers do not have to
    /// think in bytes.
    /// </summary>
    public static class ColorMathExtensions
    {
        public static ColorArgb ToColorArgb (this Color color) => new ColorArgb(color.A, color.R, color.G, color.B);
        public static Color ToMediaColor (this ColorArgb color) => Color.FromArgb(color.A, color.R, color.G, color.B);

        public static SolidColorBrush ToBrush (this ColorArgb color)
        {
            var brush = new SolidColorBrush(color.ToMediaColor());
            brush.Freeze();
            return brush;
        }

        public static Color PickReadableForeground (this Color background) => AJutColorHelper.PickReadableForeground(background.ToColorArgb()).ToMediaColor();
        public static Color Lighten (this Color color, double amount) => AJutColorHelper.Lighten(color.ToColorArgb(), amount).ToMediaColor();
        public static Color Darken (this Color color, double amount) => AJutColorHelper.Darken(color.ToColorArgb(), amount).ToMediaColor();
        public static double GetRelativeLuminance (this Color color) => AJutColorHelper.GetRelativeLuminance(color.ToColorArgb());
        public static double GetContrastRatio (this Color first, Color second) => AJutColorHelper.GetContrastRatio(first.ToColorArgb(), second.ToColorArgb());
        public static bool IsDark (this Color color) => AJutColorHelper.IsDark(color.ToColorArgb());
    }
}
