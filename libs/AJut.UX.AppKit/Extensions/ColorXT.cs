namespace AJut.UX
{
    using System;
    using AJut.UX.Helpers;
    using AppKit;

    /// <summary>Moving colors between AJut.UX's <see cref="ColorArgb"/> and AppKit's NSColor</summary>
    public static class ColorXT
    {
        private const double kByteMax = 255.0;

        /// <summary>
        /// An sRGB NSColor from a <see cref="ColorArgb"/>. AJut's palette values and color math are sRGB, the same as
        /// the Windows themes they come from, so they go in as sRGB rather than through the device color space.
        /// </summary>
        public static NSColor ToNSColor (this ColorArgb color)
        {
            return NSColor.FromSrgb(
                (nfloat)(color.R / kByteMax),
                (nfloat)(color.G / kByteMax),
                (nfloat)(color.B / kByteMax),
                (nfloat)(color.A / kByteMax)
            );
        }

        /// <summary>
        /// Reads an NSColor as sRGB bytes. A dynamic or system color resolves under the current drawing appearance,
        /// so call this while drawing (DrawRect, UpdateLayer) to get the color the view is actually showing. Returns
        /// false for a color with no sRGB equivalent, such as a pattern.
        /// </summary>
        public static bool TryToColorArgb (this NSColor color, out ColorArgb argb)
        {
            NSColor srgb = color?.UsingColorSpace(NSColorSpace.SRGBColorSpace);
            if (srgb == null)
            {
                argb = default;
                return false;
            }

            argb = new ColorArgb(ToByte(srgb.AlphaComponent), ToByte(srgb.RedComponent), ToByte(srgb.GreenComponent), ToByte(srgb.BlueComponent));
            return true;

            static byte ToByte (nfloat component) => (byte)Math.Round(Math.Clamp((double)component, 0.0, 1.0) * kByteMax);
        }
    }
}
