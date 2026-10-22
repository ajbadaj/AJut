namespace AJutShowRoomAppKit.UI.Diagnostics
{
    using System;
    using System.ComponentModel;
    using AJut.UX;
    using AJut.UX.Controls;
    using AppKit;
    using CoreGraphics;

    // Live readout of ThemedWindow's chrome state, for checking the border colors by hand: key
    // versus not key, and the close-hover warning while the pointer is over the close button.
    public sealed class WindowChromePage : ShowroomPage
    {
        private readonly NSTextField m_stateText;
        private readonly NSButton m_openSecondWindow;
        private WindowChromeStateTracker m_trackedChrome;
        private ThemedWindow m_secondWindow;

        public WindowChromePage ()
            : base("Window Chrome", "What this window's chrome tracker says right now. Hover the close button and the border should turn the close warning color; click another window and it should go to the inactive color.")
        {
            m_stateText = ShowroomStyle.Body(String.Empty);
            this.AddDemo(m_stateText);

            m_openSecondWindow = new NSButton { Title = "Open a second ThemedWindow" };
            m_openSecondWindow.BezelStyle = NSBezelStyle.Rounded;
            m_openSecondWindow.Activated += this.OnOpenSecondWindowActivated;
            this.AddDemo(m_openSecondWindow, eFillStackCrossAlignment.Start);

            this.AddCode(
@"var window = new ThemedWindow(new CGRect(0, 0, 600, 400))
{
    Title = ""A themed window"",
    WindowContents = contents,
};
window.Closing += this.OnWindowClosing;   // set e.Cancel to keep it open
window.MakeKeyAndOrderFront(this);"
            );
        }

        protected override void OnTeardown ()
        {
            m_openSecondWindow.Activated -= this.OnOpenSecondWindowActivated;
            this.TrackChrome(null);
            if (m_secondWindow != null)
            {
                m_secondWindow.Closed -= this.OnSecondWindowClosed;
                m_secondWindow.Close();
                m_secondWindow = null;
            }
        }

        // ===========[ Events ]================================================
        public override void ViewDidMoveToWindow ()
        {
            base.ViewDidMoveToWindow();
            this.TrackChrome((this.Window as ThemedWindow)?.Chrome);
        }

        private void OnChromePropertyChanged (object sender, PropertyChangedEventArgs e)
        {
            this.RefreshStateText();
        }

        private void OnOpenSecondWindowActivated (object sender, EventArgs e)
        {
            if (m_secondWindow != null)
            {
                m_secondWindow.MakeKeyAndOrderFront(this);
                return;
            }

            m_secondWindow = new ThemedWindow(new CGRect(0, 0, 420, 260)) { Title = "Second window" };
            m_secondWindow.WindowContents = ShowroomStyle.Body("Click back and forth between this and the showroom to see each border change.");
            m_secondWindow.Closed += this.OnSecondWindowClosed;
            m_secondWindow.Center();
            m_secondWindow.MakeKeyAndOrderFront(this);
        }

        private void OnSecondWindowClosed (object sender, EventArgs e)
        {
            m_secondWindow.Closed -= this.OnSecondWindowClosed;
            m_secondWindow = null;
        }

        // ===========[ Helper Methods ]========================================
        private void TrackChrome (WindowChromeStateTracker chrome)
        {
            if (m_trackedChrome != null)
            {
                m_trackedChrome.PropertyChanged -= this.OnChromePropertyChanged;
            }

            m_trackedChrome = chrome;
            if (m_trackedChrome != null)
            {
                m_trackedChrome.PropertyChanged -= this.OnChromePropertyChanged;
                m_trackedChrome.PropertyChanged += this.OnChromePropertyChanged;
            }

            this.RefreshStateText();
        }

        private void RefreshStateText ()
        {
            if (m_trackedChrome == null)
            {
                m_stateText.StringValue = "Not in a ThemedWindow";
                return;
            }

            m_stateText.StringValue =
                $"State: {m_trackedChrome.State}\n"
                + $"Activated: {m_trackedChrome.IsActivated}\n"
                + $"Window state: {m_trackedChrome.WindowState}\n"
                + $"Hovered caption button: {m_trackedChrome.HoveredCaptionButton?.ToString() ?? "none"}\n"
                + $"Shows window border: {m_trackedChrome.ShowsWindowBorder}";
        }
    }
}
