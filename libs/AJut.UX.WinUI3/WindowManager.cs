namespace AJut.UX
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Input;

    /// <summary>
    /// How child window showing/hiding is coordinated for all windows in a <see cref="WindowManager"/>
    /// </summary>
    public enum eChildWindowDisplayCoordinationStyle
    {
        /// <summary>
        /// Only allow manual show/hide of windows
        /// </summary>
        Manually,

        /// <summary>
        /// Show/hide all child windows when the root window is activated/deactivated respectively
        /// </summary>
        RootActivationDeactivation,

        /// <summary>
        /// Show/hide all child windows when the root window is restored/minized respectively
        /// </summary>
        RootMinimizationRestoration,
    }

    /// <summary>
    /// Advanced tracking and coordination of windows, to be used in conjunction with a main window (ie main window and tools windows). One of the main functions
    /// of the WindowManager is to preserve activation order, which can be useful for drop targetting. The other main function is command retargetting, this allows
    /// windows, which by definition have separate roots, to coordinate commands to centralized points of your choosing.
    /// </summary>
    /// <example>
    /// ===[ Window Order Tracking ]===
    /// If you want to have a fast cursor based action (like drop preview) that allows you to know which of several windows (who are potentially overlapping), you
    /// might be over, then you need window order tracking and to go from beginning to end.
    /// 
    /// ===[ Command Retargeting ]===
    /// If you had a window with a video, and wanted to make it so no matter which utility window had focus, a play command would start the video, you might have the
    /// video control handle a play command in it's command bindings, then use your <see cref="WindowManager"/> to sign it up for <see cref="StartCommandRouteBackRetargettingFor(UIElement)"/>
    /// which would ensure all windows would handle and forward the play command to the video control!
    /// </example>
    public class WindowManager : ReadOnlyObservableCollection<Window>, IDisposable
    {
        private eChildWindowDisplayCoordinationStyle m_showHideChildrenWhen;
        private bool m_ignoreOrderingChanges;
        private readonly List<UIElement> m_commandRetargetters = new();
        private bool m_setOwnerAsRootForEachChildWindow = true;

        public WindowManager () : base(new ObservableCollection<Window>())
        {
        }

        public void Setup(Window root)
        {
            if (root == null)
            {
                throw new Exception("Window manager requires a root window");
            }

            // Do this after so we don't add any command retargetting to itself
            this.Root = root;
            this.DoSignupAndStorageTracking(root);

            // In addition to basic tracking, we need to track a few other things too so we can coordinate
            //  show/hide of child windows properly.
            this.Root.VisibilityChanged += this.OnRootVisibilityChanged;
            this.Root.Activated += this.OnRootActivated;
            this.Root.Closed += this.OnRootClosed;
        }

        public void Dispose ()
        {
            this.DoStandaradWindowTrackingRemoval(this.Root);
            this.Root.Activated -= this.OnRootActivated;
            this.Root.Closed -= this.OnRootClosed;
            this.Root = null;
        }

        // =====================[ Property Interface ]=====================================

        /// <summary>
        /// The window that this manager utilizes as the root window
        /// </summary>
        public Window Root { get; /*Not prepared to handle change, private set only exists for dispose*/ private set; }

        public List<KeyboardAccelerator> GlobalAccelerators { get; } = new List<KeyboardAccelerator>();

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

        // =====================[ Method Interface ]=====================================

        /// <summary>
        /// Begin tracking a new window, and initiate tracking and command retargetting
        /// </summary>
        public bool Track (Window window)
        {
            if (window == null || this.Items.Contains(window))
            {
                return false;
            }

            this.DoSignupAndStorageTracking(window);

            if (window.Content is UIElement root)
            {
                root.KeyboardAccelerators.AddEach(this.GlobalAccelerators);
            }

            this.BringToFront(window);
            return true;
        }

        /// <summary>
        /// Stop tracking a window, remove tracking and remove command retargeting
        /// </summary>
        public bool StopTracking (Window window)
        {
            if (window == null)
            {
                return false;
            }

            return this.DoStandaradWindowTrackingRemoval(window);
        }

        /// <summary>
        /// Brings a window to the front. This ensures ordering is properly done, resolves any topmost issues, and pushes focus to the inside.
        /// </summary>
        public void BringToFront (Window window)
        {
            m_ignoreOrderingChanges = true;
            try
            {
                if (!window.AppWindow.IsVisible)
                {
                    window.AppWindow.Show();
                }

                window.BringToFront();
                window.Content.Focus(FocusState.Programmatic);
            }
            finally
            {
                m_ignoreOrderingChanges = false;
            }

            this.OnItemActivatationChanged(window, WindowActivationState.CodeActivated);
        }

        /// <summary>
        /// Ensure all windows are shown, preserving their current ordering
        /// </summary>
        public void ShowAllWindows (bool includingRoot = false)
        {
            this.ForEachBackToFront(includingRoot, child => child.AppWindow.Show());
        }

        /// <summary>
        /// Ensure all windows are hidden, preserving their current order
        /// </summary>
        public void HideAllWindows (bool includingRoot = false)
        {
            this.ForEach(includingRoot, w => w.AppWindow.Hide());
        }

        /// <summary>
        /// Close all windows that are currently being tracked, and remove them from tracking
        /// </summary>
        public void CloseAllChildWindows ()
        {
            this.ForEach(false, w => w.Close());
        }

        /// <summary>
        /// Run an action on each window in activation order (front to back), and without disturbing the tracked
        /// activation order.
        /// </summary>
        public void ForEach (bool includingRoot, Action<Window> action)
        {
            this.RunActionForEachWithoutDisruptingOrder(includingRoot, true, action);
        }

        /// <summary>
        /// Run an action on each window in reverse activation order (back to front), and without disturbing the tracked
        /// activation order.
        /// </summary>
        public void ForEachBackToFront (bool includingRoot, Action<Window> action)
        {
            this.RunActionForEachWithoutDisruptingOrder(includingRoot, false, action);
        }

        // =====================[ Private Utility Methods ]=====================================

        private void DoSignupAndStorageTracking (Window window)
        {
            window.Activated += this.Window_Activated;
            window.Closed += this.Window_OnClosed;
            this.Items.Add(window);

        }

        private bool DoStandaradWindowTrackingRemoval (Window window)
        {
            // Since it doesn't hurt, and it fulfills some protection from paranoia, keeping this before the remove
            window.Closed -= this.Window_OnClosed;
            window.Activated -= this.Window_Activated;

            if (window.Content is UIElement root)
            {
                root.KeyboardAccelerators.RemoveEach(this.GlobalAccelerators);
            }

            return this.Items.Remove(window);
        }

        private void OnRootClosed (object sender, WindowEventArgs e)
        {
            foreach (Window child in this.Items.Where(i => i != this.Root).ToList())
            {
                this.StopTracking(child);
                child.Close();
            }
        }

        private void OnRootActivated (object sender, WindowActivatedEventArgs e)
        {
            if (this.ShowHideChildrenWhen == eChildWindowDisplayCoordinationStyle.RootActivationDeactivation)
            {
                if (this.Items.Any(w => !w.Visible))
                {
                    this.ShowAllWindows();
                    this.Root.BringToFront();
                }
            }
        }

        private void OnWindowDeactivated (object sender, EventArgs e)
        {
            // It doesn't matter if a window is deactivated as long as another was activated
            if (this.Items.Any(w => w.AppWindow.IsVisible))
            {
                return;
            }

            if (this.ShowHideChildrenWhen == eChildWindowDisplayCoordinationStyle.RootActivationDeactivation)
            {
                this.HideAllWindows();
            }
        }

        private void OnRootVisibilityChanged (object sender, WindowVisibilityChangedEventArgs e)
        {
            if (this.ShowHideChildrenWhen != eChildWindowDisplayCoordinationStyle.RootMinimizationRestoration)
            {
                return;
            }
            else if (e.Visible)
            {
                this.ShowAllWindows();
            }
            else
            {
                this.HideAllWindows();
            }
        }

        private void OnItemActivatationChanged (Window window, WindowActivationState state)
        {
            if (m_ignoreOrderingChanges)
            {
                return;
            }

            // Update the tracking order, and bring it as forward as we can
            this.Items.Remove(window);
            this.Items.Insert(0, window);
            window.BringToFront();
            window.Content.Focus(FocusState.Programmatic);
        }

        private void RunActionForEachWithoutDisruptingOrder (bool includingRoot, bool forward, Action<Window> action)
        {
            m_ignoreOrderingChanges = true;
            try
            {
                var itemsEnum = includingRoot ? this.Items : this.Items.Where(w => w != this.Root);
                if (!forward)
                {
                    itemsEnum = itemsEnum.Reverse();
                }

                var items = itemsEnum.ToList();
                foreach (Window child in items)
                {
                    action(child);
                }
            }
            finally
            {
                m_ignoreOrderingChanges = false;
            }
        }

        private void Window_Activated (object sender, WindowActivatedEventArgs e)
        {
            this.OnItemActivatationChanged((Window)sender, e.WindowActivationState);
        }

        private void Window_OnClosed (object sender, WindowEventArgs e)
        {
            this.StopTracking((Window)sender);
        }
    }
}