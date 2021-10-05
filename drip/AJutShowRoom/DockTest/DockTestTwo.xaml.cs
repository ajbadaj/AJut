namespace AJut.TestApp.WPF.DockTest
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
            adapter.TitleContent = "Two";
            adapter.TooltipContent = "A two control";
            adapter.CanClose += this.OnCanClose;
        }

        private void OnCanClose (object sender, IsReadyToCloseEventArgs e)
        {
            var result = MessageBox.Show("Can close?", "Can Close?", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.No)
            {
                e.IsReadyToClose = false;
            }
        }

        private void OnClosing (object sender, EventArgs e)
        {
            MessageBox.Show("Closing a DockTestTwo panel");
        }
    }
}
