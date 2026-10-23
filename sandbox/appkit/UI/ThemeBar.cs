namespace AJutShowRoomAppKit.UI
{
    using System;
    using AJut.UX.Controls;
    using AJut.UX.Theming;
    using AppKit;

    // The bar across the top of the showroom window, so every page can be judged in every look without
    // leaving it: Dark, Light or System, the AJut theme on or off, and Touch on or off. The same bar the
    // Android and UIKit showrooms have.
    //
    // Touch only means something with the AJut theme loaded and a look picked here. Under System the
    // look, touch included, is the OS's answer, which on a Mac is off, so the switch shows that and is
    // disabled; with the theme off it is disabled and keeps the choice for when the theme comes back.
    // Neither the touch choice nor the switch's state is saved between runs, as in the other showrooms.
    public sealed class ThemeBar : ThemedView
    {
        private static readonly eAppThemeConfiguration[] kThemeChoices = new[] { eAppThemeConfiguration.Dark, eAppThemeConfiguration.Light, eAppThemeConfiguration.UseSameAsOS };

        private readonly ToggleStrip m_themeStrip;
        private readonly NSButton m_useThemesCheck;
        private readonly NSButton m_touchCheck;
        private bool m_touchChoice;

        // ===========[ Setup/Construction/Teardown ]===========================
        public ThemeBar ()
        {
            ShowroomStyle.EnsureRegistered();
            this.BackgroundColorKey = ShowroomStyle.SidebarBackground;
            m_touchChoice = AppDelegate.ThemeManager.IsTouchFocused;

            m_themeStrip = new ToggleStrip
            {
                ItemsSource = kThemeChoices,
                ItemNameSelector = NameOfThemeChoice,
                SelectedItem = AppDelegate.ThemeManager.ThemeConfiguration,
            };
            m_themeStrip.SelectionChanged += this.OnThemeStripSelectionChanged;

            m_useThemesCheck = new NSButton { Title = "AJut theme", ToolTip = "Off shows every control unthemed, in the macOS system colors" };
            m_useThemesCheck.SetButtonType(NSButtonType.Switch);
            m_useThemesCheck.State = AppDelegate.UseThemes ? NSCellStateValue.On : NSCellStateValue.Off;
            m_useThemesCheck.Activated += this.OnUseThemesActivated;

            m_touchCheck = new NSButton { Title = "Touch", ToolTip = "The touch palette: lifted button surfaces and no button borders, for touch with no hover" };
            m_touchCheck.SetButtonType(NSButtonType.Switch);
            m_touchCheck.Activated += this.OnTouchActivated;

            var row = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal, spacing: 14.0)
            {
                Padding = new NSEdgeInsets(8, 12, 8, 12),
            };
            row.Add(ShowroomStyle.Understated("Theme"), alignment: eFillStackCrossAlignment.Center);
            row.Add(m_themeStrip, alignment: eFillStackCrossAlignment.Center);
            row.Add(m_useThemesCheck, alignment: eFillStackCrossAlignment.Center);
            row.Add(m_touchCheck, alignment: eFillStackCrossAlignment.Center);
            row.Add(new NSView(), grows: true);
            row.TranslatesAutoresizingMaskIntoConstraints = false;
            this.AddSubview(row);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                row.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                row.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                row.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                row.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
            });

            // The row's items are centered and its spacer stretches, so nothing in it sets the bar's height, and the
            // window would split its height between the bar and the page however the layout engine liked. This pulls
            // the bar down to its content.
            NSLayoutConstraint hugContent = this.HeightAnchor.ConstraintEqualTo(0);
            hugContent.Priority = (float)NSLayoutPriority.DefaultLow;
            hugContent.Active = true;

            this.ApplyTouch();
        }

        public void Teardown ()
        {
            m_themeStrip.SelectionChanged -= this.OnThemeStripSelectionChanged;
            m_useThemesCheck.Activated -= this.OnUseThemesActivated;
            m_touchCheck.Activated -= this.OnTouchActivated;
        }

        // ===========[ Events ]================================================
        private void OnThemeStripSelectionChanged (object sender, ToggleStrip.ToggleStripSelectionChangedEventArgs e)
        {
            if (m_themeStrip.SelectedItem is eAppThemeConfiguration configuration)
            {
                AppDelegate.ThemeManager.ThemeConfiguration = configuration;
                this.ApplyTouch();
            }
        }

        private void OnUseThemesActivated (object sender, EventArgs e)
        {
            AppDelegate.UseThemes = m_useThemesCheck.State == NSCellStateValue.On;
            this.ApplyTouch();
        }

        private void OnTouchActivated (object sender, EventArgs e)
        {
            m_touchChoice = m_touchCheck.State == NSCellStateValue.On;
            this.ApplyTouch();
        }

        // ===========[ Helper Methods ]========================================
        // The switch and the theme manager follow the rules in the class comment
        private void ApplyTouch ()
        {
            bool followsTheOS = AppDelegate.ThemeManager.ThemeConfiguration == eAppThemeConfiguration.UseSameAsOS;
            bool isTouchFocused = !followsTheOS && m_touchChoice;

            AppDelegate.ThemeManager.IsTouchFocused = isTouchFocused;
            m_touchCheck.State = isTouchFocused ? NSCellStateValue.On : NSCellStateValue.Off;
            m_touchCheck.Enabled = !followsTheOS && AppDelegate.UseThemes;
        }

        private static string NameOfThemeChoice (object choice)
        {
            switch (choice)
            {
                case eAppThemeConfiguration.Dark: return "Dark";
                case eAppThemeConfiguration.Light: return "Light";
                default: return "System";
            }
        }
    }
}
