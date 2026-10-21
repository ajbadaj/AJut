namespace AJutShowRoomWinUI.UI.Pages
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// The Content Flows area: its pages are the items in the XAML, each tagged with the page type it opens.
    /// </summary>
    public sealed partial class ContentFlowsPage : Page
    {
        public ContentFlowsPage ()
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
