namespace TheAJutShowRoom.UI.Pages.DockExampleControls
{
    using AJut.TypeManagement;
    using AJut.UX.Docking;
    using System.Windows;
    using System.Windows.Controls;

    [TypeId("CommandSender")]
    public partial class CommandSender : UserControl, IDockableDisplayElement
    {
        public CommandSender()
        {
            this.InitializeComponent();
        }

        public DockingContentAdapterModel? DockingAdapter { get; private set; }

        public void Setup (DockingContentAdapterModel adapter)
        {
            this.DockingAdapter = adapter;
            this.DockingAdapter.TitleContent = "Command Route Example";
            this.DockingAdapter.HideDontClose = true;
        }

        private void SaveLayout_OnClick(object sender, RoutedEventArgs e)
        {
            this.DockingAdapter.DockingOwner.SaveDockLayoutToPersistentStorage();
        }

        private void LoadLayout_OnClick(object sender, RoutedEventArgs e)
        {
            this.DockingAdapter.DockingOwner.ReloadDockLayoutFromPersistentStorage();
        }
    }
}
