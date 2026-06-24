namespace AJutShowRoomWinUI
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Windows.UI;
    using DPUtils = AJut.UX.DPUtils<BrushButtonExample>;

    public sealed partial class BrushButtonExample : UserControl
    {
        public BrushButtonExample ()
        {
            this.InitializeComponent();
            this.AccentPicker.Color = this.DemoAccent;
        }

        public static readonly DependencyProperty DemoAccentProperty = DPUtils.Register(_ => _.DemoAccent, Color.FromArgb(255, 139, 0, 0));
        public Color DemoAccent
        {
            get => (Color)this.GetValue(DemoAccentProperty);
            set => this.SetValue(DemoAccentProperty, value);
        }

        private void OnAccentPickerColorChanged (ColorPicker sender, ColorChangedEventArgs args)
        {
            this.DemoAccent = args.NewColor;
        }
    }
}
