namespace AJutShowRoomUIKit.UI.Controls
{
    using System;
    using System.Linq;
    using AJut.Storage;
    using AJut.UX;
    using AJut.UX.Controls;
    using UIKit;

    public sealed class FlatTreeListViewPage : ShowroomPageController
    {
        private static readonly eFlatTreeSelectionMode[] kModes = { eFlatTreeSelectionMode.None, eFlatTreeSelectionMode.Single, eFlatTreeSelectionMode.Multi };

        private readonly FlatTreeListView m_tree;
        private readonly UISegmentedControl m_modes = new UISegmentedControl("None", "Single", "Multi") { SelectedSegment = 1 };
        private readonly UILabel m_selection = new UILabel { Lines = 0, Text = "Nothing selected", TextColor = UIColor.SecondaryLabel };

        public FlatTreeListViewPage () : base("FlatTreeListView")
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
                SelectionMode = eFlatTreeSelectionMode.Single,
                IsDragDropReorderEnabled = true,
                StartItemsExpanded = true,
                Root = root,
                TranslatesAutoresizingMaskIntoConstraints = false,
            };
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear(animated);
            m_tree.SelectionChanged -= this.Tree_OnSelectionChanged;
            m_tree.SelectionChanged += this.Tree_OnSelectionChanged;
            m_modes.ValueChanged -= this.Modes_OnValueChanged;
            m_modes.ValueChanged += this.Modes_OnValueChanged;
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear(animated);
            m_tree.SelectionChanged -= this.Tree_OnSelectionChanged;
            m_modes.ValueChanged -= this.Modes_OnValueChanged;
        }

        protected override void BuildPage ()
        {
            const float kTreeHeight = 360f;

            this.AddHeading("FlatTreeListView");
            this.AddText("A tree shown as a flat list. Tap a chevron, or anywhere across a row's indent, to expand or collapse it, and tap a row to select it. Press and hold a row to lift it, then drag it to move it: slide sideways while dragging to drop it a level deeper or shallower. On the desktop stacks the control is FlatTreeListControl.");
            this.AddDemo(m_modes);
            m_tree.HeightAnchor.ConstraintEqualTo(kTreeHeight).Active = true;
            this.AddWide(m_tree);
            this.AddDemo(m_selection);
            this.AddCode(@"
var tree = new FlatTreeListView
{
    SelectionMode = eFlatTreeSelectionMode.Single,   // None, Single, or Multi
    IsDragDropReorderEnabled = true,                  // press and hold, then drag
    Root = projectRoot,
};
tree.SelectionChanged += this.Tree_OnSelectionChanged;

// The rows' content, which a row tears down when the table stops showing it
tree.RowContentFactory = item => new UILabel { Text = item.Source.ToString() };");
        }

        private void Tree_OnSelectionChanged (object sender, SelectionChange<FlatTreeItem> e)
        {
            m_selection.Text = m_tree.SelectedItems.Count == 0
                ? "Nothing selected"
                : "Selected: " + string.Join(", ", m_tree.SelectedItems.Select(_NameOf));

            static string _NameOf (FlatTreeItem item) => item.Source?.ToString() ?? "?";
        }

        private void Modes_OnValueChanged (object sender, EventArgs e)
        {
            m_tree.SelectionMode = kModes[(int)m_modes.SelectedSegment];
        }

        // ===========[ Subclasses/structs ]===================================
        // A named node for the page's tree
        private sealed class SampleTreeNode : ObservableTreeNode<SampleTreeNode>
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
    }
}
