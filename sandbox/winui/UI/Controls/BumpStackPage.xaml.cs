namespace AJutShowRoomWinUI.UI.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using Windows.UI;

    /// <summary>
    /// The BumpStack demos: horizontal, vertical, and clip-only. The chips are stocked from code because they are
    /// demo data, not layout.
    /// </summary>
    public sealed partial class BumpStackPage : Page
    {
        public BumpStackPage ()
        {
            this.InitializeComponent();
            this.StockBumpStackDemos();
        }

        public string DemoCode { get; } = @"<ajut:BumpStack x:Name=""BumpStackHorizontal"" Width=""320"" Height=""44""
                BorderBrush=""#80808080"" BorderThickness=""1""/>
<ajut:BumpStack x:Name=""BumpStackVertical"" Orientation=""Vertical"" Width=""160"" Height=""130""
                BorderBrush=""#80808080"" BorderThickness=""1""/>
<ajut:BumpStack x:Name=""BumpStackClip"" Width=""320"" Height=""44"" ScrollingEnabled=""False""
                BorderBrush=""#80808080"" BorderThickness=""1""/>

// Children are added like any panel
this.BumpStackHorizontal.Children.Add(chip);";

        private void StockBumpStackDemos ()
        {
            string[] words = { "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten" };
            for (int i = 0; i < words.Length; ++i)
            {
                this.BumpStackHorizontal.Children.Add(MakeBumpChip(words[i], i, vertical: false));
                this.BumpStackClip.Children.Add(MakeBumpChip(words[i], i, vertical: false));
            }

            for (int i = 0; i < 8; ++i)
            {
                this.BumpStackVertical.Children.Add(MakeBumpChip($"Row {i + 1}", i, vertical: true));
            }
        }

        private static Border MakeBumpChip (string text, int index, bool vertical)
        {
            byte tint = (byte)(70 + ((index * 22) % 150));
            return new Border
            {
                Width = vertical ? 130 : 70,
                Margin = new Thickness(3),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromArgb(0x55, tint, 0x90, 0xC8)),
                Child = new TextBlock
                {
                    Text = text,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 4),
                },
            };
        }
    }
}
