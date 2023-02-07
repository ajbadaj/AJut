namespace AJut.UX.Controls
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using AJut.UX;
    using DPUtils = AJut.UX.DPUtils<StackNavDisplay>;

    /// <summary>
    /// A default implementation of the stack nav display, puts together header, drawer, and content display
    /// </summary>
    public class StackNavDisplay : Control, IStackNavPresenter
    {
        static StackNavDisplay ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(StackNavDisplay), new FrameworkPropertyMetadata(typeof(StackNavDisplay)));
        }

        public StackNavDisplay ()
        {
            this.SetupBasicNavigatorCommandBindings();
        }

        protected override void OnGotFocus (RoutedEventArgs e)
        {
            this.FindBestDescendantToFocusOn();
        }

        public static readonly DependencyProperty NavigatorProperty = DPUtils.Register(_ => _.Navigator, (d, e) => d.OnNavigatorChanged(e));
        public StackNavFlowController Navigator
        {
            get => (StackNavFlowController)this.GetValue(NavigatorProperty);
            set => this.SetValue(NavigatorProperty, value);
        }

        public static readonly DependencyProperty OverlayColumnMarginWidthProperty = DPUtils.Register(_ => _.OverlayColumnMarginWidth);
        public GridLength OverlayColumnMarginWidth
        {
            get => (GridLength)this.GetValue(OverlayColumnMarginWidthProperty);
            set => this.SetValue(OverlayColumnMarginWidthProperty, value);
        }

        public static readonly DependencyProperty OverlayRowMarginHeightProperty = DPUtils.Register(_ => _.OverlayRowMarginHeight);
        public GridLength OverlayRowMarginHeight
        {
            get => (GridLength)this.GetValue(OverlayRowMarginHeightProperty);
            set => this.SetValue(OverlayRowMarginHeightProperty, value);
        }

        public static readonly DependencyProperty BusyWaitOverlayWidthProperty = DPUtils.Register(_ => _.BusyWaitOverlayWidth);
        public GridLength BusyWaitOverlayWidth
        {
            get => (GridLength)this.GetValue(BusyWaitOverlayWidthProperty);
            set => this.SetValue(BusyWaitOverlayWidthProperty, value);
        }

        public static readonly DependencyProperty BusyWaitOverlayHeightProperty = DPUtils.Register(_ => _.BusyWaitOverlayHeight);
        public GridLength BusyWaitOverlayHeight
        {
            get => (GridLength)this.GetValue(BusyWaitOverlayHeightProperty);
            set => this.SetValue(BusyWaitOverlayHeightProperty, value);
        }

        public static readonly DependencyProperty PopoverOverlayWidthProperty = DPUtils.Register(_ => _.PopoverOverlayWidth);
        public GridLength PopoverOverlayWidth
        {
            get => (GridLength)this.GetValue(PopoverOverlayWidthProperty);
            set => this.SetValue(PopoverOverlayWidthProperty, value);
        }

        public static readonly DependencyProperty PopoverOverlayHeightProperty = DPUtils.Register(_ => _.PopoverOverlayHeight);
        public GridLength PopoverOverlayHeight
        {
            get => (GridLength)this.GetValue(PopoverOverlayHeightProperty);
            set => this.SetValue(PopoverOverlayHeightProperty, value);
        }

        private void OnNavigatorChanged (DependencyPropertyChangedEventArgs<StackNavFlowController> e)
        {
            if (e.HasOldValue)
            {
                e.OldValue.NavigationComplete -= _OnNavigationComplete;
            }
            if (e.HasNewValue)
            {
                e.NewValue.NavigationComplete -= _OnNavigationComplete;
                e.NewValue.NavigationComplete += _OnNavigationComplete;
            }

            void _OnNavigationComplete (object sender, EventArgs e)
            {
                this.FindBestDescendantToFocusOn();
            }
        }

        public static readonly DependencyProperty FixedDrawerWidthProperty = DPUtils.Register(_ => _.FixedDrawerWidth, double.NaN);
        /// <summary>
        /// The fixed drawer display width (shows as variable depending on content size otherwise)
        /// </summary>
        public double FixedDrawerWidth
        {
            get => (double)this.GetValue(FixedDrawerWidthProperty);
            set => this.SetValue(FixedDrawerWidthProperty, value);
        }

        private void FindBestDescendantToFocusOn ()
        {
            var presenter = this.GetFirstChildOf<StackNavActiveContentPresenter>();
            var focusTarget = presenter?.GetFirstChildOf<IInputElement>();
            if (focusTarget != null)
            {
                Keyboard.Focus(focusTarget);
            }
            else
            {
                presenter?.Focus();
            }
        }
    }
}
