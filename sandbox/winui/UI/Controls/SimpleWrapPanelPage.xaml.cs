namespace AJutShowRoomWinUI.UI.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using Windows.UI;

    /// <summary>
    /// The SimpleWrapPanel demo: uneven chips flowing and wrapping, with one that starts collapsed so the reveal
    /// path (a never-measured child joining the flow) gets exercised.
    /// </summary>
    public sealed partial class SimpleWrapPanelPage : Page
    {
        private UIElement? m_wrapCollapseTarget;

        public SimpleWrapPanelPage ()
        {
            this.InitializeComponent();
            this.StockSimpleWrapPanelDemo();
        }

        public string DemoCode { get; } = @"<Border BorderBrush=""#80808080"" BorderThickness=""1"" Width=""360"" Padding=""6"">
    <ajut:SimpleWrapPanel x:Name=""WrapPanelDemo""/>
</Border>

// Children are added like any panel; a collapsed child takes no slot
this.WrapPanelDemo.Children.Add(chip);";

        // Content-sized chips of varying width - the point of SimpleWrapPanel is that each child stays
        // at its desired size, so deliberately uneven labels show the flow packing several per line.
        private void StockSimpleWrapPanelDemo ()
        {
            string[] labels = { "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta", "Iota", "Kappa" };
            for (int i = 0; i < labels.Length; ++i)
            {
                Border chip = MakeWrapChip(labels[i], i);
                if (i == 4)
                {
                    // Start this one hidden from launch - so the panel never measures it - and let the
                    // reveal button surface it. That is the path that must pull a never-measured child
                    // into the flow when it becomes visible.
                    chip.Visibility = Visibility.Collapsed;
                    m_wrapCollapseTarget = chip;
                }

                this.WrapPanelDemo.Children.Add(chip);
            }
        }

        private static Border MakeWrapChip (string text, int index)
        {
            byte tint = (byte)(70 + ((index * 22) % 150));
            return new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Background = new SolidColorBrush(Color.FromArgb(0x55, tint, 0x90, 0xC8)),
                Child = new TextBlock { Text = text },
            };
        }

        private void WrapPanelToggleCollapse_OnClick (object sender, RoutedEventArgs e)
        {
            if (m_wrapCollapseTarget == null)
            {
                return;
            }

            // Flip the demo item between collapsed and visible: collapsing drops it from the flow,
            // revealing has to pull it back in.
            m_wrapCollapseTarget.Visibility = m_wrapCollapseTarget.Visibility == Visibility.Collapsed
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}
