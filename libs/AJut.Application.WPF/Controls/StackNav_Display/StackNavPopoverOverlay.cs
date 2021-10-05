namespace AJut.Application.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using AJut.Application.AttachedProperties;
    using AJut.Application;
    using DPUtils = AJut.Application.DPUtils<StackNavPopoverOverlay>;

    [TemplatePart(Name = nameof(PART_PopoverDisplayArea), Type = typeof(Border))]
    public class StackNavPopoverOverlay : Control
    {
        private Border PART_PopoverDisplayArea;
        static StackNavPopoverOverlay ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(StackNavPopoverOverlay), new FrameworkPropertyMetadata(typeof(StackNavPopoverOverlay)));
        }

        public StackNavPopoverOverlay ()
        {
            ClickableXTA.AddClickHandler(this, OuterContents_OnClick);
        }

        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            if (this.PART_PopoverDisplayArea != null)
            {
                this.PART_PopoverDisplayArea.MouseDown -= _OnBlockMouseDown;
            }

            this.PART_PopoverDisplayArea = (Border)this.GetTemplateChild(nameof(PART_PopoverDisplayArea));
            if (this.PART_PopoverDisplayArea != null)
            {
                this.PART_PopoverDisplayArea.MouseDown += _OnBlockMouseDown;
            }

            void _OnBlockMouseDown (object sender, MouseButtonEventArgs e)
            {
                e.Handled = true;
            }
        }

        public static readonly DependencyProperty DisplayContentProperty = DPUtils.Register(_ => _.DisplayContent);
        public IStackNavPopoverDisplayBase DisplayContent
        {
            get => (IStackNavPopoverDisplayBase)this.GetValue(DisplayContentProperty);
            set => this.SetValue(DisplayContentProperty, value);
        }

        public static readonly DependencyProperty BackgroundCoverBrushProperty = DPUtils.Register(_ => _.BackgroundCoverBrush);
        public Brush BackgroundCoverBrush
        {
            get => (Brush)this.GetValue(BackgroundCoverBrushProperty);
            set => this.SetValue(BackgroundCoverBrushProperty, value);
        }


        public static readonly DependencyProperty HighlightBorderBrushProperty = DPUtils.Register(_ => _.HighlightBorderBrush);
        public Brush HighlightBorderBrush
        {
            get => (Brush)this.GetValue(HighlightBorderBrushProperty);
            set => this.SetValue(HighlightBorderBrushProperty, value);
        }

        public static readonly DependencyProperty HighlightBorderThicknessProperty = DPUtils.Register(_ => _.HighlightBorderThickness);
        public Thickness HighlightBorderThickness
        {
            get => (Thickness)this.GetValue(HighlightBorderThicknessProperty);
            set => this.SetValue(HighlightBorderThicknessProperty, value);
        }

        public static readonly DependencyProperty HighlightBorderPaddingProperty = DPUtils.Register(_ => _.HighlightBorderPadding);
        public Thickness HighlightBorderPadding
        {
            get => (Thickness)this.GetValue(HighlightBorderPaddingProperty);
            set => this.SetValue(HighlightBorderPaddingProperty, value);
        }

        private void OuterContents_OnClick (object sender, MouseButtonEventArgs e)
        {
            this.DisplayContent.Cancel();
        }
    }
}
