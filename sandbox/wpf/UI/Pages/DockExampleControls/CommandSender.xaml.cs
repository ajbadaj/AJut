namespace TheAJutShowRoom.UI.Pages.DockExampleControls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Documents;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Navigation;
    using System.Windows.Shapes;
    using AJut.UX.Docking;

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
        }
    }
}
