namespace AJutShowRoomWinUI.UI.Pages
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// The Controls area: its pages are the items in the XAML, each tagged with the page type it opens.
    /// </summary>
    public sealed partial class ControlsPage : Page
    {
        public ControlsPage ()
        {
            this.InitializeComponent();
        }

        private void AreaNav_OnLoaded (object sender, RoutedEventArgs e)
        {
            ShowroomNavigation.SelectFirstPage(this.AreaNav);
        }

        private void AreaNav_OnSelectionChanged (NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            ShowroomNavigation.NavigateToTaggedPage(this.AreaFrame, args.SelectedItem);
        }
    }
}
