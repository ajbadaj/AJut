namespace AJutShowRoomUIKit.UI
{
    using System;
    using System.Collections.Generic;
    using AJutShowRoomUIKit.UI.Controls;
    using AJutShowRoomUIKit.UI.Diagnostics;
    using AJutShowRoomUIKit.UI.Pages;
    using AJutShowRoomUIKit.UI.UxUtils;
    using UIKit;

    /// <summary>
    /// The showroom's page map, the same one the WPF and WinUI3 showrooms follow, cut down to what applies to a
    /// phone: Home, Controls (one page per control), UX Utils, and Debug. Content Flows (docking) and Strategies have
    /// nothing phone sized in them yet, so they are left out rather than shown empty. A new page is one line in the
    /// area it belongs to.
    /// </summary>
    public static class ShowroomMap
    {
        public static IReadOnlyList<ShowroomEntry> Areas { get; } = new[]
        {
            new ShowroomEntry("Home", static () => new HomePage()),
            Area("Controls",
                new ShowroomEntry("BrushButton & AutoBrushButton", static () => new BrushButtonPage()),
                new ShowroomEntry("ColorEditIngressControl", static () => new ColorEditIngressControlPage()),
                new ShowroomEntry("GeometryButton", static () => new GeometryButtonPage()),
                new ShowroomEntry("NumericEditor", static () => new NumericEditorPage()),
                new ShowroomEntry("ToggleStrip", static () => new ToggleStripPage()),
                new ShowroomEntry("EnumToggleStrip & EnumComboBox", static () => new EnumPickersPage())
            ),
            Area("UX Utils",
                new ShowroomEntry("BindingSet", static () => new BindingSetPage())
            ),
            Area("Debug",
                new ShowroomEntry("Theme Tester", static () => new ThemeTesterPage())
            ),
        };

        private static ShowroomEntry Area (string title, params ShowroomEntry[] pages)
        {
            return new ShowroomEntry(title, () => new ShowroomListController(title, pages));
        }
    }

    /// <summary>One row in a showroom list: its title, and what opens when it is tapped.</summary>
    public sealed record ShowroomEntry (string Title, Func<UIViewController> Open);
}
