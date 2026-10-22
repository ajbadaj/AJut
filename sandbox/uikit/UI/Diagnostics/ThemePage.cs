namespace AJutShowRoomUIKit.UI.Diagnostics
{
    using System;
    using AJut.UX.Controls;
    using AJut.UX.Theming;
    using UIKit;

    /// <summary>
    /// Switches the showroom between the AJut theme and no theme. Controls read their colors when they are made, so the
    /// switch rebuilds the showroom's views, the same as restarting an app with the theme merged in or not.
    /// </summary>
    public sealed class ThemePage : ShowroomPageController
    {
        private readonly UISegmentedControl m_themeChoice = new UISegmentedControl("Unthemed", "AJut theme");

        public ThemePage () : base("Theme")
        {
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear(animated);
            m_themeChoice.SelectedSegment = ThemedColors.IsThemeLoaded ? 1 : 0;
            m_themeChoice.ValueChanged -= this.ThemeChoice_OnValueChanged;
            m_themeChoice.ValueChanged += this.ThemeChoice_OnValueChanged;
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear(animated);
            m_themeChoice.ValueChanged -= this.ThemeChoice_OnValueChanged;
        }

        protected override void BuildPage ()
        {
            this.AddHeading("Themed and unthemed");
            this.AddText("Every AJut control has to look right with no theme (UIKit system colors) and adopt the AJut palette when the theme is loaded. Switch here, then compare any control page; Appearance switches light and dark on top.");
            this.AddDemo(m_themeChoice);

            var sample = new BrushButton();
            sample.SetTitle("BrushButton, as built now", UIControlState.Normal);
            this.AddDemoRow(sample, new NumericEditor { Value = 3 });
            this.AddCode(@"
// At startup, before the first AJut control is made
ThemedColors.IsThemeLoaded = true;");
        }

        private void ThemeChoice_OnValueChanged (object sender, EventArgs e)
        {
            UIWindow window = this.View.Window;
            ThemedColors.IsThemeLoaded = m_themeChoice.SelectedSegment == 1;
            if (window != null)
            {
                window.RootViewController = ShowroomRoot.Create(startOn: new ThemePage());
            }
        }
    }
}
