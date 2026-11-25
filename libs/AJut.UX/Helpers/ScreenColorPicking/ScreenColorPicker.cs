namespace AJut.UX.Helpers
{
    using System.Runtime.Versioning;
    using System.Threading;
    using System.Threading.Tasks;
    using Native = ScreenColorPickerNative;

    /// <summary>
    /// Picks a color from anywhere on screen, on any monitor, at any scale. The screen freezes under a crosshair, and
    /// the options' magnifier follows the cursor from the start, saying whether the left button is held. Pressing the
    /// left button and letting go takes the color under it. A right click or Esc backs out. Windows only. The WPF and WinUI3 color
    /// editors use it for their eyedropper; AppKit and UIKit have a system eyedropper of their own.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static class ScreenColorPicker
    {
        private static readonly ScreenColorPickOptions kDefaultOptions = new ScreenColorPickOptions();

        /// <summary>True while a pick is up</summary>
        public static bool IsPicking => ScreenPickOverlay.IsAnyPickRunning;

        /// <summary>
        /// Starts a pick, and finishes with the picked color, always opaque, or null if the user backed out, the pick
        /// was canceled, or the screen could not be captured. Call it on a UI thread: the pick runs on that thread's
        /// message loop, and the magnifier is called on it. A pick asked for while another is up finishes at once with
        /// null.
        /// </summary>
        /// <param name="options">The magnifier and its size, or null for no magnifier</param>
        /// <param name="cancel">Cancels the pick and takes its overlay down, as when whatever started it goes away</param>
        /// <exception cref="InvalidOperationException">Called from a thread with no UI message loop</exception>
        public static Task<ColorArgb?> PickAsync (ScreenColorPickOptions? options = null, CancellationToken cancel = default)
        {
            SynchronizationContext? uiContext = SynchronizationContext.Current;
            if (uiContext == null)
            {
                throw new InvalidOperationException("ScreenColorPicker.PickAsync must be called on a UI thread, since the pick runs on that thread's message loop");
            }

            if (ScreenPickOverlay.IsAnyPickRunning)
            {
                return Task.FromResult<ColorArgb?>(null);
            }

            return ScreenPickOverlay.Run(uiContext, options ?? kDefaultOptions, cancel);
        }

        /// <summary>
        /// Moves a magnifier's window to where <paramref name="sample"/> says it goes (see
        /// <see cref="ScreenColorPickSample.PlaceMagnifier"/>), above the pick's overlay, without activating it.
        /// Positions are physical pixels whatever DPI awareness the app or the window has.
        /// </summary>
        /// <param name="windowHandle">The magnifier's window</param>
        /// <param name="sample">The sample it is showing</param>
        /// <param name="physicalWidth">A width to size the window to, or 0 to keep its size</param>
        /// <param name="physicalHeight">A height to size the window to, or 0 to keep its size</param>
        public static void PlaceMagnifierWindow (nint windowHandle, ScreenColorPickSample sample, int physicalWidth = 0, int physicalHeight = 0)
        {
            // Coordinates given to and taken from these calls are in the calling thread's DPI awareness, so for the
            // length of the call this thread is per monitor aware, the same as the overlay, and every number is a
            // physical pixel. That holds for a window that is itself system aware: Windows scales its contents.
            nint previousDpiContext = Native.SetThreadDpiAwarenessContext(Native.kDpiAwarenessContextPerMonitorAwareV2);
            try
            {
                bool resize = physicalWidth > 0 && physicalHeight > 0;
                int width = physicalWidth;
                int height = physicalHeight;
                if (!resize)
                {
                    if (!Native.GetWindowRect(windowHandle, out Native.RECT current))
                    {
                        return;
                    }

                    width = current.right - current.left;
                    height = current.bottom - current.top;
                }

                ScreenPixelPoint at = sample.PlaceMagnifier(width, height);
                uint flags = Native.kSwpNoActivate | Native.kSwpNoOwnerZOrder | (resize ? 0 : Native.kSwpNoSize);
                _ = Native.SetWindowPos(windowHandle, Native.kHwndTopmost, at.X, at.Y, width, height, flags);

                // A move onto a monitor at another scale lets the window resize itself for that scale, so a size that
                // was asked for is asked for again once it is there
                if (resize
                    && Native.GetWindowRect(windowHandle, out Native.RECT moved)
                    && ((moved.right - moved.left) != width || (moved.bottom - moved.top) != height))
                {
                    _ = Native.SetWindowPos(windowHandle, Native.kHwndTopmost, at.X, at.Y, width, height, flags);
                }
            }
            finally
            {
                if (previousDpiContext != IntPtr.Zero)
                {
                    _ = Native.SetThreadDpiAwarenessContext(previousDpiContext);
                }
            }
        }
    }
}
