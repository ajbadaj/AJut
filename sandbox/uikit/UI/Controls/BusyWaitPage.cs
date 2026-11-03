namespace AJutShowRoomUIKit.UI.Controls
{
    using System;
    using AJut.UX.Controls;
    using UIKit;

    /// <summary>
    /// BusyWait on UIKit: three docks that start and stop together, and an overlay over a button it blocks while busy.
    /// The switches and the button are followed only while the page shows, since a handler of this page held by its own
    /// views would be a cycle the GC never breaks.
    /// </summary>
    public sealed class BusyWaitPage : ShowroomPageController
    {
        private const float kOverlayPanelHeight = 160f;

        private readonly UISwitch m_spinningSwitch = new UISwitch { On = true };
        private readonly UISwitch m_busySwitch = new UISwitch { On = false };
        private readonly BusyWait m_right = new BusyWait { Text = "Loading, spinner on the right" };
        private readonly BusyWait m_left = new BusyWait { Text = "Spinner on the left", SpinnerDock = eBusyWaitSpinnerDock.Left };
        private readonly BusyWait m_top = new BusyWait { Text = "Spinner on top, a size up", SpinnerDock = eBusyWaitSpinnerDock.Top, SpinnerSize = 40f };
        private readonly BusyWait m_overlay = new BusyWait { Text = "Saving...", IsOverlay = true, IsSpinning = false };
        private readonly UIButton m_pressMe = UIButton.FromType(UIButtonType.System);
        private readonly UILabel m_pressCount = new UILabel { Text = "Pressed 0 times" };
        private int m_presses;

        // ===========[ Setup/Construction/Teardown ]===================================
        public BusyWaitPage () : base("BusyWait")
        {
            m_pressMe.SetTitle("Press me", UIControlState.Normal);
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear(animated);
            m_spinningSwitch.ValueChanged -= this.SpinningSwitch_OnValueChanged;
            m_spinningSwitch.ValueChanged += this.SpinningSwitch_OnValueChanged;
            m_busySwitch.ValueChanged -= this.BusySwitch_OnValueChanged;
            m_busySwitch.ValueChanged += this.BusySwitch_OnValueChanged;
            m_pressMe.TouchUpInside -= this.PressMe_OnTouchUpInside;
            m_pressMe.TouchUpInside += this.PressMe_OnTouchUpInside;
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear(animated);
            m_spinningSwitch.ValueChanged -= this.SpinningSwitch_OnValueChanged;
            m_busySwitch.ValueChanged -= this.BusySwitch_OnValueChanged;
            m_pressMe.TouchUpInside -= this.PressMe_OnTouchUpInside;
        }

        protected override void BuildPage ()
        {
            this.AddHeading("BusyWait");
            this.AddText("The platform's own spinner beside a line of text. SpinnerDock puts the spinner on any side, and stopped, the spinner hides but keeps its room.");
            this.AddDemoRow(new UILabel { Text = "Spinning" }, m_spinningSwitch);
            this.AddDemo(m_right);
            this.AddDemo(m_left);
            this.AddDemo(m_top);
            this.AddCode(@"
var busy = new BusyWait
{
    Text = ""Loading"",
    SpinnerDock = eBusyWaitSpinnerDock.Left,
};
busy.IsSpinning = false;");

            this.AddHeading("As an overlay");
            this.AddText("IsOverlay lays it over what it covers. While busy it covers the panel and takes every tap, so the button under it can't be pressed; stopped, it draws nothing and the button works.");
            this.AddDemoRow(new UILabel { Text = "Busy" }, m_busySwitch);

            var panel = new UIView { TranslatesAutoresizingMaskIntoConstraints = false };
            var content = new UIStackView(new UIView[] { m_pressMe, m_pressCount })
            {
                Axis = UILayoutConstraintAxis.Vertical,
                Alignment = UIStackViewAlignment.Center,
                Spacing = 8f,
                TranslatesAutoresizingMaskIntoConstraints = false,
            };
            m_overlay.TranslatesAutoresizingMaskIntoConstraints = false;
            panel.AddSubview(content);
            panel.AddSubview(m_overlay);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                panel.HeightAnchor.ConstraintEqualTo(kOverlayPanelHeight),
                content.CenterXAnchor.ConstraintEqualTo(panel.CenterXAnchor),
                content.CenterYAnchor.ConstraintEqualTo(panel.CenterYAnchor),
                m_overlay.LeadingAnchor.ConstraintEqualTo(panel.LeadingAnchor),
                m_overlay.TrailingAnchor.ConstraintEqualTo(panel.TrailingAnchor),
                m_overlay.TopAnchor.ConstraintEqualTo(panel.TopAnchor),
                m_overlay.BottomAnchor.ConstraintEqualTo(panel.BottomAnchor),
            });
            this.AddWide(panel);
        }

        // ===========[ Events ]===================================
        private void SpinningSwitch_OnValueChanged (object sender, EventArgs e)
        {
            m_right.IsSpinning = m_spinningSwitch.On;
            m_left.IsSpinning = m_spinningSwitch.On;
            m_top.IsSpinning = m_spinningSwitch.On;
        }

        private void BusySwitch_OnValueChanged (object sender, EventArgs e)
        {
            m_overlay.IsSpinning = m_busySwitch.On;
        }

        private void PressMe_OnTouchUpInside (object sender, EventArgs e)
        {
            ++m_presses;
            m_pressCount.Text = $"Pressed {m_presses} times";
        }
    }
}
