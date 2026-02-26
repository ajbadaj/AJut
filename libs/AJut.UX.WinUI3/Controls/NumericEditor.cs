namespace AJut.UX.Controls
{
    using System;
    using Microsoft.UI.Input;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
    using Microsoft.UI.Xaml.Media;
    using Windows.System;
    using Windows.UI;
    using Windows.UI.Core;
    using DPUtils = AJut.UX.DPUtils<NumericEditor>;

    // ===========[ NumericEditor ]=============================================
    // WinUI3 numeric editor for PropertyGrid DataTemplates. Accepts and returns
    // Value as object (TwoWay-bindable to PropertyEditTarget.EditValue) and
    // preserves the source type (float, int, double, ...) across edits.
    //
    // Template parts:
    //   PART_Root            - outer Border (focus border color is set here)
    //   PART_IncreaseButton  - RepeatButton for incrementing Value
    //   PART_DecreaseButton  - RepeatButton for decrementing Value
    //   PART_TextBox         - TextBox for direct numeric text entry
    //   PART_DefaultLabel    - TextBlock showing "#" when LabelContent is null
    //   PART_LabelArea       - ContentControl showing custom LabelContent
    //
    // WinUI3 limitation note: VisualState Setters cannot source values via
    // TemplateBinding (no ancestor binding support). As a result, the hover and
    // pressed Background changes for PART_IncreaseButton and PART_DecreaseButton
    // are applied directly from PointerEntered/Exited/Pressed/Released event
    // handlers rather than from VSM states in the ControlTemplate. The DPs
    // IncreaseHoverHighlight, IncreasePressedHighlight, DecreaseHoverHighlight,
    // and DecreasePressedHighlight exist precisely to surface these as customizable
    // style properties despite this limitation.

    [TemplatePart(Name = nameof(PART_Root), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_IncreaseButton), Type = typeof(RepeatButton))]
    [TemplatePart(Name = nameof(PART_DecreaseButton), Type = typeof(RepeatButton))]
    [TemplatePart(Name = nameof(PART_TextBox), Type = typeof(TextBox))]
    [TemplatePart(Name = nameof(PART_DefaultLabel), Type = typeof(FrameworkElement))]
    [TemplatePart(Name = nameof(PART_LabelArea), Type = typeof(ContentControl))]
    public class NumericEditor : Control, INumericEditorSettings
    {
        // ===========[ Const-like ]===============================================
        // Nearly-transparent but fully hit-testable background for the RepeatButtons.
        private static readonly SolidColorBrush kNearlyTransparentBrush = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));

        // ===========[ Instance fields ]==========================================
        private Border PART_Root;
        private RepeatButton PART_IncreaseButton;
        private RepeatButton PART_DecreaseButton;
        private TextBox PART_TextBox;
        private FrameworkElement PART_DefaultLabel;
        private ContentControl PART_LabelArea;

        private NumericEditorViewModel m_vm;
        private bool m_blockReentrancy;
        private bool m_isIncreasePointerOver;
        private bool m_isDecreasePointerOver;

        // ===========[ Construction ]=============================================
        public NumericEditor ()
        {
            this.DefaultStyleKey = typeof(NumericEditor);
            this.GotFocus += this.OnGotFocus;
            this.LostFocus += this.OnLostFocus;
        }

        // ===========[ INumericEditorSettings ]===================================
        int INumericEditorSettings.DecimalPlacesAllowed => -1;

        object INumericEditorSettings.Minimum => double.IsNegativeInfinity(this.Minimum) ? null : (object)this.Minimum;

        object INumericEditorSettings.Maximum => double.IsPositiveInfinity(this.Maximum) ? null : (object)this.Maximum;

        // ===========[ Dependency Properties ]====================================

        // Value: object - TwoWay binding target for PropertyEditTarget.EditValue.
        public static readonly DependencyProperty ValueProperty = DPUtils.Register(_ => _.Value, (d, e) => d.OnValueChanged(e.NewValue));
        public object Value
        {
            get => this.GetValue(ValueProperty);
            set => this.SetValue(ValueProperty, value);
        }

        // Minimum / Maximum (±∞ sentinels = unconstrained).
        public static readonly DependencyProperty MinimumProperty = DPUtils.Register(_ => _.Minimum, double.NegativeInfinity);
        public double Minimum
        {
            get => (double)this.GetValue(MinimumProperty);
            set => this.SetValue(MinimumProperty, value);
        }

        public static readonly DependencyProperty MaximumProperty = DPUtils.Register(_ => _.Maximum, double.PositiveInfinity);
        public double Maximum
        {
            get => (double)this.GetValue(MaximumProperty);
            set => this.SetValue(MaximumProperty, value);
        }

        // SmallChange: default (normal) increment/decrement step for the RepeatButtons.
        // Hold CTRL to use BigNudge, hold SHIFT to use SmallNudge.
        public static readonly DependencyProperty SmallChangeProperty = DPUtils.Register(_ => _.SmallChange, 1.0);
        public double SmallChange
        {
            get => (double)this.GetValue(SmallChangeProperty);
            set => this.SetValue(SmallChangeProperty, value);
        }

        // BigNudge: large increment/decrement step applied when CTRL is held (default 5).
        public static readonly DependencyProperty BigNudgeProperty = DPUtils.Register(_ => _.BigNudge, 5.0);
        public double BigNudge
        {
            get => (double)this.GetValue(BigNudgeProperty);
            set => this.SetValue(BigNudgeProperty, value);
        }

        // SmallNudge: fine increment/decrement step applied when SHIFT is held (default 0.5).
        public static readonly DependencyProperty SmallNudgeProperty = DPUtils.Register(_ => _.SmallNudge, 0.5);
        public double SmallNudge
        {
            get => (double)this.GetValue(SmallNudgeProperty);
            set => this.SetValue(SmallNudgeProperty, value);
        }

        // LabelContent / LabelContentTemplate: custom label for the button area.
        // When both are null the default "#" glyph (PART_DefaultLabel) is shown.
        public static readonly DependencyProperty LabelContentProperty = DPUtils.Register(_ => _.LabelContent, (d, e) => d.UpdateLabelVisibility());
        public object LabelContent
        {
            get => this.GetValue(LabelContentProperty);
            set => this.SetValue(LabelContentProperty, value);
        }

        public static readonly DependencyProperty LabelContentTemplateProperty = DPUtils.Register(_ => _.LabelContentTemplate, (d, e) => d.UpdateLabelVisibility());
        public DataTemplate LabelContentTemplate
        {
            get => (DataTemplate)this.GetValue(LabelContentTemplateProperty);
            set => this.SetValue(LabelContentTemplateProperty, value);
        }

        // Hover / pressed highlight brushes for the increase RepeatButton.
        public static readonly DependencyProperty IncreaseHoverHighlightProperty = DPUtils.Register(_ => _.IncreaseHoverHighlight);
        public Brush IncreaseHoverHighlight
        {
            get => (Brush)this.GetValue(IncreaseHoverHighlightProperty);
            set => this.SetValue(IncreaseHoverHighlightProperty, value);
        }

        public static readonly DependencyProperty IncreasePressedHighlightProperty = DPUtils.Register(_ => _.IncreasePressedHighlight);
        public Brush IncreasePressedHighlight
        {
            get => (Brush)this.GetValue(IncreasePressedHighlightProperty);
            set => this.SetValue(IncreasePressedHighlightProperty, value);
        }

        // Hover / pressed highlight brushes for the decrease RepeatButton.
        public static readonly DependencyProperty DecreaseHoverHighlightProperty = DPUtils.Register(_ => _.DecreaseHoverHighlight);
        public Brush DecreaseHoverHighlight
        {
            get => (Brush)this.GetValue(DecreaseHoverHighlightProperty);
            set => this.SetValue(DecreaseHoverHighlightProperty, value);
        }

        public static readonly DependencyProperty DecreasePressedHighlightProperty = DPUtils.Register(_ => _.DecreasePressedHighlight);
        public Brush DecreasePressedHighlight
        {
            get => (Brush)this.GetValue(DecreasePressedHighlightProperty);
            set => this.SetValue(DecreasePressedHighlightProperty, value);
        }

        public static readonly DependencyProperty TextBoxBackgroundProperty = DPUtils.Register(_ => _.TextBoxBackground);
        public Brush TextBoxBackground
        {
            get => (Brush)this.GetValue(TextBoxBackgroundProperty);
            set => this.SetValue(TextBoxBackgroundProperty, value);
        }

        // ===========[ Template application ]=====================================
        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            // 1. Unwire old parts
            if (this.PART_IncreaseButton != null)
            {
                this.PART_IncreaseButton.Click -= this.IncreaseButton_OnClick;
                this.PART_IncreaseButton.PointerEntered -= this.IncreaseButton_OnPointerEntered;
                this.PART_IncreaseButton.PointerExited -= this.IncreaseButton_OnPointerExited;
                this.PART_IncreaseButton.PointerPressed -= this.IncreaseButton_OnPointerPressed;
                this.PART_IncreaseButton.PointerReleased -= this.IncreaseButton_OnPointerReleased;
            }

            if (this.PART_DecreaseButton != null)
            {
                this.PART_DecreaseButton.Click -= this.DecreaseButton_OnClick;
                this.PART_DecreaseButton.PointerEntered -= this.DecreaseButton_OnPointerEntered;
                this.PART_DecreaseButton.PointerExited -= this.DecreaseButton_OnPointerExited;
                this.PART_DecreaseButton.PointerPressed -= this.DecreaseButton_OnPointerPressed;
                this.PART_DecreaseButton.PointerReleased -= this.DecreaseButton_OnPointerReleased;
            }

            if (this.PART_TextBox != null)
            {
                this.PART_TextBox.TextChanged -= this.TextBox_OnTextChanged;
            }

            // 2. Acquire new parts
            this.PART_Root = this.GetTemplateChild(nameof(PART_Root)) as Border;
            this.PART_IncreaseButton = this.GetTemplateChild(nameof(PART_IncreaseButton)) as RepeatButton;
            this.PART_DecreaseButton = this.GetTemplateChild(nameof(PART_DecreaseButton)) as RepeatButton;
            this.PART_TextBox = this.GetTemplateChild(nameof(PART_TextBox)) as TextBox;
            this.PART_DefaultLabel = this.GetTemplateChild(nameof(PART_DefaultLabel)) as FrameworkElement;
            this.PART_LabelArea = this.GetTemplateChild(nameof(PART_LabelArea)) as ContentControl;

            // 3. Wire new parts
            if (this.PART_IncreaseButton != null)
            {
                this.PART_IncreaseButton.Click += this.IncreaseButton_OnClick;
                this.PART_IncreaseButton.PointerEntered += this.IncreaseButton_OnPointerEntered;
                this.PART_IncreaseButton.PointerExited += this.IncreaseButton_OnPointerExited;
                this.PART_IncreaseButton.PointerPressed += this.IncreaseButton_OnPointerPressed;
                this.PART_IncreaseButton.PointerReleased += this.IncreaseButton_OnPointerReleased;
            }

            if (this.PART_DecreaseButton != null)
            {
                this.PART_DecreaseButton.Click += this.DecreaseButton_OnClick;
                this.PART_DecreaseButton.PointerEntered += this.DecreaseButton_OnPointerEntered;
                this.PART_DecreaseButton.PointerExited += this.DecreaseButton_OnPointerExited;
                this.PART_DecreaseButton.PointerPressed += this.DecreaseButton_OnPointerPressed;
                this.PART_DecreaseButton.PointerReleased += this.DecreaseButton_OnPointerReleased;
            }

            if (this.PART_TextBox != null)
            {
                this.PART_TextBox.TextChanged += this.TextBox_OnTextChanged;

                // Push current value into the text box (template-before-data case).
                if (m_vm != null)
                {
                    m_blockReentrancy = true;
                    try { this.PART_TextBox.Text = m_vm.Text; }
                    finally { m_blockReentrancy = false; }
                }
            }

            this.UpdateLabelVisibility();
        }

        // ===========[ Events ]===================================================
        private void OnGotFocus (object sender, RoutedEventArgs e)
        {
            if (this.PART_Root != null)
            {
                this.PART_Root.BorderBrush = this.FocusedBorderBrushResolved();
            }
        }

        private void OnLostFocus (object sender, RoutedEventArgs e)
        {
            if (this.PART_Root != null)
            {
                this.PART_Root.BorderBrush = this.BorderBrush;
            }
        }

        private void IncreaseButton_OnClick (object sender, RoutedEventArgs e) => this.Nudge(positive: true);
        private void DecreaseButton_OnClick (object sender, RoutedEventArgs e) => this.Nudge(positive: false);

        private void IncreaseButton_OnPointerEntered (object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            m_isIncreasePointerOver = true;
            if (this.PART_IncreaseButton != null)
            {
                this.PART_IncreaseButton.Background = this.IncreaseHoverHighlight ?? kNearlyTransparentBrush;
            }
        }

        private void IncreaseButton_OnPointerExited (object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            m_isIncreasePointerOver = false;
            if (this.PART_IncreaseButton != null)
            {
                this.PART_IncreaseButton.Background = kNearlyTransparentBrush;
            }
        }

        private void IncreaseButton_OnPointerPressed (object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (this.PART_IncreaseButton != null)
            {
                this.PART_IncreaseButton.Background = this.IncreasePressedHighlight ?? kNearlyTransparentBrush;
            }
        }

        private void IncreaseButton_OnPointerReleased (object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (this.PART_IncreaseButton != null)
            {
                this.PART_IncreaseButton.Background = m_isIncreasePointerOver
                    ? (this.IncreaseHoverHighlight ?? kNearlyTransparentBrush)
                    : kNearlyTransparentBrush;
            }
        }

        private void DecreaseButton_OnPointerEntered (object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            m_isDecreasePointerOver = true;
            if (this.PART_DecreaseButton != null)
            {
                this.PART_DecreaseButton.Background = this.DecreaseHoverHighlight ?? kNearlyTransparentBrush;
            }
        }

        private void DecreaseButton_OnPointerExited (object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            m_isDecreasePointerOver = false;
            if (this.PART_DecreaseButton != null)
            {
                this.PART_DecreaseButton.Background = kNearlyTransparentBrush;
            }
        }

        private void DecreaseButton_OnPointerPressed (object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (this.PART_DecreaseButton != null)
            {
                this.PART_DecreaseButton.Background = this.DecreasePressedHighlight ?? kNearlyTransparentBrush;
            }
        }

        private void DecreaseButton_OnPointerReleased (object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (this.PART_DecreaseButton != null)
            {
                this.PART_DecreaseButton.Background = m_isDecreasePointerOver
                    ? (this.DecreaseHoverHighlight ?? kNearlyTransparentBrush)
                    : kNearlyTransparentBrush;
            }
        }

        private void TextBox_OnTextChanged (object sender, TextChangedEventArgs e)
        {
            if (m_blockReentrancy || this.PART_TextBox == null)
            {
                return;
            }

            // m_vm may be null if text is typed before Value was ever set.
            if (m_vm == null)
            {
                m_vm = new NumericEditorViewModel(this, 0.0);
            }

            // Route text through the view model (handles parse, capping, type-preservation).
            m_vm.Text = this.PART_TextBox.Text;

            m_blockReentrancy = true;
            try { this.Value = m_vm.SourceValue; }
            finally { m_blockReentrancy = false; }
        }

        // ===========[ Property change handlers ]=================================
        private void OnValueChanged (object newValue)
        {
            if (m_blockReentrancy)
            {
                return;
            }

            // Rebuild view model so type detection runs on the new value.
            m_vm = new NumericEditorViewModel(this, newValue ?? 0.0);

            if (this.PART_TextBox == null)
            {
                return;
            }

            m_blockReentrancy = true;
            try { this.PART_TextBox.Text = m_vm.Text; }
            finally { m_blockReentrancy = false; }
        }

        // ===========[ Helpers ]==================================================
        private void Nudge (bool positive)
        {
            if (m_vm == null)
            {
                m_vm = new NumericEditorViewModel(this, 0.0);
            }

            // CTRL → BigNudge, SHIFT → SmallNudge, otherwise normal SmallChange.
            double amount;
            if (IsKeyDown(VirtualKey.Control))
            {
                amount = this.BigNudge;
            }
            else if (IsKeyDown(VirtualKey.Shift))
            {
                amount = this.SmallNudge;
            }
            else
            {
                amount = this.SmallChange;
            }

            m_vm.Nudge(positive, amount);

            m_blockReentrancy = true;
            try
            {
                if (this.PART_TextBox != null)
                {
                    this.PART_TextBox.Text = m_vm.Text;
                }

                this.Value = m_vm.SourceValue;
            }
            finally
            {
                m_blockReentrancy = false;
            }
        }

        private static bool IsKeyDown (VirtualKey key)
        {
            return (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) != 0;
        }

        private void UpdateLabelVisibility ()
        {
            if (this.PART_DefaultLabel == null || this.PART_LabelArea == null)
            {
                return;
            }

            bool hasCustomLabel = (this.LabelContent != null) || (this.LabelContentTemplate != null);

            this.PART_DefaultLabel.Visibility = hasCustomLabel ? Visibility.Collapsed : Visibility.Visible;
            this.PART_LabelArea.Visibility = hasCustomLabel ? Visibility.Visible : Visibility.Collapsed;

            if (hasCustomLabel)
            {
                this.PART_LabelArea.Content = this.LabelContent;
                this.PART_LabelArea.ContentTemplate = this.LabelContentTemplate;
            }
        }

        // Resolves the focus-state border brush. The default implementation uses
        // the system accent color so the default style has no AJut dependency.
        // The AJut theme override replaces BorderBrush and FocusBorderBrush via
        // Style Setters, so we can check if the user set a FocusBorderBrush DP.
        private Brush FocusedBorderBrushResolved ()
        {
            // If caller set a FocusBorderBrush DP we'd return it here, but for
            // simplicity the Focused border is handled via code setting PART_Root.BorderBrush
            // directly in OnGotFocus / OnLostFocus. The AJut override style can set
            // BorderBrush to the accent, but the focused color reverts to the system accent.
            // To properly customize, subclass or use the AJut override template variant.
            return (Brush)this.GetValue(FocusBorderBrushProperty) ?? this.BorderBrush;
        }

        // FocusBorderBrush DP: set to AJut_Brush_PrimaryHighlight in AllControlThemeOverrides.xaml
        // so the focused border uses the AJut accent without duplicating the full template.
        public static readonly DependencyProperty FocusBorderBrushProperty = DPUtils.Register(
            _ => _.FocusBorderBrush);
        public Brush FocusBorderBrush
        {
            get => (Brush)this.GetValue(FocusBorderBrushProperty);
            set => this.SetValue(FocusBorderBrushProperty, value);
        }
    }
}
