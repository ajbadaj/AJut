namespace AJut.Application.AttachedProperties
{
    using System;
    using System.Runtime.CompilerServices;
#if WINDOWS_UWP
    using Windows.UI.Xaml;
#else
    using System.Windows;
    using System.Windows.Controls;
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
        public static DependencyProperty AlterRootElementMarginWhenInFullscreenProperty = APUtils.Register(GetAlterRootElementMarginWhenInFullscreen, SetAlterRootElementMarginWhenInFullscreen, true);
        public static bool GetAlterRootElementMarginWhenInFullscreen (DependencyObject obj) => (bool)obj.GetValue(AlterRootElementMarginWhenInFullscreenProperty);
        public static void SetAlterRootElementMarginWhenInFullscreen (DependencyObject obj, bool value) => obj.SetValue(AlterRootElementMarginWhenInFullscreenProperty, value);

        public static DependencyProperty FullscreenWindowPaddingProperty = APUtils.Register(GetFullscreenWindowPadding, SetFullscreenWindowPadding, new Thickness(7.0, 0.0, 7.0, 0.0));
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

                // Update the window's margin if it's a windowstyle none
                if (window.WindowStyle == WindowStyle.None && GetAlterRootElementMarginWhenInFullscreen(window))
                {
                    tracker.SetRootMargin(GetFullscreenWindowPadding(window));
                }

                //((FrameworkElement)window.Content).Margin = GetFullscreenRootOffsetMargin(window);
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


        public static DependencyProperty FixMaximizeAsFullscreenIssueProperty = APUtils.Register(GetFixMaximizeAsFullscreenIssue, SetFixMaximizeAsFullscreenIssue, HandleFixMaximizeAsFullscreenIssueChanged);
        public static bool GetFixMaximizeAsFullscreenIssue (DependencyObject obj) => (bool)obj.GetValue(FixMaximizeAsFullscreenIssueProperty);
        public static void SetFixMaximizeAsFullscreenIssue (DependencyObject obj, bool value) => obj.SetValue(FixMaximizeAsFullscreenIssueProperty, value);

        public static DependencyProperty FixMaximizeRootMarginProperty = APUtils.Register(GetFixMaximizeRootMargin, SetFixMaximizeRootMargin);
        public static Thickness GetFixMaximizeRootMargin (DependencyObject obj) => (Thickness)obj.GetValue(FixMaximizeRootMarginProperty);
        public static void SetFixMaximizeRootMargin (DependencyObject obj, Thickness value) => obj.SetValue(FixMaximizeRootMarginProperty, value);

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

        private class WindowWatcher : IDisposable
        {
            private Thickness m_formerRootMargin;

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
                }
            }

            public void Dispose()
            {
                this.Teardown();
            }

            protected virtual void Teardown()
            {
                this.RevertRootMargin();
                this.RootElement = null;
                this.Target = null;
            }

            public Window Target { get; private set; }
            public FrameworkElement RootElement { get; private set; }
            public bool IsRootMarginAltered { get; private set; }

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
                if (this.FormerWindowState == WindowState.Maximized)
                {
                    this.Target.SetCurrentValue(Window.WindowStateProperty, WindowState.Normal);
                    this.Target.SetCurrentValue(Window.WindowStateProperty, WindowState.Maximized);
                }
                else
                {
                    this.Target.SetCurrentValue(Window.WindowStateProperty, this.FormerWindowState);
                }

                this.Target.SetCurrentValue(Window.WindowStyleProperty, this.FormerWindowStyle);
                this.Target.SetCurrentValue(Window.TopmostProperty, this.WasTopmost);
                base.Teardown();
            }

            public WindowState FormerWindowState { get; set; }
            public WindowStyle FormerWindowStyle { get; set; }
            public bool WasTopmost { get; set; }

            public void WatchForStateChangeAndExitFullscreen ()
            {
                this.Target.StateChanged += _StateChanged;
                this.Target.Closed += _Closed;
                void _StateChanged (object sender, EventArgs e)
                {
                    this.Target.Closed -= _Closed;
                    this.Target.StateChanged -= _StateChanged;
                    SetIsFullscreen(this.Target, false);
                }

                void _Closed (object sender, EventArgs e)
                {
                    this.Target.Closed -= _Closed;
                    this.Target.StateChanged -= _StateChanged;
                }
            }

        }

        private class WindowMaximizeFixerTracker : WindowWatcher, IDisposable
        {
            private double m_formerMaxWidth;
            private double m_formerMaxHeight;

            public WindowMaximizeFixerTracker(Window target) : base(target)
            {
                m_formerMaxWidth = this.Target.MaxWidth;
                m_formerMaxHeight = this.Target.MaxHeight;
                this.Target.StateChanged += this.OnTargetStateChanged;
            }

            protected override void Teardown ()
            {
                this.Target.SetCurrentValue(FrameworkElement.MaxWidthProperty, m_formerMaxWidth);
                this.Target.SetCurrentValue(FrameworkElement.MaxHeightProperty, m_formerMaxHeight);

                this.Target.StateChanged -= this.OnTargetStateChanged;
                base.Teardown();
            }

            private void OnTargetStateChanged (object sender, EventArgs e)
            {
                if (this.Target.WindowState == WindowState.Maximized && !GetIsFullscreen(this.Target))
                {
                    Window sizingHelper = new Window
                    {
                        Owner = this.Target.Owner ?? this.Target,
                        Left = this.Target.Left,
                        Top = this.Target.Top,
                        Width = this.Target.Width,
                        Height = this.Target.Height,
                    };

                    sizingHelper.Show();

                    try
                    {
                        sizingHelper.WindowState = WindowState.Maximized;

                        Thickness newRootMargin = GetFixMaximizeRootMargin(this.Target);
                        this.RootElement.SetCurrentValue(FrameworkElement.MarginProperty, newRootMargin);
                        this.Target.SetCurrentValue(FrameworkElement.MaxWidthProperty, sizingHelper.Width - (newRootMargin.Left + newRootMargin.Right));
                        this.Target.SetCurrentValue(FrameworkElement.MaxHeightProperty, sizingHelper.Height - (newRootMargin.Top + newRootMargin.Bottom));
                    }
                    finally
                    {
                        sizingHelper.Close();
                    }
                }
                else if (this.Target.WindowState == WindowState.Normal)
                {
                    this.RevertRootMargin();
                    this.Target.SetCurrentValue(FrameworkElement.MaxWidthProperty, m_formerMaxWidth);
                    this.Target.SetCurrentValue(FrameworkElement.MaxHeightProperty, m_formerMaxHeight);
                }
            }
        }
    }
}
