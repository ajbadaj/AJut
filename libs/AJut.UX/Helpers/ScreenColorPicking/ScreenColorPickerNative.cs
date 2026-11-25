namespace AJut.UX.Helpers
{
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;

    /// <summary>
    /// The Win32 calls behind <see cref="ScreenColorPicker"/>: capturing the virtual screen with GDI, and the topmost
    /// popup window that shows that capture and takes the pick.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static partial class ScreenColorPickerNative
    {
        // ===========[ Window messages ]===================================
        public const uint kWmActivate = 0x0006;
        public const uint kWmPaint = 0x000F;
        public const uint kWmClose = 0x0010;
        public const uint kWmEraseBackground = 0x0014;
        public const uint kWmKeyDown = 0x0100;
        public const uint kWmKeyUp = 0x0101;
        public const uint kWmMouseMove = 0x0200;
        public const uint kWmLeftButtonDown = 0x0201;
        public const uint kWmLeftButtonUp = 0x0202;
        public const uint kWmRightButtonDown = 0x0204;
        public const uint kWmRightButtonUp = 0x0205;
        public const uint kWmCaptureChanged = 0x0215;

        public const int kWaInactive = 0;
        public const int kVkEscape = 0x1B;

        // ===========[ Window styles and calls ]===================================
        public const uint kWsPopup = 0x80000000;
        public const uint kWsExTopmost = 0x00000008;
        public const uint kWsExToolWindow = 0x00000080;
        public const int kSwShow = 5;
        public const int kIdcCross = 32515;

        // ===========[ Metrics, DPI and DWM ]===================================
        public const int kSmXVirtualScreen = 76;
        public const int kSmYVirtualScreen = 77;
        public const int kSmCxVirtualScreen = 78;
        public const int kSmCyVirtualScreen = 79;

        /// <summary>DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2, a pseudo handle</summary>
        public static readonly nint kDpiAwarenessContextPerMonitorAwareV2 = -4;

        public const uint kDwmwaTransitionsForceDisabled = 3;

        public const uint kMonitorDefaultToNearest = 2;
        public const int kMdtEffectiveDpi = 0;

        /// <summary>HWND_TOPMOST, a pseudo handle</summary>
        public static readonly nint kHwndTopmost = -1;

        public const uint kSwpNoSize = 0x0001;
        public const uint kSwpNoActivate = 0x0010;
        public const uint kSwpNoOwnerZOrder = 0x0200;

        // ===========[ GDI ]===================================
        public const uint kSrcCopy = 0x00CC0020;
        public const uint kCaptureBlt = 0x40000000;
        public const uint kBiRgb = 0;
        public const uint kDibRgbColors = 0;

        // ===========[ user32 ]===================================
        [LibraryImport("user32.dll", SetLastError = true)]
        public static partial ushort RegisterClassExW (in WNDCLASSEXW windowClass);

        [LibraryImport("user32.dll", SetLastError = true)]
        public static partial nint CreateWindowExW (uint exStyle, nint classAtom, nint windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool DestroyWindow (nint window);

        [LibraryImport("user32.dll")]
        public static partial nint DefWindowProcW (nint window, uint message, nint wParam, nint lParam);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ShowWindow (nint window, int command);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool UpdateWindow (nint window);

        [LibraryImport("user32.dll")]
        public static partial nint GetForegroundWindow ();

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetForegroundWindow (nint window);

        [LibraryImport("user32.dll")]
        public static partial nint SetCapture (nint window);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ReleaseCapture ();

        [LibraryImport("user32.dll")]
        public static partial nint LoadCursorW (nint instance, nint cursorId);

        [LibraryImport("user32.dll")]
        public static partial nint SetCursor (nint cursor);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetCursorPos (out POINT point);

        [LibraryImport("user32.dll")]
        public static partial nint BeginPaint (nint window, out PAINTSTRUCT paint);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool EndPaint (nint window, in PAINTSTRUCT paint);

        [LibraryImport("user32.dll")]
        public static partial nint GetDC (nint window);

        [LibraryImport("user32.dll")]
        public static partial int ReleaseDC (nint window, nint deviceContext);

        [LibraryImport("user32.dll")]
        public static partial int GetSystemMetrics (int index);

        [LibraryImport("user32.dll")]
        public static partial nint SetThreadDpiAwarenessContext (nint dpiContext);

        [LibraryImport("user32.dll")]
        public static partial nint MonitorFromPoint (POINT point, uint flags);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetMonitorInfoW (nint monitor, ref MONITORINFO info);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetWindowRect (nint window, out RECT rect);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetWindowPos (nint window, nint insertAfter, int x, int y, int width, int height, uint flags);

        // ===========[ shcore ]===================================
        [LibraryImport("shcore.dll")]
        public static partial int GetDpiForMonitor (nint monitor, int dpiType, out uint dpiX, out uint dpiY);

        // ===========[ gdi32 ]===================================
        [LibraryImport("gdi32.dll")]
        public static partial nint CreateCompatibleDC (nint deviceContext);

        [LibraryImport("gdi32.dll")]
        public static partial nint CreateDIBSection (nint deviceContext, in BITMAPINFOHEADER header, uint usage, out nint bits, nint section, uint offset);

        [LibraryImport("gdi32.dll")]
        public static partial nint SelectObject (nint deviceContext, nint gdiObject);

        [LibraryImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool DeleteObject (nint gdiObject);

        [LibraryImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool DeleteDC (nint deviceContext);

        [LibraryImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool BitBlt (nint destination, int x, int y, int width, int height, nint source, int sourceX, int sourceY, uint rasterOperation);

        [LibraryImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GdiFlush ();

        // ===========[ kernel32 and dwmapi ]===================================
        [LibraryImport("kernel32.dll")]
        public static partial nint GetModuleHandleW (nint moduleName);

        [LibraryImport("dwmapi.dll")]
        public static partial int DwmFlush ();

        [LibraryImport("dwmapi.dll")]
        public static partial int DwmSetWindowAttribute (nint window, uint attribute, in int value, uint valueSize);

        // ===========[ Subclasses/structs ]===================================
        [StructLayout(LayoutKind.Sequential)]
        public struct WNDCLASSEXW
        {
            public uint cbSize;
            public uint style;
            public nint lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public nint hInstance;
            public nint hIcon;
            public nint hCursor;
            public nint hbrBackground;
            public nint lpszMenuName;
            public nint lpszClassName;
            public nint hIconSm;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct PAINTSTRUCT
        {
            public nint hdc;
            public int fErase;
            public RECT rcPaint;
            public int fRestore;
            public int fIncUpdate;
            public fixed byte rgbReserved[32];
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }
    }
}
