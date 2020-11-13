namespace AJut.Application.AttachedProperties
{
    using System;
    using System.Runtime.CompilerServices;
#if WINDOWS_UWP
    using Windows.UI.Xaml;
#else
    using System.Windows;
#endif

    public static class WindowXTA
    {
        private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(WindowXTA));

        public static DependencyProperty IsFullscreenProperty = APUtils.Register(GetIsFullscreen, SetIsFullscreen, HandleIsFullscreenChanged);
        public static bool GetIsFullscreen (DependencyObject obj) => (bool)obj.GetValue(IsFullscreenProperty);
        public static void SetIsFullscreen (DependencyObject obj, bool value) => obj.SetValue(IsFullscreenProperty, value);
        public static void ToggleIsFullscreen (Window window)
        {
            SetIsFullscreen(window, !GetIsFullscreen(window));
        }

        private static void HandleIsFullscreenChanged (DependencyObject d, DependencyPropertyChangedEventArgs<bool> e)
        {
            if (!(d is Window window))
            {
                return;
            }

            if (e.NewValue)
            {
                var tracker = new WindowStateTracker(window);
                SetTracker(window, tracker);

                // Normal first as maximize definitely needs to happen after windowstyle=none
                //  if it's already maximized that would break this
                window.WindowState = WindowState.Normal;
                window.WindowStyle = WindowStyle.None;
                window.WindowState = WindowState.Maximized;

                // Force topmost by changing it (false first gurantees change)
                window.Topmost = false;
                window.Topmost = true;

                //((FrameworkElement)window.Content).Margin = GetFullscreenRootOffsetMargin(window);
                tracker.WatchForStateChange();
            }
            else
            {
                var tracker = GetTracker(window);

                // Check if the tracker exists, if it 
                if (tracker != null)
                {
                    tracker.Restore();
                }
            }
        }

        private static DependencyProperty TrackerProperty = APUtils.Register(GetTracker, SetTracker);
        private static WindowStateTracker GetTracker (DependencyObject obj) => (WindowStateTracker)obj.GetValue(TrackerProperty);
        private static void SetTracker (DependencyObject obj, WindowStateTracker value) => obj.SetValue(TrackerProperty, value);

        private class WindowStateTracker
        {
            private Window m_target;
            public WindowStateTracker (Window target)
            {
                m_target = target;
                this.FormerWindowState = target.WindowState;
                this.FormerWindowStyle = target.WindowStyle;
                this.WasTopmost = target.Topmost;
                //this.FormerRootMargin = ((FrameworkElement)target.Content).Margin;
            }

            public WindowState FormerWindowState { get; set; }
            public WindowStyle FormerWindowStyle { get; set; }
            public bool WasTopmost { get; set; }
            public Thickness FormerRootMargin { get; set; }

            public void WatchForStateChange ()
            {
                m_target.StateChanged += _StateChanged;
                void _StateChanged (object sender, EventArgs e)
                {
                    m_target.StateChanged -= _StateChanged;
                    SetIsFullscreen(m_target, false);
                }
            }
            public void Restore ()
            {
                SetTracker(m_target, null);
                if (this.FormerWindowState == WindowState.Maximized)
                {
                    m_target.WindowState = WindowState.Normal;
                    m_target.WindowState = WindowState.Maximized;
                }
                else
                {
                    m_target.WindowState = this.FormerWindowState;
                }

                m_target.WindowStyle = this.FormerWindowStyle;
                m_target.Topmost = this.WasTopmost;
                //((FrameworkElement)m_target.Content).Margin = this.FormerRootMargin;
            }
        }
    }
}
