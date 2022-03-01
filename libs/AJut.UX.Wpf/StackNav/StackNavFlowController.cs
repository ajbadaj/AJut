namespace AJut.UX
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Windows;
    using AJut;

    /// <summary>
    /// The entry point into the stack nav system. This stores the navigational stack and currently displayed item info.
    /// </summary>
    public class StackNavFlowController : NotifyPropertyChanged
    {
        private readonly Stack<StackElementStorage> m_hiddenElementStack = new();
        private StackNavAdapter m_stackTopDisplayAdapter;
        private bool m_isDrawerOpen;
        private bool m_canGoBack;
        private bool m_canCloseDrawer = true;

        // ========================================[ Events ]========================================

        /// <summary>
        /// An event that triggers when navigation is about to happen
        /// </summary>
        public event EventHandler NavigationIminent;

        /// <summary>
        /// An event that triggers when navigation to something new has finished
        /// </summary>
        public event EventHandler NavigationComplete;

        /// <summary>
        /// An event that triggers when the active drawer is opened or closed
        /// </summary>
        public event EventHandler DrawerOpenStatusChanged;

        // ========================================[ Properties ]========================================

        /// <summary>
        /// The adapter of the <see cref="IStackNavDisplayControl"/> that is currently being shown
        /// </summary>
        public StackNavAdapter StackTopDisplayAdapter
        {
            get => m_stackTopDisplayAdapter;
            set => this.SetAndRaiseIfChanged(ref m_stackTopDisplayAdapter, value);
        }

        /// <summary>
        /// Indicates if the stack nav can go back
        /// </summary>
        public bool CanGoBack
        {
            get => this.m_canGoBack;
            set => this.SetAndRaiseIfChanged(ref m_canGoBack, value);
        }

        /// <summary>
        /// Indicates if the drawer should be opened showing the <see cref="StackTopDisplayAdapter"/>'s <see cref="IStackNavDrawerDisplay"/>
        /// </summary>
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

        /// <summary>
        /// Indicates if hte drawer can be closed (some displays have need to force the drawer to remain open for a time)
        /// </summary>
        public bool CanCloseDrawer
        {
            get => this.m_canCloseDrawer;
            set => this.SetAndRaiseIfChanged(ref m_canCloseDrawer, value);
        }

        // ========================================[ Methods ]========================================

        /// <summary>
        /// Generate a display for the given type, and using the given state, and push it to the top of the stack (showing it).
        /// </summary>
        /// <typeparam name="T">The type of display to create</typeparam>
        /// <param name="state">The state to setup the display with</param>
        /// <returns>A bool indicating if the generation and push to top was successful (true) or failed (false)</returns>
        public bool GenerateAndPushDisplay<T> (object state = null) where T : IStackNavDisplayControl
        {
            StackNavAdapter adapter = this.BuildControlAndAdapter(typeof(T));
            if (adapter != null)
            {
                if (this.StackTopDisplayAdapter != null)
                {
                    m_hiddenElementStack.Push(new StackElementStorage(this.StackTopDisplayAdapter));
                }

                this.ReplaceShownDisplay(adapter, state);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Closes the current display, pops thes tack, and generates (if needed) the display down one in the stack 
        /// and shows that. This operation fails if the display is the last in the stack.
        /// </summary>
        public async Task<bool> PopDisplay ()
        {
            StackNavAdapter oldShownControl = this.StackTopDisplayAdapter;
            if (await oldShownControl.Close())
            {
                StackElementStorage newElementToShow = m_hiddenElementStack.Pop();
                this.ReplaceShownDisplay(newElementToShow.GenerateAdapterWith(this), newElementToShow.PreviousState);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Open the drawer for the current <see cref="StackTopDisplayAdapter"/>
        /// </summary>
        /// <param name="forceStayOpen">Do you wnat to force it to stay open? Default is <c>false</c>, normal 
        /// close operations will fail later until programatically <see cref="CloseDrawer(true)"/> is called.</param>
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
            this.IsDrawerOpen = true;

            if (forceStayOpen)
            {
                this.CanCloseDrawer = false;
            }

            this.StackTopDisplayAdapter.OnDrawerOpened();
        }

        /// <summary>
        /// Close the drawer
        /// </summary>
        /// <param name="releaseForceOpen">Release "force open" state (if it is on), default <c>false</c></param>
        /// <returns></returns>
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

        private StackNavAdapter BuildControlAndAdapter (Type stackNavDisplayControlType)
        {
            // Build
            object newDisplayObj;
            try
            {
                newDisplayObj = Activator.CreateInstance(stackNavDisplayControlType);
            }
            catch { newDisplayObj = null; }

            // Use it if it's valid
            if (newDisplayObj is IStackNavDisplayControl displayControl)
            {
                return new StackNavAdapter(this, displayControl);
            }

            // Toss it if it's not
            if (newDisplayObj is IDisposable disposableFailure)
            {
                disposableFailure.Dispose();
            }

            return null;
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


        // ========================================[ Subclasses ]========================================

        private class StackElementStorage
        {
            private readonly Type m_displayElementType;
            private readonly StackNavAdapter m_adapter;

            public StackElementStorage (StackNavAdapter adapter)
            {
                this.PreviousState = adapter.OnCovered();
                if (adapter.PreserveFullAdapterAndControlOnCover)
                {
                    m_adapter = adapter;
                }
                else
                {
                    m_displayElementType = adapter.Display.GetType();
                }
            }

            public object PreviousState { get; }

            public StackNavAdapter GenerateAdapterWith (StackNavFlowController controller)
            {
                if (m_adapter != null)
                {
                    return m_adapter;
                }

                return controller.BuildControlAndAdapter(m_displayElementType);
            }
        }
    }
}
