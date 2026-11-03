namespace AJut.UX
{
    using System;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;

    // ===========[ WindowManagerBase ]==========================================
    // The toolkit free half of every stack's WindowManager: the activation order (front to back, most recently activated
    // first), tracking and untracking, and the rules for showing and hiding the root's children with the root. Each stack's
    // WindowManager derives from it, subscribes to its own windows' events, reports them in through the On... methods, and
    // does the showing, hiding and raising the base asks for.
    //
    // Reporting is all a derived manager does with its events. What a report means (reorder, show the children, close them)
    // is decided here, so the rules are the same on every stack and can be tested without a window.
    /// <summary>
    /// Tracking and coordination of a root window and its child windows (tool windows, tearoffs), toolkit free: activation
    /// order for things like drop targeting, and showing and hiding the children along with the root. Each stack's
    /// WindowManager derives from this.
    /// </summary>
    /// <typeparam name="TWindow">The stack's window type</typeparam>
    public abstract class WindowManagerBase<TWindow> : ReadOnlyObservableCollection<TWindow>, IDisposable
        where TWindow : class
    {
        private eChildWindowDisplayCoordinationStyle m_showHideChildrenWhen;
        private bool m_ignoreOrderingChanges;
        private TWindow m_activeWindow;

        // ===========[ Setup/Construction/Teardown ]===========================
        protected WindowManagerBase () : base(new ObservableCollection<TWindow>())
        {
        }

        /// <summary>
        /// Makes <paramref name="root"/> the window this manager is built around, and starts tracking it. A manager that was
        /// already set up lets go of everything it had first.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="root"/> is null</exception>
        public void Setup (TWindow root)
        {
            this.Dispose();
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root), "Window manager requires a root window");
            }

            this.Root = root;
            this.StartTracking(root);
            this.OnRootSetup(root);
        }

        /// <summary>Stops tracking every window, the root included, and lets go of the root</summary>
        public void Dispose ()
        {
            // A child still tracked here would keep this manager in its event handlers, and through it every other window
            foreach (TWindow child in this.Items.Where(this.IsChild).ToList())
            {
                this.StopTracking(child);
            }

            if (this.Root != null)
            {
                TWindow root = this.Root;
                this.StopTrackingCore(root);
                this.OnRootTeardown(root);
                this.Root = null;
            }

            this.OnDisposed();
        }

        // ===========[ Properties ]============================================
        /// <summary>
        /// The window that this manager utilizes as the root window
        /// </summary>
        public TWindow Root { get; /*Not prepared to handle change, private set only exists for setup and dispose*/ private set; }

        /// <summary>
        /// The style in which window show/hiding is coordinated (see <see cref="eChildWindowDisplayCoordinationStyle"/>)
        /// </summary>
        public eChildWindowDisplayCoordinationStyle ShowHideChildrenWhen
        {
            get => m_showHideChildrenWhen;
            set
            {
                if (m_showHideChildrenWhen != value)
                {
                    m_showHideChildrenWhen = value;
                    this.OnPropertyChanged(new PropertyChangedEventArgs(nameof(ShowHideChildrenWhen)));
                }
            }
        }

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Begin tracking a new window as one of the root's children, and bring it to the front. Returns false for a null
        /// window or one already tracked.
        /// </summary>
        public bool Track (TWindow window)
        {
            if (window == null || this.Items.Contains(window))
            {
                return false;
            }

            this.StartTracking(window);
            this.OnChildTracked(window);
            this.BringToFront(window);
            return true;
        }

        /// <summary>
        /// Stop tracking a window. Returns false for a null window or one not tracked.
        /// </summary>
        public bool StopTracking (TWindow window)
        {
            if (window == null)
            {
                return false;
            }

            return this.StopTrackingCore(window);
        }

        /// <summary>
        /// Brings a window to the front: shows it if it is hidden, raises it, gives it focus, and puts it first in the
        /// activation order
        /// </summary>
        public void BringToFront (TWindow window)
        {
            m_ignoreOrderingChanges = true;
            try
            {
                this.PresentWindow(window);
            }
            finally
            {
                m_ignoreOrderingChanges = false;
            }

            this.MoveToFrontOfOrder(window);
        }

        /// <summary>
        /// Ensure all windows are shown, preserving their current ordering
        /// </summary>
        public void ShowAllWindows (bool includingRoot = false)
        {
            this.ForEachBackToFront(includingRoot, this.ShowWindow);
        }

        /// <summary>
        /// Ensure all windows are hidden, preserving their current order
        /// </summary>
        public void HideAllWindows (bool includingRoot = false)
        {
            this.ForEach(includingRoot, this.HideWindow);
        }

        /// <summary>
        /// Close all child windows that are currently being tracked, which removes them from tracking
        /// </summary>
        public void CloseAllChildWindows ()
        {
            this.ForEach(false, this.CloseWindow);
        }

        /// <summary>
        /// Run an action on each window in activation order (front to back), and without disturbing the tracked
        /// activation order.
        /// </summary>
        public void ForEach (bool includingRoot, Action<TWindow> action)
        {
            this.RunActionForEachWithoutDisruptingOrder(includingRoot, true, action);
        }

        /// <summary>
        /// Run an action on each window in reverse activation order (back to front), and without disturbing the tracked
        /// activation order.
        /// </summary>
        public void ForEachBackToFront (bool includingRoot, Action<TWindow> action)
        {
            this.RunActionForEachWithoutDisruptingOrder(includingRoot, false, action);
        }

        // ===========[ Helpers For The Toolkit ]===============================
        /// <summary>A tracked window became the active one</summary>
        protected void OnWindowActivated (TWindow window)
        {
            // Which window is active is kept even while an action runs over the windows without reordering them
            m_activeWindow = window;
            this.MoveToFrontOfOrder(window);

            if (window == this.Root
                && this.ShowHideChildrenWhen == eChildWindowDisplayCoordinationStyle.RootActivationDeactivation
                && this.Items.Any(_window => !this.IsWindowVisible(_window)))
            {
                this.ShowAllWindows();
                this.RaiseToFront(this.Root);
            }
        }

        /// <summary>
        /// A tracked window stopped being the active one. Another one may become active straight after, so what this means
        /// is decided once that has settled.
        /// </summary>
        protected void OnWindowDeactivated (TWindow window)
        {
            if (m_activeWindow == window)
            {
                m_activeWindow = null;
            }

            this.RunAfterActivationSettles(this.HideChildrenIfNoWindowIsActive);
        }

        /// <summary>The root window was minimized</summary>
        protected void OnRootMinimized ()
        {
            if (this.ShowHideChildrenWhen == eChildWindowDisplayCoordinationStyle.RootMinimizationRestoration)
            {
                this.HideAllWindows();
            }
        }

        /// <summary>The root window came back from being minimized</summary>
        protected void OnRootRestored ()
        {
            if (this.ShowHideChildrenWhen == eChildWindowDisplayCoordinationStyle.RootMinimizationRestoration)
            {
                this.ShowAllWindows();
            }
        }

        /// <summary>A tracked window closed. When it is the root, every child closes with it.</summary>
        protected void OnWindowClosed (TWindow window)
        {
            bool wasRoot = (window == this.Root);
            this.StopTracking(window);
            if (wasRoot)
            {
                foreach (TWindow child in this.Items.Where(this.IsChild).ToList())
                {
                    this.StopTracking(child);
                    this.CloseWindow(child);
                }
            }
        }

        // ===========[ Toolkit Hooks ]=========================================
        /// <summary>Subscribes to the window's activation, deactivation and close, to report them</summary>
        protected abstract void SubscribeTo (TWindow window);

        /// <summary>Undoes <see cref="SubscribeTo"/></summary>
        protected abstract void UnsubscribeFrom (TWindow window);

        /// <summary>Shows the window if it is hidden, raises it to the top, and gives it focus</summary>
        protected abstract void PresentWindow (TWindow window);

        /// <summary>Raises the window above the others, as an activation that reordered it calls for</summary>
        protected abstract void RaiseToFront (TWindow window);

        /// <summary>Shows a hidden window</summary>
        protected abstract void ShowWindow (TWindow window);

        /// <summary>Hides a window without closing it</summary>
        protected abstract void HideWindow (TWindow window);

        /// <summary>Closes a window</summary>
        protected abstract void CloseWindow (TWindow window);

        /// <summary>Whether the window is shown right now</summary>
        protected abstract bool IsWindowVisible (TWindow window);

        /// <summary>
        /// Runs <paramref name="action"/> once the activation change that is underway has finished: on the stack's UI
        /// thread, after the events already queued
        /// </summary>
        protected abstract void RunAfterActivationSettles (Action action);

        /// <summary>The root became this manager's root: subscribe to whatever only the root's events say (minimize, restore)</summary>
        protected virtual void OnRootSetup (TWindow root)
        {
        }

        /// <summary>The root is being let go of: undo <see cref="OnRootSetup"/></summary>
        protected virtual void OnRootTeardown (TWindow root)
        {
        }

        /// <summary>A child window started being tracked, before it is brought to the front</summary>
        protected virtual void OnChildTracked (TWindow window)
        {
        }

        /// <summary>A window, the root or a child, stopped being tracked</summary>
        protected virtual void OnStoppedTracking (TWindow window)
        {
        }

        /// <summary>The manager let go of every window</summary>
        protected virtual void OnDisposed ()
        {
        }

        /// <summary>
        /// Whether an activation of <paramref name="window"/> leaves it where it is in the activation order instead of
        /// moving it to the front (and raising it); false for every window by default
        /// </summary>
        protected virtual bool KeepsItsPlaceWhenActivated (TWindow window) => false;

        /// <summary>Gives <paramref name="window"/> focus after an activation reordered it, or kept it in place</summary>
        protected virtual void FocusWindow (TWindow window)
        {
        }

        // ===========[ Helper Methods ]========================================
        private bool IsChild (TWindow window) => window != this.Root;

        private void StartTracking (TWindow window)
        {
            this.SubscribeTo(window);
            this.Items.Add(window);
        }

        private bool StopTrackingCore (TWindow window)
        {
            // Unsubscribing before the remove does no harm, and leaves nothing hooked up should the remove's handlers throw
            this.UnsubscribeFrom(window);
            this.OnStoppedTracking(window);
            if (m_activeWindow == window)
            {
                m_activeWindow = null;
            }

            return this.Items.Remove(window);
        }

        private void MoveToFrontOfOrder (TWindow window)
        {
            if (m_ignoreOrderingChanges)
            {
                return;
            }

            if (this.KeepsItsPlaceWhenActivated(window))
            {
                this.FocusWindow(window);
                return;
            }

            // Update the tracking order, and bring it as forward as we can
            int index = this.Items.IndexOf(window);
            if (index > 0)
            {
                this.Items.RemoveAt(index);
                this.Items.Insert(0, window);
            }

            this.RaiseToFront(window);
            this.FocusWindow(window);
        }

        // Clicking from one of the app's windows to another deactivates the first before the second activates, so this waits
        //  for both before deciding. A manager let go of in between has nothing left to hide.
        private void HideChildrenIfNoWindowIsActive ()
        {
            // It doesn't matter if a window is deactivated as long as another was activated
            if ((m_activeWindow != null) || (this.Root == null))
            {
                return;
            }

            if (this.ShowHideChildrenWhen == eChildWindowDisplayCoordinationStyle.RootActivationDeactivation)
            {
                this.HideAllWindows();
            }
        }

        private void RunActionForEachWithoutDisruptingOrder (bool includingRoot, bool forward, Action<TWindow> action)
        {
            m_ignoreOrderingChanges = true;
            try
            {
                var windows = includingRoot ? this.Items : this.Items.Where(this.IsChild);
                if (!forward)
                {
                    windows = windows.Reverse();
                }

                foreach (TWindow window in windows.ToList())
                {
                    action(window);
                }
            }
            finally
            {
                m_ignoreOrderingChanges = false;
            }
        }
    }
}
