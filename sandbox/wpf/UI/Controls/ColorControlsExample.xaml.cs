namespace TheAJutShowRoom.UI.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using AJut.UX;

    public partial class ColorControlsExample : UserControl
    {
        public ColorControlsExample ()
        {
            this.InitializeComponent();
        }

        private void ColorEdit_OnUserEditComplete (object sender, UserEditAppliedEventArgs e)
        {
            MessageBox.Show($"Color changed from: {e.OldValue} -> {e.NewValue}");
        }
    }
}
