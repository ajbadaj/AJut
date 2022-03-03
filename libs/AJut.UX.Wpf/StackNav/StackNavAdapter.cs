namespace AJut.UX
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AJut;
    using AJut.Storage;

    /// <summary>
    /// A function which handles the closing of a <see cref="IStackNavDisplayControl"/>
    /// </summary>
    /// <returns>
    /// An awaitable <see cref="Task{bool}"/> which indicates if the close was succesful, and should continue (<c>true</c>) 
    /// or if the close failed and/or the close should not continue (<c>false</c>)
    /// </returns>
    public delegate Task<bool> ClosingHandlerFunction ();

    /// <summary>
    /// The adapter given to a <see cref="IStackNavDisplayControl"/> so that it has an easy to use entry point into the StackNav system, without
    /// having to implement many different features. This allows a control to take advantage of whatever optional features they would like.
    /// </summary>
    public class StackNavAdapter : NotifyPropertyChanged
    {
        private const string kDefaultBusyWaitText = "Please wait...";

        private object m_drawerDisplay;
        private object m_title;
        private object m_drawerHeading;
        private bool m_isBusyWaitActive;
        private IStackNavPopoverDisplayBase m_popoverDisplay;
        private bool m_preserveFullAdapterAndControlOnCover = false;
        private int m_busyWaitRefCount = 0;
        private bool m_allowInteractionDuringBusyWait = false;
        private bool m_allowInteractionDuringPopover = false;
        private string m_busyWaitText = kDefaultBusyWaitText;

        internal StackNavAdapter (StackNavFlowController navigator, IStackNavDisplayControl display)
        {
            this.Navigator = navigator;
            this.Display = display;
            this.Display.Setup(this);
        }

        // ========================================[ Events ]========================================

        /// <summary>
        /// An event triggered when the closing process begins, this allows you to stop closing if the <see cref="IStackNavDisplayControl"/> is not prepared to close.
        /// </summary>
        public event EventHandler<StackNavAttemptingDisplayCloseEventArgs> Closing;

        /// <summary>
        /// An event triggered after close is complete
        /// </summary>
        public event EventHandler<EventArgs> Closed;

        /// <summary>
        /// An event triggered when the <see cref="IStackNavDisplayControl"/> is covered
        /// </summary>
        public event EventHandler<EventArgs> Covered;

        /// <summary>
        /// An event triggered when the <see cref="IStackNavDisplayControl"/> is shown (this could be for the first time, or after being covered).
        /// </summary>
        public event EventHandler<EventArgs> Shown;

        /// <summary>
        /// An event triggered to inform that the drawer is about to be opened (allowing you to create, or modify the drawer control before it's shown)
        /// </summary>
        public event EventHandler<EventArgs> DrawerOpening;

        /// <summary>
        /// An event triggerd to inform you that the drawer was finished being opened
        /// </summary>
        public event EventHandler<EventArgs> DrawerOpened;

        /// <summary>
        /// An event triggered to inform you that the drawer has been closed
        /// </summary>
        public event EventHandler<EventArgs> DrawerClosed;

        // ========================================[ Properties ]========================================

        /// <summary>
        /// The root level controller of the StackNav system. This allows you to push/pop new controls, and perform any other tasks needed to operate in the StackNav system.
        /// </summary>
        public StackNavFlowController Navigator { get; }

        /// <summary>
        /// The display associated to this <see cref="StackNavAdapter"/>
        /// </summary>
        public IStackNavDisplayControl Display { get; }

        /// <summary>
        /// The title object displayed (default controls like the <see cref="Controls.StackNavActiveHeaderPresenter"/> will render this, if you don't have a special rendering routine it is rendered as text by default)
        /// </summary>
        public object Title
        {
            get => this.m_title;
            set => this.SetAndRaiseIfChanged(ref m_title, value);
        }

        /// <summary>
        /// The drawer object to display in the drawer should it open (or null for none)
        /// </summary>
        public object DrawerDisplay
        {
            get => m_drawerDisplay;
            set => this.SetAndRaiseIfChanged(ref m_drawerDisplay, value);
        }

        /// <summary>
        /// The heading to display for the drawer
        /// </summary>
        public object DrawerHeading
        {
            get => m_drawerHeading ?? "Settings";
            set => this.SetAndRaiseIfChanged(ref m_drawerHeading, value);
        }

        /// <summary>
        /// An asynchronous closing handler should you need to handle closing in an asynchronous way
        /// </summary>
        public ClosingHandlerFunction AsyncClosingHandler { get; set; }

        /// <summary>
        /// Indicates if the busy wait cover (set by the method <see cref="GenerateBusyWait"/>) is currently requested to be displayed
        /// </summary>
        public bool IsBusyWaitActive
        {
            get => m_isBusyWaitActive;
            private set
            {
                if (this.SetAndRaiseIfChanged(ref m_isBusyWaitActive, value))
                {
                    this.RaisePropertiesChanged(nameof(AnyCoversShown), nameof(AnyBlockingCoversShown));
                }
            }
        }

        /// <summary>
        /// Indicates if the user is allowed to perform any header interactions (ie browse back) during an active busywait
        /// </summary>
        public bool AllowInteractionDuringBusyWait
        {
            get => m_allowInteractionDuringBusyWait;
            set
            {
                if (this.SetAndRaiseIfChanged(ref m_allowInteractionDuringBusyWait, value))
                {
                    this.RaisePropertiesChanged(nameof(AnyCoversShown), nameof(AnyBlockingCoversShown));
                }
            }
        }

        /// <summary>
        /// Indicates if the user is allowed to perform any header interactions (ie browse back) while there is an active popover
        /// </summary>
        public bool AllowInteractionDuringPopover
        {
            get => m_allowInteractionDuringPopover;
            set
            {
                if (this.SetAndRaiseIfChanged(ref m_allowInteractionDuringPopover, value))
                {
                    this.RaisePropertiesChanged(nameof(AnyCoversShown), nameof(AnyBlockingCoversShown));
                }
            }
        }

        /// <summary>
        /// Indicates if a popover cover (set by the method <see cref="ShowPopover"/>) is currently requested to be displayed
        /// </summary>
        public bool IsShowingPopover => m_popoverDisplay != null;

        /// <summary>
        /// The current popover display (set by the method <see cref="ShowPopover"/>)
        /// </summary>
        public IStackNavPopoverDisplayBase PopoverDisplay
        {
            get => m_popoverDisplay;
            private set
            {
                if (this.SetAndRaiseIfChanged(ref m_popoverDisplay, value))
                {
                    this.RaisePropertiesChanged(nameof(IsShowingPopover), nameof(AnyCoversShown), nameof(AnyBlockingCoversShown));
                }
            }
        }

        /// <summary>
        /// Indicates if any of the cover displays (busy wait, or a popover) are being shown
        /// </summary>
        public bool AnyCoversShown => this.IsBusyWaitActive || this.IsShowingPopover;

        /// <summary>
        /// Are there any blocking covers being shown (busy wait or popover when matching interaction allower flag is false)
        /// </summary>
        public bool AnyBlockingCoversShown => (this.IsBusyWaitActive && !this.AllowInteractionDuringBusyWait) || (this.IsShowingPopover && !this.AllowInteractionDuringPopover);

        /// <summary>
        /// Indicates if this adapter and it's <see cref="IStackNavDisplayControl"/> control should be preserved on cover (default is false for 
        /// minimzed memory footprint). This might be needed if a control(s) in your <see cref="IStackNavDisplayControl"/> are too expensive or
        /// difficult to build and the <see cref="IStackNavDisplayControl"/> would rather be preserved each run isntead.
        /// </summary>
        public bool PreserveFullAdapterAndControlOnCover
        {
            get => m_preserveFullAdapterAndControlOnCover;
            set => this.SetAndRaiseIfChanged(ref m_preserveFullAdapterAndControlOnCover, value);
        }

        /// <summary>
        /// The text displayed when the busy wait is active, set by the <see cref="GenerateBusyWait(string)"/>
        /// </summary>
        public string BusyWaitText
        {
            get => m_busyWaitText;
            private set => this.SetAndRaiseIfChanged(ref m_busyWaitText, value);
        }

        // ========================================[ Methods ]========================================

        /// <summary>
        /// Indicates that the passed in popover should be shown.
        /// </summary>
        /// <returns>The user selected option <see cref="Result"/> of the Popover's run</returns>
        public async Task<Result> ShowPopover (IStackNavPopoverDisplay popover)
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

        /// <summary>
        /// Indicates that the passed in popover should be shown.
        /// </summary>
        /// <returns>The user selected option <see cref="Result{T}"/> of the Popover's run</returns>
        public async Task<Result<T>> ShowPopover<T> (IStackNavPopoverDisplay<T> popover)
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

        /// <summary>
        /// Puts the display in the busy wait state, default controls like the <see cref="Controls.StackNavActiveContentPresenter"/> will 
        /// show a busy wait covering when this is done. The result is an <see cref="IDisposable"/> tracker that should be disposed when you're
        /// ready for the busywait to go away.
        /// </summary>
        /// <returns>A <see cref="BusyWaitTracker"/> that should be disposed when you're ready for the busy wait to go away</returns>
        public BusyWaitTracker GenerateBusyWait (string displayText = kDefaultBusyWaitText)
        {
            var busyWaitTracker = new BusyWaitTracker(this);
            ++m_busyWaitRefCount;
            this.IsBusyWaitActive = true;
            this.BusyWaitText = displayText;
            return busyWaitTracker;
        }

        internal void OnShown (object state)
        {
            this.Display.SetState(state);
            this.Shown?.Invoke(this, EventArgs.Empty);
        }

        internal async Task<bool> Close ()
        {
            var attemptingClose = new StackNavAttemptingDisplayCloseEventArgs();
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

        /// <summary>
        /// Get state when another control is pushed to the top over this one
        /// </summary>
        internal object OnCovered ()
        {
            this.Covered?.Invoke(this, EventArgs.Empty);
            return this.Display.GenerateState();
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

        private void ReturnBusyWait ()
        {
            if (--m_busyWaitRefCount < 0)
            {
                m_busyWaitRefCount = 0;
            }

            this.IsBusyWaitActive = m_busyWaitRefCount != 0;
        }

        // ========================================[ Sub Classes ]========================================

        /// <summary>
        /// A temporary busy wait display lifetime, to remove busy wait cover simply <see cref="IDisposable.Dispose"/> of this tracker.
        /// </summary>
        public class BusyWaitTracker : IDisposable
        {
            StackNavAdapter m_owner;
            public BusyWaitTracker (StackNavAdapter owner)
            {
                m_owner = owner;
            }

            public void Dispose ()
            {
                m_owner.ReturnBusyWait();
                m_owner = null;
            }
        }
    }
}
