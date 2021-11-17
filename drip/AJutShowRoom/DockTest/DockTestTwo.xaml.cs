namespace AJutShowRoom.DockTest
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using AJut.UX.Docking;

    public partial class DockTestTwo : UserControl, IDockableDisplayElement
    {
        public DockTestTwo ()
        {
            this.InitializeComponent();
        }

        public DockingContentAdapterModel DockingAdapter { get; private set; }
        void IDockableDisplayElement.Setup (DockingContentAdapterModel adapter)
        {
            this.DockingAdapter = adapter;
            adapter.TitleContent = "Two Has A Long Name";
            adapter.TooltipContent = "A two control";
            adapter.CanClose += this.OnCanClose;
            adapter.Closed += this.OnClosed;
        }

        private void OnCanClose (object sender, IsReadyToCloseEventArgs e)
        {
            var result = MessageBox.Show("Can close?", "Can Close?", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.No)
            {
                e.IsReadyToClose = false;
            }
        }

        private void OnClosed (object sender, EventArgs e)
        {
            MessageBox.Show("Closed a DockTestTwo panel");
        }
    }
}
