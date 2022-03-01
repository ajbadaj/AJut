namespace AJut.UX.AttachedProperties
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;

    /// <summary>
    /// Extension attached properties for windows
    /// </summary>
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
                tracker.SetRootMargin(GetFullscreenRootElementMargin(window));
                tracker.SetRootBorderThickness(GetFullscreenRootElementBorderThickness(window));

                tracker.WatchForStateChangeAndExitFullscreen();
            }
            else
            {
                var tracker = GetFullscreenInspectorAttachment(window);

                // Check if the tracker exists, if it 
                if (tracker != null)
                {
                    tracker.Dispose();
                    SetFullscreenInspectorAttachment(window, null);
                }
            }
        }

        private static DependencyProperty FullscreenInspectorAttachmentProperty = APUtils.Register(GetFullscreenInspectorAttachment, SetFullscreenInspectorAttachment);
        private static FullscreenWatcher GetFullscreenInspectorAttachment (DependencyObject obj) => (FullscreenWatcher)obj.GetValue(FullscreenInspectorAttachmentProperty);
        private static void SetFullscreenInspectorAttachment (DependencyObject obj, FullscreenWatcher value) => obj.SetValue(FullscreenInspectorAttachmentProperty, value);

        #endregion // IsFullscreen

        #region FixMaximizeAsFullscreenIssue
        public static DependencyProperty FixMaximizeAsFullscreenIssueProperty = APUtils.Register(GetFixMaximizeAsFullscreenIssue, SetFixMaximizeAsFullscreenIssue, HandleFixMaximizeAsFullscreenIssueChanged);
        public static bool GetFixMaximizeAsFullscreenIssue (DependencyObject obj) => (bool)obj.GetValue(FixMaximizeAsFullscreenIssueProperty);
        public static void SetFixMaximizeAsFullscreenIssue (DependencyObject obj, bool value) => obj.SetValue(FixMaximizeAsFullscreenIssueProperty, value);

        private static DependencyPropertyKey WindowMaxFixerInspectorAttachmentPropertyKey = APUtils.RegisterReadOnly(GetWindowMaxFixerInspectorAttachment, SetWindowMaxFixerInspectorAttachment);
        public static DependencyProperty WindowMaxFixerInspectorAttachmentProperty = WindowMaxFixerInspectorAttachmentPropertyKey.DependencyProperty;
        private static WindowMaximizeFixerTracker GetWindowMaxFixerInspectorAttachment (DependencyObject obj) => (WindowMaximizeFixerTracker)obj.GetValue(WindowMaxFixerInspectorAttachmentProperty);
        private static void SetWindowMaxFixerInspectorAttachment (DependencyObject obj, WindowMaximizeFixerTracker value) => obj.SetValue(WindowMaxFixerInspectorAttachmentPropertyKey, value);

        private static void HandleFixMaximizeAsFullscreenIssueChanged (DependencyObject d, DependencyPropertyChangedEventArgs<bool> e)
        {
            if (!(d is Window window))
            {
                return;
            }

            if (e.NewValue)
            {
                var tracker = new WindowMaximizeFixerTracker(window);
                SetWindowMaxFixerInspectorAttachment(window, tracker);
            }
            else
            {
                var tracker = GetWindowMaxFixerInspectorAttachment(window);

                // Check if the tracker exists, if it 
                if (tracker != null)
                {
                    tracker.Dispose();
                    SetFullscreenInspectorAttachment(window, null);
                }
            }
        }
        #endregion // Fix Maximize As Fullscreen

        #region Root Element Helpers
        public static DependencyProperty FullscreenRootElementMarginProperty = APUtils.Register(GetFullscreenRootElementMargin, SetFullscreenRootElementMargin, new Thickness(7));
        public static Thickness GetFullscreenRootElementMargin (DependencyObject obj) => (Thickness)obj.GetValue(FullscreenRootElementMarginProperty);
        public static void SetFullscreenRootElementMargin (DependencyObject obj, Thickness value) => obj.SetValue(FullscreenRootElementMarginProperty, value);

        public static DependencyProperty MaximizedRootElementMarginProperty = APUtils.Register(GetMaximizedRootElementMargin, SetMaximizedRootElementMargin, new Thickness(8));
        public static Thickness GetMaximizedRootElementMargin (DependencyObject obj) => (Thickness)obj.GetValue(MaximizedRootElementMarginProperty);
        public static void SetMaximizedRootElementMargin (DependencyObject obj, Thickness value) => obj.SetValue(MaximizedRootElementMarginProperty, value);

        public static DependencyProperty FullscreenRootElementBorderThicknessProperty = APUtils.Register(GetFullscreenRootElementBorderThickness, SetFullscreenRootElementBorderThickness, new Thickness(0.0));
        public static Thickness GetFullscreenRootElementBorderThickness (DependencyObject obj) => (Thickness)obj.GetValue(FullscreenRootElementBorderThicknessProperty);
        public static void SetFullscreenRootElementBorderThickness (DependencyObject obj, Thickness value) => obj.SetValue(FullscreenRootElementBorderThicknessProperty, value);

        public static DependencyProperty MaximizedRootElementBorderThicknessProperty = APUtils.Register(GetMaximizedRootElementBorderThickness, SetMaximizedRootElementBorderThickness, new Thickness(0.0));
        public static Thickness GetMaximizedRootElementBorderThickness (DependencyObject obj) => (Thickness)obj.GetValue(MaximizedRootElementBorderThicknessProperty);
        public static void SetMaximizedRootElementBorderThickness (DependencyObject obj, Thickness value) => obj.SetValue(MaximizedRootElementBorderThicknessProperty, value);
        #endregion

        private class WindowWatcher : IDisposable
        {
            private Thickness m_formerRootMargin;
            private Thickness m_formerRootBorderThickness;

            public WindowWatcher (Window target)
            {
                this.Target = target;

                if (this.Target.IsLoaded)
                {
                    _SetRootElement();
                }
                else
                {
                    this.Target.Loaded += _OnTargetLoaded;
                    void _OnTargetLoaded (object sender, RoutedEventArgs e)
                    {
                        this.Target.Loaded -= _OnTargetLoaded;
                        _SetRootElement();
                    }
                }

                void _SetRootElement ()
                {
                    this.RootElement = target.GetFirstChildOf<FrameworkElement>();
                    m_formerRootMargin = this.RootElement?.Margin ?? new Thickness();
                    if (this.RootElement is Border rootBorder)
                    {
                        m_formerRootBorderThickness = rootBorder.BorderThickness;
                    }
                }
            }

            public void Dispose()
            {
                this.Teardown();
            }

            protected virtual void Teardown()
            {
                this.RevertRootMargin();
                this.RevertRootBorderThickness();
                this.RootElement = null;
                this.Target = null;
            }

            public Window Target { get; private set; }
            public FrameworkElement RootElement { get; private set; }
            public bool IsRootMarginAltered { get; protected set; }
            public bool IsRootBorderThicknessAltered { get; protected set; }

            public void SetRootMargin (Thickness margin)
            {
                if (this.RootElement != null)
                {
                    this.RootElement.SetCurrentValue(Window.MarginProperty, margin);
                    this.IsRootMarginAltered = true;
                }
            }

            public void RevertRootMargin ()
            {
                if (this.IsRootMarginAltered)
                {
                    this.RootElement.SetCurrentValue(Window.MarginProperty, m_formerRootMargin);
                    this.IsRootMarginAltered = false;
                }
            }


            public void SetRootBorderThickness (Thickness borderThickness)
            {
                if (this.RootElement is Border)
                {
                    this.RootElement.SetCurrentValue(Border.BorderThicknessProperty, borderThickness);
                    this.IsRootBorderThicknessAltered = true;
                }
            }

            public void RevertRootBorderThickness ()
            {
                if (this.IsRootBorderThicknessAltered)
                {
                    this.RootElement.SetCurrentValue(Border.BorderThicknessProperty, m_formerRootBorderThickness);
                    this.IsRootBorderThicknessAltered = false;
                }
            }
        }

        private class FullscreenWatcher : WindowWatcher
        {
            public FullscreenWatcher (Window target) : base(target)
            {
                this.FormerWindowState = this.Target.WindowState;
                this.FormerWindowStyle = this.Target.WindowStyle;
                this.WasTopmost = this.Target.Topmost;
            }

            protected override void Teardown ()
            {
                this.Target.Closed -= OnTargetClosed;
                this.Target.StateChanged -= OnTargetStateChanged;
                if (this.FormerWindowState == WindowState.Maximized && this.Target.WindowState == WindowState.Normal && GetFixMaximizeAsFullscreenIssue(this.Target))
                {
                    GetWindowMaxFixerInspectorAttachment(this.Target)?.RevertFromMaximize();
                    this.IsRootBorderThicknessAltered = false;
                    this.IsRootMarginAltered = false;
                }
                else
                {
                    this.Target.SetCurrentValue(Window.WindowStyleProperty, this.FormerWindowStyle);
                    this.RevertRootMargin();
                    this.RevertRootBorderThickness();

                    if (this.FormerWindowState == WindowState.Maximized)
                    {
                        this.Target.SetCurrentValue(Window.WindowStateProperty, WindowState.Normal);
                        this.Target.SetCurrentValue(Window.WindowStateProperty, WindowState.Maximized);
                    }
                    else
                    {
                        this.Target.SetCurrentValue(Window.WindowStateProperty, this.FormerWindowState);
                    }
                }
                
                this.Target.SetCurrentValue(Window.TopmostProperty, this.WasTopmost);

                base.Teardown();
            }

            public WindowState FormerWindowState { get; set; }
            public WindowStyle FormerWindowStyle { get; set; }
            public bool WasTopmost { get; set; }

            public void WatchForStateChangeAndExitFullscreen ()
            {
                this.Target.StateChanged += this.OnTargetStateChanged;
                this.Target.Closed += this.OnTargetClosed;
            }

            private void OnTargetStateChanged (object sender, EventArgs e)
            {
                this.Target.Closed -= OnTargetClosed;
                this.Target.StateChanged -= OnTargetStateChanged;
                SetIsFullscreen(this.Target, false);
            }

            private void OnTargetClosed (object sender, EventArgs e)
            {
                this.Target.Closed -= OnTargetClosed;
                this.Target.StateChanged -= OnTargetStateChanged;
            }
        }

        private class WindowMaximizeFixerTracker : WindowWatcher, IDisposable
        {
            private WindowState m_lastWindowState;
            private WindowStyle m_formerWindowStyle;

            public WindowMaximizeFixerTracker (Window target) : base(target)
            {
                this.Target.StateChanged += this.OnTargetStateChanged;
            }

            protected override void Teardown ()
            {
                this.Target.StateChanged -= this.OnTargetStateChanged;
                base.Teardown();
            }

            private void OnTargetStateChanged (object sender, EventArgs e)
            {
                var last = m_lastWindowState;
                m_lastWindowState = this.Target.WindowState;
                if (last == WindowState.Minimized)
                {
                    return;
                }

                // If we're dealing with fullscreen, then we don't care
                if (GetFullscreenInspectorAttachment(this.Target) != null)
                {
                    return;
                }

                if (this.Target.WindowState == WindowState.Maximized)
                {
                    m_formerWindowStyle = this.Target.WindowStyle;
                    this.Target.SetCurrentValue(Window.WindowStyleProperty, WindowStyle.SingleBorderWindow);

                    if (this.RootElement != null)
                    {
                        this.SetRootMargin(GetMaximizedRootElementMargin(this.Target));
                        this.SetRootBorderThickness(GetMaximizedRootElementBorderThickness(this.Target));
                    }
                }
                else if (this.Target.WindowState == WindowState.Normal)
                {
                    this.RevertFromMaximize();
                }
            }

            public void RevertFromMaximize()
            {
                this.Target.SetCurrentValue(Window.WindowStyleProperty, m_formerWindowStyle);
                this.RevertRootBorderThickness();
                this.RevertRootMargin();
            }
        }
    }
}
