namespace AJut.UX
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Media;
    using AJut;

    /// <summary>
    /// The entry point into the stack nav system. This stores the stack and currently displayed item info.
    /// </summary>
    public class StackNavFlowController : NotifyPropertyChanged
    {
        private Stack<StackElementStorage> m_hiddenElementStack = new Stack<StackElementStorage>();
        private StackNavAdapter m_stackTopDisplayAdapter;
        private bool m_isDrawerOpen;
        private bool m_canGoBack;
        private bool m_canCloseDrawer = true;
        private bool m_showDrawerContents;
        private bool m_supportsDrawerDisplay;

        public StackNavFlowController (Window rootWindow)
        {
            this.RootWindow = rootWindow;
        }

        public event EventHandler NavigationIminent;
        public event EventHandler NavigationComplete;
        public event EventHandler DrawerOpenStatusChanged;

        // ==================================================
        // = Properties                                     =
        // ==================================================
        public Window RootWindow { get; }

        public bool SupportsDrawerDisplay
        {
            get => m_supportsDrawerDisplay;
            set => this.SetAndRaiseIfChanged(ref m_supportsDrawerDisplay, value);
        }

        public bool IsDrawerOpen
        {
            get => m_isDrawerOpen;
            private set
            {
                if (this.SetAndRaiseIfChanged(ref m_isDrawerOpen, value))
                {
                    if (m_isDrawerOpen)
                    {
                        m_stackTopDisplayAdapter.OnDrawerOpened();
                    }
                    else
                    {
                        m_stackTopDisplayAdapter.OnDrawerClosed();
                    }

                    this.DrawerOpenStatusChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public StackNavAdapter StackTopDisplayAdapter
        {
            get => m_stackTopDisplayAdapter;
            set => this.SetAndRaiseIfChanged(ref m_stackTopDisplayAdapter, value);
        }

        public bool CanGoBack
        {
            get => this.m_canGoBack;
            set => this.SetAndRaiseIfChanged(ref m_canGoBack, value);
        }

        public bool CanCloseDrawer
        {
            get => this.m_canCloseDrawer;
            set => this.SetAndRaiseIfChanged(ref m_canCloseDrawer, value);
        }

        public bool ShowDrawerContents
        {
            get => m_showDrawerContents;
            private set => this.SetAndRaiseIfChanged(ref m_showDrawerContents, value);
        }

        // ==================================================
        // = Methods                                        =
        // ==================================================

        public bool GenerateAndPushDisplay<T> (object state = null)
        {
            // Build
            object newDisplayObj;
            try
            {
                newDisplayObj = Activator.CreateInstance(typeof(T));
            }
            catch { newDisplayObj = null; }

            // Use it if it's valid
            if (newDisplayObj is IStackNavDisplayControl displayControl)
            {
                if (this.StackTopDisplayAdapter != null)
                {
                    m_hiddenElementStack.Push(new StackElementStorage(this.StackTopDisplayAdapter));
                }

                this.ReplaceShownDisplay(new StackNavAdapter(this, displayControl), state);
                return true;
            }

            // Toss it if it's not
            if (newDisplayObj is IDisposable disposableFailure)
            {
                disposableFailure.Dispose();
            }

            return false;
            
        }

        public async Task PopDisplay ()
        {
            StackNavAdapter oldShownControl = this.StackTopDisplayAdapter;
            if (await oldShownControl.Close())
            {
                StackElementStorage newElementToShow = m_hiddenElementStack.Pop();
                this.ReplaceShownDisplay(newElementToShow.Adapter, newElementToShow.PreviousState);
            }
        }

        private void ReplaceShownDisplay (StackNavAdapter newStackTop, object newState)
        {
            this.NavigationIminent?.Invoke(this, EventArgs.Empty);

            this.StackTopDisplayAdapter = newStackTop;
            this.CanGoBack = this.m_hiddenElementStack.Count >= 1;
            bool wasDrawerOpen = this.IsDrawerOpen;
            this.CloseDrawer(true);

            // ---------------------------------------------------------------------------------
            // NOTE - MUST STAY LAST
            // ---------------------------------------------------------------------------------
            // This must stay last, if we allow displays when shown to say no, this can't be
            //  then they need to be able to pop, replace, or otherwise push something new on.
            newStackTop.OnShown(newState);

            this.NavigationComplete?.Invoke(this, EventArgs.Empty);
        }

        public void OpenDrawer (bool forceStayOpen = false)
        {
            if (this.IsDrawerOpen)
            {
                if (forceStayOpen)
                {
                    this.CanCloseDrawer = false;
                }
                return;
            }

            this.StackTopDisplayAdapter.OnDrawerOpening();
            var drawer = this.StackTopDisplayAdapter.Drawer;
            this.ShowDrawerContents = drawer is DependencyObject;

            this.IsDrawerOpen = true;

            if (drawer is IStackNavFlowControllerReactiveDrawerDisplay drawerNeedsSetup)
            {
                drawerNeedsSetup.Setup(this);
            }

            if (forceStayOpen)
            {
                this.CanCloseDrawer = false;
            }

            this.StackTopDisplayAdapter.OnDrawerOpened();
        }

        public bool CloseDrawer (bool releaseForceOpen = false)
        {
            if (releaseForceOpen)
            {
                this.CanCloseDrawer = true;
            }

            if (this.CanCloseDrawer)
            {
                this.IsDrawerOpen = false;
                this.StackTopDisplayAdapter.OnDrawerClosed();
                return true;
            }

            return false;
        }

        private class StackElementStorage
        {
            public StackElementStorage (StackNavAdapter adapter)
            {
                this.Adapter = adapter;
                this.PreviousState = adapter.OnCovered();
            }

            public StackNavAdapter Adapter { get; }
            public object PreviousState { get; }
        }
    }
}
