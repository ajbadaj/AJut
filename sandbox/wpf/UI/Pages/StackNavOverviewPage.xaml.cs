namespace TheAJutShowRoom.UI.Pages
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
    using AJut.UX;

    public partial class StackNavOverviewPage : UserControl, IStackNavDisplayControl
    {
        public StackNavOverviewPage()
        {
            this.InitializeComponent();
        }

        public void Setup (StackNavAdapter adapter)
        {
            this.PageNav = adapter;
            this.PageNav.Title = "Stack Nav: Overview";
        }

        public StackNavAdapter? PageNav { get; private set; }
    }
}
