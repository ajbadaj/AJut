namespace AJutShowRoomAppKit.UI.Pages
{
    using System;
    using AJut.UX.Controls;
    using AJut.UX.Theming;
    using AppKit;

    // The landing page: what the showroom is, and a card per area
    public sealed class HomePage : ShowroomPage
    {
        private readonly Action<int> m_openArea;
        private readonly NSButton[] m_exploreButtons;

        public HomePage (Action<int> openArea)
            : base("What do we have here?", "This app shows what AJut.UX.AppKit can do, with the code beside each example, and it's the testing ground for the library on macOS.")
        {
            m_openArea = openArea;
            this.AddText("Pick an area on the left, or from the cards below. The WinUI3 and WPF showrooms have the same areas and the same page names, so what you find here you can find there.");
            this.AddText("Use the theme bar along the top to switch between dark, light, and the OS setting, to turn the AJut theme off, and to try the touch look. Every control has to look right every way. Debug > Theme Tester shows each one in its states side by side.");

            m_exploreButtons = new NSButton[ShowroomAreas.All.Count];
            for (int i = 0; i < ShowroomAreas.All.Count; ++i)
            {
                ShowroomArea area = ShowroomAreas.All[i];
                var card = new ThemedView
                {
                    BackgroundColorKey = ShowroomStyle.SidebarBackground,
                    BorderColorKey = ShowroomStyle.Divider,
                    BorderThickness = 1.0,
                    CornerRadius = 6.0,
                };

                var cardStack = new FillStack(NSUserInterfaceLayoutOrientation.Vertical, spacing: 6.0)
                {
                    Padding = new NSEdgeInsets(10, 12, 10, 12),
                };
                cardStack.TranslatesAutoresizingMaskIntoConstraints = false;
                card.AddSubview(cardStack);
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    cardStack.TopAnchor.ConstraintEqualTo(card.TopAnchor),
                    cardStack.BottomAnchor.ConstraintEqualTo(card.BottomAnchor),
                    cardStack.LeadingAnchor.ConstraintEqualTo(card.LeadingAnchor),
                    cardStack.TrailingAnchor.ConstraintEqualTo(card.TrailingAnchor),
                });

                cardStack.Add(ShowroomStyle.Heading(area.Title));
                cardStack.Add(ShowroomStyle.Body(area.Blurb));

                var explore = new NSButton { Title = "Explore" };
                explore.BezelStyle = NSBezelStyle.Rounded;
                explore.Tag = i;
                explore.Activated += this.OnExploreActivated;
                m_exploreButtons[i] = explore;
                cardStack.Add(explore, alignment: eFillStackCrossAlignment.Start);

                this.AddDemo(card);
            }
        }

        protected override void OnTeardown ()
        {
            foreach (NSButton button in m_exploreButtons)
            {
                button.Activated -= this.OnExploreActivated;
            }
        }

        private void OnExploreActivated (object sender, EventArgs e)
        {
            m_openArea((int)((NSButton)sender).Tag);
        }
    }
}
