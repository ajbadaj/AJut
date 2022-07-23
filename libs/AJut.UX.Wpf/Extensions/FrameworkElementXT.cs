namespace AJut.UX
{
    using System;
    using System.IO;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    public static class FrameworkElementXT
    {

        /// <summary>
        /// Render the <see cref="FrameworkElement"/> to a png memory stream targeting output size in inches.
        /// </summary>
        /// <remarks>
        /// If width is specified and height isn't, then the height will be specified proportionally. Similarly if the height is specified and the width isn't, then the width will be set proportionally.
        /// </remarks>
        /// <param name="This">The target <see cref="FrameworkElement"/></param>
        /// <param name="targetWidthInches">The width of the resulting png in specified in inches (default = <see cref="double.NaN"/>, which will use image width to determine output)</param>
        /// <param name="targetHeightInches">The height of the resulting png in specified in inches (default = <see cref="double.NaN"/>, which will use image height to determine output)</param>
        /// <param name="format">The pixel format to render in, (default = null which results in <see cref="PixelFormats.Default"/>)</param>
        /// <returns>The <see cref="MemoryStream"/> containing the png data (Could create an <see cref="Image"/> or file with it)</returns>
        public static MemoryStream RenderToPngInInches (this FrameworkElement This, double targetWidthInches = double.NaN, double targetHeightInches = double.NaN, PixelFormat? format = null)
        {
            Size targetPx = GetSizeInInches(This, targetWidthInches, targetHeightInches);
            return RenderToPng(This, (int)targetPx.Width, (int)targetPx.Height, format ?? PixelFormats.Default);
        }

        /// <summary>
        /// Render the <see cref="FrameworkElement"/> to a png memory stream.
        /// </summary>
        /// <param name="This">The target <see cref="FrameworkElement"/></param>
        /// <param name="targetWidthPx">The width of the resulting png in pixels (default = -1, which is simply the image width)</param>
        /// <param name="targetHeightPx">The height of the resulting png in pixels (default = -1, which is simply the image height)</param>
        /// <param name="format">The pixel format to render in, (default = null which results in <see cref="PixelFormats.Default"/>)</param>
        /// <returns>The <see cref="MemoryStream"/> containing the png data (Could create an <see cref="Image"/> or file with it)</returns>
        public static MemoryStream RenderToPng (this FrameworkElement This, int targetWidthPx = -1, int targetHeightPx = -1, PixelFormat? format = null)
        {
            MemoryStream ms = new MemoryStream ();
            if (DoRenderToPng(This, ms, targetWidthPx, targetHeightPx, format ?? PixelFormats.Default))
            {
                ms.Position = 0;
                return ms;
            }

            return null;
        }

        public static bool RenderToPng (this FrameworkElement This, Stream stream, int targetWidthPx = -1, int targetHeightPx = -1, PixelFormat? format = null)
        {
            return DoRenderToPng(This, stream, targetWidthPx, targetHeightPx, format ?? PixelFormats.Default);
        }

        /// <summary>
        /// Render the <see cref="FrameworkElement"/> to a png memory stream targeting output size in inches.
        /// </summary>
        /// <remarks>
        /// If width is specified and height isn't, then the height will be specified proportionally. Similarly if the height is specified and the width isn't, then the width will be set proportionally.
        /// </remarks>
        public static bool RenderToPngInInches (this FrameworkElement This, Stream stream, double targetWidthInches = double.NaN, double targetHeightInches = double.NaN, PixelFormat? format = null)
        {
            Size targetPx = GetSizeInInches(This, targetWidthInches, targetHeightInches);
            return DoRenderToPng(This, stream, (int)targetPx.Width, (int)targetPx.Height, format ?? PixelFormats.Default);
        }
        private static bool DoRenderToPng (this FrameworkElement This, Stream target, int targetWidthPx, int targetHeightPx, PixelFormat pixelFormat)
        {
            try
            {
                DpiScale dpi = VisualTreeHelper.GetDpi(This);
                var renderer = new RenderTargetBitmap((int)(This.ActualWidth * dpi.DpiScaleX), (int)(This.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, pixelFormat);
                renderer.Render(This);

                if (targetWidthPx == -1)
                {
                    targetWidthPx = (int)This.ActualWidth;
                }
                if (targetHeightPx == -1)
                {
                    targetHeightPx = (int)This.ActualHeight;
                }

                var bitmap = new TransformedBitmap(BitmapFrame.Create(renderer),
                    new ScaleTransform(
                        targetWidthPx / This.ActualWidth,
                        targetHeightPx / This.ActualHeight
                    )
                );

                bitmap.WriteTo(target);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error trying to create png from control", ex);
                return false;
            }

            return true;
        }

        private static Size GetSizeInInches (FrameworkElement element, double targetWidthInches, double targetHeightInches)
        {
            if (double.IsNaN(targetWidthInches))
            {
                if (!double.IsNaN(targetHeightInches))
                {
                    // Just the width is NaN, not the height - scale width by height
                    DpiScale dpi = VisualTreeHelper.GetDpi(element);
                    double heightPx = targetHeightInches * dpi.PixelsPerInchY;
                    return new Size
                    {
                        Width = element.ActualWidth * (heightPx / element.ActualHeight),
                        Height = heightPx,
                    };
                }
            }
            else if (double.IsNaN(targetHeightInches))
            {
                // Just the height is NaN, not the width - scale height by width
                DpiScale dpi = VisualTreeHelper.GetDpi(element);
                double widthPx = targetWidthInches * dpi.PixelsPerInchX;
                return new Size
                {
                    Width = widthPx,
                    Height = element.ActualHeight * (widthPx / element.ActualWidth),
                };
            }

            // Both...
            return new Size
            {
                Width = element.ActualWidth,
                Height = element.ActualHeight,
            };
        }

    }
}
