namespace AJut.UX
{
    using System;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Controls;

    public static class WindowsTaskbar
    {
        private static APPBARDATA kAppBarData;

        /// <summary>Static initializer of the <see cref="Taskbar" /> class.</summary>
        static WindowsTaskbar ()
        {
            kAppBarData = new APPBARDATA
            {
                cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA)),
                hWnd = PInvoke.FindTaskbarWindow()
            };
        }

        /// <summary>
        ///   Gets a value indicating whether the taskbar is always on top of other windows.
        /// </summary>
        /// <value><c>true</c> if the taskbar is always on top of other windows; otherwise, <c>false</c>.</value>
        /// <remarks>This property always returns <c>false</c> on Windows 7 and newer.</remarks>
        public static bool AlwaysOnTop
        {
            get
            {
                int state = PInvoke.SHAppBarMessage(AppBarMessage.GetState, ref kAppBarData).ToInt32();
                return ((ABS)state).HasFlag(ABS.AlwaysOnTop);
            }
        }

        /// <summary>
        ///   Gets a value indicating whether the taskbar is automatically hidden when inactive.
        /// </summary>
        /// <value><c>true</c> if the taskbar is set to auto-hide is enabled; otherwise, <c>false</c>.</value>
        public static bool AutoHide
        {
            get
            {
                int state = PInvoke.SHAppBarMessage(AppBarMessage.GetState, ref kAppBarData).ToInt32();
                return ((ABS)state).HasFlag(ABS.AutoHide);
            }
        }

        /// <summary>Gets the taskbar's window handle.</summary>
        public static IntPtr TaskbarWindowHandle => kAppBarData.hWnd;

        /// <summary>Gets the current display bounds of the taskbar.</summary>
        public static Rect FindCurrentBounds ()
        {
            var rect = new RECT();
            if (PInvoke.GetWindowRect(TaskbarWindowHandle, ref rect))
            {
                return GetWrekd(rect);
            }

            return Rect.Empty;
        }

        /// <summary>Gets the display bounds when the taskbar is fully visible.</summary>
        public static Rect DisplayBounds
        {
            get
            {
                if (RefreshBoundsAndPosition())
                {
                    return GetWrekd(kAppBarData.rect);
                }

                return FindCurrentBounds();
            }
        }

        /// <summary>Gets the taskbar's position on the screen.</summary>
        public static Dock DockSide
        {
            get
            {
                if (RefreshBoundsAndPosition())
                {
                    if ((int)kAppBarData.uEdge == -1)
                    {
                        return Dock.Bottom;
                    }

                    return (Dock)kAppBarData.uEdge;
                }

                return Dock.Bottom;
            }
        }

        /// <summary>Hides the taskbar.</summary>
        public static void Hide ()
        {
            const int SW_HIDE = 0;
            PInvoke.ShowWindow(TaskbarWindowHandle, SW_HIDE);
        }

        /// <summary>Shows the taskbar.</summary>
        public static void Show ()
        {
            const int SW_SHOW = 1;
            PInvoke.ShowWindow(TaskbarWindowHandle, SW_SHOW);
        }

        // =======================[ Private Utilities ]===================

        private static Rect GetWrekd (RECT rect)
        {
            return new Rect(
                    rect.Left,
                    rect.Top,
                    rect.Right - rect.Left,
                    rect.Bottom - rect.Top);
        }

        private static bool RefreshBoundsAndPosition ()
        {
            //! PInvoke.SHAppBarMessage returns IntPtr.Zero **if it fails**
            return PInvoke.SHAppBarMessage(AppBarMessage.GetTaskbarPos, ref kAppBarData) != IntPtr.Zero;
        }

        private static class PInvoke
        {
            private const string kClassName = "Shell_TrayWnd";

            public static IntPtr FindTaskbarWindow () => FindWindow(kClassName, null);

            [DllImport("user32.dll", SetLastError = true)]
            private static extern IntPtr FindWindow (string lpClassName, string lpWindowName);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool GetWindowRect (IntPtr hWnd, ref RECT lpRect);

            [DllImport("shell32.dll", SetLastError = true)]
            public static extern IntPtr SHAppBarMessage (AppBarMessage dwMessage, [In] ref APPBARDATA pData);

            [DllImport("user32.dll")]
            public static extern int ShowWindow (IntPtr hwnd, int command);

        }

        private enum ABS
        {
            AutoHide = 0x01,
            AlwaysOnTop = 0x02,
        }

        ////private enum ABE : uint
        private enum AppBarEdge : uint
        {
            Left = 0,
            Top = 1,
            Right = 2,
            Bottom = 3
        }

        ////private enum ABM : uint
        private enum AppBarMessage : uint
        {
            New = 0x00000000,
            Remove = 0x00000001,
            QueryPos = 0x00000002,
            SetPos = 0x00000003,
            GetState = 0x00000004,
            GetTaskbarPos = 0x00000005,
            Activate = 0x00000006,
            GetAutoHideBar = 0x00000007,
            SetAutoHideBar = 0x00000008,
            WindowPosChanged = 0x00000009,
            SetState = 0x0000000A,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public AppBarEdge uEdge;
            public RECT rect;
            public int lParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}