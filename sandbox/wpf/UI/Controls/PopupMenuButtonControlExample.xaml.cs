namespace TheAJutShowRoom.UI.Controls
{
    using System.Windows;
    using System.Windows.Controls;

    public partial class PopupMenuButtonControlExample : UserControl
    {
        public PopupMenuButtonControlExample ()
        {
            this.InitializeComponent();
        }

        private void Test1_OnClick (object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Got it!");
        }
    }
}
