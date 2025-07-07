// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AJutShowRoomWinUI
{
    using System.Linq;
    using Microsoft.UI.Xaml;


    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow ()
        {
            this.Application = App.Instance;
            this.InitializeComponent();
            this.Root.SetupFor(this);
        }

        public App Application { get; }

        private void myButton_Click (object sender, RoutedEventArgs e)
        {
            myButton.Content = "Clicked";
            //this.PerformPresenterTask<OverlappedPresenter>(p => p.SetBorderAndTitleBar(true, false));

            //this.AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            //this.SetTitleBar(this.CustomTitleBar);
        }


        // Helper method to get a theme brush by key from resources
        private T? GetThemeResource<T> (string key)
        {
            // Try window resources first
            if (App.Current.Resources.TryGetValue(key, out var value) && value is T resource)
            {
                return resource;
            }
            // Try theme dictionaries (Light/Dark/Default)
            foreach (var themeDict in App.Current.Resources.ThemeDictionaries.Values.OfType<ResourceDictionary>())
            {
                if (themeDict.TryGetValue(key, out value) && value is T dictionaryResource)
                {
                    return dictionaryResource;
                }
            }

            return default;
        }
    }
}
