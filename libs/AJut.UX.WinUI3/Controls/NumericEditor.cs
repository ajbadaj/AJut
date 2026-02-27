namespace AJut.UX.Controls
{
    using Microsoft.UI.Input;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Controls.Primitives;
    using Microsoft.UI.Xaml.Media;
    using Windows.System;
    using Windows.UI.Core;
    using DPUtils = AJut.UX.DPUtils<NumericEditor>;

    // ===========[ NumericEditor ]=============================================
    // WinUI3 numeric editor for PropertyGrid DataTemplates. Accepts and returns
    // Value as object (TwoWay-bindable to PropertyEditTarget.EditValue) and
    // preserves the source type (float, int, double, ...) across edits.
    //
    // Template parts:
    //   PART_Root            - outer Border (FocusStates VSM group drives BorderBrush)
    //   PART_IncreaseButton  - RepeatButton for incrementing Value (VSM CommonStates drives Background)
    //   PART_DecreaseButton  - RepeatButton for decrementing Value (VSM CommonStates drives Background)
    //   PART_TextBox         - TextBox for direct numeric text entry
    //   PART_DefaultLabel    - Viewbox showing "#" glyph when LabelContent is null
    //   PART_LabelArea       - ContentControl showing custom LabelContent
    //
    // VSM groups:
    //   FocusStates (on PART_Root)           — Focused / Unfocused
    //   CommonStates (on each RepeatButton)  — Normal / PointerOver / Pressed
    //     (ButtonBase drives RepeatButton's CommonStates automatically)

    [TemplatePart(Name = nameof(PART_Root), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_IncreaseButton), Type = typeof(RepeatButton))]
    [TemplatePart(Name = nameof(PART_DecreaseButton), Type = typeof(RepeatButton))]
    [TemplatePart(Name = nameof(PART_TextBox), Type = typeof(TextBox))]
    [TemplatePart(Name = nameof(PART_DefaultLabel), Type = typeof(FrameworkElement))]
    [TemplatePart(Name = nameof(PART_LabelArea), Type = typeof(ContentControl))]
    public class NumericEditor : Control, INumericEditorSettings
    {
        // ===========[ Instance fields ]==========================================
        private Border PART_Root;
        private RepeatButton PART_IncreaseButton;
        private RepeatButton PART_DecreaseButton;
        private TextBox PART_TextBox;
        private FrameworkElement PART_DefaultLabel;
        private ContentControl PART_LabelArea;

        private NumericEditorViewModel m_vm;
        private bool m_blockReentrancy;

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
            }

            if (this.PART_DecreaseButton != null)
            {
                this.PART_DecreaseButton.Click -= this.DecreaseButton_OnClick;
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

            // 3. Seed VSM — Unfocused is the initial state (no TemplateBinding on BorderBrush)
            VisualStateManager.GoToState(this, "Unfocused", false);

            // 4. Wire new parts
            if (this.PART_IncreaseButton != null)
            {
                this.PART_IncreaseButton.Click += this.IncreaseButton_OnClick;
            }

            if (this.PART_DecreaseButton != null)
            {
                this.PART_DecreaseButton.Click += this.DecreaseButton_OnClick;
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
            VisualStateManager.GoToState(this, "Focused", true);
        }

        private void OnLostFocus (object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, "Unfocused", true);
        }

        private void IncreaseButton_OnClick (object sender, RoutedEventArgs e) => this.Nudge(positive: true);
        private void DecreaseButton_OnClick (object sender, RoutedEventArgs e) => this.Nudge(positive: false);

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

        // ===========[ Helpers ]=================================================
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
    }
}
