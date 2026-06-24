namespace AJutShowRoomWinUI.UseCaseTest
{
    using System.Linq;
    using AJut.UX.Controls;
    using AJut.UX.Docking;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using Microsoft.UI.Xaml.Navigation;

    // ===========[ UseCaseEditorPage ]===========================================
    // The editor surface, mimicking a real tool editor: a docking experience whose panels are
    // a flat tree of tools (Items) and a property grid (Properties). Selecting a tool routes it
    // to the property grid. The page builds the whole graph on load and tears it ALL down on
    // navigate-away (manager.Dispose + panel teardown + unhook), then records weak references so
    // the landing page's probe can confirm none of it survived. This is the deterministic
    // open/close cycle that a host hits when navigating in and out of an editor.

    public sealed partial class UseCaseEditorPage : Page
    {
        // ===========[ Fields ]===============================================
        private Window m_hostWindow;
        private DockingManager m_manager;
        private ItemsDockPanel m_itemsPanel;
        private PropertiesDockPanel m_propertiesPanel;
        private ToolItemNode m_treeRoot;

        // ===========[ Construction ]=========================================
        public UseCaseEditorPage ()
        {
            this.InitializeComponent();
            this.Loaded += this.OnEditorLoaded;
        }

        // ===========[ Navigation ]===========================================
        protected override void OnNavigatedTo (NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            m_hostWindow = e.Parameter as Window;
        }

        protected override void OnNavigatedFrom (NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            this.Loaded -= this.OnEditorLoaded;
            this.TeardownEditor();
        }

        // ===========[ Setup / Teardown ]=====================================
        private void OnEditorLoaded (object sender, RoutedEventArgs e)
        {
            // One-shot: the graph is built once per navigation onto this page.
            this.Loaded -= this.OnEditorLoaded;
            if (m_manager != null || m_hostWindow == null)
            {
                return;
            }

            m_manager = new DockingManager(m_hostWindow, "usecase-editor-dock");
            m_manager.RegisterDisplayFactory<ItemsDockPanel>();
            m_manager.RegisterDisplayFactory<PropertiesDockPanel>();
            this.DockToolbar.DockingManager = m_manager;
            m_manager.RegisterMainWindowRootDockZones(this.EditorDockZone);

            DockZoneViewModel zone = m_manager.FindFirstAvailableDockZone();
            if (zone != null)
            {
                m_itemsPanel = m_manager.DockNewPanel<ItemsDockPanel>(zone);
                m_propertiesPanel = m_manager.DockNewPanel<PropertiesDockPanel>(zone);
            }

            m_treeRoot = BuildSampleTree();
            m_itemsPanel?.SetTree(m_treeRoot);

            if (m_itemsPanel != null)
            {
                m_itemsPanel.ItemSelected += this.OnItemSelected;
            }

            // Seed the property grid with the first tool so it realizes immediately.
            this.ShowItem(this.FirstTool());
        }

        private void TeardownEditor ()
        {
            if (m_manager == null)
            {
                return;
            }

            // 1. Drop the cross-panel subscription (its matching += is in OnEditorLoaded).
            if (m_itemsPanel != null)
            {
                m_itemsPanel.ItemSelected -= this.OnItemSelected;
            }

            // 2. Snapshot proof-of-realization + the inner controls before we dispose anything.
            int builtRows = CountDescendants<DockLeafLayout>(this.EditorDockZone);
            FlatTreeListControl treeControl = m_itemsPanel?.TreeControl;
            PropertyGrid gridControl = m_propertiesPanel?.GridControl;
            ToolItem sampleItem = this.FirstTool();

            // 3. Consumer half of teardown: panels release their inner state, then the manager
            //    disposes (the path the docking-overlay fix and the rest of teardown run through).
            m_itemsPanel?.Teardown();
            m_propertiesPanel?.Teardown();
            this.DockToolbar.DockingManager = null;
            m_manager.Dispose();

            // 4. Record weak refs to everything that must collect, then drop our strong refs.
            UseCaseLeakRegistry.CaptureEditorTeardown(
                this,
                m_manager,
                this.EditorDockZone,
                m_itemsPanel,
                m_propertiesPanel,
                treeControl,
                gridControl,
                m_treeRoot,
                sampleItem,
                builtRows
            );

            m_manager = null;
            m_itemsPanel = null;
            m_propertiesPanel = null;
            m_treeRoot = null;
            m_hostWindow = null;
        }

        // ===========[ Event Handlers ]=======================================
        private void OnItemSelected (object sender, ToolItem item)
        {
            this.ShowItem(item);
        }

        private void OnBackClicked (object sender, RoutedEventArgs e)
        {
            if (this.Frame != null)
            {
                // Navigate before teardown runs (Navigate triggers OnNavigatedFrom). Clear the
                // back stack so the editor page instance isn't held for a back navigation.
                this.Frame.Navigate(typeof(UseCaseLandingPage), m_hostWindow);
                this.Frame.BackStack.Clear();
            }
        }

        private void OnSaveLayoutClicked (object sender, RoutedEventArgs e)
        {
            m_manager?.SaveDockLayoutToPersistentStorage();
        }

        private void OnLoadLayoutClicked (object sender, RoutedEventArgs e)
        {
            if (m_manager != null && m_manager.ReloadDockLayoutFromPersistentStorage())
            {
                this.RewireAfterLayoutChange();
            }
        }

        // ===========[ Helpers ]==============================================

        // After a layout reload the manager rebuilds fresh panel instances, so re-acquire them,
        // re-push the tree, and re-wire selection (dropping the subscription to the old instance).
        private void RewireAfterLayoutChange ()
        {
            if (m_itemsPanel != null)
            {
                m_itemsPanel.ItemSelected -= this.OnItemSelected;
            }

            m_itemsPanel = m_manager.EnumerateDisplays().OfType<ItemsDockPanel>().FirstOrDefault();
            m_propertiesPanel = m_manager.EnumerateDisplays().OfType<PropertiesDockPanel>().FirstOrDefault();

            if (m_itemsPanel != null)
            {
                m_itemsPanel.SetTree(m_treeRoot);
                m_itemsPanel.ItemSelected -= this.OnItemSelected;
                m_itemsPanel.ItemSelected += this.OnItemSelected;
            }

            this.ShowItem(this.FirstTool());
        }

        private void ShowItem (ToolItem item)
        {
            m_propertiesPanel?.ShowItem(item);
        }

        private ToolItem FirstTool ()
        {
            return (m_treeRoot != null && m_treeRoot.Children.Count > 0)
                ? m_treeRoot.Children[0].Item
                : null;
        }

        private static ToolItemNode BuildSampleTree ()
        {
            var root = new ToolItemNode("(root)") { CanHaveChildren = true };
            for (int i = 1; i <= 6; ++i)
            {
                root.AddChildItem($"Tool {i}", new ToolItem($"Tool {i}"));
            }

            return root;
        }

        private static int CountDescendants<T> (DependencyObject root) where T : DependencyObject
        {
            if (root == null)
            {
                return 0;
            }

            int count = root is T ? 1 : 0;
            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; ++i)
            {
                count += CountDescendants<T>(VisualTreeHelper.GetChild(root, i));
            }

            return count;
        }
    }
}
