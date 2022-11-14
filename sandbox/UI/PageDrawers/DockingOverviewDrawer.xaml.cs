namespace TheAJutShowRoom.UI.PageDrawers
{
    using System.Windows;
    using System.Windows.Controls;
    using AJut.UX.Docking;

    public partial class DockingOverviewDrawer : UserControl
    {
        private DockingManager m_docking;
        public DockingOverviewDrawer (DockingManager dockingManager)
        {
            m_docking = dockingManager;
            this.InitializeComponent();
        }

        private void AddColorPanel_OnClick (object sender, RoutedEventArgs e)
        {
            DockZoneViewModel rootAdd = m_docking.FindFirstAvailableDockZone();
            if (rootAdd == null)
            {
                return;
            }

            rootAdd.AddDockedContent(m_docking.BuildNewDisplayElement<Pages.DockExampleControls.ColorController>());
        }
    }
}
