namespace TheAJutShowRoom.UI.Pages
{
    using System.Windows.Controls;
    using AJut.UX;

    public partial class LandingPage : UserControl, IStackNavDisplayControl
    {
        private StackNavAdapter? m_adapter;

        public LandingPage ()
        {
            this.InitializeComponent();
        }

        public void Setup (StackNavAdapter adapter)
        {
            m_adapter = adapter;
            m_adapter.Title = "Welcome to the AJut Showroom!";
            m_adapter.DrawerHeading = "Showroom Home";
            m_adapter.DrawerDisplay = new PageDrawers.LandingPageDrawer(adapter);
        }
    }
}
