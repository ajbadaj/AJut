namespace AJut.UX.Controls
{
    using System.Windows;
    using System.Windows.Controls;
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

        public static readonly DependencyProperty NavigatorProperty = DPUtils.Register(_ => _.Navigator);
        public StackNavFlowController Navigator
        {
            get => (StackNavFlowController)this.GetValue(NavigatorProperty);
            set => this.SetValue(NavigatorProperty, value);
        }

        public static readonly DependencyProperty FixedDrawerWidthProperty = DPUtils.Register(_ => _.FixedDrawerWidth, double.NaN);
        public double FixedDrawerWidth
        {
            get => (double)this.GetValue(FixedDrawerWidthProperty);
            set => this.SetValue(FixedDrawerWidthProperty, value);
        }
    }
}
