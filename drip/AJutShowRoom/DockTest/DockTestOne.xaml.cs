namespace AJutShowRoom.DockTest
{
    using System.Windows;
    using System.Windows.Controls;
    using AJut.TypeManagement;
    using AJut.UX.Docking;

    [TypeId("AJutShowRoom.DockTest.DockTestOne")]
    public partial class DockTestOne : UserControl, IDockableDisplayElement
    {
        public DockTestOne ()
        {
            this.InitializeComponent();
        }

        public DockingContentAdapterModel DockingAdapter { get; private set; }
        void IDockableDisplayElement.Setup (DockingContentAdapterModel adapter)
        {
            this.DockingAdapter = adapter;
            adapter.TitleContent = "One";
            adapter.TooltipContent = "A one control";
            adapter.Closed += this.OnClosed;
        }

        private void OnClosed (object sender, ClosedEventArgs e)
        {
            if (!e.IsForForcedClose)
            {
                MessageBox.Show("Closed a DockTestOne panel");
            }
        }
    }
}
