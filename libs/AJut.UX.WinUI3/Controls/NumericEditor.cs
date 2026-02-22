namespace AJut.UX.Controls
{
    using System;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using DPUtils = AJut.UX.DPUtils<NumericEditor>;

    // ===========[ NumericEditor ]=============================================
    // WinUI3 numeric editor for PropertyGrid DataTemplates. Accepts and returns
    // Value as object (TwoWay-bindable to PropertyEditTarget.EditValue) and
    // preserves the source type (float, int, double, …) across edits.
    //
    // Wraps WinUI3 NumberBox which handles text entry, spin buttons, and
    // min/max enforcement. NumericEditorViewModel (shared with WPF version) is
    // used for type-detection and round-trip conversion.
    //
    // Template part:
    //   PART_NumberBox  — inner NumberBox

    [TemplatePart(Name = nameof(PART_NumberBox), Type = typeof(NumberBox))]
    public class NumericEditor : Control, INumericEditorSettings
    {
        // ===========[ Instance fields ]==========================================
        private NumberBox PART_NumberBox;
        private NumericEditorViewModel m_vm;
        private bool m_blockReentrancy;

        // ===========[ Construction ]=============================================
        public NumericEditor ()
        {
            this.DefaultStyleKey = typeof(NumericEditor);
        }

        // ===========[ INumericEditorSettings ]===================================
        // DecimalPlacesAllowed: -1 means unconstrained; NumberBox handles
        // formatting so the view model's text-cap logic is a no-op here.
        int INumericEditorSettings.DecimalPlacesAllowed => -1;

        // Translate double sentinels (±∞) back to null so the view model
        // treats them as unconstrained bounds.
        object INumericEditorSettings.Minimum
            => double.IsNegativeInfinity(this.Minimum) ? null : (object)this.Minimum;

        object INumericEditorSettings.Maximum
            => double.IsPositiveInfinity(this.Maximum) ? null : (object)this.Maximum;

        // ===========[ Dependency Properties ]====================================

        // Value: object — TwoWay binding target for PropertyEditTarget.EditValue.
        public static readonly DependencyProperty ValueProperty = DPUtils.Register(
            _ => _.Value,
            (d, e) => d.OnValueChanged(e.NewValue));
        public object Value
        {
            get => this.GetValue(ValueProperty);
            set => this.SetValue(ValueProperty, value);
        }

        // Minimum / Maximum forwarded to NumberBox (optional).
        // ±∞ sentinel means unconstrained (matches NumberBox default behaviour).
        public static readonly DependencyProperty MinimumProperty = DPUtils.Register(
            _ => _.Minimum, double.NegativeInfinity,
            (d, e) => { if (d.PART_NumberBox != null) d.PART_NumberBox.Minimum = e.NewValue; });
        public double Minimum
        {
            get => (double)this.GetValue(MinimumProperty);
            set => this.SetValue(MinimumProperty, value);
        }

        public static readonly DependencyProperty MaximumProperty = DPUtils.Register(
            _ => _.Maximum, double.PositiveInfinity,
            (d, e) => { if (d.PART_NumberBox != null) d.PART_NumberBox.Maximum = e.NewValue; });
        public double Maximum
        {
            get => (double)this.GetValue(MaximumProperty);
            set => this.SetValue(MaximumProperty, value);
        }

        // SmallChange: step for spin buttons / arrow keys (default 1).
        public static readonly DependencyProperty SmallChangeProperty = DPUtils.Register(
            _ => _.SmallChange, 1.0,
            (d, e) => { if (d.PART_NumberBox != null) d.PART_NumberBox.SmallChange = e.NewValue; });
        public double SmallChange
        {
            get => (double)this.GetValue(SmallChangeProperty);
            set => this.SetValue(SmallChangeProperty, value);
        }

        // ===========[ Template application ]====================================
        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            if (this.PART_NumberBox != null)
            {
                this.PART_NumberBox.ValueChanged -= this.NumberBox_OnValueChanged;
            }

            this.PART_NumberBox = (NumberBox)this.GetTemplateChild(nameof(PART_NumberBox));
            if (this.PART_NumberBox == null)
            {
                return;
            }

            this.PART_NumberBox.Minimum = this.Minimum;
            this.PART_NumberBox.Maximum = this.Maximum;
            this.PART_NumberBox.SmallChange = this.SmallChange;

            // Sync current Value → NumberBox (m_vm may already be initialised if
            // Value was set before the template was applied).
            if (m_vm != null)
            {
                this.PART_NumberBox.Value = ToDouble(m_vm.SourceValue);
            }

            this.PART_NumberBox.ValueChanged += this.NumberBox_OnValueChanged;
        }

        // ===========[ Property change handlers ]================================
        private void OnValueChanged (object newValue)
        {
            if (m_blockReentrancy)
            {
                return;
            }

            // Rebuild the view model so type detection runs on the new value.
            m_vm = new NumericEditorViewModel(this, newValue ?? 0.0);

            if (this.PART_NumberBox == null)
            {
                return;
            }

            m_blockReentrancy = true;
            try
            {
                this.PART_NumberBox.Value = ToDouble(m_vm.SourceValue);
            }
            finally
            {
                m_blockReentrancy = false;
            }
        }

        private void NumberBox_OnValueChanged (NumberBox sender, NumberBoxValueChangedEventArgs e)
        {
            if (m_blockReentrancy || double.IsNaN(e.NewValue))
            {
                return;
            }

            // m_vm may be null if the template was applied before Value was ever set.
            if (m_vm == null)
            {
                m_vm = new NumericEditorViewModel(this, e.NewValue);
            }

            m_blockReentrancy = true;
            try
            {
                // Update view model — it converts the double back to the source type.
                m_vm.SourceValue = e.NewValue;
                this.Value = m_vm.SourceValue;
            }
            finally
            {
                m_blockReentrancy = false;
            }
        }

        // ===========[ Helpers ]=================================================
        private static double ToDouble (object value)
        {
            if (value == null) return 0.0;
            try { return Convert.ToDouble(value); }
            catch { return 0.0; }
        }
    }
}
