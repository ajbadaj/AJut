namespace AJut.UX.Helpers
{
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Threading;
    using System.Threading.Tasks;
    using Native = ScreenColorPickerNative;

    /// <summary>
    /// One pick from screen: a frozen capture of every monitor, shown in a chromeless topmost window over the whole
    /// virtual screen, which takes the click that picks. See <see cref="Open"/> for how and why.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal sealed unsafe class ScreenPickOverlay
    {
        private const string kWindowClassName = "AJut.ScreenColorPicker.Overlay";

        // The overlay a window handle belongs to, for the window procedure, which only gets the handle. An overlay is in
        // here only from just after its window is made until its teardown starts, so nothing outlives its pick.
        private static readonly Dictionary<nint, ScreenPickOverlay> g_liveOverlays = new Dictionary<nint, ScreenPickOverlay>();
        private static ushort g_windowClassAtom;

        private readonly ScreenPickSession m_session = new ScreenPickSession();
        private readonly TaskCompletionSource<ColorArgb?> m_result = new TaskCompletionSource<ColorArgb?>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly SynchronizationContext m_uiContext;
        private readonly IScreenColorPickMagnifier? m_magnifier;
        private readonly int m_magnifierPixelCount;
        private ScreenColorPickSample? m_pendingSample;
        private bool m_isSampleDeliveryPosted;
        private bool m_isMagnifierShowing;
        private Capture? m_capture;
        private nint m_window;
        private nint m_previousForeground;
        private nint m_crossCursor;
        private CancellationTokenRegistration m_cancelRegistration;
        private bool m_isTornDown;

        // ===========[ Setup/Construction/Teardown ]===================================
        private ScreenPickOverlay (SynchronizationContext uiContext, ScreenColorPickOptions options)
        {
            m_uiContext = uiContext;
            m_magnifier = options.Magnifier;
            m_magnifierPixelCount = options.OddMagnifierPixelCount;
        }

        /// <summary>
        /// Starts a pick and gives back its result: the picked color, or null when the pick was canceled or could not
        /// start. Call on the UI thread, whose message loop runs the overlay, and whose context is
        /// <paramref name="uiContext"/>.
        /// </summary>
        public static Task<ColorArgb?> Run (SynchronizationContext uiContext, ScreenColorPickOptions options, CancellationToken cancel)
        {
            var overlay = new ScreenPickOverlay(uiContext, options);
            overlay.Open(cancel);
            return overlay.m_result.Task;
        }

        private void Open (CancellationToken cancel)
        {
            // How a pick from anywhere on screen works, and why it is built this way:
            //
            // * A frozen frame. Every monitor is captured into one bitmap first, and the overlay shows that bitmap, so
            //   the color picked is exactly the pixel the user sees under the crosshair. The capture is taken before the
            //   overlay exists, so the overlay can never end up in its own capture. The cost is that the screen holds
            //   still while picking (an animation under it stops), which also means a live preview of the color would
            //   show nowhere: the app's own windows are under the frozen frame too.
            // * One plain Win32 popup over the virtual screen (the bounding box of every monitor), topmost, with no
            //   chrome and no taskbar button. It is shared by every AJut stack: WPF and WinUI3 both run a Win32 message
            //   loop on their UI thread, so a window made on that thread gets its messages from it with nothing more.
            //   A WinUI3 Window across monitors of different scales would fight the framework the whole way.
            // * DPI. Under per monitor awareness, the virtual screen metrics, the screen DC and the window's own
            //   coordinates are all physical pixels and agree on every monitor. An app that is system aware, or not DPI
            //   aware at all (WPF's default without a manifest entry), gets all three scaled for it, and on mixed DPI
            //   monitors they disagree. SetThreadDpiAwarenessContext switches this thread to per monitor v2 while the
            //   capture is taken and the window is made, and the window keeps that awareness for its life: Windows
            //   switches the thread to a window's own awareness whenever it handles that window's messages.
            //   https://learn.microsoft.com/windows/win32/hidpi/high-dpi-improvements-for-desktop-applications
            // * CAPTUREBLT makes the capture include layered windows (WPF popups with AllowsTransparency, tooltips,
            //   menus), which a plain SRCCOPY from the screen DC leaves out. Its known side effect is a flicker of the
            //   mouse cursor during the copy, once per pick.
            // * DwmFlush first waits for the next desktop composition, so whatever the app changed just before calling
            //   (an editor hidden so it does not cover what is being picked) has reached the screen before the copy.
            // * The overlay takes activation, so Esc reaches it, and mouse capture, so every click does. Losing either
            //   (Alt+Tab, the Windows key, a system prompt) cancels, since a pick the user can no longer see or finish
            //   must not be left over everything. Each click or key acts on its release; ScreenPickSession says why.
            // * A magnifier (the stack's own, so it themes like its controls) follows the cursor from the moment the
            //   overlay is up, showing the color and the pixels under it, so it is plain that the pick has started.
            //   Each sample says whether the left button is held, so the magnifier can ask for a press and then for
            //   the release. The first sample is taken at the cursor before any mouse message arrives, since a pick
            //   started from the keyboard may get none until the mouse moves. While the button is held the cursor is
            //   hidden, so it never covers the pixels being picked from.
            // * Samples are handed over through the UI thread's SynchronizationContext rather than called from the
            //   window procedure: Windows runs that procedure under the overlay's per monitor awareness, and a stack's
            //   own window laid out under an awareness it was not made with would place and size itself wrongly. The
            //   latest sample wins, so a fast move never queues up stale ones.
            // * On the way out, the window that was in the foreground before the pick gets it back, so the app's editor
            //   is active again rather than whatever window happens to be next in the z order.

            if (cancel.IsCancellationRequested)
            {
                m_result.TrySetResult(null);
                return;
            }

            // 1. Capture and make the window as a per monitor aware thread
            nint previousDpiContext = Native.SetThreadDpiAwarenessContext(Native.kDpiAwarenessContextPerMonitorAwareV2);
            try
            {
                _ = Native.DwmFlush();
                m_capture = Capture.TakeVirtualScreen();
                if (m_capture == null)
                {
                    this.TearDown();
                    return;
                }

                m_window = Native.CreateWindowExW(
                    Native.kWsExTopmost | Native.kWsExToolWindow,
                    EnsureWindowClass(),
                    IntPtr.Zero,
                    Native.kWsPopup,
                    m_capture.Left,
                    m_capture.Top,
                    m_capture.Width,
                    m_capture.Height,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    Native.GetModuleHandleW(IntPtr.Zero),
                    IntPtr.Zero
                );

                // The first sample, from where the cursor is now; it is built here, still per monitor aware, for the
                // same physical pixel coordinates the window procedure gets
                if (m_window != IntPtr.Zero && Native.GetCursorPos(out Native.POINT cursor))
                {
                    this.UpdateMagnifier(cursor.x - m_capture.Left, cursor.y - m_capture.Top);
                }
            }
            finally
            {
                if (previousDpiContext != IntPtr.Zero)
                {
                    _ = Native.SetThreadDpiAwarenessContext(previousDpiContext);
                }
            }

            if (m_window == IntPtr.Zero)
            {
                Logger.LogInfo($"[SCREEN-COLOR-PICK] Could not make the pick overlay (error {Marshal.GetLastPInvokeError()})");
                this.TearDown();
                return;
            }

            // 2. Registered before it shows, since showing and activating it send it messages right away
            g_liveOverlays[m_window] = this;

            // The default show and hide animations would fade the frozen frame in over the live screen
            int disableTransitions = 1;
            _ = Native.DwmSetWindowAttribute(m_window, Native.kDwmwaTransitionsForceDisabled, in disableTransitions, sizeof(int));

            // 3. Show it, and take activation and the mouse
            m_previousForeground = Native.GetForegroundWindow();
            m_crossCursor = Native.LoadCursorW(IntPtr.Zero, Native.kIdcCross);
            _ = Native.ShowWindow(m_window, Native.kSwShow);
            _ = Native.UpdateWindow(m_window);
            _ = Native.SetForegroundWindow(m_window);
            _ = Native.SetCapture(m_window);
            _ = Native.SetCursor(m_crossCursor);

            // 4. A cancel from the caller lands on this thread, where the window can be torn down
            m_cancelRegistration = cancel.Register(OnCancelRequested, this, useSynchronizationContext: true);
        }

        private void TearDown ()
        {
            if (m_isTornDown)
            {
                return;
            }

            m_isTornDown = true;
            m_cancelRegistration.Unregister();

            // Out of the lookup first, so the messages the teardown itself sends (losing capture, losing activation)
            // go to the default window procedure and not back into this pick
            if (m_window != IntPtr.Zero)
            {
                g_liveOverlays.Remove(m_window);
                _ = Native.ReleaseCapture();
                _ = Native.DestroyWindow(m_window);
                m_window = IntPtr.Zero;
            }

            m_capture?.Dispose();
            m_capture = null;

            m_pendingSample = null;
            if (m_isMagnifierShowing || m_isSampleDeliveryPosted)
            {
                m_isMagnifierShowing = false;
                m_uiContext.Post(HideMagnifier, m_magnifier);
            }

            if (m_previousForeground != IntPtr.Zero)
            {
                _ = Native.SetForegroundWindow(m_previousForeground);
                m_previousForeground = IntPtr.Zero;
            }

            if (!m_result.Task.IsCompleted)
            {
                m_result.TrySetResult(null);
            }
        }

        // ===========[ Properties ]===================================
        /// <summary>True while a pick has its overlay up</summary>
        public static bool IsAnyPickRunning => g_liveOverlays.Count > 0;

        // ===========[ Helper Methods ]===================================
        private static nint EnsureWindowClass ()
        {
            if (g_windowClassAtom != 0)
            {
                return g_windowClassAtom;
            }

            fixed (char* className = kWindowClassName)
            {
                var windowClass = new Native.WNDCLASSEXW
                {
                    cbSize = (uint)sizeof(Native.WNDCLASSEXW),
                    lpfnWndProc = (nint)(delegate* unmanaged[Stdcall]<nint, uint, nint, nint, nint>)&WindowProcedure,
                    hInstance = Native.GetModuleHandleW(IntPtr.Zero),
                    hCursor = Native.LoadCursorW(IntPtr.Zero, Native.kIdcCross),
                    lpszClassName = (nint)className,
                };

                g_windowClassAtom = Native.RegisterClassExW(in windowClass);
            }

            if (g_windowClassAtom == 0)
            {
                Logger.LogInfo($"[SCREEN-COLOR-PICK] Could not register the pick overlay's window class (error {Marshal.GetLastPInvokeError()})");
            }

            return g_windowClassAtom;
        }

        private static void OnCancelRequested (object? state)
        {
            var overlay = (ScreenPickOverlay)state!;
            overlay.m_session.Interrupt();
            overlay.FinishIfDone();
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
        private static nint WindowProcedure (nint window, uint message, nint wParam, nint lParam)
        {
            if (g_liveOverlays.TryGetValue(window, out ScreenPickOverlay? overlay))
            {
                // Nothing may be thrown back across into Windows from here, so a failure cancels the pick instead
                try
                {
                    if (overlay.HandleMessage(message, wParam, lParam))
                    {
                        return (message == Native.kWmEraseBackground) ? 1 : IntPtr.Zero;
                    }
                }
                catch (Exception exc)
                {
                    Logger.LogError("[SCREEN-COLOR-PICK] The pick overlay failed handling a window message, canceling the pick", exc);
                    overlay.m_session.Interrupt();
                    overlay.FinishIfDone();
                    return IntPtr.Zero;
                }
            }

            return Native.DefWindowProcW(window, message, wParam, lParam);
        }

        // True when the message was handled here
        private bool HandleMessage (uint message, nint wParam, nint lParam)
        {
            switch (message)
            {
                case Native.kWmPaint:
                    this.Paint();
                    return true;

                // The paint covers every pixel, so erasing first would only flash the background
                case Native.kWmEraseBackground:
                    return true;

                // A window with the mouse captured gets no WM_SETCURSOR, so the cursor is kept here: a crosshair, or
                // nothing while the button is held, so it never covers the pixels the magnifier is showing
                case Native.kWmMouseMove:
                    _ = Native.SetCursor(m_session.IsPressing ? IntPtr.Zero : m_crossCursor);
                    this.UpdateMagnifier(LowWordSigned(lParam), HighWordSigned(lParam));
                    return true;

                case Native.kWmLeftButtonDown:
                    m_session.LeftButtonDown();
                    _ = Native.SetCursor(IntPtr.Zero);
                    this.UpdateMagnifier(LowWordSigned(lParam), HighWordSigned(lParam));
                    break;

                case Native.kWmLeftButtonUp:
                    m_session.LeftButtonUp(LowWordSigned(lParam), HighWordSigned(lParam));
                    break;

                case Native.kWmRightButtonDown:
                    m_session.RightButtonDown();
                    break;

                case Native.kWmRightButtonUp:
                    m_session.RightButtonUp();
                    break;

                case Native.kWmKeyDown:
                    if ((int)wParam == Native.kVkEscape)
                    {
                        m_session.EscapeDown();
                    }
                    break;

                case Native.kWmKeyUp:
                    if ((int)wParam == Native.kVkEscape)
                    {
                        m_session.EscapeUp();
                    }
                    break;

                case Native.kWmCaptureChanged:
                    m_session.Interrupt();
                    break;

                // Left to the default window procedure as well while the pick goes on, since that is what gives the
                // overlay keyboard focus when it activates, and Esc only reaches a window with focus
                case Native.kWmActivate:
                    if (((int)wParam & 0xFFFF) == Native.kWaInactive)
                    {
                        m_session.Interrupt();
                    }

                    this.FinishIfDone();
                    return m_isTornDown;

                // Alt+F4 backs out of the pick, and the teardown destroys the window
                case Native.kWmClose:
                    m_session.Interrupt();
                    break;

                default:
                    return false;
            }

            this.FinishIfDone();
            return true;
        }

        private void FinishIfDone ()
        {
            if (!m_session.IsDone || m_isTornDown)
            {
                return;
            }

            ColorArgb? picked = null;
            if (m_session.State == eScreenPickState.Picked
                && (m_capture?.TryRead(m_session.PickedX, m_session.PickedY, out ColorArgb color) ?? false))
            {
                picked = color;
            }

            // The result is set before the teardown, which would otherwise settle it as canceled
            m_result.TrySetResult(picked);
            this.TearDown();
        }

        private void Paint ()
        {
            nint paintContext = Native.BeginPaint(m_window, out Native.PAINTSTRUCT paint);
            if (paintContext == IntPtr.Zero)
            {
                return;
            }

            if (m_capture != null)
            {
                Native.RECT area = paint.rcPaint;
                _ = Native.BitBlt(
                    paintContext,
                    area.left,
                    area.top,
                    area.right - area.left,
                    area.bottom - area.top,
                    m_capture.DeviceContext,
                    area.left,
                    area.top,
                    Native.kSrcCopy
                );
            }

            _ = Native.EndPaint(m_window, in paint);
        }

        // Samples what is under x, y (the overlay's own coordinates) and hands it to the magnifier, through the UI
        // thread's context, see Open
        private void UpdateMagnifier (int x, int y)
        {
            if (m_magnifier == null || m_capture == null)
            {
                return;
            }

            m_pendingSample = this.BuildSample(x, y);
            if (!m_isSampleDeliveryPosted)
            {
                m_isSampleDeliveryPosted = true;
                m_uiContext.Post(DeliverSample, this);
            }
        }

        private ScreenColorPickSample BuildSample (int x, int y)
        {
            // 1. The square of pixels around the target, transparent where it runs off the capture
            int count = m_magnifierPixelCount;
            int half = count / 2;
            var pixels = new ColorArgb[count * count];
            for (int row = 0; row < count; ++row)
            {
                for (int column = 0; column < count; ++column)
                {
                    if (m_capture!.TryRead(x - half + column, y - half + row, out ColorArgb color))
                    {
                        pixels[(row * count) + column] = color;
                    }
                }
            }

            // 2. Where it is: this runs in the window procedure, under the overlay's per monitor awareness (or in Open,
            //    under the same), so the monitor's work area and DPI come back in physical pixels
            var cursor = new ScreenPixelPoint(m_capture!.Left + x, m_capture.Top + y);
            var workArea = new ScreenPixelRect(m_capture.Left, m_capture.Top, m_capture.Width, m_capture.Height);
            double scale = 1.0;
            nint monitor = Native.MonitorFromPoint(new Native.POINT { x = cursor.X, y = cursor.Y }, Native.kMonitorDefaultToNearest);
            if (monitor != IntPtr.Zero)
            {
                var info = new Native.MONITORINFO { cbSize = (uint)sizeof(Native.MONITORINFO) };
                if (Native.GetMonitorInfoW(monitor, ref info))
                {
                    Native.RECT work = info.rcWork;
                    workArea = new ScreenPixelRect(work.left, work.top, work.right - work.left, work.bottom - work.top);
                }

                if (Native.GetDpiForMonitor(monitor, Native.kMdtEffectiveDpi, out uint dpi, out _) >= 0 && dpi > 0)
                {
                    scale = ScreenColorPickSample.ScaleForDpi(dpi);
                }
            }

            return new ScreenColorPickSample(pixels, count, cursor, workArea, scale, m_session.IsPressing);
        }

        private static void DeliverSample (object? state)
        {
            var overlay = (ScreenPickOverlay)state!;
            overlay.m_isSampleDeliveryPosted = false;
            ScreenColorPickSample? sample = overlay.m_pendingSample;
            overlay.m_pendingSample = null;
            if (overlay.m_isTornDown || sample == null || overlay.m_magnifier == null)
            {
                return;
            }

            try
            {
                overlay.m_isMagnifierShowing = true;
                overlay.m_magnifier.ShowSample(sample);
            }
            catch (Exception exc)
            {
                Logger.LogError("[SCREEN-COLOR-PICK] The pick's magnifier failed to show a sample", exc);
            }
        }

        private static void HideMagnifier (object? state)
        {
            try
            {
                ((IScreenColorPickMagnifier?)state)?.Hide();
            }
            catch (Exception exc)
            {
                Logger.LogError("[SCREEN-COLOR-PICK] The pick's magnifier failed to hide", exc);
            }
        }

        // The signed x and y a mouse message packs into its lParam (GET_X_LPARAM and GET_Y_LPARAM)
        private static int LowWordSigned (nint lParam) => (short)((long)lParam & 0xFFFF);
        private static int HighWordSigned (nint lParam) => (short)(((long)lParam >> 16) & 0xFFFF);

        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// The virtual screen copied into a top down 32 bit DIB section, which is both what the overlay paints and what
        /// the pick reads from. It is a full copy of every monitor (three 4K monitors come to about 100 MB), held only
        /// for the life of one pick.
        /// </summary>
        private sealed class Capture : IDisposable
        {
            private readonly nint m_bitmap;
            private readonly nint m_previousBitmap;
            private readonly nint m_bits;
            private bool m_isDisposed;

            private Capture (int left, int top, int width, int height, nint deviceContext, nint bitmap, nint previousBitmap, nint bits)
            {
                this.Left = left;
                this.Top = top;
                this.Width = width;
                this.Height = height;
                this.DeviceContext = deviceContext;
                m_bitmap = bitmap;
                m_previousBitmap = previousBitmap;
                m_bits = bits;
            }

            /// <summary>Copies every monitor, or gives back null and logs why when the copy can't be made</summary>
            public static Capture? TakeVirtualScreen ()
            {
                int left = Native.GetSystemMetrics(Native.kSmXVirtualScreen);
                int top = Native.GetSystemMetrics(Native.kSmYVirtualScreen);
                int width = Native.GetSystemMetrics(Native.kSmCxVirtualScreen);
                int height = Native.GetSystemMetrics(Native.kSmCyVirtualScreen);
                if (width <= 0 || height <= 0)
                {
                    Logger.LogInfo($"[SCREEN-COLOR-PICK] No screen to capture ({width} x {height})");
                    return null;
                }

                nint screenContext = Native.GetDC(IntPtr.Zero);
                if (screenContext == IntPtr.Zero)
                {
                    Logger.LogInfo("[SCREEN-COLOR-PICK] Could not get the screen to capture");
                    return null;
                }

                try
                {
                    nint memoryContext = Native.CreateCompatibleDC(screenContext);
                    if (memoryContext == IntPtr.Zero)
                    {
                        Logger.LogInfo("[SCREEN-COLOR-PICK] Could not make a device context for the screen capture");
                        return null;
                    }

                    // A negative height makes the DIB top down, so row 0 is the top of the virtual screen
                    var header = new Native.BITMAPINFOHEADER
                    {
                        biSize = (uint)sizeof(Native.BITMAPINFOHEADER),
                        biWidth = width,
                        biHeight = -height,
                        biPlanes = 1,
                        biBitCount = BgraPixels.kBytesPerPixel * 8,
                        biCompression = Native.kBiRgb,
                    };

                    nint bitmap = Native.CreateDIBSection(screenContext, in header, Native.kDibRgbColors, out nint bits, IntPtr.Zero, 0);
                    if (bitmap == IntPtr.Zero || bits == IntPtr.Zero)
                    {
                        Logger.LogInfo($"[SCREEN-COLOR-PICK] Could not make a {width} x {height} bitmap for the screen capture");
                        _ = Native.DeleteDC(memoryContext);
                        return null;
                    }

                    nint previousBitmap = Native.SelectObject(memoryContext, bitmap);
                    var capture = new Capture(left, top, width, height, memoryContext, bitmap, previousBitmap, bits);
                    if (!Native.BitBlt(memoryContext, 0, 0, width, height, screenContext, left, top, Native.kSrcCopy | Native.kCaptureBlt))
                    {
                        Logger.LogInfo("[SCREEN-COLOR-PICK] Could not copy the screen");
                        capture.Dispose();
                        return null;
                    }

                    // GDI may batch the copy, and the bits are read directly, so make sure it has landed
                    _ = Native.GdiFlush();
                    return capture;
                }
                finally
                {
                    _ = Native.ReleaseDC(IntPtr.Zero, screenContext);
                }
            }

            public void Dispose ()
            {
                if (m_isDisposed)
                {
                    return;
                }

                m_isDisposed = true;
                _ = Native.SelectObject(this.DeviceContext, m_previousBitmap);
                _ = Native.DeleteObject(m_bitmap);
                _ = Native.DeleteDC(this.DeviceContext);
            }

            /// <summary>The virtual screen's left edge, in physical pixels</summary>
            public int Left { get; }

            /// <summary>The virtual screen's top edge, in physical pixels</summary>
            public int Top { get; }

            public int Width { get; }
            public int Height { get; }

            /// <summary>The memory device context the capture is selected into, to paint from</summary>
            public nint DeviceContext { get; }

            /// <summary>
            /// The color at <paramref name="x"/>, <paramref name="y"/> from the virtual screen's top left corner (the
            /// overlay's own coordinates), or false when the point is off the capture. A point between monitors of
            /// different sizes is still on it, and reads as the black GDI gives the parts of the box no monitor covers.
            /// </summary>
            public bool TryRead (int x, int y, out ColorArgb color)
            {
                if (m_isDisposed || x < 0 || y < 0 || x >= this.Width || y >= this.Height)
                {
                    color = default;
                    return false;
                }

                int stride = this.Width * BgraPixels.kBytesPerPixel;
                var pixels = new ReadOnlySpan<byte>((void*)m_bits, stride * this.Height);
                color = BgraPixels.ReadOpaque(pixels, stride, x, y);
                return true;
            }
        }
    }
}
