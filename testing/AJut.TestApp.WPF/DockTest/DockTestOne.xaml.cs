namespace AJut.TestApp.WPF.DockTest
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using AJut.Application.Docking;

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

        private void OnClosed (object sender, EventArgs e)
        {
            MessageBox.Show("Closed a DockTestOne panel");
        }
    }
}
