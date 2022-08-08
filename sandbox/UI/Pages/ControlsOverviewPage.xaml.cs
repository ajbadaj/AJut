namespace TheAJutShowRoom.UI.Pages
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using AJut.UX;
    using AJut.UX.Event;
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
            this.Navigator.Title = "ajut.ux.controls";
        }

        private void Test1_OnClick (object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Got it!");
        }

        private void ColorEdit_OnUserEditComplete (object sender, UserEditAppliedEventArgs e)
        {
            MessageBox.Show($"Color changed from: {e.OldValue} → {e.NewValue}");

        }
    }
}
