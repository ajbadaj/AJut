namespace AJutShowRoomAppKit.UI.Controls
{
    using System;
    using AJut;
    using AJut.UX;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using AppKit;

    /// <summary>The model the NumericEditor page binds its editors to</summary>
    public sealed class NumericSampleModel : NotifyPropertyChanged
    {
        private int m_count = 3;
        private double m_opacity = 0.75;
        private float m_temperature = 21.5f;

        public int Count
        {
            get => m_count;
            set => this.SetAndRaiseIfChanged(ref m_count, value);
        }

        public double Opacity
        {
            get => m_opacity;
            set => this.SetAndRaiseIfChanged(ref m_opacity, value);
        }

        public float Temperature
        {
            get => m_temperature;
            set => this.SetAndRaiseIfChanged(ref m_temperature, value);
        }
    }

    public sealed class NumericEditorPage : ShowroomPage
    {
        private const double kEditorWidth = 140.0;

        private readonly NumericSampleModel m_model = new NumericSampleModel();
        private readonly BindingSet m_bindings = new BindingSet();
        private readonly NumericEditor m_enforced;
        private readonly NSTextField m_enforcedReadout = ShowroomStyle.Understated(String.Empty);

        public NumericEditorPage ()
            : base("NumericEditor", "A number field that keeps its value's type: bind an int and it edits an int. Up and Down arrows, or the buttons (hold to repeat), nudge it; Command or Control nudges big, Shift nudges small. Out-of-range text either shows an error or snaps to the bound on commit.")
        {
            NumericEditor count = MakeEditor();
            count.Minimum = 0;
            count.Maximum = 10;
            m_bindings.BindValue(count, m_model, nameof(NumericSampleModel.Count), this.ReadCount, this.WriteCount);
            this.AddLabeledDemo("int, 0 to 10 (error)", count);
            this.AddReadout(nameof(NumericSampleModel.Count), this.ReadCountText);

            NumericEditor opacity = MakeEditor();
            opacity.Minimum = 0.0;
            opacity.Maximum = 1.0;
            opacity.Nudge = 0.1;
            opacity.BigNudge = 0.25;
            opacity.SmallNudge = 0.01;
            opacity.DecimalPlacesAllowed = 2;
            opacity.OutOfBoundsResponse = eOutOfBoundsResponse.FixOnCommit;
            opacity.Label = "Opacity";
            m_bindings.BindValue(opacity, m_model, nameof(NumericSampleModel.Opacity), this.ReadOpacity, this.WriteOpacity);
            this.AddLabeledDemo("double, 0 to 1, 2 places (snap)", opacity);
            this.AddReadout(nameof(NumericSampleModel.Opacity), this.ReadOpacityText);

            NumericEditor temperature = MakeEditor();
            m_bindings.BindValue(temperature, m_model, nameof(NumericSampleModel.Temperature), this.ReadTemperature, this.WriteTemperature);
            this.AddLabeledDemo("float, unbounded", temperature);
            this.AddReadout(nameof(NumericSampleModel.Temperature), this.ReadTemperatureText);

            // Given as text, kept as an int: what a value read from a file or a setting arrives as
            m_enforced = MakeEditor();
            m_enforced.EnforceNumericType = typeof(int);
            m_enforced.Value = "42";
            m_enforced.ValueChanged += this.OnEnforcedValueChanged;
            this.AddLabeledDemo("EnforceNumericType int, Value \"42\"", m_enforced);
            this.AddLabeledDemo("Value is", m_enforcedReadout);
            this.RefreshEnforcedReadout();

            this.AddCode(
@"var count = new NumericEditor { Minimum = 0, Maximum = 10 };
bindings.BindValue(
    count, model, nameof(Model.Count),
    () => model.Count, value => model.Count = (int)value
);"
            );
        }

        protected override void OnTeardown ()
        {
            m_enforced.ValueChanged -= this.OnEnforcedValueChanged;
            m_bindings.Dispose();
        }

        private void OnEnforcedValueChanged (object sender, EventArgs e) => this.RefreshEnforcedReadout();

        private void RefreshEnforcedReadout ()
        {
            object value = m_enforced.Value;
            m_enforcedReadout.StringValue = value == null ? "(null)" : $"{value} ({value.GetType().Name})";
        }

        private void AddReadout (string propertyName, Func<string> read)
        {
            NSTextField readout = ShowroomStyle.Understated(String.Empty);
            m_bindings.BindLabel(readout, m_model, propertyName, read);
            this.AddLabeledDemo("Model says", readout);
        }

        private object ReadCount () => m_model.Count;
        private void WriteCount (object value) => m_model.Count = (int)value;
        private string ReadCountText () => m_model.Count.ToString();
        private object ReadOpacity () => m_model.Opacity;
        private void WriteOpacity (object value) => m_model.Opacity = (double)value;
        private string ReadOpacityText () => m_model.Opacity.ToString();
        private object ReadTemperature () => m_model.Temperature;
        private void WriteTemperature (object value) => m_model.Temperature = (float)value;
        private string ReadTemperatureText () => m_model.Temperature.ToString();

        private static NumericEditor MakeEditor ()
        {
            var editor = new NumericEditor();
            editor.TranslatesAutoresizingMaskIntoConstraints = false;
            editor.WidthAnchor.ConstraintEqualTo((nfloat)kEditorWidth).Active = true;
            return editor;
        }
    }
}
