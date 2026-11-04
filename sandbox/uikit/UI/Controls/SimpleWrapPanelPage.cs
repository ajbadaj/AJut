namespace AJutShowRoomUIKit.UI.Controls
{
    using AJut.UX.Controls;
    using UIKit;

    /// <summary>
    /// The SimpleWrapPanel demo, as WinUI3's: uneven chips flowing and wrapping, with one that starts hidden so bringing it
    /// back exercises a subview joining the flow.
    /// </summary>
    public sealed class SimpleWrapPanelPage : ShowroomPageController
    {
        private readonly SimpleWrapPanel m_panel = new SimpleWrapPanel();
        private UIView m_epsilon;

        public SimpleWrapPanelPage () : base("SimpleWrapPanel")
        {
        }

        protected override void BuildPage ()
        {
            this.AddHeading("SimpleWrapPanel");
            this.AddText("Flows its subviews left to right at their own size and wraps to the next line when the next one would run past the width. HorizontalSpacing and VerticalSpacing gap them, and a hidden subview takes no place. Epsilon starts hidden: the button brings it back into the flow. Turn the phone to see the lines rewrap.");

            var toggle = UIButton.FromType(UIButtonType.System);
            toggle.SetTitle("Show or hide Epsilon", UIControlState.Normal);
            toggle.PrimaryActionTriggered += this.OnToggleEpsilon;
            this.AddDemo(toggle);

            string[] labels = { "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta", "Iota", "Kappa" };
            for (int index = 0; index < labels.Length; ++index)
            {
                UIButton chip = _Chip(labels[index], index);
                if (index == 4)
                {
                    chip.Hidden = true;
                    m_epsilon = chip;
                }

                m_panel.AddSubview(chip);
            }

            this.AddWide(m_panel);
            this.AddCode(@"
var panel = new SimpleWrapPanel { HorizontalSpacing = 8, VerticalSpacing = 6 };
panel.AddSubview(chip);

// UIKit tells no parent when a subview hides or shows, so say so
chip.Hidden = true;
panel.SetNeedsLayout();");
        }

        // The button and its handler go with the page, so the handler needs no unhooking: nothing native holds the page
        private void OnToggleEpsilon (object sender, System.EventArgs e)
        {
            m_epsilon.Hidden = !m_epsilon.Hidden;
            m_panel.SetNeedsLayout();
        }

        // Content-sized chips of uneven width, so the flow packs several on a line
        private static UIButton _Chip (string text, int index)
        {
            int tint = 70 + ((index * 22) % 150);
            UIButtonConfiguration configuration = UIButtonConfiguration.PlainButtonConfiguration;
            configuration.Title = text;
            configuration.ContentInsets = new NSDirectionalEdgeInsets(4, 8, 4, 8);
            UIBackgroundConfiguration background = configuration.Background;
            background.BackgroundColor = UIColor.FromRGBA(tint, 0x90, 0xC8, 0x55);
            background.CornerRadius = 4;
            configuration.Background = background;
            configuration.BaseForegroundColor = UIColor.Label;
            return new UIButton(CoreGraphics.CGRect.Empty) { Configuration = configuration, UserInteractionEnabled = false };
        }
    }
}
