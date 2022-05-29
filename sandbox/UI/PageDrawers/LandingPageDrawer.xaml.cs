namespace TheAJutShowRoom.UI.PageDrawers
{
    using System.Windows;
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

        private void NavToControls_OnClick (object sender, RoutedEventArgs e)
        {
            m_adapter.Navigator.GenerateAndPushDisplay<ControlsOverviewPage>();
        }

        private void NavContentFlows_OnClick (object sender, RoutedEventArgs e)
        {
            m_adapter?.Navigator.GenerateAndPushDisplay<ContentFlowsOverviewPage>();
        }

        private void NavUxUtils_OnClick (object sender, RoutedEventArgs e)
        {
            m_adapter?.Navigator.GenerateAndPushDisplay<UxUtilsOverviewPage>();
        }

        private void NavUxStrats_OnClick (object sender, RoutedEventArgs e)
        {
            m_adapter?.Navigator.GenerateAndPushDisplay<UxStrategiesOverviewPage>();
        }
    }
}
