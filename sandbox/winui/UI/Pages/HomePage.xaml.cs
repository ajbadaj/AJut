namespace AJutShowRoomWinUI.UI.Pages
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// The landing page: what the showroom is, and a card per area that selects it in the window's navigation.
    /// </summary>
    public sealed partial class HomePage : Page
    {
        public HomePage ()
        {
            this.InitializeComponent();
            this.TasteStrip.ItemsSource = new[] { "A", "Really", "Cool", "Toggle", "Strip" };
        }

        private void NavControls_OnClick (object sender, RoutedEventArgs e) => App.Instance.MainWindow.ShowArea(typeof(ControlsPage));
        private void NavContentFlows_OnClick (object sender, RoutedEventArgs e) => App.Instance.MainWindow.ShowArea(typeof(ContentFlowsPage));
        private void NavUxUtils_OnClick (object sender, RoutedEventArgs e) => App.Instance.MainWindow.ShowArea(typeof(UxUtilsPage));
        private void NavStrategies_OnClick (object sender, RoutedEventArgs e) => App.Instance.MainWindow.ShowArea(typeof(StrategiesPage));
        private void NavDebug_OnClick (object sender, RoutedEventArgs e) => App.Instance.MainWindow.ShowArea(typeof(DebugPage));
    }
}
