namespace AJutShowRoomWinUI
{
    using System;
    using AJut.UX;
    using AJut.UX.Controls;
    using AJut.UX.Theming;
    using AJutShowRoomWinUI.UI;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// The showroom window: AJut's chrome around a NavigationView of the showroom's areas. Everything shown lives in
    /// a page under UI; this is the shell and nothing else.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private const int kStartupWidth = 1200;
        private const int kStartupHeight = 850;

        public MainWindow (WindowManager manager, AppThemeManager themeManager)
        {
            this.Application = App.Instance;
            manager.Setup(this);
            themeManager.ApplyTheme(this);
            this.AppWindow.SetIcon("Assets/app.ico");
            this.InitializeComponent();
            this.Root.SetupFor(this);
            // AppWindow sizes are physical pixels, so scale the logical size by the DPI or the window comes up small on a high DPI screen.
            double dpiScale = this.DetermineActiveDPIScale();
            this.AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(kStartupWidth * dpiScale), (int)(kStartupHeight * dpiScale)));
        }

        // ===========[ Properties ]===================================

        /// <summary>The app, for the navigation footer's theme switcher and version lines.</summary>
        public App Application { get; }

        /// <summary>The window chrome, for probes that read or drive it.</summary>
        public ThemedWindowRootControl Chrome => this.Root;

        // ===========[ Public Interface Methods ]===================================

        /// <summary>
        /// Selects the area whose page is the given type, which navigates to it. The home page's cards use it.
        /// </summary>
        public void ShowArea (Type areaPageType)
        {
            foreach (object candidate in this.AreaNav.MenuItems)
            {
                if (candidate is NavigationViewItem item && item.Tag is string tag && tag == areaPageType.FullName)
                {
                    this.AreaNav.SelectedItem = item;
                    return;
                }
            }
        }

        // ===========[ Event Handlers ]===================================

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
