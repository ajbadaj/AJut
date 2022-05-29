namespace TheAJutShowRoom.UI.Pages
{
    using System.Windows;
    using System.Windows.Controls;
    using AJut.UX;
    using DPUtils = AJut.UX.DPUtils<LandingPage>;

    public partial class LandingPage : UserControl, IStackNavDisplayControl
    {
        private StackNavAdapter? m_adapter;

        public LandingPage ()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty SelectedTabIndexProperty = DPUtils.Register(_ => _.SelectedTabIndex);
        public int SelectedTabIndex
        {
            get => (int)this.GetValue(SelectedTabIndexProperty);
            set => this.SetValue(SelectedTabIndexProperty, value);
        }

        public void Setup (StackNavAdapter adapter)
        {
            m_adapter = adapter;
            m_adapter.Title = "Welcome to the AJut Showroom!";
            m_adapter.DrawerHeading = "Showroom Home";
            m_adapter.DrawerDisplay = new PageDrawers.LandingPageDrawer(adapter);
        }
        
        void IStackNavDisplayControl.SetState (object state)
        {
            if (state is LandingPageUIState stateCasted)
            {
                this.SelectedTabIndex = stateCasted.SelectedTabIndex;
            }
        }

        object IStackNavDisplayControl.GenerateState () => new LandingPageUIState { SelectedTabIndex = this.SelectedTabIndex };

        private void NavControls_OnClick (object sender, RoutedEventArgs e)
        {
            m_adapter?.Navigator.GenerateAndPushDisplay<ControlsOverviewPage>();
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

        private class LandingPageUIState
        {
            public int SelectedTabIndex { get; set; }
        }
    }
}
