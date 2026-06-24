namespace AJutShowRoomWinUI.UseCaseTest
{
    using AJut;
    using AJut.Storage;
    using AJut.UX.PropertyInteraction;

    // ===========[ ToolItem ]====================================================
    // Stand-in "tool" with a handful of editable properties. When its tree node is
    // selected in the items panel, this is what the properties panel edits. Implements
    // NotifyPropertyChanged so edits round-trip live, matching a real editor model.
    public class ToolItem : NotifyPropertyChanged
    {
        private string m_name;
        private float m_width = 100f;
        private float m_height = 100f;
        private string m_notes = string.Empty;

        public ToolItem (string name)
        {
            m_name = name;
        }

        [PGEditor("Text")]
        public string Name
        {
            get => m_name;
            set => this.SetAndRaiseIfChanged(ref m_name, value);
        }

        [PGEditor("Single")]
        public float Width
        {
            get => m_width;
            set => this.SetAndRaiseIfChanged(ref m_width, value);
        }

        [PGEditor("Single")]
        public float Height
        {
            get => m_height;
            set => this.SetAndRaiseIfChanged(ref m_height, value);
        }

        [PGEditor("Text")]
        public string Notes
        {
            get => m_notes;
            set => this.SetAndRaiseIfChanged(ref m_notes, value);
        }
    }

    // ===========[ ToolItemNode ]================================================
    // The tree-source node behind each row of the items flat tree. Carries a display
    // name and the ToolItem payload the properties panel edits. ObservableTreeNode gives
    // the IObservableTreeNode plumbing FlatTreeListControl needs.
    public class ToolItemNode : ObservableTreeNode<ToolItemNode>
    {
        public ToolItemNode (string name, ToolItem item = null)
        {
            this.NodeName = name;
            this.Item = item;
        }

        public string NodeName { get; }

        public ToolItem Item { get; }

        public ToolItemNode AddChildItem (string name, ToolItem item = null)
        {
            var child = new ToolItemNode(name, item);
            this.InsertChild(this.Children.Count, child);
            return child;
        }
    }
}
