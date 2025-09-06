// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AJutShowRoomWinUI
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;

    public sealed partial class MainWindowDisplay : UserControl
    {
        public MainWindowDisplay()
        {
            this.Application = App.Instance;
            this.InitializeComponent();
            this.Root.SetupFor(App.Instance.Windows.Root);
        }

        public App Application { get; }

        private void myButton_Click(object sender, RoutedEventArgs e)
        {
            myButton.Content = "Clicked";
            //this.PerformPresenterTask<OverlappedPresenter>(p => p.SetBorderAndTitleBar(true, false));

            //this.AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            //this.SetTitleBar(this.CustomTitleBar);
        }

    }
}
