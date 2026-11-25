namespace AJutShowRoomUIKit.UI.Controls
{
    using System;
    using AJut.UX.Controls;
    using UIKit;

    /// <summary>
    /// The PageStack demos, the other showrooms' three: horizontal, vertical, and a few pages, with one switch for the
    /// dots, one for the page count and one for the swipe, each switching all three. The pages are stocked from code
    /// because they are demo data, not layout, in translucent tints of their own like the other showrooms' pages.
    /// </summary>
    public sealed class PageStackPage : ShowroomPageController
    {
        private const float kHorizontalHeight = 180f;
        private const float kVerticalWidth = 260f;
        private const float kVerticalHeight = 240f;
        private const float kFewHeight = 120f;

        private readonly UISwitch m_showDotsSwitch = new UISwitch { On = true };
        private readonly UISwitch m_showPageCountSwitch = new UISwitch { On = true };
        private readonly UISwitch m_swipeSwitch = new UISwitch { On = true };
        private readonly UILabel m_selectedIndexLabel = new UILabel();
        private readonly PageStack m_horizontal = new PageStack { DotReach = 3 };
        private readonly PageStack m_vertical = new PageStack { Orientation = UILayoutConstraintAxis.Vertical, TransitionDuration = TimeSpan.FromMilliseconds(600) };
        private readonly PageStack m_few = new PageStack { SelectedIndex = 2 };

        // ===========[ Setup/Construction/Teardown ]===================================
        public PageStackPage () : base("PageStack")
        {
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear(animated);
            m_showDotsSwitch.ValueChanged -= this.ShowDotsSwitch_OnValueChanged;
            m_showDotsSwitch.ValueChanged += this.ShowDotsSwitch_OnValueChanged;
            m_showPageCountSwitch.ValueChanged -= this.ShowPageCountSwitch_OnValueChanged;
            m_showPageCountSwitch.ValueChanged += this.ShowPageCountSwitch_OnValueChanged;
            m_swipeSwitch.ValueChanged -= this.SwipeSwitch_OnValueChanged;
            m_swipeSwitch.ValueChanged += this.SwipeSwitch_OnValueChanged;
            m_horizontal.SelectedIndexChanged -= this.Horizontal_OnSelectedIndexChanged;
            m_horizontal.SelectedIndexChanged += this.Horizontal_OnSelectedIndexChanged;
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear(animated);
            m_showDotsSwitch.ValueChanged -= this.ShowDotsSwitch_OnValueChanged;
            m_showPageCountSwitch.ValueChanged -= this.ShowPageCountSwitch_OnValueChanged;
            m_swipeSwitch.ValueChanged -= this.SwipeSwitch_OnValueChanged;
            m_horizontal.SelectedIndexChanged -= this.Horizontal_OnSelectedIndexChanged;
        }

        // ===========[ Events ]===================================
        private void ShowDotsSwitch_OnValueChanged (object sender, EventArgs e) => this.ApplySwitches();

        private void ShowPageCountSwitch_OnValueChanged (object sender, EventArgs e) => this.ApplySwitches();

        private void SwipeSwitch_OnValueChanged (object sender, EventArgs e) => this.ApplySwitches();

        private void Horizontal_OnSelectedIndexChanged (object sender, EventArgs e) => this.UpdateSelectedIndexLabel();

        // ===========[ Public Interface Methods ]===================================
        protected override void BuildPage ()
        {
            this.AddHeading("PageStack");
            this.AddText("BumpStack's sibling for paging: one page shows at a time, and the neighbor pages peek in, faded, past the page buttons. A tap on a page button or a dot runs an animated transition, and a swipe along the stack drags the pages with the finger, settling on the nearest page when let go, or on a flick, the next one. The dot strip shows DotReach dots each side of the current page, plus a smaller, faded edge dot while there are more pages past them; tapping an edge dot goes just past the range.");
            this.AddDemoRow(new UILabel { Text = "ShowDots" }, m_showDotsSwitch);
            this.AddDemoRow(new UILabel { Text = "ShowPageCount" }, m_showPageCountSwitch);
            this.AddDemoRow(new UILabel { Text = "IsSwipeEnabled" }, m_swipeSwitch);

            // 1. Horizontal, 20 pages
            this.AddText("Horizontal, 20 pages, DotReach = 3");
            for (int index = 0; index < 20; ++index)
            {
                m_horizontal.Children.Add(_Page($"Page {index + 1}", index));
            }

            m_horizontal.HeightAnchor.ConstraintEqualTo(kHorizontalHeight).Active = true;
            this.AddWide(m_horizontal);
            this.UpdateSelectedIndexLabel();
            this.AddDemo(m_selectedIndexLabel);

            // 2. Vertical, with a slower transition
            this.AddText("Vertical, 6 pages, a slower TransitionDuration");
            for (int index = 0; index < 6; ++index)
            {
                m_vertical.Children.Add(_Page($"Card {index + 1}", index));
            }

            m_vertical.WidthAnchor.ConstraintEqualTo(kVerticalWidth).Active = true;
            m_vertical.HeightAnchor.ConstraintEqualTo(kVerticalHeight).Active = true;
            this.AddDemo(m_vertical);

            // 3. A few pages, starting on the last: the index is set before the pages are added
            this.AddText("Three pages, starting on the last");
            string[] words = { "First", "Second", "Third" };
            for (int index = 0; index < words.Length; ++index)
            {
                m_few.Children.Add(_Page(words[index], index));
            }

            m_few.HeightAnchor.ConstraintEqualTo(kFewHeight).Active = true;
            this.AddWide(m_few);
            this.ApplySwitches();

            this.AddCode(@"
var stack = new PageStack { ShowDots = true, DotReach = 3, ShowPageCount = true };
stack.Children.Add(page); // each page is pinned to the page area
stack.SelectedIndex = 4;  // runs a transition to it
stack.IsSwipeEnabled = false; // the page buttons and dots only
stack.SelectedIndexChanged += this.OnPageChanged;");
        }

        private void ApplySwitches ()
        {
            foreach (PageStack stack in new[] { m_horizontal, m_vertical, m_few })
            {
                stack.ShowDots = m_showDotsSwitch.On;
                stack.ShowPageCount = m_showPageCountSwitch.On;
                stack.IsSwipeEnabled = m_swipeSwitch.On;
            }
        }

        private void UpdateSelectedIndexLabel ()
        {
            m_selectedIndexLabel.Text = $"SelectedIndexChanged, SelectedIndex: {m_horizontal.SelectedIndex}";
        }

        // A page: a rounded tint with its name in the middle
        private static UIView _Page (string text, int index)
        {
            const int kTintAlpha = 0x88;
            const int kTintGreen = 0x90;
            const int kTintBlue = 0xC8;
            const float kCornerRadius = 6f;
            int tint = 70 + ((index * 37) % 150);

            var page = new UIView { BackgroundColor = UIColor.FromRGBA(tint, kTintGreen, kTintBlue, kTintAlpha), AccessibilityIdentifier = text };
            page.Layer.CornerRadius = kCornerRadius;

            var label = new UILabel { Text = text, TextColor = UIColor.Label, TranslatesAutoresizingMaskIntoConstraints = false };
            page.AddSubview(label);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                label.CenterXAnchor.ConstraintEqualTo(page.CenterXAnchor),
                label.CenterYAnchor.ConstraintEqualTo(page.CenterYAnchor),
            });

            return page;
        }
    }
}
