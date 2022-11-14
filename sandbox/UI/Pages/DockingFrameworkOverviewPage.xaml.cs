namespace TheAJutShowRoom.UI.Pages
{
    using System.Windows.Controls;
    using AJut.UX;

    public partial class DockingFrameworkOverviewPage : UserControl, IStackNavDisplayControl
    {
        public DockingFrameworkOverviewPage ()
        {
            this.InitializeComponent();
        }

        public void Setup (StackNavAdapter adapter)
        {
            this.PageNav = adapter;
            this.PageNav.Title = "Docking Framework";
        }

        public StackNavAdapter? PageNav { get; private set; }
    }
}
