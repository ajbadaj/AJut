namespace AJutShowRoomAndroid
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using AJut;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using AJut.UX.PropertyInteraction;
    using Android.Views;
    using Android.Widget;

    /// <summary>The PropertyGrid demo: a grid over a sample with most kinds of row, and a switch to make it read-only.</summary>
    public sealed class PropertyGridPage : ShowroomPage
    {
        private PropertyGrid? m_grid;
        private Switch? m_readOnly;

        public override string Title => "PropertyGrid";

        public override int LayoutId => Resource.Layout.demo_property_grid;

        public override int MarkupSourceId => Resource.Raw.demo_property_grid_source;

        // SetSingleItemSource keeps PropertyGridSample's members; Offset's type is a sub-object the grid expands, which the
        // generic doesn't reach, so it is kept here. Without it a fully trimmed build shows Offset with no X or Y.
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(PropertyGridSample.SampleOffset))]
        public override void Bind (View root, MainActivity host, BindingSet binder)
        {
            m_grid = root.FindViewById<PropertyGrid>(Resource.Id.demo_property_grid)!;
            m_grid.Editors.RegisterStandardEditors();
            m_grid.SetSingleItemSource(new PropertyGridSample());

            m_readOnly = root.FindViewById<Switch>(Resource.Id.demo_property_grid_read_only)!;
            m_readOnly.CheckedChange += this.ReadOnly_OnCheckedChange;
        }

        public override void Unbind ()
        {
            if (m_readOnly != null)
            {
                m_readOnly.CheckedChange -= this.ReadOnly_OnCheckedChange;
                m_readOnly = null;
            }

            m_grid = null;
        }

        private void ReadOnly_OnCheckedChange (object? sender, CompoundButton.CheckedChangeEventArgs e)
        {
            if (m_grid != null)
            {
                m_grid.IsReadOnly = e.IsChecked;
            }
        }

        // ===========[ Subclasses/structs ]===================================
        /// <summary>The object the page edits: one of most kinds of row, the same sample the AppKit and UIKit showrooms show</summary>
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
