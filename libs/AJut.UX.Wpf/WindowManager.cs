// eChildWindowDisplayCoordinationStyle moved to AJut.UX, in the same namespace, so every stack's WindowManager and the
// rules they share use one. This forwards anything built against it here to where it is now.
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(AJut.UX.eChildWindowDisplayCoordinationStyle))]

namespace AJut.UX
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Windows;
    using System.Windows.Input;

    /// <summary>
    /// Advanced tracking and coordination of windows, to be used in conjunction with a main window (ie main window and tools windows). One of the main functions
    /// of the WindowManager is to preserve activation order, which can be useful for drop targetting. The other main function is command retargetting, this allows
    /// windows, which by definition have separate roots, to coordinate commands to centralized points of your choosing.
    /// </summary>
    /// <remarks>
    /// The order and the showing and hiding of child windows are <see cref="WindowManagerBase{TWindow}"/>'s, shared with every stack; this class reports
    /// WPF's window events to it, and adds what only WPF has: window ownership and command retargeting.
    /// </remarks>
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
    public class WindowManager : WindowManagerBase<Window>
    {
        private readonly List<UIElement> m_commandRetargetters = new List<UIElement>();
        private bool m_setOwnerAsRootForEachChildWindow = true;

        // ===========[ Setup/Construction/Teardown ]===========================
        public WindowManager (Window root)
        {
            if (root == null)
            {
                throw new YouCantDoThatException("Window manager requires a root window");
            }

            this.Setup(root);
        }

        // ===========[ Events ]================================================
        private void Window_Activated (object sender, EventArgs e) => this.OnWindowActivated((Window)sender);
        private void Window_Deactivated (object sender, EventArgs e) => this.OnWindowDeactivated((Window)sender);
        private void Window_OnClosed (object sender, EventArgs e) => this.OnWindowClosed((Window)sender);

        private void OnRootStateChanged (object sender, EventArgs e)
        {
            if (this.Root.WindowState == WindowState.Minimized)
            {
                this.OnRootMinimized();
            }
            else
            {
                this.OnRootRestored();
            }
        }

        // ===========[ Properties ]============================================
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
                            this.ForEachBackToFront(false, child => MakeWindowAsTippityTopAsPossible(child));
                        }
                        else
                        {
                            this.BringToFront(this.Root);
                        }
                    }
                }
            }
        }

        // ===========[ Public Interface Methods ]==============================
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

        // ===========[ Toolkit Hooks ]=========================================
        protected override void SubscribeTo (Window window)
        {
            window.Activated += this.Window_Activated;
            window.Deactivated += this.Window_Deactivated;
            window.Closed += this.Window_OnClosed;
        }

        protected override void UnsubscribeFrom (Window window)
        {
            window.Closed -= this.Window_OnClosed;
            window.Deactivated -= this.Window_Deactivated;
            window.Activated -= this.Window_Activated;
        }

        protected override void OnRootSetup (Window root)
        {
            root.StateChanged += this.OnRootStateChanged;
        }

        protected override void OnRootTeardown (Window root)
        {
            root.StateChanged -= this.OnRootStateChanged;
        }

        protected override void OnChildTracked (Window window)
        {
            if (this.SetOwnerAsRootForEachChildWindow)
            {
                window.Owner = this.Root;
            }

            foreach (UIElement commandRetargetSource in m_commandRetargetters)
            {
                this.RetargetCommandsOnto(window, from: commandRetargetSource);
            }
        }

        protected override void OnStoppedTracking (Window window)
        {
            foreach (RetargetingCommandBinding command in window.CommandBindings.OfType<RetargetingCommandBinding>().ToList())
            {
                window.CommandBindings.Remove(command);
            }
        }

        protected override void PresentWindow (Window window)
        {
            if (!window.IsActive)
            {
                window.Show();
            }

            MakeWindowAsTippityTopAsPossible(window);
            window.Focus();
        }

        protected override void RaiseToFront (Window window) => MakeWindowAsTippityTopAsPossible(window);
        protected override void FocusWindow (Window window) => window.Focus();

        // Root activation of the enmasse Owner doesn't matter because Root will always be forced (by the OS) to be bottom most
        protected override bool KeepsItsPlaceWhenActivated (Window window) => this.SetOwnerAsRootForEachChildWindow && window == this.Root;

        protected override void ShowWindow (Window window) => window.Show();
        protected override void HideWindow (Window window) => window.Hide();
        protected override void CloseWindow (Window window) => window.Close();
        protected override bool IsWindowVisible (Window window) => window.IsVisible;

        // Deactivating one window comes before activating the next, so the check waits for the dispatcher to get past both
        protected override void RunAfterActivationSettles (Action action) => this.Root?.Dispatcher.InvokeAsync(action);

        // ===========[ Helper Methods ]========================================
        private static void MakeWindowAsTippityTopAsPossible (Window window)
        {
            bool wasTopMost = window.Topmost;
            window.Topmost = false;
            window.Topmost = true;
            window.Topmost = wasTopMost;
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

        // ===========[ Subclasses/structs ]====================================
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
