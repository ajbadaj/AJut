namespace AJut.Application.AttachedProperties
{
    using System;
    using System.Runtime.CompilerServices;
#if WINDOWS_UWP
    using Windows.UI.Xaml;
#else
    using System.Windows;
    using System.Windows.Input;
#endif

    public static class WindowXTA
    {
        private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(WindowXTA));

        public static RoutedUICommand ToggleFullscreenCommand = new RoutedUICommand("Toggle Fullscreen", nameof(ToggleFullscreenCommand), typeof(WindowXT), new InputGestureCollection(new[] { new KeyGesture(Key.F11) }));

        #region IsFullscreen
        public static DependencyProperty IsFullscreenProperty = APUtils.Register(GetIsFullscreen, SetIsFullscreen, HandleIsFullscreenChanged);
        public static bool GetIsFullscreen (DependencyObject obj) => (bool)obj.GetValue(IsFullscreenProperty);
        public static void SetIsFullscreen (DependencyObject obj, bool value) => obj.SetValue(IsFullscreenProperty, value);
        public static void ToggleIsFullscreen (Window window)
        {
            SetIsFullscreen(window, !GetIsFullscreen(window));
        }

        public static DependencyProperty FullscreenWindowPaddingProperty = APUtils.Register(GetFullscreenWindowPadding, SetFullscreenWindowPadding, new Thickness(10));
        public static Thickness GetFullscreenWindowPadding (DependencyObject obj) => (Thickness)obj.GetValue(FullscreenWindowPaddingProperty);
        public static void SetFullscreenWindowPadding (DependencyObject obj, Thickness value) => obj.SetValue(FullscreenWindowPaddingProperty, value);

        private static void HandleIsFullscreenChanged (DependencyObject d, DependencyPropertyChangedEventArgs<bool> e)
        {
            if (!(d is Window window))
            {
                return;
            }

            if (e.NewValue)
            {
                var tracker = new FullscreenWatcher(window);
                SetFullscreenInspectorAttachment(window, tracker);

                // Normal first as maximize definitely needs to happen after windowstyle=none
                //  if it's already maximized that would break this
                window.SetCurrentValue(Window.WindowStateProperty, WindowState.Normal);
                window.SetCurrentValue(Window.WindowStyleProperty, WindowStyle.None);
                window.SetCurrentValue(Window.WindowStateProperty, WindowState.Maximized);

                // Force topmost by changing it (false first gurantees change)
                window.SetCurrentValue(Window.TopmostProperty, false);
                window.SetCurrentValue(Window.TopmostProperty, true);

                // Update the window's margin
                window.SetCurrentValue(Window.MarginProperty, GetFullscreenWindowPadding(window));

                //((FrameworkElement)window.Content).Margin = GetFullscreenRootOffsetMargin(window);
                tracker.WatchForStateChangeAndExitFullscreen();
            }
            else
            {
                var tracker = GetFullscreenInspectorAttachment(window);

                // Check if the tracker exists, if it 
                if (tracker != null)
                {
                    tracker.TeardownFullscreenTracker();
                }
            }
        }

        private static DependencyProperty FullscreenInspectorAttachmentProperty = APUtils.Register(GetFullscreenInspectorAttachment, SetFullscreenInspectorAttachment);
        private static FullscreenWatcher GetFullscreenInspectorAttachment (DependencyObject obj) => (FullscreenWatcher)obj.GetValue(FullscreenInspectorAttachmentProperty);
        private static void SetFullscreenInspectorAttachment (DependencyObject obj, FullscreenWatcher value) => obj.SetValue(FullscreenInspectorAttachmentProperty, value);

        private class FullscreenWatcher
        {
            private Window m_target;
            public FullscreenWatcher (Window target)
            {
                m_target = target;
                this.FormerWindowState = target.WindowState;
                this.FormerWindowStyle = target.WindowStyle;
                this.WasTopmost = target.Topmost;
                this.FormerMargin = target.Margin;
                //this.FormerRootMargin = ((FrameworkElement)target.Content).Margin;
            }

            public WindowState FormerWindowState { get; set; }
            public WindowStyle FormerWindowStyle { get; set; }
            public bool WasTopmost { get; set; }
            public Thickness FormerMargin { get; set; }

            public void WatchForStateChangeAndExitFullscreen ()
            {
                m_target.StateChanged += _StateChanged;
                m_target.Closed += _Closed;
                void _StateChanged (object sender, EventArgs e)
                {
                    m_target.Closed -= _Closed;
                    m_target.StateChanged -= _StateChanged;
                    SetIsFullscreen(m_target, false);
                }

                void _Closed (object sender, EventArgs e)
                {
                    m_target.Closed -= _Closed;
                    m_target.StateChanged -= _StateChanged;
                }
            }


            public void TeardownFullscreenTracker ()
            {
                SetFullscreenInspectorAttachment(m_target, null);
                if (this.FormerWindowState == WindowState.Maximized)
                {
                    m_target.SetCurrentValue(Window.WindowStateProperty, WindowState.Normal);
                    m_target.SetCurrentValue(Window.WindowStateProperty, WindowState.Maximized);
                }
                else
                {
                    m_target.SetCurrentValue(Window.WindowStateProperty, this.FormerWindowState);
                }

                m_target.SetCurrentValue(Window.WindowStyleProperty, this.FormerWindowStyle);
                m_target.SetCurrentValue(Window.TopmostProperty, this.WasTopmost);
                m_target.SetCurrentValue(Window.MarginProperty, this.FormerMargin);
                m_target = null;
            }
        }
        #endregion // IsFullscreen


        public static DependencyProperty FixMaximizeAsFullscreenIssueProperty = APUtils.Register(GetFixMaximizeAsFullscreenIssue, SetFixMaximizeAsFullscreenIssue);
        public static bool GetFixMaximizeAsFullscreenIssue (DependencyObject obj) => (bool)obj.GetValue(FixMaximizeAsFullscreenIssueProperty);
        public static void SetFixMaximizeAsFullscreenIssue (DependencyObject obj, bool value) => obj.SetValue(FixMaximizeAsFullscreenIssueProperty, value);

        // One potential fix for this is.
        //public enum eEffecitveWindowState
        //{
        //    Minimized,
        //    Normal,
        //    Maximized,
        //    Fullscreened,
        //}
        // then have an attached property EffectiveWindowState
        // then when you want to maximize, set the EffectiveWindowState to Maximized, but the real one to Normal
        // then get the real working area (the area minus the task bar, however tall, wide, and whichever side it be docked)
        // and temporarily override the window's width/maxwidth, and height/maxheight. May also need to do border thickness, margin, etc
        
        private class WindowMaximizeFixerTracker : IDisposable
        {
            private Window m_target;
            public WindowMaximizeFixerTracker(Window target)
            {
                m_target = target;
                m_target.StateChanged += this.OnTargetStateChanged;
            }

            void IDisposable.Dispose ()
            {
                m_target.StateChanged -= this.OnTargetStateChanged;
                m_target = null;
            }

            private void OnTargetStateChanged (object sender, EventArgs e)
            {
                if (m_target.WindowState == WindowState.Maximized && !GetIsFullscreen(m_target))
                {
                    var margin = m_target.Margin;

                }
            }
        }
    }
}
