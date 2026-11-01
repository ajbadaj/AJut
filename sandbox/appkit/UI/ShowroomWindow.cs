namespace AJutShowRoomAppKit.UI
{
    using System;
    using System.Reflection;
    using AJut.UX.Controls;
    using AJut.UX.Theming;
    using AJutShowRoomAppKit.UI.Pages;
    using AppKit;
    using CoreGraphics;

    // The showroom shell: the theme bar across the top, Home and the five areas down the left, the
    // selected one on the right, and the library versions at the bottom of the list.
    public sealed class ShowroomWindow : ThemedWindow
    {
        private const string kHomeTitle = "Home";

        private readonly ThemeBar m_themeBar;
        private readonly NavColumn m_areaNav;
        private readonly PageHost m_pageHost;

        // ===========[ Setup/Construction/Teardown ]===========================
        public ShowroomWindow ()
            : base(new CGRect(0, 0, 1200, 800))
        {
            this.Title = "AJut Showroom (AppKit)";
            this.ContentMinSize = new CGSize(800, 500);
            ShowroomStyle.EnsureRegistered();

            // 1. Area list: Home first, then the shared areas, on the shell's level with the theme bar above it
            m_areaNav = new NavColumn(ShowroomStyle.ShellBackground);
            m_areaNav.AddItem(kHomeTitle);
            foreach (ShowroomArea area in ShowroomAreas.All)
            {
                m_areaNav.AddItem(area.Title);
            }

            // 2. Footer: versions. The theme controls are in the theme bar.
            m_areaNav.AddFooter(ShowroomStyle.Understated($"AJut.Core {VersionOf(typeof(AJut.Logger))}"));
            m_areaNav.AddFooter(ShowroomStyle.Understated($"AJut.UX.AppKit {VersionOf(typeof(ThemedColors))}"));

            // 3. Layout: the theme bar over the list, divider and page
            m_themeBar = new ThemeBar();
            m_pageHost = new PageHost();
            var body = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal);
            body.Add(m_areaNav);
            body.Add(ShowroomStyle.VerticalDivider());
            body.Add(m_pageHost, grows: true);

            var root = new FillStack(NSUserInterfaceLayoutOrientation.Vertical);
            root.Add(m_themeBar);
            root.Add(ShowroomStyle.HorizontalDivider());
            root.Add(body, grows: true);
            this.WindowContents = root;

            m_areaNav.SelectionChanged += this.OnAreaSelectionChanged;
            this.Closed += this.OnClosed;
            m_areaNav.Select(0);
        }

        private void OnClosed (object sender, EventArgs e)
        {
            this.Closed -= this.OnClosed;
            m_areaNav.SelectionChanged -= this.OnAreaSelectionChanged;
            m_themeBar.Teardown();
            m_areaNav.Teardown();
            m_pageHost.Clear();
        }

        // ===========[ Events ]================================================
        private void OnAreaSelectionChanged (object sender, EventArgs e)
        {
            this.OpenArea(m_areaNav.SelectedIndex);
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
