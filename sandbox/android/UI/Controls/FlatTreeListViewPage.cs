namespace AJutShowRoomAndroid
{
    using System.Linq;
    using AJut.Storage;
    using AJut.UX;
    using AJut.UX.CodeBinding;
    using AJut.UX.Controls;
    using Android.Views;
    using Android.Widget;

    /// <summary>
    /// The FlatTreeListView demo: a tree to expand, select, and drag rows in, a strip to pick how many rows a tap selects,
    /// a line saying what is selected, and a disabled tree.
    /// </summary>
    public sealed class FlatTreeListViewPage : ShowroomPage
    {
        private FlatTreeListView? m_tree;
        private ToggleStrip? m_modes;
        private TextView? m_selection;

        public override string Title => "FlatTreeListView";

        public override int LayoutId => Resource.Layout.demo_flat_tree_list_view;

        public override int MarkupSourceId => Resource.Raw.demo_flat_tree_list_view_source;

        public override void Bind (View root, MainActivity host, BindingSet binder)
        {
            m_tree = root.FindViewById<FlatTreeListView>(Resource.Id.demo_flat_tree)!;
            m_modes = root.FindViewById<ToggleStrip>(Resource.Id.demo_flat_tree_modes)!;
            m_selection = root.FindViewById<TextView>(Resource.Id.demo_flat_tree_selection)!;

            // The last row is a child of an expanded parent (API, in Docs), and the tree is tall enough to leave room under
            // it, so a drag past the last row can be tried at each depth: in Docs after API, or after Docs in Project
            m_tree.Root = new SampleTreeNode(
                "Project",
                new SampleTreeNode("Assets", new SampleTreeNode("Icons"), new SampleTreeNode("Sounds")),
                new SampleTreeNode("Code", new SampleTreeNode("UI", new SampleTreeNode("Pages")), new SampleTreeNode("Models")),
                new SampleTreeNode("Docs", new SampleTreeNode("Guide"), new SampleTreeNode("API"))
            );

            m_modes.SelectedItem = "Single";
            m_tree.SelectionChanged += this.Tree_OnSelectionChanged;
            m_modes.SelectionChanged += this.Modes_OnSelectionChanged;

            var disabled = root.FindViewById<FlatTreeListView>(Resource.Id.demo_flat_tree_disabled)!;
            disabled.Root = new SampleTreeNode("Locked", new SampleTreeNode("First"), new SampleTreeNode("Second"));
        }

        public override void Unbind ()
        {
            if (m_tree != null)
            {
                m_tree.SelectionChanged -= this.Tree_OnSelectionChanged;
                m_tree = null;
            }

            if (m_modes != null)
            {
                m_modes.SelectionChanged -= this.Modes_OnSelectionChanged;
                m_modes = null;
            }

            m_selection = null;
        }

        private void Tree_OnSelectionChanged (object? sender, SelectionChange<FlatTreeItem> e)
        {
            if ((m_tree == null) || (m_selection == null))
            {
                return;
            }

            m_selection.Text = m_tree.SelectedItems.Count == 0
                ? "Nothing selected"
                : "Selected: " + string.Join(", ", m_tree.SelectedItems.Select(_NameOf));

            static string _NameOf (FlatTreeItem item) => item.Source?.ToString() ?? "?";
        }

        private void Modes_OnSelectionChanged (object? sender, ToggleStripSelectionChange e)
        {
            if ((m_tree != null) && (m_modes?.SelectedItem is string mode))
            {
                m_tree.SelectionMode = mode switch
                {
                    "None" => eFlatTreeSelectionMode.None,
                    "Multi" => eFlatTreeSelectionMode.Multi,
                    _ => eFlatTreeSelectionMode.Single,
                };
            }
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
