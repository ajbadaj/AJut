// eChildWindowDisplayCoordinationStyle moved to AJut.UX, in the same namespace, so every stack's WindowManager and the
// rules they share use one. This forwards anything built against it here to where it is now.
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(AJut.UX.eChildWindowDisplayCoordinationStyle))]

namespace AJut.UX
{
    using System;
    using System.Collections.Generic;
    using Microsoft.UI.Dispatching;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Input;

    /// <summary>
    /// Advanced tracking and coordination of windows, to be used in conjunction with a main window (ie main window and tools windows). One of the main functions
    /// of the WindowManager is to preserve activation order, which can be useful for drop targeting. It also keeps each child window on the root's theme, and
    /// gives every window the <see cref="GlobalAccelerators"/>.
    /// </summary>
    /// <remarks>
    /// The order and the showing and hiding of child windows are <see cref="WindowManagerBase{TWindow}"/>'s, shared with every stack; this class reports
    /// WinUI3's window events to it, and adds what only WinUI3 needs: the root's theme on each child, and the global accelerators.
    /// </remarks>
    /// <example>
    /// ===[ Window Order Tracking ]===
    /// If you want to have a fast cursor based action (like drop preview) that allows you to know which of several windows (who are potentially overlapping), you
    /// might be over, then you need window order tracking and to go from beginning to end.
    /// </example>
    public class WindowManager : WindowManagerBase<Window>
    {
        private FrameworkElement m_rootElement;

        // ===========[ Events ]================================================
        // WinUI3 reports both activation and deactivation through Activated. The root's content may not exist yet when Setup
        //  runs (before InitializeComponent), so each root activation tries again to capture it for theme tracking.
        private void Window_Activated (object sender, WindowActivatedEventArgs e)
        {
            if (sender is not Window window)
            {
                return;
            }

            if (e.WindowActivationState == WindowActivationState.Deactivated)
            {
                this.OnWindowDeactivated(window);
                return;
            }

            if (window == this.Root)
            {
                this.TryInitRootElement();
            }

            this.OnWindowActivated(window);
            this.ApplyTheme(window);
        }

        private void Window_OnClosed (object sender, WindowEventArgs e)
        {
            this.OnWindowClosed((Window)sender);
        }

        private void OnRootVisibilityChanged (object sender, WindowVisibilityChangedEventArgs e)
        {
            if (e.Visible)
            {
                this.OnRootRestored();
            }
            else
            {
                this.OnRootMinimized();
            }
        }

        private void OnRootThemeChanged (FrameworkElement sender, object args)
        {
            if (m_rootElement == null)
            {
                return;
            }

            foreach (Window window in this.Items)
            {
                this.ApplyTheme(window);
            }
        }

        // ===========[ Properties ]============================================
        /// <summary>Keyboard accelerators given to each tracked child window's content, so they work whichever window has focus</summary>
        public List<KeyboardAccelerator> GlobalAccelerators { get; } = new List<KeyboardAccelerator>();

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Puts <paramref name="window"/> on the root window's theme (the root's own theme is the app's to set). Called for each
        /// child as it is tracked, as it activates, and whenever the root's theme changes.
        /// </summary>
        public void ApplyTheme (Window window)
        {
            this.TryInitRootElement();
            if (m_rootElement != null && window != this.Root && window.Content is FrameworkElement windowRoot)
            {
                windowRoot.RequestedTheme = m_rootElement.ActualTheme;
            }
        }

        // ===========[ Toolkit Hooks ]=========================================
        protected override void SubscribeTo (Window window)
        {
            window.Activated += this.Window_Activated;
            window.Closed += this.Window_OnClosed;
        }

        protected override void UnsubscribeFrom (Window window)
        {
            window.Closed -= this.Window_OnClosed;
            window.Activated -= this.Window_Activated;
        }

        protected override void OnRootSetup (Window root)
        {
            root.VisibilityChanged += this.OnRootVisibilityChanged;

            // Captured now if the root's content is already there; otherwise the root's first activation tries again
            this.TryInitRootElement();
        }

        protected override void OnRootTeardown (Window root)
        {
            root.VisibilityChanged -= this.OnRootVisibilityChanged;
        }

        protected override void OnDisposed ()
        {
            if (m_rootElement != null)
            {
                m_rootElement.ActualThemeChanged -= this.OnRootThemeChanged;
                m_rootElement = null;
            }
        }

        protected override void OnChildTracked (Window window)
        {
            if (window.Content is UIElement root)
            {
                root.KeyboardAccelerators.AddEach(this.GlobalAccelerators);
                this.ApplyTheme(window);
            }
        }

        protected override void OnStoppedTracking (Window window)
        {
            if (window.Content is UIElement root)
            {
                root.KeyboardAccelerators.RemoveEach(this.GlobalAccelerators);
            }
        }

        protected override void PresentWindow (Window window)
        {
            if (!window.AppWindow.IsVisible)
            {
                window.AppWindow.Show();
            }

            window.BringToFront();
            window.Content?.Focus(FocusState.Programmatic);
        }

        protected override void RaiseToFront (Window window) => window.BringToFront();
        protected override void FocusWindow (Window window) => window.Content?.Focus(FocusState.Programmatic);

        // WindowXT's Show restores a minimized window, and its Hide minimizes, so a minimized window counts as hidden
        protected override void ShowWindow (Window window) => window.Show();
        protected override void HideWindow (Window window) => window.Hide();
        protected override void CloseWindow (Window window) => window.Close();
        protected override bool IsWindowVisible (Window window) => window.AppWindow.IsVisible && (window.GetWindowState() != eWindowState.Minimized);

        // Deactivating one window comes before activating the next, so the check waits for the dispatcher to get past both
        protected override void RunAfterActivationSettles (Action action)
        {
            this.Root?.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, new DispatcherQueueHandler(action));
        }

        // ===========[ Helper Methods ]========================================
        private void TryInitRootElement ()
        {
            if (m_rootElement == null && this.Root?.Content is FrameworkElement rootElement)
            {
                m_rootElement = rootElement;
                m_rootElement.ActualThemeChanged += this.OnRootThemeChanged;
            }
        }
    }
}
