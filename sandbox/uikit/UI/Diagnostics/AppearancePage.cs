namespace AJutShowRoomUIKit.UI.Diagnostics
{
    using System;
    using UIKit;

    /// <summary>Forces the whole window light or dark, or hands it back to the system, to check every page in both.</summary>
    public sealed class AppearancePage : ShowroomPageController
    {
        private static readonly UIUserInterfaceStyle[] kStyles = { UIUserInterfaceStyle.Unspecified, UIUserInterfaceStyle.Light, UIUserInterfaceStyle.Dark };
        private readonly UISegmentedControl m_styleChoice = new UISegmentedControl("System", "Light", "Dark");

        public AppearancePage () : base("Appearance")
        {
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear(animated);
            m_styleChoice.SelectedSegment = Array.IndexOf(kStyles, this.View.Window?.OverrideUserInterfaceStyle ?? UIUserInterfaceStyle.Unspecified);
            m_styleChoice.ValueChanged -= this.StyleChoice_OnValueChanged;
            m_styleChoice.ValueChanged += this.StyleChoice_OnValueChanged;
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear(animated);
            m_styleChoice.ValueChanged -= this.StyleChoice_OnValueChanged;
        }

        protected override void BuildPage ()
        {
            this.AddHeading("Light and dark");
            this.AddText("Overrides the appearance for the whole showroom window. Every AJut control defaults to UIKit system colors, so each should follow the switch with nothing reloaded.");
            this.AddDemo(m_styleChoice);
        }

        private void StyleChoice_OnValueChanged (object sender, EventArgs e)
        {
            UIWindow window = this.View.Window;
            if (window != null && m_styleChoice.SelectedSegment >= 0)
            {
                window.OverrideUserInterfaceStyle = kStyles[m_styleChoice.SelectedSegment];
            }
        }
    }
}
