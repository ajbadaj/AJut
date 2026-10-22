namespace AJutShowRoomAppKit.UI
{
    using System;
    using System.Reflection;
    using AJut.UX.Controls;
    using AJut.UX.Theming;
    using AJutShowRoomAppKit.UI.Pages;
    using AppKit;
    using CoreGraphics;

    // The showroom shell: Home and the five areas down the left, the selected one on the right,
    // and the theme controls and library versions at the bottom of the list.
    public sealed class ShowroomWindow : ThemedWindow
    {
        private const string kHomeTitle = "Home";

        private static readonly eAppThemeConfiguration[] kThemeChoices = new[] { eAppThemeConfiguration.UseSameAsOS, eAppThemeConfiguration.Light, eAppThemeConfiguration.Dark };

        private readonly NavColumn m_areaNav;
        private readonly PageHost m_pageHost;
        private readonly NSPopUpButton m_themePicker;
        private readonly NSButton m_useThemesCheck;

        // ===========[ Setup/Construction/Teardown ]===========================
        public ShowroomWindow ()
            : base(new CGRect(0, 0, 1200, 800))
        {
            this.Title = "AJut Showroom (AppKit)";
            this.ContentMinSize = new CGSize(800, 500);
            ShowroomStyle.EnsureRegistered();

            // 1. Area list: Home first, then the shared areas
            m_areaNav = new NavColumn();
            m_areaNav.AddItem(kHomeTitle);
            foreach (ShowroomArea area in ShowroomAreas.All)
            {
                m_areaNav.AddItem(area.Title);
            }

            // 2. Footer: theme choice, themes on and off, versions
            m_themePicker = new NSPopUpButton(CGRect.Empty, false);
            m_themePicker.AddItems(new[] { "Same as the OS", "Light", "Dark" });
            m_themePicker.SelectItem(Array.IndexOf(kThemeChoices, AppDelegate.ThemeManager.ThemeConfiguration));
            m_themePicker.Activated += this.OnThemePickerActivated;
            m_areaNav.AddFooter(ShowroomStyle.Understated("Theme"));
            m_areaNav.AddFooter(m_themePicker);

            m_useThemesCheck = new NSButton { Title = "Use the AJut theme" };
            m_useThemesCheck.SetButtonType(NSButtonType.Switch);
            m_useThemesCheck.State = AppDelegate.UseThemes ? NSCellStateValue.On : NSCellStateValue.Off;
            m_useThemesCheck.Activated += this.OnUseThemesActivated;
            m_areaNav.AddFooter(m_useThemesCheck);

            m_areaNav.AddFooter(ShowroomStyle.Understated($"AJut.Core {VersionOf(typeof(AJut.Logger))}"));
            m_areaNav.AddFooter(ShowroomStyle.Understated($"AJut.UX.AppKit {VersionOf(typeof(ThemedColors))}"));

            // 3. Layout: list, divider, page
            m_pageHost = new PageHost();
            var root = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal);
            root.Add(m_areaNav);
            root.Add(ShowroomStyle.VerticalDivider());
            root.Add(m_pageHost, grows: true);
            this.WindowContents = root;

            m_areaNav.SelectionChanged += this.OnAreaSelectionChanged;
            this.Closed += this.OnClosed;
            m_areaNav.Select(0);
        }

        private void OnClosed (object sender, EventArgs e)
        {
            this.Closed -= this.OnClosed;
            m_areaNav.SelectionChanged -= this.OnAreaSelectionChanged;
            m_themePicker.Activated -= this.OnThemePickerActivated;
            m_useThemesCheck.Activated -= this.OnUseThemesActivated;
            m_areaNav.Teardown();
            m_pageHost.Clear();
        }

        // ===========[ Events ]================================================
        private void OnAreaSelectionChanged (object sender, EventArgs e)
        {
            this.OpenArea(m_areaNav.SelectedIndex);
        }

        private void OnThemePickerActivated (object sender, EventArgs e)
        {
            int index = (int)m_themePicker.IndexOfSelectedItem;
            if (index >= 0 && index < kThemeChoices.Length)
            {
                AppDelegate.ThemeManager.ThemeConfiguration = kThemeChoices[index];
            }
        }

        private void OnUseThemesActivated (object sender, EventArgs e)
        {
            AppDelegate.UseThemes = m_useThemesCheck.State == NSCellStateValue.On;
        }

        // ===========[ Helper Methods ]========================================
        // Index 0 is Home, the areas follow in ShowroomAreas order
        private void OpenArea (int navIndex)
        {
            if (navIndex == 0)
            {
                m_pageHost.Show(new HomePage(this.OpenAreaFromHome));
            }
            else
            {
                m_pageHost.Show(new AreaView(ShowroomAreas.All[navIndex - 1]));
            }
        }

        private void OpenAreaFromHome (int areaIndex)
        {
            m_areaNav.Select(areaIndex + 1);
        }

        private static string VersionOf (Type type) => type.Assembly.GetName().Version?.ToString() ?? "?";
    }
}
