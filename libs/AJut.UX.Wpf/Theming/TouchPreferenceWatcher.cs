namespace AJut.UX.Theming
{
    using System;
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Threading;

    /// <summary>
    /// Follows whether Windows prefers touch for a window, on that window's UI thread, so an app can set
    /// <see cref="AppThemeManager.IsTouchFocused"/> from it. AJut never sets that by itself: an app opts in by making one
    /// of these and handling <see cref="IsTouchPreferredChanged"/>. Dispose it when the window closes.
    /// </summary>
    /// <remarks>
    /// Windows offers the signal from Windows 11 24H2 on (see <see cref="WindowsTouchPreferenceSource"/>). Before that
    /// <see cref="IsAvailable"/> is false, <see cref="IsTouchPreferred"/> stays false and nothing is raised.
    /// </remarks>
    public sealed class TouchPreferenceWatcher : NotifyPropertyChanged, IDisposable
    {
        private readonly Dispatcher m_dispatcher;
        private readonly bool m_ownsSource;
        private ITouchPreferenceSource m_source;
        private bool m_isTouchPreferred;

        // ===========[ Setup/Construction/Teardown ]===================================
        /// <summary>
        /// Follows Windows' touch preference for <paramref name="window"/>. Make it on the window's UI thread; the window's
        /// handle is created now if it hasn't been yet.
        /// </summary>
        public TouchPreferenceWatcher (Window window)
            : this(WindowsTouchPreferenceSource.TryCreateForWindow(new WindowInteropHelper(window).EnsureHandle()), window.Dispatcher, ownsSource: true)
        {
        }

        /// <summary>
        /// Follows <paramref name="source"/>, raising changes on <paramref name="dispatcher"/>'s thread. The source stays
        /// the caller's to dispose.
        /// </summary>
        public TouchPreferenceWatcher (ITouchPreferenceSource source, Dispatcher dispatcher)
            : this(source ?? throw new ArgumentNullException(nameof(source)), dispatcher, ownsSource: false)
        {
        }

        private TouchPreferenceWatcher (ITouchPreferenceSource source, Dispatcher dispatcher, bool ownsSource)
        {
            m_dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            m_source = source;
            m_ownsSource = ownsSource;
            if (m_source != null)
            {
                m_isTouchPreferred = m_source.IsTouchPreferred;
                m_source.PreferenceMayHaveChanged += this.OnSourcePreferenceMayHaveChanged;
            }
        }

        /// <summary>Stops following. Safe to call more than once.</summary>
        public void Dispose ()
        {
            ITouchPreferenceSource source = m_source;
            m_source = null;
            if (source == null)
            {
                return;
            }

            source.PreferenceMayHaveChanged -= this.OnSourcePreferenceMayHaveChanged;
            if (m_ownsSource && source is IDisposable disposableSource)
            {
                disposableSource.Dispose();
            }

            this.IsTouchPreferredChanged = null;
        }

        // ===========[ Events ]===================================
        /// <summary>Raised on the window's UI thread when <see cref="IsTouchPreferred"/> changes, with its new value</summary>
        public event EventHandler<EventArgs<bool>> IsTouchPreferredChanged;

        // ===========[ Properties ]===================================
        /// <summary>
        /// False when there's nothing to follow: Windows offers no touch preference for the window (before Windows 11
        /// 24H2), or this has been disposed
        /// </summary>
        public bool IsAvailable => m_source != null;

        /// <summary>True when the window should be touch focused, as last heard on the UI thread</summary>
        public bool IsTouchPreferred
        {
            get => m_isTouchPreferred;
            private set
            {
                if (this.SetAndRaiseIfChanged(ref m_isTouchPreferred, value))
                {
                    this.IsTouchPreferredChanged?.Invoke(this, value);
                }
            }
        }

        // ===========[ Helper Methods ]===================================
        // The source can say so on any thread, so the check is moved to the UI thread
        private void OnSourcePreferenceMayHaveChanged (object sender, EventArgs e)
        {
            if (m_dispatcher.CheckAccess())
            {
                this.RefreshFromSource();
            }
            else
            {
                m_dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(this.RefreshFromSource));
            }
        }

        private void RefreshFromSource ()
        {
            // Disposed while the notice was on its way
            ITouchPreferenceSource source = m_source;
            if (source != null)
            {
                this.IsTouchPreferred = source.IsTouchPreferred;
            }
        }
    }
}
