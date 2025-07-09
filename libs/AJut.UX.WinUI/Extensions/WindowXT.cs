namespace AJut.UX
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Microsoft.UI;
    using Microsoft.UI.Windowing;
    using Microsoft.UI.Xaml;

    public enum eWindowState
    {
        Unknown,
        Maximized,
        Minimized,
        Restored,
        FullScreened,
    }

    /// <summary>
    /// Window extensions
    /// </summary>
    public static class WindowXT
    {
        private static readonly Dictionary<Window, bool> g_isActivatedTracker = new Dictionary<Window, bool>();
        public static void TrackActivation(this Window window, bool isActivated = false)
        {
            if (!g_isActivatedTracker.ContainsKey(window))
            {
                g_isActivatedTracker[window] = isActivated;
                window.Activated -= Window_OnActivationChanged;
                window.Activated += Window_OnActivationChanged;
                window.Closed -= Window_OnClosed;
                window.Closed += Window_OnClosed;
            }
            else
            {
                g_isActivatedTracker[window] = isActivated;
            }
        }
        public static void StopTrackingActivation(this Window window)
        {
            if (g_isActivatedTracker.ContainsKey(window))
            {
                g_isActivatedTracker.Remove(window);
                window.Activated -= Window_OnActivationChanged;
                window.Closed -= Window_OnClosed;
            }
        }

        public static bool IsActivated(this Window window) => g_isActivatedTracker.TryGetValue(window, out bool isActivated) ? isActivated : false;
        public static bool IsDeactivated(this Window window) => !window.IsActivated();

        private static void Window_OnClosed (object sender, WindowEventArgs args)
        {
            (sender as Window)?.StopTrackingActivation();
        }

        private static void Window_OnActivationChanged (object sender, WindowActivatedEventArgs args)
        {
            if (sender is Window window)
            {
                window.TrackActivation(args.WindowActivationState != WindowActivationState.Deactivated);
            }
        }

        public static int DetermineActiveDPI(this Window window)
        {
            try
            {
                return NativeMethods.GetDpiForWindow(window.GetHWND());
            }
            catch (Exception ex)
            {
                // Handle the case where the DPI cannot be determined
                Logger.LogError($"Error determining DPI for window: {ex.Message}");
                return 96; // Default DPI value
            }
        }

        public static double DetermineActiveDPIScale(this Window window)
        {
            return window.DetermineActiveDPI() / 96.0; // Assuming 96 DPI is the base scale 
        }

        public static eWindowState GetWindowState (this Window window)
        {
            // Get the AppWindow from a WinUI Window
            if (window.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                if (presenter.State == OverlappedPresenterState.Maximized)
                {
                    return eWindowState.Maximized;
                }
                else if (presenter.State == OverlappedPresenterState.Minimized)
                {
                    return eWindowState.Minimized;
                }
                else if (presenter.State == OverlappedPresenterState.Restored)
                {
                    return eWindowState.Restored;
                }
            }
            else if (window.AppWindow.Presenter is FullScreenPresenter)
            {
                return eWindowState.FullScreened;
            }

            return eWindowState.Unknown;
        }

        public static bool IsFullScreened(this Window window)
        {
            return window.AppWindow.Presenter is FullScreenPresenter;
        }

        public static bool EnterFullscreen (this Window window)
        {
            return window.PerformPresenterTask<OverlappedPresenter>((presenter) =>
            {
                // Enter fullscreen mode
                presenter.SetBorderAndTitleBar(false, false);
                presenter.Maximize();
                window.AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            });
        }

        public static bool ExitFullscreen (this Window window, int? minimmumWidth = null, int? minimumHeight = null)
        {
            window.AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            return window.PerformPresenterTask<OverlappedPresenter>(presenter =>
            {
                // Exit fullscreen mode
                presenter.SetBorderAndTitleBar(true, true);
                presenter.Restore();
                presenter.PreferredMinimumWidth = minimmumWidth;
                presenter.PreferredMinimumHeight = minimumHeight;
            });
        }

        public static void BringToFront(this Window window)
        {
            window.AppWindow.MoveInZOrderAtTop();
        }

        public static void Show(this Window window)
        {
            // Get the AppWindow from a WinUI Window
            if (window.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                if (presenter.State == OverlappedPresenterState.Minimized)
                {
                    presenter.Restore();
                }    
            }

            window.AppWindow.Show(true);
            window.BringToFront();
        }

        public static nint GetHWND(this Window window)
        {
            return WinRT.Interop.WindowNative.GetWindowHandle(window);
        }
        public static WindowId GetWindowId(this Window window)
        {
            return Win32Interop.GetWindowIdFromWindow(window.GetHWND());
        }

        public static bool PerformPresenterTask<TPresenter> (this Window window, Action<TPresenter> presenterTask)
            where TPresenter : AppWindowPresenter
        {
            if (window.AppWindow.Presenter is TPresenter found)
            {
                presenterTask(found);
                return true;
            }

            return false;
        }

        public static int GetWindowLong (this Window window, int nIndex)
        {
            return NativeMethods.GetWindowLong(window.GetHWND(), nIndex);
        }

        public static void SetWindowLong (this Window window, int nIndex, int dwNewLong)
        {
            NativeMethods.SetWindowLong(window.GetHWND(), nIndex, dwNewLong);
        }

        public static void SetWindowAsFrameless(this Window window)
        {
            window.SetWindowLong(NativeMethods.GWL_STYLE, window.GetWindowLong(NativeMethods.GWL_STYLE) & ~NativeMethods.WS_CAPTION);
        }

        internal class NativeMethods
        {
            public const int GWL_STYLE = -16;
            public const int WS_CAPTION = 0x00C00000;

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern int GetWindowLong (IntPtr hWnd, int nIndex);

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern int SetWindowLong (IntPtr hWnd, int nIndex, int dwNewLong);

            [DllImport("User32.dll")]
            public static extern int GetDpiForWindow (IntPtr hwnd);
        }
    }
}
