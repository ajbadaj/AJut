namespace AJut.UX.Controls
{
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Media;
    using System.Windows.Media.Effects;
    using DPUtils = AJut.UX.DPUtils<ToggleSwitchButton>;

    [TemplatePart(Name = nameof(PART_SwitchHolder), Type = typeof(Panel))]
    public class ToggleSwitchButton : ButtonBase
    {
        static ToggleSwitchButton ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ToggleSwitchButton), new FrameworkPropertyMetadata(typeof(ToggleSwitchButton)));
        }

        public ToggleSwitchButton ()
        {
            this.Click += _OnClick;

            void _OnClick (object sender, RoutedEventArgs e)
            {
                this.IsChecked = !this.IsChecked;
            }
        }

        public override void OnApplyTemplate ()
        {
            if (this.PART_SwitchHolder != null)
            {
                this.PART_SwitchHolder.SizeChanged -= _OnSwitchAreaChanged;
                this.PART_SwitchHolder = null;
            }
            base.OnApplyTemplate();
            this.PART_SwitchHolder = this.GetTemplateChild(nameof(PART_SwitchHolder)) as Panel;
            Debug.Assert(this.PART_SwitchHolder != null);
            this.ResetCalculations();
            this.PART_SwitchHolder.SizeChanged += _OnSwitchAreaChanged;

            void _OnSwitchAreaChanged (object sender, SizeChangedEventArgs e)
            {
                this.ResetCalculations();
            }
        }

        private Panel PART_SwitchHolder { get; set; }

        // ===========================[ Main Properties ]==================================

        public static readonly DependencyProperty IsCheckedProperty = DPUtils.Register(_ => _.IsChecked, (d, e) => d.ResetCalculations());
        public bool IsChecked
        {
            get => (bool)this.GetValue(IsCheckedProperty);
            set => this.SetValue(IsCheckedProperty, value);
        }

        public static readonly DependencyProperty InsetLabelTrueProperty = DPUtils.Register(_ => _.InsetLabelTrue);
        public object InsetLabelTrue
        {
            get => (object)this.GetValue(InsetLabelTrueProperty);
            set => this.SetValue(InsetLabelTrueProperty, value);
        }

        public static readonly DependencyProperty InsetLabelFalseProperty = DPUtils.Register(_ => _.InsetLabelFalse);
        public object InsetLabelFalse
        {
            get => (object)this.GetValue(InsetLabelFalseProperty);
            set => this.SetValue(InsetLabelFalseProperty, value);
        }

        public static readonly DependencyProperty InsetLabelTemplateProperty = DPUtils.Register(_ => _.InsetLabelTemplate);
        public DataTemplate InsetLabelTemplate
        {
            get => (DataTemplate)this.GetValue(InsetLabelTemplateProperty);
            set => this.SetValue(InsetLabelTemplateProperty, value);
        }

        // ===========================[ Style Indicators ]============================

        public static readonly DependencyProperty ContentForegroundProperty = DPUtils.Register(_ => _.ContentForeground);
        public Brush ContentForeground
        {
            get => (Brush)this.GetValue(ContentForegroundProperty);
            set => this.SetValue(ContentForegroundProperty, value);
        }

        public static readonly DependencyProperty ContentPaddingProperty = DPUtils.Register(_ => _.ContentPadding);
        public Thickness ContentPadding
        {
            get => (Thickness)this.GetValue(ContentPaddingProperty);
            set => this.SetValue(ContentPaddingProperty, value);
        }

        public static readonly DependencyProperty InsetLabelFalseOpacityProperty = DPUtils.Register(_ => _.InsetLabelFalseOpacity, 1.0);
        public double InsetLabelFalseOpacity
        {
            get => (double)this.GetValue(InsetLabelFalseOpacityProperty);
            set => this.SetValue(InsetLabelFalseOpacityProperty, value);
        }

        // ========== High Level ==========

        public static readonly DependencyProperty SwitchBorderBrushProperty = DPUtils.Register(_ => _.SwitchBorderBrush);
        public Brush SwitchBorderBrush
        {
            get => (Brush)this.GetValue(SwitchBorderBrushProperty);
            set => this.SetValue(SwitchBorderBrushProperty, value);
        }

        public static readonly DependencyProperty SwitchBackgroundProperty = DPUtils.Register(_ => _.SwitchBackground);
        public Brush SwitchBackground
        {
            get => (Brush)this.GetValue(SwitchBackgroundProperty);
            set => this.SetValue(SwitchBackgroundProperty, value);
        }


        public static readonly DependencyProperty SwitchBorderThicknessProperty = DPUtils.Register(_ => _.SwitchBorderThickness);
        public Thickness SwitchBorderThickness
        {
            get => (Thickness)this.GetValue(SwitchBorderThicknessProperty);
            set => this.SetValue(SwitchBorderThicknessProperty, value);
        }

        public static readonly DependencyProperty SwitchBorderCornerRadiusProperty = DPUtils.Register(_ => _.SwitchBorderCornerRadius);
        public CornerRadius SwitchBorderCornerRadius
        {
            get => (CornerRadius)this.GetValue(SwitchBorderCornerRadiusProperty);
            set => this.SetValue(SwitchBorderCornerRadiusProperty, value);
        }

        public static readonly DependencyProperty SwitchBorderBrushHoverProperty = DPUtils.Register(_ => _.SwitchBorderBrushHover);
        public Brush SwitchBorderBrushHover
        {
            get => (Brush)this.GetValue(SwitchBorderBrushHoverProperty);
            set => this.SetValue(SwitchBorderBrushHoverProperty, value);
        }

        public static readonly DependencyProperty SwitchBackgroundHoverProperty = DPUtils.Register(_ => _.SwitchBackgroundHover);
        public Brush SwitchBackgroundHover
        {
            get => (Brush)this.GetValue(SwitchBackgroundHoverProperty);
            set => this.SetValue(SwitchBackgroundHoverProperty, value);
        }

        public static readonly DependencyProperty SwitchSizingPercentProperty = DPUtils.Register(_ => _.SwitchSizingPercent, (d, _) => d.ResetCalculations());
        public double SwitchSizingPercent
        {
            get => (double)this.GetValue(SwitchSizingPercentProperty);
            set => this.SetValue(SwitchSizingPercentProperty, value);
        }

        public static readonly DependencyProperty SwitchPaddingProperty = DPUtils.Register(_ => _.SwitchPadding);
        public Thickness SwitchPadding
        {
            get => (Thickness)this.GetValue(SwitchPaddingProperty);
            set => this.SetValue(SwitchPaddingProperty, value);
        }

        public static readonly DependencyProperty InsetElementHorizontalAlignmentProperty = DPUtils.Register(_ => _.InsetElementHorizontalAlignment);
        public HorizontalAlignment InsetElementHorizontalAlignment
        {
            get => (HorizontalAlignment)this.GetValue(InsetElementHorizontalAlignmentProperty);
            set => this.SetValue(InsetElementHorizontalAlignmentProperty, value);
        }

        public static readonly DependencyProperty InsetElementVerticalAlignmentProperty = DPUtils.Register(_ => _.InsetElementVerticalAlignment);
        public VerticalAlignment InsetElementVerticalAlignment
        {
            get => (VerticalAlignment)this.GetValue(InsetElementVerticalAlignmentProperty);
            set => this.SetValue(InsetElementVerticalAlignmentProperty, value);
        }

        public static readonly DependencyProperty BorderCornerRadiusProperty = DPUtils.Register(_ => _.BorderCornerRadius);
        public CornerRadius BorderCornerRadius
        {
            get => (CornerRadius)this.GetValue(BorderCornerRadiusProperty);
            set => this.SetValue(BorderCornerRadiusProperty, value);
        }

        public static readonly DependencyProperty SwitchEffectProperty = DPUtils.Register(_ => _.SwitchEffect);
        public Effect SwitchEffect
        {
            get => (Effect)this.GetValue(SwitchEffectProperty);
            set => this.SetValue(SwitchEffectProperty, value);
        }

        // ========== Brushes: True ==========

        public static readonly DependencyProperty BorderBrushWhenTrueProperty = DPUtils.Register(_ => _.BorderBrushWhenTrue);
        public Brush BorderBrushWhenTrue
        {
            get => (Brush)this.GetValue(BorderBrushWhenTrueProperty);
            set => this.SetValue(BorderBrushWhenTrueProperty, value);
        }

        public static readonly DependencyProperty BackgroundWhenTrueProperty = DPUtils.Register(_ => _.BackgroundWhenTrue);
        public Brush BackgroundWhenTrue
        {
            get => (Brush)this.GetValue(BackgroundWhenTrueProperty);
            set => this.SetValue(BackgroundWhenTrueProperty, value);
        }

        public static readonly DependencyProperty ForegroundWhenTrueProperty = DPUtils.Register(_ => _.ForegroundWhenTrue);
        public Brush ForegroundWhenTrue
        {
            get => (Brush)this.GetValue(ForegroundWhenTrueProperty);
            set => this.SetValue(ForegroundWhenTrueProperty, value);
        }

        // ===========================[ Calculated ]============================


        private static readonly DependencyPropertyKey CalculatedAnteriorRowPropertyKey = DPUtils.RegisterReadOnly(_ => _.CalculatedAnteriorRow);
        public static readonly DependencyProperty CalculatedAnteriorRowProperty = CalculatedAnteriorRowPropertyKey.DependencyProperty;
        public int CalculatedAnteriorRow
        {
            get => (int)this.GetValue(CalculatedAnteriorRowProperty);
            protected set => this.SetValue(CalculatedAnteriorRowPropertyKey, value);
        }

        private static readonly DependencyPropertyKey CalculatedAnteriorColumnPropertyKey = DPUtils.RegisterReadOnly(_ => _.CalculatedAnteriorColumn);
        public static readonly DependencyProperty CalculatedAnteriorColumnProperty = CalculatedAnteriorColumnPropertyKey.DependencyProperty;
        public int CalculatedAnteriorColumn
        {
            get => (int)this.GetValue(CalculatedAnteriorColumnProperty);
            protected set => this.SetValue(CalculatedAnteriorColumnPropertyKey, value);
        }

        private static readonly DependencyPropertyKey CalculatedPosteriorRowPropertyKey = DPUtils.RegisterReadOnly(_ => _.CalculatedPosteriorRow);
        public static readonly DependencyProperty CalculatedPosteriorRowProperty = CalculatedPosteriorRowPropertyKey.DependencyProperty;
        public int CalculatedPosteriorRow
        {
            get => (int)this.GetValue(CalculatedPosteriorRowProperty);
            protected set => this.SetValue(CalculatedPosteriorRowPropertyKey, value);
        }

        private static readonly DependencyPropertyKey CalculatedPosteriorColumnPropertyKey = DPUtils.RegisterReadOnly(_ => _.CalculatedPosteriorColumn);
        public static readonly DependencyProperty CalculatedPosteriorColumnProperty = CalculatedPosteriorColumnPropertyKey.DependencyProperty;
        public int CalculatedPosteriorColumn
        {
            get => (int)this.GetValue(CalculatedPosteriorColumnProperty);
            protected set => this.SetValue(CalculatedPosteriorColumnPropertyKey, value);
        }

        private static readonly DependencyPropertyKey CalculatedRowSpanPropertyKey = DPUtils.RegisterReadOnly(_ => _.CalculatedRowSpan);
        public static readonly DependencyProperty CalculatedRowSpanProperty = CalculatedRowSpanPropertyKey.DependencyProperty;
        public int CalculatedRowSpan
        {
            get => (int)this.GetValue(CalculatedRowSpanProperty);
            protected set => this.SetValue(CalculatedRowSpanPropertyKey, value);
        }

        private static readonly DependencyPropertyKey CalculatedColumnSpanPropertyKey = DPUtils.RegisterReadOnly(_ => _.CalculatedColumnSpan);
        public static readonly DependencyProperty CalculatedColumnSpanProperty = CalculatedColumnSpanPropertyKey.DependencyProperty;
        public int CalculatedColumnSpan
        {
            get => (int)this.GetValue(CalculatedColumnSpanProperty);
            protected set => this.SetValue(CalculatedColumnSpanPropertyKey, value);
        }


        private static readonly DependencyPropertyKey CalculatedSwitchWidthPropertyKey = DPUtils.RegisterReadOnly(_ => _.CalculatedSwitchWidth);
        public static readonly DependencyProperty CalculatedSwitchWidthProperty = CalculatedSwitchWidthPropertyKey.DependencyProperty;
        public double CalculatedSwitchWidth
        {
            get => (double)this.GetValue(CalculatedSwitchWidthProperty);
            protected set => this.SetValue(CalculatedSwitchWidthPropertyKey, value);
        }

        private static readonly DependencyPropertyKey CalculatedSwitchHeightPropertyKey = DPUtils.RegisterReadOnly(_ => _.CalculatedSwitchHeight);
        public static readonly DependencyProperty CalculatedSwitchHeightProperty = CalculatedSwitchHeightPropertyKey.DependencyProperty;
        public double CalculatedSwitchHeight
        {
            get => (double)this.GetValue(CalculatedSwitchHeightProperty);
            protected set => this.SetValue(CalculatedSwitchHeightPropertyKey, value);
        }

        private static readonly DependencyPropertyKey CalculatedSwitchLeftPropertyKey = DPUtils.RegisterReadOnly(_ => _.CalculatedSwitchLeft);
        public static readonly DependencyProperty CalculatedSwitchLeftProperty = CalculatedSwitchLeftPropertyKey.DependencyProperty;
        public double CalculatedSwitchLeft
        {
            get => (double)this.GetValue(CalculatedSwitchLeftProperty);
            protected set => this.SetValue(CalculatedSwitchLeftPropertyKey, value);
        }

        private static readonly DependencyPropertyKey CalculatedSwitchTopPropertyKey = DPUtils.RegisterReadOnly(_ => _.CalculatedSwitchTop);
        public static readonly DependencyProperty CalculatedSwitchTopProperty = CalculatedSwitchTopPropertyKey.DependencyProperty;
        public double CalculatedSwitchTop
        {
            get => (double)this.GetValue(CalculatedSwitchTopProperty);
            protected set => this.SetValue(CalculatedSwitchTopPropertyKey, value);
        }

        // ===========================[ Utilities / Handlers / Calculators ]============================

        private void ResetCalculations ()
        {
            double switchAreaWidth = this.PART_SwitchHolder?.ActualWidth ?? 0;
            double switchAreaHeight = this.PART_SwitchHolder?.ActualHeight ?? 0;

            this.CalculatedColumnSpan = 1;
            this.CalculatedRowSpan = 3;

            this.CalculatedAnteriorRow = 0;
            this.CalculatedAnteriorColumn = 0;

            this.CalculatedPosteriorRow = 0;
            this.CalculatedPosteriorColumn = 2;

            this.CalculatedSwitchWidth = switchAreaWidth * this.SwitchSizingPercent;
            this.CalculatedSwitchHeight = switchAreaHeight;

            this.CalculatedSwitchLeft = this.IsChecked ? switchAreaWidth - this.CalculatedSwitchWidth : 0;
            this.CalculatedSwitchTop = 0;
        }
    }
}
