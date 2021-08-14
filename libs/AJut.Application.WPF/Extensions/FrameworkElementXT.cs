namespace AJut.Application
{
    using System;
    using System.IO;
    using System.Windows;
#if WINDOWS_UWP
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml.Media.Imaging;
#else
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
#endif

    public static class FrameworkElementXT
    {
        /// <summary>
        /// Render a <see cref="FrameworkElement"/> to a png memory stream.
        /// </summary>
        /// <param name="This">The target <see cref="FrameworkElement"/></param>
        /// <param name="ppiX">The pixels-per-inch to render with horizontally</param>
        /// <param name="ppiY">The pixels-per-inch to render with vertically</param>
        /// <returns>The <see cref="MemoryStream"/> containing the png data (Could create an <see cref="Image"/> or file with it)</returns>
        public static MemoryStream RenderToPngAsIs(this FrameworkElement This, int ppiX = 96, int ppiY = 96, bool updateLayout = false)
        {
            int width = (int)This.ActualWidth;
            int height = (int)This.ActualHeight;

            if (width == 0 && This.Width != 0)
            {
                width = (int)This.Width;
            }

            if (height == 0 && This.Height != 0)
            {
                height = (int)This.Width;
            }
            return RenderToPng(This, width, height, PixelFormats.Pbgra32, ppiX, ppiY, updateLayout);
        }

        /// <summary>
        /// Render a <see cref="FrameworkElement"/> to a png memory stream.
        /// </summary>
        /// <param name="This">The target <see cref="FrameworkElement"/></param>
        /// <param name="targetWidthPx">The target width to render at (in pixels)</param>
        /// <param name="targetHeightPx">The target height to render at (in pixels)</param>
        /// <param name="ppiX">The pixels-per-inch to render with horizontally</param>
        /// <param name="ppiY">The pixels-per-inch to render with vertically</param>
        /// <param name="updateLayoutFirst">Bool indicating whether or notto meadure, arrange, and update layout before rendering (default == true)</param>
        /// <returns>The <see cref="MemoryStream"/> containing the png data (Could create an <see cref="Image"/> or file with it)</returns>
        public static MemoryStream RenderToPngWithFixedDimensionsPixels(this FrameworkElement This, int targetWidthPx, int targetHeightPx, int ppiX = 96, int ppiY = 96, bool updateLayoutFirst = true)
        {
            return RenderToPng(This, targetWidthPx, targetHeightPx, PixelFormats.Pbgra32, ppiX, ppiY, updateLayoutFirst);
        }

        /// <summary>
        /// Render a <see cref="FrameworkElement"/> to a png memory stream.
        /// </summary>
        /// <param name="This">The target <see cref="FrameworkElement"/></param>
        /// <param name="targetWidthIn">The target width to render at (in inches)</param>
        /// <param name="targetHeightIn">The target height to render at (in inches)</param>
        /// <param name="ppiX">The pixels-per-inch to render with horizontally</param>
        /// <param name="ppiY">The pixels-per-inch to render with vertically</param>
        /// <param name="updateLayoutFirst">Bool indicating whether or notto meadure, arrange, and update layout before rendering (default == true)</param>
        /// <returns>The <see cref="MemoryStream"/> containing the png data (Could create an <see cref="Image"/> or file with it)</returns>
        public static MemoryStream RenderToPngWithFixedDimensionsInches(this FrameworkElement This, float targetWidthIn, float targetHeightIn, int ppiX = 96, int ppiY = 96, bool updateLayoutFirst = true)
        {
            int targetWidthPx = (int)(targetWidthIn * ppiX);
            int targetHeightPx = (int)(targetHeightIn * ppiY);
            return RenderToPng(This, targetWidthPx, targetHeightPx, PixelFormats.Pbgra32, ppiX, ppiY, updateLayoutFirst);
        }

        /// <summary>
        /// Render the <see cref="FrameworkElement"/> to a png memory stream.
        /// </summary>
        /// <param name="This">The target <see cref="FrameworkElement"/></param>
        /// <param name="targetHeightPx">The height to set the control for rendering</param>
        /// <param name="targetWidthPx">The width to set the control for rendering</param>
        /// <param name="ppiX">The pixels-per-inch to render with horizontally</param>
        /// <param name="ppiY">The pixels-per-inch to render with vertically</param>
        /// <param name="updateLayoutFirst">Bool indicating whether or notto meadure, arrange, and update layout before rendering (default == true)</param>
        /// <returns>The <see cref="MemoryStream"/> containing the png data (Could create an <see cref="Image"/> or file with it)</returns>
        public static MemoryStream RenderToPng (this FrameworkElement This, int targetWidthPx, int targetHeightPx, int ppiX = 96, int ppiY = 96, bool updateLayoutFirst = true)
        {
            return RenderToPng(This, targetWidthPx, targetHeightPx, PixelFormats.Default, ppiX, ppiY, updateLayoutFirst);
        }

        public static bool RenderToPng (this FrameworkElement This, Stream stream, int targetWidthPx, int targetHeightPx, int ppiX = 96, int ppiY = 96, bool updateLayoutFirst = true)
        {
            return RenderToPng(This, stream, targetWidthPx, targetHeightPx, PixelFormats.Default, ppiX, ppiY, updateLayoutFirst);
        }

        /// <summary>
        /// Render the <see cref="FrameworkElement"/> to a png memory stream.
        /// </summary>
        /// <param name="This">The target <see cref="FrameworkElement"/></param>
        /// <param name="targetHeightPx">The height to set the control for rendering</param>
        /// <param name="targetWidthPx">The width to set the control for rendering</param>
        /// <param name="pixelFormat">The <see cref="PixelFormat'"/> to apply</param>
        /// <param name="ppiX">The pixels-per-inch to render with horizontally</param>
        /// <param name="ppiY">The pixels-per-inch to render with vertically</param>
        /// <param name="updateLayoutFirst">Bool indicating whether or notto meadure, arrange, and update layout before rendering (default == true)</param>
        /// <returns>The <see cref="MemoryStream"/> containing the png data (Could create an <see cref="Image"/> or file with it)</returns>
        public static MemoryStream RenderToPng(this FrameworkElement This, int targetWidthPx, int targetHeightPx, PixelFormat pixelFormat, int ppiX = 96, int ppiY = 96, bool updateLayoutFirst = true)
        {
            var stream = new MemoryStream();
            This.RenderToPng(stream, targetWidthPx, targetHeightPx, pixelFormat, ppiX, ppiY, updateLayoutFirst);
            return stream;
        }

        public static bool RenderToPng(this FrameworkElement This, Stream target, int targetWidthPx, int targetHeightPx, PixelFormat pixelFormat, int ppiX = 96, int ppiY = 96, bool updateLayoutFirst = true)
        {
            try
            {
                if (updateLayoutFirst)
                {
                    This.Measure(new Size(targetWidthPx, targetHeightPx));
                    This.Arrange(new Rect(0, 0, targetWidthPx, targetHeightPx));
                    This.UpdateLayout();
                }

                var renderer = new RenderTargetBitmap(targetWidthPx, targetHeightPx, ppiX, ppiY, pixelFormat);
                renderer.Render(This);
                var png = new PngBitmapEncoder();
                png.Frames.Add(BitmapFrame.Create(renderer));
                png.Save(target);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error trying to create png from control", ex);
                return false;
            }

            return true;
        }
    }
}
