namespace AJut.UX.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using AJut.UX;
    using DPUtils = AJut.UX.DPUtils<StackNavActiveContentPresenter>;

    public class StackNavActiveContentPresenter : Control, IStackNavPresenter
    {
        static StackNavActiveContentPresenter ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(StackNavActiveContentPresenter), new FrameworkPropertyMetadata(typeof(StackNavActiveContentPresenter)));
        }

        public StackNavActiveContentPresenter ()
        {
            this.SetupBasicNavigatorCommandBindings();
        }

        public static readonly DependencyProperty NavigatorProperty = DPUtils.Register(_ => _.Navigator);
        public StackNavFlowController Navigator
        {
            get => (StackNavFlowController)this.GetValue(NavigatorProperty);
            set => this.SetValue(NavigatorProperty, value);
        }

        public static readonly DependencyProperty CoverBackgroundBrushProperty = DPUtils.Register(_ => _.CoverBackgroundBrush);
        public Brush CoverBackgroundBrush
        {
            get => (Brush)this.GetValue(CoverBackgroundBrushProperty);
            set => this.SetValue(CoverBackgroundBrushProperty, value);
        }
        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();
        }
    }
}
