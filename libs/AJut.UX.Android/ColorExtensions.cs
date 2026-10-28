namespace AJut.UX
{
    using AJut.UX.Helpers;
    using AndroidColor = Android.Graphics.Color;

    /// <summary>Moves colors between Android's <see cref="AndroidColor"/> and AJut.UX's toolkit-free <see cref="ColorArgb"/>.</summary>
    public static class ColorExtensions
    {
        public static ColorArgb ToColorArgb (this AndroidColor color) => new ColorArgb(color.A, color.R, color.G, color.B);

        public static AndroidColor ToAndroidColor (this ColorArgb color) => AndroidColor.Argb(color.A, color.R, color.G, color.B);
    }
}
