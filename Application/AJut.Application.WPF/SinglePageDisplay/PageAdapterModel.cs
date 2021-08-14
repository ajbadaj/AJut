namespace AJut.Application.SinglePageDisplay
{
    using System;
    using System.Threading.Tasks;
    using AJut;
    using AJut.Storage;

    public delegate Task<bool> ClosingHandlerFunction ();
    public delegate object StateGenerator ();
    
    public class PageAdapterModel : NotifyPropertyChanged
    {
        private Lazy<EmptyDrawer> m_emptyDrawerFallback;
        private IDrawerDisplay m_drawer;
        private string m_title;
        private bool m_displayTitlePrefix = true;
        private bool m_isBusyWaitActive;
        private IPopoverDisplayBase m_popoverDisplay;

        public PageAdapterModel (PageManager pageManager, IPageDisplayControl page)
        {
            m_emptyDrawerFallback = new Lazy<EmptyDrawer>(() => new EmptyDrawer(page));
            this.Manager = pageManager;
            this.PageDisplay = page;
            this.PageDisplay.Setup(this);
        }

        public event EventHandler<AttemptingCloseEventArgs> Closing;
        public event EventHandler<EventArgs> Closed;

        public event EventHandler<EventArgs> Covered;
        public event EventHandler<EventArgs> Shown;

        public event EventHandler<EventArgs> DrawerOpening;
        public event EventHandler<EventArgs> DrawerOpened;
        public event EventHandler<EventArgs> DrawerClosed;

        // =============================[ Properties ]========================================
        public PageManager Manager { get; }
        public IPageDisplayControl PageDisplay { get; }
        public IDrawerDisplay Drawer
        {
            get => m_drawer ?? m_emptyDrawerFallback.Value;
            set => this.SetAndRaiseIfChanged(ref m_drawer, value);
        }

        public ClosingHandlerFunction AsyncClosingHandler { get; set; }

        public string Title
        {
            get => this.m_title;
            set => this.SetAndRaiseIfChanged(ref m_title, value);
        }

        public bool DisplayTitlePrefix
        {
            get => m_displayTitlePrefix;
            set => this.SetAndRaiseIfChanged(ref m_displayTitlePrefix, value);
        }

        public bool IsBusyWaitActive
        {
            get => m_isBusyWaitActive;
            private set
            {
                if (this.SetAndRaiseIfChanged(ref m_isBusyWaitActive, value))
                {
                    this.RaisePropertiesChanged(nameof(AnyCoversShown));
                }
            }
        }

        public bool IsShowingPopover => m_popoverDisplay != null;

        public IPopoverDisplayBase PopoverDisplay
        {
            get => m_popoverDisplay;
            private set
            {
                if (this.SetAndRaiseIfChanged(ref m_popoverDisplay, value))
                {
                    this.RaisePropertiesChanged(nameof(IsShowingPopover), nameof(AnyCoversShown));
                }
            }
        }

        public bool AnyCoversShown => this.IsBusyWaitActive || this.IsShowingPopover;

        // =============================[ Methods ]========================================

        public void OnShown (object state)
        {
            this.PageDisplay.SetState(state);
            this.Shown?.Invoke(this, EventArgs.Empty);
        }

        public async Task<bool> Close ()
        {
            AttemptingCloseEventArgs attemptingClose = new AttemptingCloseEventArgs();
            this.Closing?.Invoke(this, attemptingClose);
            if (!attemptingClose.CanClose)
            {
                return false;
            }

            Task<bool> closingHandler = this.AsyncClosingHandler?.Invoke();
            if (closingHandler != null && !await closingHandler)
            {
                return false;
            }

            this.Closed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public async Task<Result> ShowPopover (IPopoverDisplay popover)
        {
            this.PopoverDisplay = popover;
            var resultWaiter = new TaskCompletionSource<Result>();
            popover.ResultSet += _OnResultSet;
            return await resultWaiter.Task.ConfigureAwait(false);

            void _OnResultSet (object _sender, EventArgs<Result> _e)
            {
                popover.ResultSet -= _OnResultSet;
                this.PopoverDisplay = null;
                resultWaiter.TrySetResult(_e.Value);
            }
        }

        public async Task<Result<T>> ShowPopover<T> (IPopoverDisplay<T> popover)
        {
            this.PopoverDisplay = popover;
            var resultWaiter = new TaskCompletionSource<Result<T>>();
            popover.ResultSet += _OnResultSet;
            return await resultWaiter.Task.ConfigureAwait(false);

            void _OnResultSet (object _sender, EventArgs<Result<T>> _e)
            {
                this.PopoverDisplay = null;
                popover.ResultSet -= _OnResultSet;
                resultWaiter.TrySetResult(_e.Value);
            }
        }

        public BusyWaitTracker GenerateBusyWait () => new BusyWaitTracker(this);

        /// <summary>
        /// Get state when another page is pushed on top
        /// </summary>
        /// <returns></returns>
        internal object OnCovered ()
        {
            this.Covered?.Invoke(this, EventArgs.Empty);
            return this.PageDisplay.GenerateState();
        }

        internal void OnDrawerOpening ()
        {
            this.DrawerOpening?.Invoke(this, EventArgs.Empty);
        }

        internal void OnDrawerOpened ()
        {
            this.DrawerOpened?.Invoke(this, EventArgs.Empty);
        }

        internal void OnDrawerClosed ()
        {
            this.DrawerClosed?.Invoke(this, EventArgs.Empty);
        }

        public class BusyWaitTracker : IDisposable
        {
            PageAdapterModel m_owner;
            public BusyWaitTracker (PageAdapterModel owner)
            {
                m_owner = owner;
                m_owner.IsBusyWaitActive = true;
            }

            public void Dispose ()
            {
                m_owner.IsBusyWaitActive = false;
                m_owner = null;
            }
        }

        private class EmptyDrawer : IDrawerDisplay
        {
            public EmptyDrawer (IPageDisplayControl shownPage)
            {
                this.Title = shownPage.GetType().Name.Replace("Page", String.Empty).ConvertToFriendlyEn();
            }

            public string Title { get; }
        }
    }
}
