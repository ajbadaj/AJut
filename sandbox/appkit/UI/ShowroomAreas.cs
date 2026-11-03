namespace AJutShowRoomAppKit.UI
{
    using System;
    using System.Collections.Generic;
    using AppKit;

    /// <summary>One page in an area: its name in the area's list, and how to build it</summary>
    public sealed class ShowroomPageEntry
    {
        public ShowroomPageEntry (string title, Func<NSView> create)
        {
            this.Title = title;
            this.Create = create;
        }

        public string Title { get; }
        public Func<NSView> Create { get; }
    }

    /// <summary>One of the showroom's areas, with the pages listed under it</summary>
    public sealed class ShowroomArea
    {
        public ShowroomArea (string title, string blurb, string otherStacksNote, params ShowroomPageEntry[] pages)
        {
            this.Title = title;
            this.Blurb = blurb;
            this.OtherStacksNote = otherStacksNote;
            this.Pages = pages;
        }

        public string Title { get; }
        public string Blurb { get; }

        /// <summary>What the WinUI3 and WPF showrooms have in this area that AppKit does not (yet)</summary>
        public string OtherStacksNote { get; }

        public IReadOnlyList<ShowroomPageEntry> Pages { get; }
    }

    // ===========[ ShowroomAreas ]==============================================
    // The page map, shared with the WinUI3 and WPF showrooms: the same areas with the same names,
    // and the same page names where a feature exists on more than one stack. A page goes in
    // here when its control lands on AppKit. Anything only AppKit has is labeled "(AppKit only)".
    public static class ShowroomAreas
    {
        public static IReadOnlyList<ShowroomArea> All { get; } = new[]
        {
            new ShowroomArea(
                "Controls",
                "The standalone controls, one page each.",
                "On WinUI3: GridSplitter.",
                new ShowroomPageEntry("AutoGrid", CreateAutoGridPage),
                new ShowroomPageEntry("BrushButton", CreateBrushButtonPage),
                new ShowroomPageEntry("BumpStack", CreateBumpStackPage),
                new ShowroomPageEntry("BusyWait", CreateBusyWaitPage),
                new ShowroomPageEntry("ColorEditIngressControl", CreateColorEditIngressControlPage),
                new ShowroomPageEntry("EditableTextBlock", CreateEditableTextBlockPage),
                new ShowroomPageEntry("EnumComboBox", CreateEnumComboBoxPage),
                new ShowroomPageEntry("EnumToggleStrip", CreateEnumToggleStripPage),
                new ShowroomPageEntry("FlatTreeListControl", CreateFlatTreeListControlPage),
                new ShowroomPageEntry("GeometryButton", CreateGeometryButtonPage),
                new ShowroomPageEntry("NumericEditor", CreateNumericEditorPage),
                new ShowroomPageEntry("PathSelectionControl", CreatePathSelectionControlPage),
                new ShowroomPageEntry("PopupMenuButton", CreatePopupMenuButtonPage),
                new ShowroomPageEntry("PropertyGrid", CreatePropertyGridPage),
                new ShowroomPageEntry("SimpleWrapPanel", CreateSimpleWrapPanelPage),
                new ShowroomPageEntry("Svg", CreateSvgPage),
                new ShowroomPageEntry("ThemedSplitView (AppKit only)", CreateThemedSplitViewPage),
                new ShowroomPageEntry("ToggleStrip", CreateToggleStripPage),
                new ShowroomPageEntry("UIElementRepeater", CreateUIElementRepeaterPage)
            ),
            new ShowroomArea(
                "Content Flows",
                "More than a control: the docking framework, with its models, controls and managers working together.",
                "On WPF: Stack Nav.",
                new ShowroomPageEntry("Docking Framework", CreateDockingFrameworkPage)
            ),
            new ShowroomArea(
                "UX Utils",
                "The code-side helpers: layout, theming lookups and color math.",
                "On WinUI3 and WPF: Converters and Code Helpers. On WPF: Attached Properties.",
                new ShowroomPageEntry("WindowManager", CreateWindowManagerPage)
            ),
            new ShowroomArea(
                "Strategies",
                "Worked examples: undo and redo, settings saved as JSON, layered overrides, or editing any object, each built with AJut.",
                "On WinUI3 and WPF: Undo and Redo, Settings as JSON, Layered Overrides with Stratabase, and Edit Any Object."
            ),
            new ShowroomArea(
                "Debug",
                "The probes, repros and diagnostics. Kept apart from the demos on purpose.",
                "On WinUI3: ThemeResource resolver, Palette Doubles, Window Chrome, and more. On WPF: Theme state.",
                new ShowroomPageEntry("Leak Probe", CreateLeakProbePage),
                new ShowroomPageEntry("Theme Tester", CreateThemeTesterPage),
                new ShowroomPageEntry("Window Chrome", CreateWindowChromePage)
            ),
        };

        // Named factories rather than lambdas, so the list reads as a table of pages
        private static NSView CreateAutoGridPage () => new Controls.AutoGridPage();
        private static NSView CreateBrushButtonPage () => new Controls.BrushButtonPage();
        private static NSView CreateGeometryButtonPage () => new Controls.GeometryButtonPage();
        private static NSView CreateThemedSplitViewPage () => new Controls.ThemedSplitViewPage();
        private static NSView CreateEnumComboBoxPage () => new Controls.EnumComboBoxPage();
        private static NSView CreateEnumToggleStripPage () => new Controls.EnumToggleStripPage();
        private static NSView CreateToggleStripPage () => new Controls.ToggleStripPage();
        private static NSView CreateUIElementRepeaterPage () => new Controls.UIElementRepeaterPage();
        private static NSView CreateNumericEditorPage () => new Controls.NumericEditorPage();
        private static NSView CreatePropertyGridPage () => new Controls.PropertyGridPage();
        private static NSView CreateColorEditIngressControlPage () => new Controls.ColorEditIngressControlPage();
        private static NSView CreateFlatTreeListControlPage () => new Controls.FlatTreeListControlPage();
        private static NSView CreateBusyWaitPage () => new Controls.BusyWaitPage();
        private static NSView CreateEditableTextBlockPage () => new Controls.EditableTextBlockPage();
        private static NSView CreatePopupMenuButtonPage () => new Controls.PopupMenuButtonPage();
        private static NSView CreateSimpleWrapPanelPage () => new Controls.SimpleWrapPanelPage();
        private static NSView CreateSvgPage () => new Controls.SvgPage();
        private static NSView CreateBumpStackPage () => new Controls.BumpStackPage();
        private static NSView CreatePathSelectionControlPage () => new Controls.PathSelectionControlPage();
        private static NSView CreateWindowManagerPage () => new Utilities.WindowManagerPage();
        private static NSView CreateLeakProbePage () => new Diagnostics.LeakProbePage();
        private static NSView CreateThemeTesterPage () => new Diagnostics.ThemeTesterPage();
        private static NSView CreateWindowChromePage () => new Diagnostics.WindowChromePage();
        private static NSView CreateDockingFrameworkPage () => new ContentFlows.DockingFrameworkPage();
    }
}
