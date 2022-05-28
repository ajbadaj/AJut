namespace TheAJutShowRoom.UI.PageDrawers
{
    using System.Windows.Controls;
    using AJut.UX;
    using TheAJutShowRoom.UI.Pages;

    public partial class LandingPageDrawer : UserControl
    {
        StackNavAdapter m_adapter;
        public LandingPageDrawer (StackNavAdapter nav)
        {
            m_adapter = nav;
            this.InitializeComponent();
        }

        private void NavToControls_OnClick (object sender, System.Windows.RoutedEventArgs e)
        {
            m_adapter.Navigator.GenerateAndPushDisplay<ControlsOverviewPage>();
        }
    }
}
