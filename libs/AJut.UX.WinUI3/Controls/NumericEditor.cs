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
    //   FocusStates (on PART_Root)           - Focused / Unfocused
    //   CommonStates (on each RepeatButton)  - Normal / PointerOver / Pressed
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
            this.IsTabStop = false;
        }

        // ===========[ INumericEditorSettings ]===================================
        int INumericEditorSettings.DecimalPlacesAllowed => -1;

        object INumericEditorSettings.Minimum => double.IsNegativeInfinity(this.Minimum) ? null : (object)this.Minimum;

        object INumericEditorSettings.Maximum => double.IsPositiveInfinity(this.Maximum) ? null : (object)this.Maximum;

        eOutOfBoundsResponse INumericEditorSettings.OutOfBoundsResponse => this.OutOfBoundsResponse;

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

        // The normal nudge (default) increment/decrement step for the RepeatButtons.
        //  > Hold CTRL to use BigNudge, hold SHIFT to use SmallNudge.
        public static readonly DependencyProperty NudgeProperty = DPUtils.Register(_ => _.Nudge, 1.0);
        public double Nudge
        {
            get => (double)this.GetValue(NudgeProperty);
            set => this.SetValue(NudgeProperty, value);
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

        public static readonly DependencyProperty TextBoxHeaderProperty = DPUtils.Register(_ => _.TextBoxHeader);
        public string TextBoxHeader
        {
            get => (string)this.GetValue(TextBoxHeaderProperty);
            set => this.SetValue(TextBoxHeaderProperty, value);
        }

        public static readonly DependencyProperty TextBoxHeightProperty = DPUtils.Register(_ => _.TextBoxHeight);
        public double TextBoxHeight
        {
            get => (double)this.GetValue(TextBoxHeightProperty);
            set => this.SetValue(TextBoxHeightProperty, value);
        }

        public static readonly DependencyProperty TextBoxPaddingProperty = DPUtils.Register(_ => _.TextBoxPadding);
        public Thickness TextBoxPadding
        {
            get => (Thickness)this.GetValue(TextBoxPaddingProperty);
            set => this.SetValue(TextBoxPaddingProperty, value);
        }

        public static readonly DependencyProperty TextBoxBackgroundProperty = DPUtils.Register(_ => _.TextBoxBackground);
        public Brush TextBoxBackground
        {
            get => (Brush)this.GetValue(TextBoxBackgroundProperty);
            set => this.SetValue(TextBoxBackgroundProperty, value);
        }


        public static readonly DependencyProperty TextAlignmentProperty = DPUtils.Register(_ => _.TextAlignment, TextAlignment.Left);
        public TextAlignment TextAlignment
        {
            get => (TextAlignment)this.GetValue(TextAlignmentProperty);
            set => this.SetValue(TextAlignmentProperty, value);
        }

        // How the editor responds to text outside the min/max bounds (default: ErrorAndToolTip).
        public static readonly DependencyProperty OutOfBoundsResponseProperty = DPUtils.Register(_ => _.OutOfBoundsResponse, eOutOfBoundsResponse.ErrorAndToolTip, (d, e) => d.RefreshErrorState());
        public eOutOfBoundsResponse OutOfBoundsResponse
        {
            get => (eOutOfBoundsResponse)this.GetValue(OutOfBoundsResponseProperty);
            set => this.SetValue(OutOfBoundsResponseProperty, value);
        }

        // ErrorMessage: drives the error tooltip; null when there is no displayed error.
        public static readonly DependencyProperty ErrorMessageProperty = DPUtils.Register(_ => _.ErrorMessage);
        public string ErrorMessage
        {
            get => (string)this.GetValue(ErrorMessageProperty);
            private set => this.SetValue(ErrorMessageProperty, value);
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
                this.PART_TextBox.KeyDown -= this.TextBox_OnKeyDown;
            }

            // 2. Acquire new parts
            this.PART_Root = this.GetTemplateChild(nameof(PART_Root)) as Border;
            this.PART_IncreaseButton = this.GetTemplateChild(nameof(PART_IncreaseButton)) as RepeatButton;
            this.PART_DecreaseButton = this.GetTemplateChild(nameof(PART_DecreaseButton)) as RepeatButton;
            this.PART_TextBox = this.GetTemplateChild(nameof(PART_TextBox)) as TextBox;
            this.PART_DefaultLabel = this.GetTemplateChild(nameof(PART_DefaultLabel)) as FrameworkElement;
            this.PART_LabelArea = this.GetTemplateChild(nameof(PART_LabelArea)) as ContentControl;

            // 3. Seed VSM - Unfocused + NoError are the initial states (no TemplateBinding on BorderBrush)
            VisualStateManager.GoToState(this, "Unfocused", false);
            VisualStateManager.GoToState(this, "NoError", false);

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
                this.PART_TextBox.KeyDown += this.TextBox_OnKeyDown;

                // Push current value into the text box (template-before-data case).
                if (m_vm != null)
                {
                    m_blockReentrancy = true;
                    try { this.PART_TextBox.Text = m_vm.Text; }
                    finally { m_blockReentrancy = false; }
                }
            }

            this.UpdateLabelVisibility();
            this.RefreshErrorState();
        }

        // ===========[ Events ]===================================================
        private void OnGotFocus (object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, "Focused", true);

            // Forward focus to the inner TextBox when the NumericEditor itself receives
            // focus (e.g. programmatic Focus() call). When a child already has focus
            // (e.g. PART_TextBox via Tab), OriginalSource != this so we skip forwarding.
            if (ReferenceEquals(e.OriginalSource, this) && this.PART_TextBox != null)
            {
                this.PART_TextBox.Focus(FocusState.Programmatic);
                this.PART_TextBox.SelectAll();
            }
        }

        private void OnLostFocus (object sender, RoutedEventArgs e)
        {
            VisualStateManager.GoToState(this, "Unfocused", true);
            this.CommitEditedText();
        }

        private void IncreaseButton_OnClick (object sender, RoutedEventArgs e) => this.DoNudge(positive: true);
        private void DecreaseButton_OnClick (object sender, RoutedEventArgs e) => this.DoNudge(positive: false);

        private void TextBox_OnKeyDown (object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Up)
            {
                this.DoNudge(positive: true);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Down)
            {
                this.DoNudge(positive: false);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Enter)
            {
                // Commit the edit - reconcile the displayed text with the capped value, then
                // mark handled so the Enter does not bubble further.
                this.CommitEditedText();
                e.Handled = true;
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

            this.RefreshErrorState();
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

            this.RefreshErrorState();
        }

        // ===========[ Helpers ]=================================================
        private void CommitEditedText ()
        {
            if (m_vm == null || this.PART_TextBox == null)
            {
                return;
            }

            // The source value is already capped as text is typed. CommitEdit decides what to do with
            // the out of range text per OutOfBoundsResponse: FixOnCommit snaps it back to the clamped
            // value (e.g. 5500 against a max of 10 becomes 10); ErrorAndToolTip leaves it flagged.
            m_vm.CommitEdit();

            m_blockReentrancy = true;
            try
            {
                this.PART_TextBox.Text = m_vm.Text;
                this.Value = m_vm.SourceValue;
            }
            finally
            {
                m_blockReentrancy = false;
            }

            this.RefreshErrorState();
        }

        private void DoNudge (bool positive)
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
                amount = this.Nudge;
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

            this.RefreshErrorState();
        }

        private void RefreshErrorState ()
        {
            bool showError = m_vm != null && m_vm.ShouldShowError;
            this.ErrorMessage = showError ? m_vm.TextErrorMessage : null;
            VisualStateManager.GoToState(this, showError ? "HasError" : "NoError", true);
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
