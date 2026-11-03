namespace AJut.UX
{
    using System;
    using System.Collections.Generic;
    using AppKit;
    using Foundation;

    // ===========[ WindowManager ]==============================================
    // AppKit's WindowManager: AJut.UX's WindowManagerBase, which keeps the activation order and decides when the root's
    // children show and hide, told about each tracked window through AppKit's window notifications. A window's activation
    // is it becoming the key window, and its deactivation is it resigning key. Notifications rather than the window's
    // delegate, so a tracked window keeps its delegate (a ThemedWindow is its own) and nothing here competes with it.
    //
    // What the Mac does differently from Windows, and so what this does and doesn't do:
    //  - Activating a Mac app brings every one of its windows forward, so there is no raising of children over other apps'
    //    windows to do, and raising after an activation only orders the window front within the app.
    //  - A window's theme is its appearance. One with no appearance of its own follows the app's (which AppThemeManager
    //    sets), so a child only needs the root's appearance when the root has one of its own, which ApplyTheme copies,
    //    and keeps copying as the root's changes.
    //  - RootActivationDeactivation hides the children (orders them out) once no tracked window is key, which is when the
    //    app goes to the background or another of its windows takes over, and shows them when the root is key again.
    //  - RootMinimizationRestoration follows the root into and out of the Dock.
    //  - There are no global accelerators to hand out (WinUI3's GlobalAccelerators) and no commands to retarget (WPF's):
    //    the app's main menu and its key equivalents already reach whichever window is key, through the responder chain.
    //  - A Mac app keeps running when its root window closes, so a closed root ends the manager: its children close, and
    //    it lets go of every window, as Dispose does.
    //
    // Lifetime: every notification watch and the root's appearance watch are held here and disposed when their window
    //  stops being tracked, or on Dispose. Until then the notification center holds this manager, the same as a window's
    //  event holds the WinUI3 and WPF ones, so a manager is disposed when its app is done with it.
    /// <summary>
    /// Tracking and coordination of an AppKit root window and its child windows (tool windows, tearoffs): activation order
    /// for things like drop targeting, the root's appearance on each child, and showing and hiding the children with the
    /// root. The order and the show and hide rules are <see cref="WindowManagerBase{TWindow}"/>'s, shared with every stack.
    /// </summary>
    public class WindowManager : WindowManagerBase<NSWindow>
    {
        private readonly Dictionary<NSWindow, WindowWatch> m_windowWatches = new Dictionary<NSWindow, WindowWatch>();
        private NSObject m_rootMiniaturizeWatch;
        private NSObject m_rootDeminiaturizeWatch;
        private IDisposable m_rootAppearanceWatch;

        // ===========[ Setup/Construction/Teardown ]===========================
        /// <summary>A manager with no root yet: call <see cref="WindowManagerBase{TWindow}.Setup"/> to give it one</summary>
        public WindowManager ()
        {
        }

        /// <summary>A manager built around <paramref name="root"/></summary>
        public WindowManager (NSWindow root)
        {
            this.Setup(root);
        }

        // ===========[ Events ]================================================
        private void OnWindowDidBecomeKey (object sender, NSNotificationEventArgs e)
        {
            if (e.Notification.Object is NSWindow window)
            {
                this.OnWindowActivated(window);
                this.ApplyTheme(window);
            }
        }

        private void OnWindowDidResignKey (object sender, NSNotificationEventArgs e)
        {
            if (e.Notification.Object is NSWindow window)
            {
                this.OnWindowDeactivated(window);
            }
        }

        private void OnWindowWillClose (object sender, NSNotificationEventArgs e)
        {
            if (e.Notification.Object is not NSWindow window)
            {
                return;
            }

            bool wasRoot = (window == this.Root);
            this.OnWindowClosed(window);
            if (wasRoot)
            {
                this.Dispose();
            }
        }

        private void OnRootDidMiniaturize (object sender, NSNotificationEventArgs e) => this.OnRootMinimized();
        private void OnRootDidDeminiaturize (object sender, NSNotificationEventArgs e) => this.OnRootRestored();

        private void OnRootAppearanceChanged (NSObservedChange change)
        {
            foreach (NSWindow window in this.Items)
            {
                this.ApplyTheme(window);
            }
        }

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Gives <paramref name="window"/> the root's appearance, so it is drawn light or dark as the root is. Called for
        /// each child as it is tracked and as it becomes key, and whenever the root's appearance changes. A root with no
        /// appearance of its own leaves the child following the app's, as the root does.
        /// </summary>
        public void ApplyTheme (NSWindow window)
        {
            if ((this.Root != null) && (window != null) && (window != this.Root))
            {
                window.Appearance = this.Root.Appearance;
            }
        }

        // ===========[ Toolkit Hooks ]=========================================
        protected override void SubscribeTo (NSWindow window)
        {
            m_windowWatches[window] = new WindowWatch(
                NSWindow.Notifications.ObserveDidBecomeKey(window, this.OnWindowDidBecomeKey),
                NSWindow.Notifications.ObserveDidResignKey(window, this.OnWindowDidResignKey),
                NSWindow.Notifications.ObserveWillClose(window, this.OnWindowWillClose)
            );
        }

        protected override void UnsubscribeFrom (NSWindow window)
        {
            if (m_windowWatches.Remove(window, out WindowWatch watch))
            {
                watch.Dispose();
            }
        }

        protected override void OnRootSetup (NSWindow root)
        {
            m_rootMiniaturizeWatch = NSWindow.Notifications.ObserveDidMiniaturize(root, this.OnRootDidMiniaturize);
            m_rootDeminiaturizeWatch = NSWindow.Notifications.ObserveDidDeminiaturize(root, this.OnRootDidDeminiaturize);
            m_rootAppearanceWatch = root.AddObserver("appearance", NSKeyValueObservingOptions.New, this.OnRootAppearanceChanged);
        }

        protected override void OnRootTeardown (NSWindow root)
        {
            m_rootMiniaturizeWatch?.Dispose();
            m_rootMiniaturizeWatch = null;
            m_rootDeminiaturizeWatch?.Dispose();
            m_rootDeminiaturizeWatch = null;
            m_rootAppearanceWatch?.Dispose();
            m_rootAppearanceWatch = null;
        }

        protected override void OnChildTracked (NSWindow window) => this.ApplyTheme(window);

        protected override void PresentWindow (NSWindow window) => window.MakeKeyAndOrderFront(null);

        // A window that just became key is already in front; this keeps the order when the base raises the root over the
        //  children it just showed
        protected override void RaiseToFront (NSWindow window)
        {
            if (window.IsVisible)
            {
                window.OrderFront(null);
            }
        }

        protected override void ShowWindow (NSWindow window) => window.OrderFront(null);
        protected override void HideWindow (NSWindow window) => window.OrderOut(null);
        protected override void CloseWindow (NSWindow window) => window.Close();

        // A miniaturized window is in the Dock, not on screen
        protected override bool IsWindowVisible (NSWindow window) => window.IsVisible && !window.IsMiniaturized;

        // One window resigns key before the next becomes key, so the check waits for the next pass of the main run loop
        protected override void RunAfterActivationSettles (Action action) => NSApplication.SharedApplication.BeginInvokeOnMainThread(action);

        // ===========[ Subclasses/structs ]====================================
        // The notification watches on one tracked window, disposed together when it stops being tracked
        private sealed class WindowWatch : IDisposable
        {
            private readonly NSObject[] m_watches;

            public WindowWatch (params NSObject[] watches)
            {
                m_watches = watches;
            }

            public void Dispose ()
            {
                foreach (NSObject watch in m_watches)
                {
                    watch.Dispose();
                }
            }
        }
    }
}
