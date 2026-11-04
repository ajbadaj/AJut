namespace AJutShowRoomAppKit.UI.Controls
{
    using System;
    using System.ComponentModel;
    using AJut;
    using AJut.UX;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using AppKit;

    /// <summary>The enums the enum control pages show</summary>
    public enum eSampleShape
    {
        Circle,
        Square,
        Triangle,

        /// <summary>Hidden from the controls by [ExcludeFromSelection]</summary>
        [ExcludeFromSelection]
        Secret,
    }

    [Flags]
    public enum eSampleSides
    {
        None = 0,
        Top = 1,
        Right = 2,
        Bottom = 4,
        Left = 8,
    }

    /// <summary>A plain model the enum pages bind their controls to, so a pick in one control shows in the others</summary>
    public sealed class EnumSampleModel : NotifyPropertyChanged
    {
        private eSampleShape m_shape = eSampleShape.Square;
        private eSampleSides m_sides = eSampleSides.Top | eSampleSides.Left;

        public eSampleShape Shape
        {
            get => m_shape;
            set => this.SetAndRaiseIfChanged(ref m_shape, value);
        }

        public eSampleSides Sides
        {
            get => m_sides;
            set => this.SetAndRaiseIfChanged(ref m_sides, value);
        }
    }

    public sealed class EnumComboBoxPage : ShowroomPage
    {
        private readonly EnumSampleModel m_model = new EnumSampleModel();
        private readonly BindingSet m_bindings = new BindingSet();

        public EnumComboBoxPage ()
            : base("EnumComboBox", "A pop-up button of an enum's values. Set SelectedItem to any value and the list fills with that enum's values. Values marked [Browsable(false)] or [ExcludeFromSelection] are left out: Secret is not listed here.")
        {
            var comboBox = new EnumComboBox();
            m_bindings.BindSelectedItem(comboBox, m_model, nameof(EnumSampleModel.Shape), this.ReadShape, this.WriteShape);
            this.AddLabeledDemo("Shape", comboBox);

            var mirror = new EnumComboBox();
            m_bindings.BindSelectedItem(mirror, m_model, nameof(EnumSampleModel.Shape), this.ReadShape, this.WriteShape);
            this.AddLabeledDemo("Bound to the same model", mirror);

            NSTextField readout = ShowroomStyle.Understated(String.Empty);
            m_bindings.BindLabel(readout, m_model, nameof(EnumSampleModel.Shape), this.ReadShapeText);
            this.AddLabeledDemo("Model says", readout);

            this.AddCode(
@"var shapes = new EnumComboBox();
bindings.BindSelectedItem(
    shapes, model, nameof(Model.Shape),
    () => model.Shape, value => model.Shape = (eShape)value
);"
            );
        }

        protected override void OnTeardown ()
        {
            m_bindings.Dispose();
        }

        private object ReadShape () => m_model.Shape;
        private void WriteShape (object value) => m_model.Shape = (eSampleShape)value;
        private string ReadShapeText () => m_model.Shape.ToString();
    }

    public sealed class EnumToggleStripPage : ShowroomPage
    {
        private readonly EnumSampleModel m_model = new EnumSampleModel();
        private readonly BindingSet m_bindings = new BindingSet();

        public EnumToggleStripPage ()
            : base("EnumToggleStrip", "A ToggleStrip of an enum's values. With AllowMultiSelect on a [Flags] enum, SelectedItem is every selected flag combined, and setting it selects each flag it holds.")
        {
            var shapes = new EnumToggleStrip();
            m_bindings.BindSelectedItem(shapes, m_model, nameof(EnumSampleModel.Shape), this.ReadShape, this.WriteShape);
            this.AddLabeledDemo("Shape", shapes);

            NSTextField shapeReadout = ShowroomStyle.Understated(String.Empty);
            m_bindings.BindLabel(shapeReadout, m_model, nameof(EnumSampleModel.Shape), this.ReadShapeText);
            this.AddLabeledDemo("Model says", shapeReadout);

            var sides = new EnumToggleStrip { AllowMultiSelect = true, AllowNoSelection = true };
            m_bindings.BindSelectedItem(sides, m_model, nameof(EnumSampleModel.Sides), this.ReadSides, this.WriteSides);
            this.AddLabeledDemo("Sides ([Flags])", sides);

            NSTextField sidesReadout = ShowroomStyle.Understated(String.Empty);
            m_bindings.BindLabel(sidesReadout, m_model, nameof(EnumSampleModel.Sides), this.ReadSidesText);
            this.AddLabeledDemo("Model says", sidesReadout);

            this.AddCode(
@"var sides = new EnumToggleStrip { AllowMultiSelect = true, AllowNoSelection = true };
bindings.BindSelectedItem(
    sides, model, nameof(Model.Sides),
    () => model.Sides, value => model.Sides = (eSides)value
);"
            );
        }

        protected override void OnTeardown ()
        {
            m_bindings.Dispose();
        }

        private object ReadShape () => m_model.Shape;
        private void WriteShape (object value) => m_model.Shape = (eSampleShape)value;
        private string ReadShapeText () => m_model.Shape.ToString();
        private object ReadSides () => m_model.Sides;
        private void WriteSides (object value) => m_model.Sides = (eSampleSides)value;
        private string ReadSidesText () => m_model.Sides.ToString();
    }
}
