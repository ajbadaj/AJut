namespace TheAJutShowRoom.UI.Pages
{
    using System.Windows;
    using System.Windows.Controls;
    using AJut.UX;
    using DPUtils = AJut.UX.DPUtils<ControlsOverviewPage>;

    public partial class ControlsOverviewPage : UserControl, IStackNavDisplayControl
    {
        public ControlsOverviewPage()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty NavigatorProperty = DPUtils.Register(_ => _.Navigator);
        public StackNavAdapter Navigator
        {
            get => (StackNavAdapter)this.GetValue(NavigatorProperty);
            set => this.SetValue(NavigatorProperty, value);
        }

        public void Setup (StackNavAdapter adapter)
        {
            this.Navigator = adapter;
            this.Navigator.Title = "Controls";
        }
    }
}
