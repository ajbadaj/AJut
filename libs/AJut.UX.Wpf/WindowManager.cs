namespace AJut.UX
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;
    using System.Windows;
    using System.Windows.Input;

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
        private readonly List<UIElement> m_commandRetargetters = new List<UIElement>();
        private bool m_setOwnerAsRootForEachChildWindow = true;

        public WindowManager (Window root) : base(new ObservableCollection<Window>())
        {
            if (root == null)
            {
                throw new YouCantDoThatException("Window manager requires a root window");
            }

            // Do this after so we don't add any command retargetting to itself
            this.Root = root;
            this.DoSignupAndStorageTracking(root);

            // In addition to basic tracking, we need to track a few other things too so we can coordinate
            //  show/hide of child windows properly.
            this.Root.StateChanged += this.OnRootStateChanged;
            this.Root.Activated += this.OnRootActivated;
            this.Root.Closed += this.OnRootClosed;
        }

        public void Dispose ()
        {
            this.DoStandaradWindowTrackingRemoval(this.Root);
            this.Root.StateChanged -= this.OnRootStateChanged;
            this.Root.Activated -= this.OnRootActivated;
            this.Root.Closed -= this.OnRootClosed;
            this.Root = null;
        }

        // =====================[ Property Interface ]=====================================

        /// <summary>
        /// The window that this manager utilizes as the root window
        /// </summary>
        public Window Root { get; /*Not prepared to handle change, private set only exists for dispose*/ private set; }

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

        /// <summary>
        /// Indicates if each child window's <see cref="Window.Owner"/> should be set as root (default is true). When owner
        /// is set, the root window will be forced (by the os) to be bottom most, so <see cref="WindowManager"/> will track
        /// it the same way. Window owner is usually set for automatic functionality similar to what <see cref="WindowManager"/> provides
        /// (though less extensive) via child window collection (though not necessarily ordered) and to make sure all child windows
        /// are closed when root is closed (which the <see cref="WindowManager"/> will do for you if <see cref="SetOwnerAsRootForEachChildWindow"/> is set to false).
        /// </summary>
        public bool SetOwnerAsRootForEachChildWindow
        {
            get => m_setOwnerAsRootForEachChildWindow;
            set
            {
                if (m_setOwnerAsRootForEachChildWindow != value)
                {
                    m_setOwnerAsRootForEachChildWindow = value;
                    this.OnPropertyChanged(new PropertyChangedEventArgs(nameof(SetOwnerAsRootForEachChildWindow)));
                    this.ForEach(false, w => w.Owner = value ? this.Root : null);

                    // Since this involves supression of bring to front, there are certain
                    // considerations that must be taken if root is active
                    if (this.Root.IsActive)
                    {
                        if (value)
                        {
                            this.Items.Remove(this.Root);
                            this.Items.Add(this.Root);
                            this.ForEachBackToFront(false, child => this.MakeWindowAsTippityTopAsPossible(child));
                        }
                        else
                        {
                            this.BringToFront(this.Root);
                        }
                    }
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

            if (this.SetOwnerAsRootForEachChildWindow)
            {
                window.Owner = this.Root;
            }
            this.DoSignupAndStorageTracking(window);

            foreach (UIElement commandRetargetSource in m_commandRetargetters)
            {
                this.RetargetCommandsOnto(window, from: commandRetargetSource);
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
        /// Stores the given element, goes over all command bindings it currently has and makes it so windows execute said commands, and target them
        /// back onto the given element for execution. This allows elements to handle commands even when their sources are executing in separate windows.
        /// As new windows are added this process will be repeated for them. Modifying the command's <see cref="UIElement.CommandBindings"/> cannot be tracked
        /// and so may affect your abillity to cleanup, and may have mixed affect as windows are added to tracking later.
        /// </summary>
        public void StartCommandRouteBackRetargettingFor (UIElement element)
        {
            m_commandRetargetters.Add(element);
            foreach (Window window in this.Items)
            {
                this.RetargetCommandsOnto(window, from: element);
            }
        }

        /// <summary>
        /// Removes storage of the given element for command retargetting, and attempts to cleanup all targetting that element has requested.
        /// </summary>
        public void StopCommandRouteBackRetargettingFor (UIElement commandSource)
        {
            foreach (Window window in this.Items)
            {
                var commandsToRemove = window.CommandBindings.OfType<RetargetingCommandBinding>().Where(cb => cb.Source == commandSource).ToList();
                foreach (var command in commandsToRemove)
                {
                    window.CommandBindings.Remove(command);
                }
            }
        }

        /// <summary>
        /// Brings a window to the front. This ensures ordering is properly done, resolves any topmost issues, and pushes focus to the inside.
        /// </summary>
        public void BringToFront (Window window)
        {
            m_ignoreOrderingChanges = true;
            try
            {
                if (!window.IsActive)
                {
                    window.Show();
                }

                this.MakeWindowAsTippityTopAsPossible(window);
                window.Focus();
            }
            finally
            {
                m_ignoreOrderingChanges = false;
            }

            this.OnItemActivated(window);
        }

        /// <summary>
        /// Ensure all windows are shown, preserving their current ordering
        /// </summary>
        public void ShowAllWindows (bool includingRoot = false)
        {
            this.ForEachBackToFront(includingRoot, child => child.Show());
        }

        /// <summary>
        /// Ensure all windows are hidden, preserving their current order
        /// </summary>
        public void HideAllWindows (bool includingRoot = false)
        {
            this.ForEach(includingRoot, w => w.Hide());
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
            window.Deactivated += this.OnWindowDeactivated;
            window.Closed += this.Window_OnClosed;
            this.Items.Add(window);
        }

        private bool DoStandaradWindowTrackingRemoval (Window window)
        {
            // Since it doesn't hurt, and it fulfills some protection from paranoia, keeping this before the remove
            window.Closed -= this.Window_OnClosed;
            window.Deactivated -= this.OnWindowDeactivated;
            window.Activated -= this.Window_Activated;
            foreach (RetargetingCommandBinding command in window.CommandBindings.OfType<RetargetingCommandBinding>().ToList())
            {
                window.CommandBindings.Remove(command);
            }

            return this.Items.Remove(window);
        }

        private void OnRootClosed (object _sender, EventArgs _e)
        {
            foreach (Window child in this.Items.Where(i => i != this.Root).ToList())
            {
                this.StopTracking(child);
                child.Close();
            }
        }

        private void OnRootActivated (object sender, EventArgs e)
        {
            if (this.ShowHideChildrenWhen == eChildWindowDisplayCoordinationStyle.RootActivationDeactivation)
            {
                if (this.Items.Any(w => !w.IsVisible))
                {
                    this.ShowAllWindows();
                    this.MakeWindowAsTippityTopAsPossible(this.Root);
                }
            }
        }

        private void OnWindowDeactivated (object sender, EventArgs e)
        {
            this.Root.Dispatcher.InvokeAsync(() =>
            {
                // It doesn't matter if a window is deactivated as long as another was activated
                if (this.Items.OfType<Window>().Any(w => w.IsActive))
                {
                    return;
                }

                if (this.ShowHideChildrenWhen == eChildWindowDisplayCoordinationStyle.RootActivationDeactivation)
                {
                    this.HideAllWindows();
                }
            }
            );
        }

        private void OnRootStateChanged (object sender, EventArgs e)
        {
            if (this.ShowHideChildrenWhen != eChildWindowDisplayCoordinationStyle.RootMinimizationRestoration)
            {
                return;
            }
            else if (this.Root.WindowState != WindowState.Minimized)
            {
                this.ShowAllWindows();
            }
            else if (this.Root.WindowState == WindowState.Minimized)
            {
                this.HideAllWindows();
            }
        }

        private void OnItemActivated (Window window)
        {
            if (m_ignoreOrderingChanges)
            {
                return;
            }

            // Root activation of the enmasse Owner doesn't matter because
            //  Root will always be forced (by the OS) to be bottom most
            if (this.SetOwnerAsRootForEachChildWindow && this.Root == window)
            {
                window.Focus();
                return;
            }

            // Update the tracking order, and bring it as forward as we can
            this.Items.Remove(window);
            this.Items.Insert(0, window);
            this.MakeWindowAsTippityTopAsPossible(window);
            window.Focus();
        }

        private void MakeWindowAsTippityTopAsPossible (Window window)
        {
            bool wasTopMost = window.Topmost;
            window.Topmost = false;
            window.Topmost = true;
            window.Topmost = wasTopMost;
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

        private void RetargetCommandsOnto (Window window, UIElement from)
        {
            foreach (CommandBinding cmdBinding in from.CommandBindings)
            {
                if (cmdBinding.Command is RoutedCommand command)
                {
                    window.CommandBindings.Add(new RetargetingCommandBinding(from, command, this.Root));
                }
            }
        }

        private void Window_Activated (object sender, EventArgs e)
        {
            this.OnItemActivated((Window)sender);
        }

        private void Window_OnClosed (object sender, EventArgs e)
        {
            this.StopTracking((Window)sender);
        }

        // =====================[ Subclasses ]=====================================

        private class RetargetingCommandBinding : CommandBinding
        {
            private readonly RoutedCommand m_command;
            private readonly IInputElement m_target;
            public RetargetingCommandBinding (UIElement source, RoutedCommand command, IInputElement newTarget) : base(command)
            {
                this.Source = source;
                m_command = command;
                m_target = newTarget;

                this.CanExecute += this.CanExecuteRoutedEventHandler;
                this.Executed += this.ExecutedRoutedEventHandler;
            }

            public UIElement Source { get; }

            private void CanExecuteRoutedEventHandler (object sender, CanExecuteRoutedEventArgs e)
            {
                if (m_command.CanExecute(e.Parameter, m_target))
                {
                    e.CanExecute = true;
                }
            }

            private void ExecutedRoutedEventHandler (object sender, ExecutedRoutedEventArgs e)
            {
                m_command.Execute(e.Parameter, m_target);
            }
        }
    }
}