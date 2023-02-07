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
