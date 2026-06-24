namespace AJutShowRoomWinUI.UseCaseTest
{
    using AJut.TypeManagement;
    using AJut.UX.Controls;
    using AJut.UX.Docking;
    using Microsoft.UI.Xaml.Controls;

    // ===========[ PropertiesDockPanel ]=========================================
    // Dockable panel that edits the currently selected ToolItem in a PropertyGrid. The host
    // page calls ShowItem when the items panel raises a selection change.
    [TypeId("UseCasePropertiesPanel")]
    public sealed partial class PropertiesDockPanel : UserControl, IDockableDisplayElement
    {
        public PropertiesDockPanel ()
        {
            this.InitializeComponent();
        }

        // ===========[ Properties ]===========================================
        public DockingContentAdapterModel DockingAdapter { get; private set; }

        // Exposed so the host page's leak probe can weak-reference the grid and confirm it
        // collects on teardown.
        public PropertyGrid GridControl => this.PropertyEditor;

        // ===========[ IDockableDisplayElement ]==============================
        public void Setup (DockingContentAdapterModel adapter)
        {
            this.DockingAdapter = adapter;
            adapter.TitleContent = "Properties";
        }

        // ===========[ Public Interface ]=====================================
        public void ShowItem (ToolItem item)
        {
            this.PropertyEditor.SingleItemSource = item;
        }

        // Called from the host page's teardown - drop the edited source and dispose the grid
        // (PropertyGrid is IDisposable; this is the consumer's half of the teardown contract).
        public void Teardown ()
        {
            this.PropertyEditor.SingleItemSource = null;
            this.PropertyEditor.Dispose();
        }
    }
}
