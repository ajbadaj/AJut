namespace AJut.UX.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using AJut.UX;
    using DPUtils = AJut.UX.DPUtils<StackNavActiveDrawerPresenter>;

    public class StackNavActiveDrawerPresenter : Control, IStackNavPresenter
    {
        static StackNavActiveDrawerPresenter ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(StackNavActiveDrawerPresenter), new FrameworkPropertyMetadata(typeof(StackNavActiveDrawerPresenter)));
        }

        public StackNavActiveDrawerPresenter ()
        {
            this.SetupBasicNavigatorCommandBindings();
        }

        public static readonly DependencyProperty FixedDrawerDisplayAreaProperty = DPUtils.Register(_ => _.FixedDrawerDisplayArea);
        public DependencyObject FixedDrawerDisplayArea
        {
            get => (DependencyObject)this.GetValue(FixedDrawerDisplayAreaProperty);
            set => this.SetValue(FixedDrawerDisplayAreaProperty, value);
        }

        public static readonly DependencyProperty NavigatorProperty = DPUtils.Register(_ => _.Navigator);
        public StackNavFlowController Navigator
        {
            get => (StackNavFlowController)this.GetValue(NavigatorProperty);
            set => this.SetValue(NavigatorProperty, value);
        }
    }
}
