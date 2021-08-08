namespace AJut.Application.SinglePageDisplay
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Media;
    using AJut;

    public class PageManager : NotifyPropertyChanged
    {
        private Stack<PageStorage> m_pageStack = new Stack<PageStorage>();
        private PageAdapterModel m_shownPage;
        private bool m_isDrawerOpen;
        private bool m_canGoBack;
        private bool m_canCloseDrawer = true;
        private bool m_showDrawerAsControl;

        public PageManager (Window rootWindow)
        {
            this.RootWindow = rootWindow;
        }

        public event EventHandler NavigationComplete;
        public event EventHandler DrawerOpenStatusChanged;

        // ==================================================
        // = Properties                                     =
        // ==================================================
        public Window RootWindow { get; }

        public bool IsDrawerOpen
        {
            get => m_isDrawerOpen;
            private set
            {
                if (this.SetAndRaiseIfChanged(ref m_isDrawerOpen, value))
                {
                    if (m_isDrawerOpen)
                    {
                        m_shownPage.OnDrawerOpened();
                    }
                    else
                    {
                        m_shownPage.OnDrawerClosed();
                    }

                    this.DrawerOpenStatusChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public PageAdapterModel ShownPage
        {
            get => m_shownPage;
            set => this.SetAndRaiseIfChanged(ref m_shownPage, value);
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

        public bool ShowDrawerAsControl
        {
            get => m_showDrawerAsControl;
            set => this.SetAndRaiseIfChanged(ref m_showDrawerAsControl, value);
        }

        // ==================================================
        // = Methods                                        =
        // ==================================================

        public bool PushPage<T> (object state = null)
        {
            var page = Activator.CreateInstance(typeof(T)) as IPageDisplayControl;
            if (page == null)
            {
                return false;
            }

            if (this.ShownPage != null)
            {
                m_pageStack.Push(new PageStorage(this.ShownPage));
            }

            this.ReplaceShownPage(new PageAdapterModel(this, page), state);
            return true;
        }

        public async Task Pop ()
        {
            PageAdapterModel oldShownPage = this.ShownPage;
            PageStorage newPage = m_pageStack.Pop();

            if (await oldShownPage.Close())
            {
                this.ReplaceShownPage(newPage.Page, newPage.PreviousState);
            }
            else
            {
                m_pageStack.Push(newPage);
            }
        }

        private void ReplaceShownPage (PageAdapterModel newPage, object newState)
        {
            this.ShownPage = newPage;
            this.CanGoBack = this.m_pageStack.Count >= 1;
            bool wasDrawerOpen = this.IsDrawerOpen;
            this.CloseDrawer(true);

            // ---------------------------------------------------------------------------------
            // NOTE - MUST STAY LAST
            // ---------------------------------------------------------------------------------
            // This must stay last, if we allow pages when shown to say no, this can't be
            //  then they need to be able to pop, replace, or otherwise push something new on.
            newPage.OnShown(newState);

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

            this.ShownPage.OnDrawerOpening();
            var drawer = this.ShownPage.Drawer;
            this.ShowDrawerAsControl = drawer is Visual;

            this.IsDrawerOpen = true;

            if (drawer is IManagerReactiveDrawerDisplay drawerNeedsSetup)
            {
                drawerNeedsSetup.Setup(this);
            }

            if (forceStayOpen)
            {
                this.CanCloseDrawer = false;
            }

            this.ShownPage.OnDrawerOpened();
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
                this.ShownPage.OnDrawerClosed();
                return true;
            }

            return false;
        }

        private class PageStorage
        {
            public PageStorage (PageAdapterModel page)
            {
                this.Page = page;
                this.PreviousState = page.OnCovered();
            }

            public PageAdapterModel Page { get; }
            public object PreviousState { get; }
        }
    }
}
