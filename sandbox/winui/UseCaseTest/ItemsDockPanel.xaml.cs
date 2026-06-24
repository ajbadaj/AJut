namespace AJutShowRoomWinUI.UseCaseTest
{
    using System;
    using AJut.TypeManagement;
    using AJut.UX;
    using AJut.UX.Controls;
    using AJut.UX.Docking;
    using Microsoft.UI.Xaml.Controls;

    // ===========[ ItemsDockPanel ]==============================================
    // Dockable panel that shows the tool tree in a FlatTreeListControl. Raises ItemSelected
    // when the selected row changes so the host page can route the selected ToolItem to the
    // properties panel. A dockable panel is built by the manager factory, so the constructor
    // is parameterless and the tree data is pushed in later via SetTree.
    [TypeId("UseCaseItemsPanel")]
    public sealed partial class ItemsDockPanel : UserControl, IDockableDisplayElement
    {
        public ItemsDockPanel ()
        {
            this.InitializeComponent();
        }

        // ===========[ Events ]===============================================
        public event EventHandler<ToolItem> ItemSelected;

        // ===========[ Properties ]===========================================
        public DockingContentAdapterModel DockingAdapter { get; private set; }

        // Exposed so the host page's leak probe can weak-reference the inner control and
        // confirm the flat tree (and its store) collects on teardown.
        public FlatTreeListControl TreeControl => this.ItemsTree;

        // ===========[ IDockableDisplayElement ]==============================
        public void Setup (DockingContentAdapterModel adapter)
        {
            this.DockingAdapter = adapter;
            adapter.TitleContent = "Items";
        }

        // ===========[ Public Interface ]=====================================
        public void SetTree (ToolItemNode root)
        {
            this.ItemsTree.Root = root;
        }

        // Called from the host page's teardown so the tree store drops its source
        // subscriptions and we stop pointing at the source tree.
        public void Teardown ()
        {
            this.ItemsTree.Root = null;
        }

        // ===========[ Event Handlers ]=======================================
        private void OnTreeSelectionChanged (object sender, SelectionChange<FlatTreeItem> e)
        {
            ToolItem selected = null;
            if (e.Added != null && e.Added.Length > 0)
            {
                if (e.Added[e.Added.Length - 1].Source is ToolItemNode node)
                {
                    selected = node.Item;
                }
            }

            this.ItemSelected?.Invoke(this, selected);
        }
    }
}
