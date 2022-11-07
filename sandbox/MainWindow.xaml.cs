namespace TheAJutShowRoom
{
    using System.Windows;
    using AJut.UX;

    using DPUtils = AJut.UX.DPUtils<MainWindow>;

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.Navigator.GenerateAndPushDisplay<UI.Pages.LandingPage>();
        }

        public static readonly DependencyProperty UseThemesProperty = DPUtils.Register(_ => _.UseThemes, true, (d, e) => App.SetUseThemes(e.NewValue));
        public bool UseThemes
        {
            get => (bool)this.GetValue(UseThemesProperty);
            set => this.SetValue(UseThemesProperty, value);
        }

        public StackNavFlowController Navigator { get; } = new StackNavFlowController();
    }
}
