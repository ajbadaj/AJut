namespace AJutShowRoomAndroid
{
    using System;
    using System.Collections.Generic;
    using AJut.UX;
    using AJut.UX.CodeBinding;
    using AJut.UX.Helpers;
    using AJut.UX.Theming;
    using Android.Views;
    using Android.Widget;

    /// <summary>
    /// The showroom's page map. It follows the WinUI3 and WPF showrooms (Home, Controls, Content Flows, UX Utils,
    /// Strategies, Debug) with the same page names, cut down to what a phone gets: the areas with nothing in them
    /// on Android yet are left out rather than shown empty.
    /// </summary>
    public static class ShowroomMap
    {
        public static AreaPage CreateControlsArea () => new AreaPage(
            "Controls",
            new AreaEntry("BrushButton", () => new DemoPage("BrushButton", Resource.Layout.demo_brush_button, Resource.Raw.demo_brush_button_source)),
            new AreaEntry("ColorEditIngressControl", () => new ColorEditIngressControlPage()),
            new AreaEntry("EnumComboBox", () => new EnumComboBoxPage()),
            new AreaEntry("EnumToggleStrip", () => new EnumToggleStripPage()),
            new AreaEntry("GeometryButton", () => new DemoPage("GeometryButton", Resource.Layout.demo_geometry_button, Resource.Raw.demo_geometry_button_source)),
            new AreaEntry("NumericEditor", () => new NumericEditorPage()),
            new AreaEntry("ToggleStrip", () => new DemoPage("ToggleStrip", Resource.Layout.demo_toggle_strip, Resource.Raw.demo_toggle_strip_source))
        );

        public static AreaPage CreateDebugArea () => new AreaPage(
            "Debug",
            new AreaEntry("Theme Tester", () => new ThemeTesterPage())
        );

        /// <summary>
        /// Opens the page titled <paramref name="title"/> the way a user would reach it, area first, so back works
        /// as usual. An area's own title opens the area. Returns false when no page has that title.
        /// </summary>
        public static bool TryOpen (MainActivity host, string title)
        {
            foreach (AreaPage area in new[] { CreateControlsArea(), CreateDebugArea() })
            {
                if (string.Equals(title, area.Title, StringComparison.OrdinalIgnoreCase))
                {
                    host.Navigate(area);
                    return true;
                }

                AreaEntry? entry = area.FindEntry(title);
                if (entry != null)
                {
                    host.Navigate(area);
                    host.Navigate(entry.CreatePage());
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>One page an area lists, built when it is picked.</summary>
    public sealed record AreaEntry (string Title, Func<ShowroomPage> CreatePage);

    /// <summary>An area of the page map: a list of its pages, each opening on tap.</summary>
    public sealed class AreaPage : ShowroomPage
    {
        private readonly string m_title;
        private readonly AreaEntry[] m_entries;
        private ListView? m_list;
        private MainActivity? m_host;

        public AreaPage (string title, params AreaEntry[] entries)
        {
            m_title = title;
            m_entries = entries;
        }

        public override string Title => m_title;

        public override int LayoutId => Resource.Layout.page_area;

        public override void Bind (View root, MainActivity host, BindingSet binder)
        {
            m_host = host;
            m_list = root.FindViewById<ListView>(Resource.Id.area_pages)!;

            var titles = new List<string>(m_entries.Length);
            foreach (AreaEntry entry in m_entries)
            {
                titles.Add(entry.Title);
            }

            m_list.Adapter = new ArrayAdapter<string>(host, Android.Resource.Layout.SimpleListItem1, titles);
            m_list.ItemClick += this.OnItemClick;

            // Themed, the list of pages sits on the navigation level, as an area's page list does in the WinUI3 showroom
            if (ThemeColors.IsThemeLoaded
                && AJutPalette.For(ThemeColors.IsDark(host), ThemeColors.IsTouchFocused).TryGetColor("AJut_Brush_NavigationPanelBackground", out ColorArgb navigation))
            {
                m_list.SetBackgroundColor(navigation.ToAndroidColor());
            }
        }

        public AreaEntry? FindEntry (string title)
        {
            foreach (AreaEntry entry in m_entries)
            {
                if (string.Equals(entry.Title, title, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        public override void Unbind ()
        {
            if (m_list != null)
            {
                m_list.ItemClick -= this.OnItemClick;
                m_list = null;
            }

            m_host = null;
        }

        private void OnItemClick (object? sender, AdapterView.ItemClickEventArgs e)
        {
            m_host?.Navigate(m_entries[e.Position].CreatePage());
        }
    }

    /// <summary>The landing page: what the showroom is, and the way into each area.</summary>
    public sealed class HomePage : ShowroomPage
    {
        public override string Title => "AJut Showroom";

        public override int LayoutId => Resource.Layout.page_home;

        public override void Bind (View root, MainActivity host, BindingSet binder)
        {
            binder.OnClick(root.FindViewById(Resource.Id.home_controls)!, () => host.Navigate(ShowroomMap.CreateControlsArea()));
            binder.OnClick(root.FindViewById(Resource.Id.home_debug)!, () => host.Navigate(ShowroomMap.CreateDebugArea()));
        }
    }
}
