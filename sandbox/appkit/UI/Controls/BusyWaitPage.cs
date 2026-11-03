namespace AJutShowRoomAppKit.UI.Controls
{
    using System;
    using AJut.UX.Controls;
    using AJut.UX.Theming;
    using AppKit;

    // Three docks that start and stop together, and an overlay over a button it blocks while busy
    public sealed class BusyWaitPage : ShowroomPage
    {
        private const double kOverlayPanelHeight = 160.0;

        private readonly NSButton m_spinningCheck;
        private readonly NSButton m_busyCheck;
        private readonly NSButton m_pressMe;
        private readonly NSTextField m_pressCount;
        private readonly BusyWait m_right = new BusyWait { Text = "Loading, spinner on the right" };
        private readonly BusyWait m_left = new BusyWait { Text = "Spinner on the left", SpinnerDock = eBusyWaitSpinnerDock.Left };
        private readonly BusyWait m_top = new BusyWait { Text = "Spinner on top, a size up", SpinnerDock = eBusyWaitSpinnerDock.Top, SpinnerSize = 40.0 };
        private readonly BusyWait m_overlay = new BusyWait { Text = "Saving...", IsOverlay = true, IsSpinning = false };
        private int m_presses;

        public BusyWaitPage ()
            : base("BusyWait", "The system's own spinner beside a line of text. SpinnerDock puts the spinner on any side, and stopped, the spinner hides but keeps its room.")
        {
            m_spinningCheck = new NSButton { Title = "Spinning", State = NSCellStateValue.On };
            m_spinningCheck.SetButtonType(NSButtonType.Switch);
            m_spinningCheck.Activated += this.OnSpinningActivated;
            this.AddDemo(m_spinningCheck, eFillStackCrossAlignment.Start);
            this.AddLabeledDemo("Right (the default)", m_right);
            this.AddLabeledDemo("Left", m_left);
            this.AddLabeledDemo("Top, SpinnerSize 40", m_top);

            this.AddCode(
@"var busy = new BusyWait
{
    Text = ""Loading"",
    SpinnerDock = eBusyWaitSpinnerDock.Left,
};
busy.IsSpinning = false;"
            );

            this.AddHeading("As an overlay");
            this.AddText("IsOverlay lays it over what it covers. While busy it covers the panel and takes every click and scroll, so the button under it can't be pressed; stopped, it draws nothing and the button works.");
            m_busyCheck = new NSButton { Title = "Busy" };
            m_busyCheck.SetButtonType(NSButtonType.Switch);
            m_busyCheck.Activated += this.OnBusyActivated;
            this.AddDemo(m_busyCheck, eFillStackCrossAlignment.Start);

            m_pressMe = new NSButton { Title = "Press me" };
            m_pressMe.BezelStyle = NSBezelStyle.Rounded;
            m_pressMe.Activated += this.OnPressMeActivated;
            m_pressCount = ShowroomStyle.Body("Pressed 0 times");
            this.AddDemo(this.MakeOverlayPanel());

            this.AddCode(
@"var overlay = new BusyWait { Text = ""Saving..."", IsOverlay = true, IsSpinning = false };
// Pinned over the page, then turned on while it is busy
overlay.IsSpinning = true;"
            );
        }

        protected override void OnTeardown ()
        {
            m_spinningCheck.Activated -= this.OnSpinningActivated;
            m_busyCheck.Activated -= this.OnBusyActivated;
            m_pressMe.Activated -= this.OnPressMeActivated;
        }

        // ===========[ Events ]================================================
        private void OnSpinningActivated (object sender, EventArgs e)
        {
            bool isSpinning = m_spinningCheck.State == NSCellStateValue.On;
            m_right.IsSpinning = isSpinning;
            m_left.IsSpinning = isSpinning;
            m_top.IsSpinning = isSpinning;
        }

        private void OnBusyActivated (object sender, EventArgs e) => m_overlay.IsSpinning = m_busyCheck.State == NSCellStateValue.On;

        private void OnPressMeActivated (object sender, EventArgs e)
        {
            ++m_presses;
            m_pressCount.StringValue = $"Pressed {m_presses} times";
        }

        // ===========[ Helper Methods ]========================================
        // The button and its count in the middle of a panel, with the overlay laid over the whole panel
        private NSView MakeOverlayPanel ()
        {
            var panel = new ThemedView { BackgroundColorKey = ShowroomStyle.SidebarBackground };
            panel.TranslatesAutoresizingMaskIntoConstraints = false;

            var content = new FillStack(NSUserInterfaceLayoutOrientation.Vertical, spacing: 8.0);
            content.TranslatesAutoresizingMaskIntoConstraints = false;
            content.Add(m_pressMe, alignment: eFillStackCrossAlignment.Center);
            content.Add(m_pressCount, alignment: eFillStackCrossAlignment.Center);

            m_overlay.TranslatesAutoresizingMaskIntoConstraints = false;
            panel.AddSubview(content);
            panel.AddSubview(m_overlay);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                panel.HeightAnchor.ConstraintEqualTo((nfloat)kOverlayPanelHeight),
                content.CenterXAnchor.ConstraintEqualTo(panel.CenterXAnchor),
                content.CenterYAnchor.ConstraintEqualTo(panel.CenterYAnchor),
                m_overlay.LeadingAnchor.ConstraintEqualTo(panel.LeadingAnchor),
                m_overlay.TrailingAnchor.ConstraintEqualTo(panel.TrailingAnchor),
                m_overlay.TopAnchor.ConstraintEqualTo(panel.TopAnchor),
                m_overlay.BottomAnchor.ConstraintEqualTo(panel.BottomAnchor),
            });

            return panel;
        }
    }
}
