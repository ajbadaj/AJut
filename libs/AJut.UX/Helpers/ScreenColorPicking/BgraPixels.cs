namespace AJut.UX.Helpers
{
    /// <summary>Reads pixels out of a 32 bit BGRA capture, the layout a GDI screen capture lands in</summary>
    internal static class BgraPixels
    {
        /// <summary>Bytes per pixel in a 32 bit capture</summary>
        public const int kBytesPerPixel = 4;

        /// <summary>
        /// The color of the pixel at <paramref name="x"/>, <paramref name="y"/>, always opaque. A screen capture's fourth
        /// byte is not an alpha: GDI leaves it at whatever it likes (often zero), and what is on screen has no
        /// translucency left to speak of anyway.
        /// </summary>
        public static ColorArgb ReadOpaque (ReadOnlySpan<byte> pixels, int stride, int x, int y)
        {
            int index = (y * stride) + (x * kBytesPerPixel);
            return ColorArgb.FromRgb(pixels[index + 2], pixels[index + 1], pixels[index]);
        }
    }
}
