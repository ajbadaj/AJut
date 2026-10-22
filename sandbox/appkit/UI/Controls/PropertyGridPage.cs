namespace AJutShowRoomAppKit.UI.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using AJut;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using AJut.UX.PropertyInteraction;
    using AppKit;

    public enum eSampleQuality
    {
        Draft,
        Good,
        Best,
    }

    /// <summary>A sub-object, shown as an expandable row whose children are its properties</summary>
    /// <remarks>Its members are kept for the property grid's reflection in a trimmed build, as the grid's warning asks</remarks>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public sealed class SampleOffset : NotifyPropertyChanged
    {
        private double m_x;
        private double m_y;

        public double X
        {
            get => m_x;
            set => this.SetAndRaiseIfChanged(ref m_x, value);
        }

        public double Y
        {
            get => m_y;
            set => this.SetAndRaiseIfChanged(ref m_y, value);
        }
    }

    /// <summary>The object the PropertyGrid page edits: one of most kinds of row</summary>
    /// <remarks>Its members are kept for the property grid's reflection in a trimmed build, as the grid's warning asks</remarks>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public sealed class PropertyGridSample : NotifyPropertyChanged
    {
        private string m_name = "Sample";
        private bool m_isVisible = true;
        private int m_count = 3;
        private float? m_scale;
        private eSampleQuality m_quality = eSampleQuality.Good;
        private bool m_showAdvanced;
        private int m_clicks;

        [PGLabel("Name", "What this sample is called")]
        [PGToolTip("Any text at all")]
        public string Name
        {
            get => m_name;
            set => this.SetAndRaiseIfChanged(ref m_name, value);
        }

        public bool IsVisible
        {
            get => m_isVisible;
            set => this.SetAndRaiseIfChanged(ref m_isVisible, value);
        }

        [PGOverrideDefault(3)]
        public int Count
        {
            get => m_count;
            set => this.SetAndRaiseIfChanged(ref m_count, value);
        }

        public float? Scale
        {
            get => m_scale;
            set => this.SetAndRaiseIfChanged(ref m_scale, value);
        }

        public eSampleQuality Quality
        {
            get => m_quality;
            set => this.SetAndRaiseIfChanged(ref m_quality, value);
        }

        public SampleOffset Offset { get; set; } = new SampleOffset();

        public ColorArgb Tint { get; set; } = new ColorArgb(0xFF, 0x21, 0x96, 0xF3);

        [PGList]
        public List<int> Numbers { get; set; } = new List<int> { 1, 2, 3 };

        [PGGroup("Advanced")]
        public bool ShowAdvanced
        {
            get => m_showAdvanced;
            set => this.SetAndRaiseIfChanged(ref m_showAdvanced, value);
        }

        [PGGroup("Advanced")]
        [PGShowIf(nameof(ShowAdvanced))]
        public string Notes { get; set; } = "Only shown when ShowAdvanced is on";

        [PGShowReadonly]
        public int Clicks => m_clicks;

        [PGButton("Click me")]
        public void Click ()
        {
            ++m_clicks;
            this.RaisePropertiesChanged(nameof(Clicks));
        }
    }

    public sealed class PropertyGridPage : ShowroomPage
    {
        private const double kGridHeight = 420.0;

        private readonly PropertyGrid m_grid;
        private readonly NSButton m_readOnlyCheck;

        [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Every type the grid shows here carries DynamicallyAccessedMembers(All), which is what the grid's warning asks for.")]
        public PropertyGridPage ()
            : base("PropertyGrid", "Give it an object and it shows a row per public property, with an editor picked by the property's type or a [PGEditor] key. Sub-objects expand, lists can add, remove and drag to reorder, and the PG* attributes label, group, order, hide and show rows.")
        {
            m_readOnlyCheck = new NSButton { Title = "Read-only grid" };
            m_readOnlyCheck.SetButtonType(NSButtonType.Switch);
            m_readOnlyCheck.Activated += this.OnReadOnlyActivated;
            this.AddDemo(m_readOnlyCheck, eFillStackCrossAlignment.Start);

            m_grid = new PropertyGrid { LabelColumnWidth = 180.0 };
            m_grid.Editors.RegisterStandardEditors();
            m_grid.SingleItemSource = new PropertyGridSample();
            m_grid.TranslatesAutoresizingMaskIntoConstraints = false;
            m_grid.HeightAnchor.ConstraintEqualTo((nfloat)kGridHeight).Active = true;
            this.AddDemo(m_grid);

            this.AddCode(
@"var grid = new PropertyGrid { LabelColumnWidth = 180 };
grid.Editors.RegisterStandardEditors();
grid.Editors.Register(""Percent"", (target, bindings, isReadOnly) => MakePercentSlider(target, bindings, isReadOnly));
grid.SingleItemSource = mySettings;

// Nothing in the grid can be edited, added, removed or reordered
grid.IsReadOnly = true;"
            );
        }

        protected override void OnTeardown ()
        {
            m_readOnlyCheck.Activated -= this.OnReadOnlyActivated;
            m_grid.SingleItemSource = null;
        }

        private void OnReadOnlyActivated (object sender, EventArgs e)
        {
            m_grid.IsReadOnly = m_readOnlyCheck.State == NSCellStateValue.On;
        }
    }
}
