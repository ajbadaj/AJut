namespace TheAJutShowRoom
{
    using System.Windows;
    using AJut.UX;

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.Navigator.GenerateAndPushDisplay<UI.Pages.LandingPage>();
        }

        public StackNavFlowController Navigator { get; } = new StackNavFlowController();
    }
}
