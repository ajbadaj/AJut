namespace AJutShowRoomAppKit.UI.Controls
{
    using System;
    using AJut;
    using AJut.Storage;
    using AJut.UX;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using AppKit;

    /// <summary>A named node for the tree page</summary>
    public sealed class SampleTreeNode : ObservableTreeNode<SampleTreeNode>
    {
        public SampleTreeNode (string name, params SampleTreeNode[] children)
        {
            this.Name = name;

            // CanHaveChildren defaults to false, and the tree only shows a chevron or expands a node that says it can
            this.CanHaveChildren = children.Length > 0;
            foreach (SampleTreeNode child in children)
            {
                this.InsertChild(this.Children.Count, child);
            }
        }

        public string Name { get; }

        public override string ToString () => this.Name;
    }

    public sealed class FlatTreeListControlPage : ShowroomPage
    {
        private const double kTreeHeight = 340.0;

        private readonly FlatTreeListView m_tree;
        private readonly NSTextField m_selection;

        public FlatTreeListControlPage ()
            : base("FlatTreeListControl", "A tree shown as a flat list: expand with the chevrons, a double click, or the Left and Right arrow keys. Drag rows to reorder or reparent them. On AppKit the control is FlatTreeListView.")
        {
            // The last row is a child of an expanded parent (API, in Docs), and the tree is tall enough to leave room under
            // it, so a drag past the last row can be tried at each depth: in Docs after API, or after Docs in Project
            var root = new SampleTreeNode(
                "Project",
                new SampleTreeNode("Assets", new SampleTreeNode("Icons"), new SampleTreeNode("Sounds")),
                new SampleTreeNode("Code", new SampleTreeNode("UI", new SampleTreeNode("Pages")), new SampleTreeNode("Models")),
                new SampleTreeNode("Docs", new SampleTreeNode("Guide"), new SampleTreeNode("API"))
            );

            m_tree = new FlatTreeListView
            {
                SelectionMode = eFlatTreeSelectionMode.Extended,
                IsDragDropReorderEnabled = true,
                StartItemsExpanded = true,
                Root = root,
            };
            m_tree.SelectionChanged += this.OnTreeSelectionChanged;
            m_tree.TranslatesAutoresizingMaskIntoConstraints = false;
            m_tree.HeightAnchor.ConstraintEqualTo((nfloat)kTreeHeight).Active = true;
            this.AddDemo(m_tree);

            m_selection = ShowroomStyle.Understated("Nothing selected");
            this.AddLabeledDemo("Selected", m_selection);

            this.AddCode(
@"var tree = new FlatTreeListView
{
    SelectionMode = eFlatTreeSelectionMode.Extended,
    IsDragDropReorderEnabled = true,
    Root = projectRoot,
};
tree.SelectionChanged += this.OnTreeSelectionChanged;"
            );
        }

        protected override void OnTeardown ()
        {
            m_tree.SelectionChanged -= this.OnTreeSelectionChanged;
            m_tree.Root = null;
        }

        private void OnTreeSelectionChanged (object sender, SelectionChange<FlatTreeItem> e)
        {
            m_selection.StringValue = m_tree.SelectedItems.Count == 0
                ? "Nothing selected"
                : String.Join(", ", System.Linq.Enumerable.Select(m_tree.SelectedItems, _NameOf));

            static string _NameOf (FlatTreeItem item) => item.Source?.ToString() ?? "?";
        }
    }

    /// <summary>The model the color page binds to</summary>
    public sealed class ColorSampleModel : NotifyPropertyChanged
    {
        private ColorArgb m_tint = new ColorArgb(0xFF, 0x21, 0x96, 0xF3);

        public ColorArgb Tint
        {
            get => m_tint;
            set => this.SetAndRaiseIfChanged(ref m_tint, value);
        }
    }

    public sealed class ColorEditIngressControlPage : ShowroomPage
    {
        private readonly ColorSampleModel m_model = new ColorSampleModel();
        private readonly BindingSet m_bindings = new BindingSet();

        public ColorEditIngressControlPage ()
            : base("ColorEditIngressControl", "A color swatch that opens the color panel when clicked. The color updates live while you pick, and UserEditComplete fires once when the panel closes. EditColor is a ColorArgb on AppKit, UIKit and Android.")
        {
            var swatch = new ColorEditIngressControl();
            m_bindings.BindEditColor(swatch, m_model, nameof(ColorSampleModel.Tint), this.ReadTint, this.WriteTint);
            this.AddLabeledDemo("Tint", swatch);

            NSTextField readout = ShowroomStyle.Understated(String.Empty);
            m_bindings.BindLabel(readout, m_model, nameof(ColorSampleModel.Tint), this.ReadTintText);
            this.AddLabeledDemo("Model says", readout);

            this.AddLabeledDemo("Read only", new ColorEditIngressControl { EditColor = new ColorArgb(0xFF, 0xE8, 0x11, 0x23), IsReadOnly = true });

            this.AddCode(
@"var swatch = new ColorEditIngressControl();
bindings.BindEditColor(swatch, model, nameof(Model.Tint), () => model.Tint, value => model.Tint = value);"
            );
        }

        protected override void OnTeardown ()
        {
            m_bindings.Dispose();
        }

        private ColorArgb ReadTint () => m_model.Tint;
        private void WriteTint (ColorArgb value) => m_model.Tint = value;
        private string ReadTintText () => AJutColorHelper.GetSmallestHexString(m_model.Tint.A, m_model.Tint.R, m_model.Tint.G, m_model.Tint.B);
    }
}
