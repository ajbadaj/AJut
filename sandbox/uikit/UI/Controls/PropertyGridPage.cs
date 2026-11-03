namespace AJutShowRoomUIKit.UI.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using AJut;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using AJut.UX.PropertyInteraction;
    using UIKit;

    public sealed class PropertyGridPage : ShowroomPageController
    {
        private readonly PropertyGrid m_grid;
        private readonly UISwitch m_readOnly = new UISwitch();

        // SetSingleItemSource keeps PropertyGridSample's members; Offset's type is a sub-object the grid expands, which the
        // generic doesn't reach, so it is kept here. Without it a fully trimmed build shows Offset with no X or Y.
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(PropertyGridSample.SampleOffset))]
        public PropertyGridPage () : base("PropertyGrid")
        {
            m_grid = new PropertyGrid { TranslatesAutoresizingMaskIntoConstraints = false };
            m_grid.Editors.RegisterStandardEditors();
            m_grid.SetSingleItemSource(new PropertyGridSample());
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear(animated);
            m_readOnly.ValueChanged -= this.ReadOnly_OnValueChanged;
            m_readOnly.ValueChanged += this.ReadOnly_OnValueChanged;
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear(animated);
            m_readOnly.ValueChanged -= this.ReadOnly_OnValueChanged;
        }

        protected override void BuildPage ()
        {
            const float kGridHeight = 520f;

            this.AddHeading("PropertyGrid");
            this.AddText("Give it an object and it shows a row per public property, with an editor picked by the property's type or a [PGEditor] key. On a phone each property's name sits above its editor; a switch or a color swatch sits at the end of the name's line. Sub-objects expand, lists add, remove, and drag to reorder, and the PG* attributes label, group, order, hide, and show rows. Press and hold a changed value's row for Set to default.");

            var readOnlyLabel = new UILabel { Text = "Read-only grid" };
            this.AddDemoRow(readOnlyLabel, m_readOnly);

            m_grid.HeightAnchor.ConstraintEqualTo(kGridHeight).Active = true;
            this.AddWide(m_grid);
            this.AddCode(@"
var grid = new PropertyGrid();
grid.Editors.RegisterStandardEditors();
grid.Editors.Register(""Percent"", MakePercentSlider);                                   // under the name
grid.Editors.Register(""Flag"", MakeFlagSwitch, ePropertyEditorPlacement.BesideLabel);    // at the end of its line

// The generic keeps the shown type's members in an app that trims; keep a sub-object's
// type with [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(...))]
grid.SetSingleItemSource(mySettings);

// Nothing in the grid can be edited, added, removed, or reordered
grid.IsReadOnly = true;");
        }

        private void ReadOnly_OnValueChanged (object sender, EventArgs e)
        {
            m_grid.IsReadOnly = m_readOnly.On;
        }

        // ===========[ Subclasses/structs ]===================================
        /// <summary>The object the page edits: one of most kinds of row, the same sample the AppKit showroom shows</summary>
        /// <remarks>Its members are kept for a trimmed build by SetSingleItemSource, which the page shows it through</remarks>
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

            public enum eSampleQuality
            {
                Draft,
                Good,
                Best,
            }

            /// <summary>A sub-object, shown as an expandable row whose children are its properties</summary>
            /// <remarks>Its members are kept for a trimmed build by the [DynamicDependency] on the page that shows it</remarks>
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
        }
    }
}
