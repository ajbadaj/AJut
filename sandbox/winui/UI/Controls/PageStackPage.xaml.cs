namespace AJutShowRoomWinUI.UI.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using Windows.UI;

    /// <summary>
    /// The PageStack demos: horizontal with dots, vertical, and a few pages with no dots. The pages are stocked from code
    /// because they are demo data, not layout.
    /// </summary>
    public sealed partial class PageStackPage : Page
    {
        public PageStackPage ()
        {
            this.InitializeComponent();
            this.StockPageStackDemos();
        }

        public string DemoCode { get; } = @"<ajut:PageStack x:Name=""PageStackHorizontal"" Width=""420"" Height=""180""
                ShowDots=""True"" DotReach=""3"" ShowPageCount=""True""/>
<ajut:PageStack Orientation=""Vertical"" ShowDots=""True"" TransitionDuration=""0:0:0.6""/>

// Pages are added like any panel's children, and SelectedIndex picks one (two way bindable)
this.PageStackHorizontal.Children.Add(page);
this.PageStackHorizontal.SelectedIndex = 4;";

        private void StockPageStackDemos ()
        {
            for (int i = 0; i < 20; ++i)
            {
                this.PageStackHorizontal.Children.Add(MakePage($"Page {i + 1}", i));
            }

            for (int i = 0; i < 6; ++i)
            {
                this.PageStackVertical.Children.Add(MakePage($"Card {i + 1}", i));
            }

            string[] words = { "First", "Second", "Third" };
            for (int i = 0; i < words.Length; ++i)
            {
                this.PageStackFew.Children.Add(MakePage(words[i], i));
            }
        }

        private static Border MakePage (string text, int index)
        {
            byte tint = (byte)(70 + ((index * 37) % 150));
            return new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(0x88, tint, 0x90, 0xC8)),
                Child = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock { Text = text, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center },
                        new Button { Content = "A button on the page", HorizontalAlignment = HorizontalAlignment.Center },
                    },
                },
            };
        }
    }
}
