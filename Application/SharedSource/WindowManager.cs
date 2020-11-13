namespace AJut.Application
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Linq;

#if WINDOWS_UWP
    using Windows.UI.Xaml;
#else
    using System.Windows;
#endif
    using System.Windows.Input;

    public enum eChildWindowAction
    {
        Manually,
        RootActivationDeactivation,
        RootMinimizationRestoration,
    }
    public class WindowManager : ReadOnlyObservableCollection<Window>
    {
        private eChildWindowAction m_showHideChildrenWhen;
        private bool m_ignoreOrderingChanges;
        private readonly List<UIElement> m_commandRetargetters = new List<UIElement>();

        public WindowManager(Window root) : base(new ObservableCollection<Window>())
        {
            if (root == null)
            {
                throw new YouCantDoThatException("Window manager requires a root window");
            }

            this.Track(root);
            
            // Do this after so we don't add any command retargetting to itself
            this.Root = root;

            this.Root.StateChanged += _OnRootStateChanged;
            this.Root.Activated += _OnRootActivated;
            this.Root.Deactivated += _OnRootDeactivated;
            this.Root.Closed += _OnRootClosed;
            void _OnRootClosed (object _sender, System.EventArgs _e)
            {
                foreach (Window child in this.Items.Where(i=>i != this.Root).ToList())
                {
                    this.StopTracking(child);
                    child.Close();
                }
            }

            void _OnRootActivated (object sender, System.EventArgs e)
            {
                if (this.ShowHideChildrenWhen == eChildWindowAction.RootActivationDeactivation)
                {
                    this.ShowAllWindows();
                }

                this.BringWindowForward(this.Root);
            }
            void _OnRootDeactivated (object sender, System.EventArgs e)
            {
                this.Root.Dispatcher.InvokeAsync((System.Action)(() =>
                    {
                        // It doesn't matter if the root was deactivated if a child was the new activation target
                        if (this.Items.OfType<Window>().Any(w => w.IsActive))
                        {
                            return;
                        }

                        if (this.ShowHideChildrenWhen == eChildWindowAction.RootActivationDeactivation)
                        {
                            this.HideAllWindows();
                        }
                    })
                );
            }

            void _OnRootStateChanged (object sender, System.EventArgs e)
            {
                if (this.ShowHideChildrenWhen != eChildWindowAction.RootMinimizationRestoration)
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
        }

        public Window Root { get; }
        public eChildWindowAction ShowHideChildrenWhen
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

        private bool m_blockAll;
        public bool BlockAll
        {
            get => m_blockAll;
            set
            {
                if (m_blockAll == value)
                {
                    return;
                }    

                m_blockAll = value;
                foreach (Window window in this)
                {
                    window.IsEnabled = !m_blockAll;
                }

                this.OnPropertyChanged(new PropertyChangedEventArgs(nameof(BlockAll)));
            }
        }

        public void Track(Window window)
        {
            if (this.Items.Contains(window))
            {
                return;
            }

            window.Activated += this.Window_Activated;
            window.Closed += this.Window_OnClosed;
            this.Items.Add(window);

            foreach (UIElement commandRetargetSource in m_commandRetargetters)
            {
                this.RetargetCommandsOnto(window, from: commandRetargetSource);
            }
        }

        private void RetargetCommandsOnto(Window window, UIElement from)
        {
            foreach (CommandBinding cmdBinding in from.CommandBindings)
            {
                if (cmdBinding.Command is RoutedCommand command)
                {
                    window.CommandBindings.Add(new RetargetingCommandBinding(from, command, this.Root));
                }
            }
        }

        public void StopTracking (Window window)
        {
            window.Closed -= this.Window_OnClosed;
            window.Activated -= this.Window_Activated;
            this.Items.Remove(window);
            foreach (RetargetingCommandBinding command in window.CommandBindings.OfType<RetargetingCommandBinding>().ToList())
            {
                window.CommandBindings.Remove(command);
            }
        }

        public void RetargetCommands (UIElement element)
        {
            m_commandRetargetters.Add(element);
            foreach (Window window in this.Items)
            {
                this.RetargetCommandsOnto(window, from: element);
            }
        }

        public void RemoveRetargettingFor(UIElement commandSource)
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

        public void BringToFront (Window window)
        {
            m_ignoreOrderingChanges = true;
            try
            {
                if (!window.IsActive)
                {
                    window.Show();
                }

                this.BringWindowForward(window);
                window.Focus();
            }
            finally
            {
                m_ignoreOrderingChanges = false;
            }

            this.OnItemActivated(window);
        }

        private void BringWindowForward (Window window)
        {
            bool wasTopMost = window.Topmost;
            window.Topmost = false;
            window.Topmost = true;
            window.Topmost = wasTopMost;
        }

        private void OnItemActivated (Window window)
        {
            if (!m_ignoreOrderingChanges)
            {
                this.Items.Remove(window);
                this.Items.Insert(0, window);
            }
        }

        private void Window_Activated (object sender, System.EventArgs e)
        {
            this.OnItemActivated((Window)sender);
        }

        private void Window_OnClosed (object sender, EventArgs e)
        {
            this.StopTracking((Window)sender);
        }

        public void ShowAllWindows (bool includingRoot = false)
        {
            m_ignoreOrderingChanges = true;
            try
            {
                var items = (includingRoot ? this.Items : this.Items.Where(w => w != this.Root)).ToList();
                foreach (Window child in items)
                {
                    child.Show();
                }
            }
            finally
            {
                m_ignoreOrderingChanges = false;
            }
        }

        public void HideAllWindows (bool includingRoot = false)
        {
            this.ToAllWindowsDo(includingRoot, w => w.Hide());
        }

        public void CloseAllWindows (bool includingRoot = false)
        {
            this.ToAllWindowsDo(includingRoot, w => w.Close());
        }

        private void ToAllWindowsDo(bool includingRoot, Action<Window> action)
        {
            m_ignoreOrderingChanges = true;
            try
            {
                var items = (includingRoot ? this.Items : this.Items.Where(w => w != this.Root)).ToList();
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