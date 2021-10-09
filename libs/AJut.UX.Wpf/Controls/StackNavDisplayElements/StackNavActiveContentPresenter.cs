namespace AJut.UX.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using AJut.UX;
    using DPUtils = AJut.UX.DPUtils<StackNavActiveContentPresenter>;

    /// <summary>
    /// The stack nav presenter that displays the active <see cref="IStackNavDisplayControl"/> control. This handles covers as well for busy wait and popovers.
    /// </summary>
    [TemplatePart(Name = nameof(PART_ContentDisplay), Type = typeof(ContentPresenter))]
    public class StackNavActiveContentPresenter : Control, IStackNavPresenter
    {
        private ContentPresenter PART_ContentDisplay { get; set; }
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
            this.PART_ContentDisplay = (ContentPresenter)this.GetTemplateChild(nameof(PART_ContentDisplay));
            this.PART_ContentDisplay.Focus();
        }
    }
}
