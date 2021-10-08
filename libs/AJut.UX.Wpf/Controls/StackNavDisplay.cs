namespace AJut.UX.Controls
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using AJut.UX;
    using DPUtils = AJut.UX.DPUtils<StackNavDisplay>;

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

        public static readonly DependencyProperty NavigatorProperty = DPUtils.Register(_ => _.Navigator, (d,e)=>d.OnNavigatorChanged(e));
        public StackNavFlowController Navigator
        {
            get => (StackNavFlowController)this.GetValue(NavigatorProperty);
            set => this.SetValue(NavigatorProperty, value);
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
        public double FixedDrawerWidth
        {
            get => (double)this.GetValue(FixedDrawerWidthProperty);
            set => this.SetValue(FixedDrawerWidthProperty, value);
        }

        private void FindBestDescendantToFocusOn()
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
