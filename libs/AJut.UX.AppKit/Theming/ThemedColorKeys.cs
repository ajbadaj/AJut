namespace AJut.UX.Theming
{
    using AJut.UX.Helpers;
    using AppKit;

    // ===========[ ThemedColorKeys ]============================================
    // Every color key AJut's AppKit controls draw with, and the color each one shows when no theme
    // is loaded. This is the AppKit counterpart to WinUI3's AJutCustomControlStylingDefaults.xaml.
    //
    // The themed half is not here. Which palette entry a key resolves to when a theme is loaded
    // lives in AJut.UX (AJutThemeKeys and the palette tables), shared by every stack, so a key
    // means the same thing everywhere. The keys below use those shared names. The few that only
    // AppKit has (WinUI3 draws them from properties with no theme key) get an alias straight to a
    // palette key here, since no shared table knows them.
    //
    // Unthemed defaults are macOS system colors wherever one fits, so an unthemed AJut control
    // sits among stock AppKit controls without standing out. A literal is used only where WinUI3
    // also uses a literal and macOS has no matching system color (the close-hover red, for one).
    public static class ThemedColorKeys
    {
        // ===========[ ThemedWindow ]==========================================
        /// <summary>Window border while another window is key</summary>
        public const string ThemedWindow_InactiveBorder = "AJut_ThemedWindowRootControl_InactiveBorderBrush";

        /// <summary>Window border while the pointer is over the close button, the warning that a click closes the window</summary>
        public const string ThemedWindow_CloseHoverBorder = "AJut_ThemedWindowRootControl_CloseHoverBorderBrush";

        /// <summary>Title strip background while the window is key</summary>
        public const string ThemedWindow_ActiveTitleBarBackground = "AJut_ThemedWindowRootControl_ActiveTitleBarBackground";

        /// <summary>Title strip background while another window is key</summary>
        public const string ThemedWindow_InactiveTitleBarBackground = "AJut_ThemedWindowRootControl_InactiveTitlebarBackground";

        /// <summary>Window border while the window is key. AppKit only: WinUI3 takes this from the window's own BorderBrush.</summary>
        public const string ThemedWindow_ActiveBorder = "AJut_ThemedWindow_ActiveBorderBrush";

        /// <summary>Title text. AppKit only: WinUI3's title is whatever content the app puts in its title bar.</summary>
        public const string ThemedWindow_TitleForeground = "AJut_ThemedWindow_TitleForeground";

        /// <summary>The window's body background, under its contents. AppKit only: WinUI3 leaves it to the window's content.</summary>
        public const string ThemedWindow_Background = "AJut_ThemedWindow_Background";

        // ===========[ ThemedSplitView ]=======================================
        /// <summary>The divider between split panes. Its WinUI3 counterpart is GridSplitter's background.</summary>
        public const string Splitter_Divider = "AJut_ThemedSplitView_DividerBrush";

        // ===========[ GeometryButton ]========================================
        // The fills are AppKit only, each aliased to the Button* key the WinUI3 GeometryButton uses
        // for that state. They are separate keys because unthemed, a Button* key means a button
        // face, and a shape filled with a button face color barely shows; unthemed, these are label
        // colors instead. The traces are shared keys, the WinUI3 GeometryButton's: the shape's
        // outline is part of the glyph, so they take the common borders, which the touch palettes
        // leave alone, rather than the button borders, which touch takes off.

        /// <summary>The shape's fill at rest</summary>
        public const string GeometryButton_Fill = "AJut_GeometryButton_Fill";

        /// <summary>The shape's fill with the pointer over it</summary>
        public const string GeometryButton_FillPointerOver = "AJut_GeometryButton_FillPointerOver";

        /// <summary>The shape's fill while pressed</summary>
        public const string GeometryButton_FillPressed = "AJut_GeometryButton_FillPressed";

        /// <summary>The shape's fill while disabled</summary>
        public const string GeometryButton_FillDisabled = "AJut_GeometryButton_FillDisabled";

        /// <summary>The traced outline at rest. Shared key.</summary>
        public const string GeometryButton_Trace = "AJut_GeometryButton_Trace";

        /// <summary>The traced outline with the pointer over it. Shared key.</summary>
        public const string GeometryButton_TracePointerOver = "AJut_GeometryButton_TracePointerOver";

        /// <summary>The traced outline while pressed. Shared key.</summary>
        public const string GeometryButton_TracePressed = "AJut_GeometryButton_TracePressed";

        /// <summary>The traced outline while disabled. Shared key.</summary>
        public const string GeometryButton_TraceDisabled = "AJut_GeometryButton_TraceDisabled";

        // The title drawn over the shape, AppKit only, each aliased to the ButtonForeground* key the WinUI3
        // GeometryButton's content takes for that state. Unthemed the shape is a label color, so the title is the
        // window background, which reads on a label gray in light and dark alike.

        /// <summary>The title at rest</summary>
        public const string GeometryButton_Foreground = "AJut_GeometryButton_Foreground";

        /// <summary>The title with the pointer over it</summary>
        public const string GeometryButton_ForegroundPointerOver = "AJut_GeometryButton_ForegroundPointerOver";

        /// <summary>The title while pressed</summary>
        public const string GeometryButton_ForegroundPressed = "AJut_GeometryButton_ForegroundPressed";

        /// <summary>The title while disabled</summary>
        public const string GeometryButton_ForegroundDisabled = "AJut_GeometryButton_ForegroundDisabled";

        // ===========[ BrushButton ]===========================================
        // AppKit only, each aliased to the Button* key the WinUI3 BrushButton's default style uses
        // for that part and state. Unthemed, they are macOS control colors, with translucent label
        // grays for hover and pressed since macOS has no hover or pressed face color of its own.

        public const string BrushButton_NormalBackground = "AJut_BrushButton_NormalBackground";
        public const string BrushButton_NormalForeground = "AJut_BrushButton_NormalForeground";
        public const string BrushButton_NormalBorder = "AJut_BrushButton_NormalBorder";
        public const string BrushButton_PointerOverBackground = "AJut_BrushButton_PointerOverBackground";
        public const string BrushButton_PointerOverForeground = "AJut_BrushButton_PointerOverForeground";
        public const string BrushButton_PointerOverBorder = "AJut_BrushButton_PointerOverBorder";
        public const string BrushButton_PressedBackground = "AJut_BrushButton_PressedBackground";
        public const string BrushButton_PressedForeground = "AJut_BrushButton_PressedForeground";
        public const string BrushButton_PressedBorder = "AJut_BrushButton_PressedBorder";
        public const string BrushButton_DisabledBackground = "AJut_BrushButton_DisabledBackground";
        public const string BrushButton_DisabledForeground = "AJut_BrushButton_DisabledForeground";
        public const string BrushButton_DisabledBorder = "AJut_BrushButton_DisabledBorder";

        // ===========[ AutoBrushButton ]=======================================
        /// <summary>
        /// The accent an AutoBrushButton derives its colors from when none is set. Shared key, the one WinUI3's
        /// AutoBrushButton takes: the AJut accent with the theme loaded, and the macOS accent the user picks without it.
        /// </summary>
        public const string AutoBrushButton_Accent = "SystemAccentColor";

        // ===========[ ToggleStrip ]===========================================
        // The button backgrounds are shared keys (WinUI3's AJutThemeMapAliasing has them, and the
        // resting checked one is a gradient), and so is the name's color over the pointer-over and
        // pressed fills. The strip's own background and border, and the other name colors, are AppKit
        // only: WinUI3 sets those through style setters rather than keys.

        public const string ToggleStripButton_PointerOverBackground = "AJut_ToggleStripButton_PointerOverBackground";
        public const string ToggleStripButton_PressedBackground = "AJut_ToggleStripButton_PressedBackground";
        public const string ToggleStripButton_CheckedBackground = "AJut_ToggleStripButton_CheckedBackground";
        public const string ToggleStripButton_CheckedPointerOverBackground = "AJut_ToggleStripButton_CheckedPointerOverBackground";
        public const string ToggleStripButton_CheckedPressedBackground = "AJut_ToggleStripButton_CheckedPressedBackground";

        /// <summary>A selected item's fill while the strip is disabled: the muted primary. Shared with WinUI3.</summary>
        public const string ToggleStripButton_CheckedDisabledBackground = "AJut_ToggleStripButton_CheckedDisabledBackground";

        /// <summary>An item's name over the pointer-over and pressed fills. Shared with WinUI3.</summary>
        public const string ToggleStripButton_HighlightForeground = "AJut_ToggleStripButton_HighlightForeground";

        /// <summary>Every item's name while the strip is disabled: the understated text, faded. Shared with WinUI3.</summary>
        public const string ToggleStrip_DisabledForeground = "AJut_ToggleStrip_DisabledForeground";

        /// <summary>An item's name while it is not selected. AppKit only.</summary>
        public const string ToggleStripButton_Foreground = "AJut_ToggleStripButton_Foreground";

        /// <summary>An item's name while it is selected. AppKit only: unthemed, it sits on the accent color, so it is white.</summary>
        public const string ToggleStripButton_CheckedForeground = "AJut_ToggleStripButton_CheckedForeground";

        /// <summary>The strip's background behind its items. AppKit only.</summary>
        public const string ToggleStrip_Background = "AJut_ToggleStrip_Background";

        /// <summary>The strip's outline and the separators between items. AppKit only.</summary>
        public const string ToggleStrip_Border = "AJut_ToggleStrip_Border";

        /// <summary>The strip's background while it is disabled: the flat fill a disabled ComboBox takes, as on every stack. AppKit only.</summary>
        public const string ToggleStrip_DisabledBackground = "AJut_ToggleStrip_DisabledBackground";

        /// <summary>The strip's outline and the separators between items while it is disabled: a disabled ComboBox's border. AppKit only.</summary>
        public const string ToggleStrip_DisabledBorder = "AJut_ToggleStrip_DisabledBorder";

        /// <summary>The dots just outside a Scroll strip's ends that show it scrolls further that way. AppKit and UIKit only.</summary>
        public const string ToggleStrip_ScrollIndicator = "AJut_ToggleStrip_ScrollIndicator";

        // ===========[ NumericEditor ]=========================================
        // The look MobileLook.NumericEditor sets out for every mobile and Mac editor. The AppKit only keys alias its keys.

        /// <summary>The frame's border while the field has focus. Shared key.</summary>
        public const string NumericEditor_FocusBorder = "AJut_NumericEditor_FocusBorderBrush";

        /// <summary>The frame's border at rest. Shared key.</summary>
        public const string NumericEditor_NormalBorder = "AJut_NumericEditor_NormalBorderBrush";

        /// <summary>The frame's border while the mouse is over it, focused or not. Shared key.</summary>
        public const string NumericEditor_PointerOverBorder = MobileLook.NumericEditor.FrameBorderPointerOver;

        /// <summary>The frame's border, and the warning glyph beside the number, while the text is out of range. Shared key.</summary>
        public const string NumericEditor_ErrorBorder = "AJut_NumericEditor_ErrorBrush";

        /// <summary>The frame's surface, a gradient themed. AppKit only, as MobileLook.NumericEditor.FrameSurface.</summary>
        public const string NumericEditor_FrameBackground = "AJut_NumericEditor_FrameBackground";

        /// <summary>The well the number sits in. AppKit only, as MobileLook.NumericEditor.WellSurface.</summary>
        public const string NumericEditor_TextBoxBackground = "AJut_NumericEditor_TextBoxBackground";

        /// <summary>The typed text. AppKit only, as MobileLook.NumericEditor.Number.</summary>
        public const string NumericEditor_Text = "AJut_NumericEditor_Text";

        /// <summary>The number, and the step glyphs, while the editor is read-only. AppKit only, as MobileLook.NumericEditor.ReadOnlyText.</summary>
        public const string NumericEditor_ReadOnlyText = "AJut_NumericEditor_ReadOnlyText";

        /// <summary>The label in front of the frame, when one is given. AppKit only, as MobileLook.NumericEditor.Label.</summary>
        public const string NumericEditor_Label = "AJut_NumericEditor_Label";

        /// <summary>The step buttons' minus and plus. AppKit only, as MobileLook.NumericEditor.StepGlyph.</summary>
        public const string NumericEditor_StepGlyph = "AJut_NumericEditor_StepGlyph";

        /// <summary>A step button's fill under the mouse, drawn at MobileLook.NumericEditor.StepPointerOverOpacity. Shared key.</summary>
        public const string NumericEditor_StepPointerOverFill = MobileLook.NumericEditor.StepPointerOverFill;

        /// <summary>A step button's fill while pressed. Shared key.</summary>
        public const string NumericEditor_StepPressedFill = MobileLook.NumericEditor.StepPressedFill;

        // ===========[ FlatTreeListView ]======================================
        /// <summary>A selected row's leading strip and outline while its window is key. Shared key.</summary>
        public const string FlatTree_Selection = "AJut_FlatTree_SelectionBrush";

        /// <summary>A selected row's leading strip and outline while the list is not the focused control of the key window. Shared key.</summary>
        public const string FlatTree_SelectionUnfocused = "AJut_FlatTree_SelectionBrushUnfocused";

        /// <summary>A selected row's fill. Shared key.</summary>
        public const string FlatTree_SelectionBackground = "AJut_FlatTree_SelectionBackground";

        /// <summary>A selected row's leading strip and outline while the pointer is over it; its fill stays. Shared key.</summary>
        public const string FlatTree_SelectionPointerOver = "AJut_FlatTree_SelectionPointerOverBrush";

        /// <summary>The fill of the row under the pointer. Shared key.</summary>
        public const string FlatTree_HoverBackground = "AJut_FlatTree_HoverBackground";

        /// <summary>The outline of the row under the pointer. Shared key.</summary>
        public const string FlatTree_HoverBorder = "AJut_FlatTree_HoverBorderBrush";

        /// <summary>The line drawn where a dragged row will land. Shared key.</summary>
        public const string FlatTree_DragInsertionLine = "AJut_FlatTree_DragInsertionLine";

        /// <summary>The dashed line from the drop's parent row to the insertion line. Shared key.</summary>
        public const string FlatTree_DragParentConnector = "AJut_FlatTree_DragParentConnector";

        /// <summary>The drop's parent row's chevron while a drag is over it. Shared key.</summary>
        public const string FlatTree_DragTargetHighlight = "AJut_FlatTree_DragTargetHighlight";

        /// <summary>The expand/collapse chevron. Themed, it is the symbol foreground the WinUI3 tree's chevron uses. AppKit only.</summary>
        public const string FlatTree_ExpanderGlyph = "AJut_FlatTree_ExpanderGlyph";

        /// <summary>The chevron while the pointer is over it or it is pressed, lighter than at rest. Shared key.</summary>
        public const string FlatTree_ExpanderGlyphPointerOver = "AJut_FlatTreeItemRow_ExpanderGlyphPointerOverForeground";

        /// <summary>The fill behind the chevron while the pointer is over it. Shared key.</summary>
        public const string FlatTree_ExpanderPointerOverBackground = "AJut_FlatTreeItemRow_ExpanderPointerOverBackground";

        /// <summary>The fill behind the chevron while it is pressed. Shared key.</summary>
        public const string FlatTree_ExpanderPressedBackground = "AJut_FlatTreeItemRow_ExpanderPressedBackground";

        /// <summary>The list's fill. AppKit only: WinUI3's theme style sets FlatTreeListControl's Background, clear without a theme.</summary>
        public const string FlatTree_ListBackground = "AJut_FlatTree_ListBackground";

        /// <summary>The list's outline. AppKit only: WinUI3's theme style sets FlatTreeListControl's BorderBrush, none without a theme.</summary>
        public const string FlatTree_ListBorder = "AJut_FlatTree_ListBorder";

        /// <summary>The fill of the label that follows the pointer while rows are dragged. Shared key.</summary>
        public const string FlatTree_DragGhostBackground = "AJut_FlatTree_DragGhostBackground";

        /// <summary>The outline of the label that follows the pointer while rows are dragged. Shared key.</summary>
        public const string FlatTree_DragGhostBorder = "AJut_FlatTree_DragGhostBorderBrush";

        /// <summary>The text of the label that follows the pointer while rows are dragged. Shared key.</summary>
        public const string FlatTree_DragGhostForeground = "AJut_FlatTree_DragGhostForeground";

        // ===========[ PropertyGrid ]==========================================
        /// <summary>A property row's name, and, faded, its subtitle, a list element's header text and a list's element count. Shared key.</summary>
        public const string PropertyGridRow_LabelForeground = "AJut_PropertyGridItemRow_LabelForeground";

        /// <summary>The leading nub and outline around a selected row's label. Shared key.</summary>
        public const string PropertyGridRow_SelectionBorder = "AJut_PropertyGridItemRow_SelectionBorderBrush";

        /// <summary>The line the label splitter shows over the label and editor boundary. Shared key.</summary>
        public const string PropertyGrid_LabelSplitter = "AJut_PropertyGrid_LabelSplitterBrush";

        /// <summary>The "Not set" text of a null nullable value. Shared key.</summary>
        public const string NullableEditor_NotSetForeground = "AJut_NullableEditor_NotSetForeground";

        /// <summary>The shade over a null nullable value's dimmed editor. Shared key, the same dark shade in every theme.</summary>
        public const string NullableEditor_OverlayBackground = "AJut_NullableEditor_OverlayBackground";

        // ===========[ ColorEditIngressControl ]===============================
        /// <summary>The swatch border at rest. Shared key.</summary>
        public const string ColorEditIngress_NormalBorder = "AJut_ColorEditIngressControl_NormalBorderBrush";

        /// <summary>The swatch border while it has the color panel. Shared key.</summary>
        public const string ColorEditIngress_HighlightBorder = "AJut_ColorEditIngressControl_HighlightBorderBrush";

        // ===========[ DockZoneView ]==========================================
        /// <summary>An empty zone's fill. Shared key.</summary>
        public const string DockZone_EmptyBackground = "AJut_DockZone_EmptyBackground";

        /// <summary>An empty zone's outline. Shared key.</summary>
        public const string DockZone_EmptyBorder = "AJut_DockZone_EmptyBorderBrush";

        /// <summary>An empty zone's placeholder text. Shared key.</summary>
        public const string DockZone_EmptyForeground = "AJut_DockZone_EmptyForeground";

        /// <summary>A panel zone's fill. AppKit only: WinUI3 sets it on DockLeafLayout by style.</summary>
        public const string DockZone_PanelBackground = "AJut_DockZone_PanelBackground";

        /// <summary>A panel zone's outline, the line between its header and its content, and the tabs' edges. AppKit only: WinUI3 sets it on DockLeafLayout and DockTabItem by style.</summary>
        public const string DockZone_PanelBorder = "AJut_DockZone_PanelBorderBrush";

        /// <summary>The header bar above the shown panel, a level apart from the panel, as the unselected tabs are. AppKit only.</summary>
        public const string DockZone_HeaderBackground = "AJut_DockZone_HeaderBackground";

        /// <summary>Header and tab titles, and the close X beside them. AppKit only.</summary>
        public const string DockZone_HeaderForeground = "AJut_DockZone_HeaderForeground";

        /// <summary>The header bar while its panel is the one being typed into. AppKit only: WinUI3 sets it on DockLeafLayout by style.</summary>
        public const string DockZone_HeaderFocusedBackground = "AJut_DockZone_HeaderFocusedBackground";

        /// <summary>The header's title and close X while its panel is the one being typed into. AppKit only.</summary>
        public const string DockZone_HeaderFocusedForeground = "AJut_DockZone_HeaderFocusedForeground";

        /// <summary>
        /// The tab strip behind the tabs: clear themed or not, as WinUI3's TabStripBackground is, so the strip shows what
        /// is behind the zone. AppKit only; give it a default or an alias to fill the strip.
        /// </summary>
        public const string DockZone_TabStripBackground = "AJut_DockZone_TabStripBackground";

        /// <summary>An unselected tab, a level apart from the panel, as the header is. AppKit only.</summary>
        public const string DockZone_TabBackground = "AJut_DockZone_TabBackground";

        /// <summary>The selected tab, in the panel's own fill so the tab reads as part of the panel above it. AppKit only.</summary>
        public const string DockZone_SelectedTabBackground = "AJut_DockZone_SelectedTabBackground";

        /// <summary>
        /// The fill over a tab, selected or not, while the pointer is over it: the muted primary, which says the tab can
        /// be dragged off. AppKit only: WinUI3 sets it on DockTabItem by style.
        /// </summary>
        public const string DockZone_TabPointerOverBackground = "AJut_DockZone_TabPointerOverBackground";

        /// <summary>The line where a tab dragged along the strip will land. Shared key.</summary>
        public const string DockZone_DragInsertLine = "AJut_DockZone_DragInsertLineBrush";

        /// <summary>The copy of a tab that follows the pointer while it is dragged along the strip. Shared key.</summary>
        public const string DockZone_DragGhostBackground = "AJut_DockZone_DragGhostBackground";

        /// <summary>The outline of the copy of a tab that follows the pointer along the strip. Shared key.</summary>
        public const string DockZone_DragGhostBorder = "AJut_DockZone_DragGhostBorderBrush";

        // ===========[ DockDropOverlayView ]===================================
        // Shared keys, the WinUI3 DockDropInsertionDriverWidget's

        public const string DockDropWidget_BorderIdle = "AJut_DockDropInsertionDriverWidget_BorderIdleBrush";
        public const string DockDropWidget_BackgroundIdle = "AJut_DockDropInsertionDriverWidget_BackgroundIdleBrush";
        public const string DockDropWidget_GlyphIdle = "AJut_DockDropInsertionDriverWidget_GlyphIdleBrush";
        public const string DockDropWidget_BorderEngaged = "AJut_DockDropInsertionDriverWidget_BorderEngagedBrush";
        public const string DockDropWidget_BackgroundEngaged = "AJut_DockDropInsertionDriverWidget_BackgroundEngagedBrush";
        public const string DockDropWidget_GlyphEngaged = "AJut_DockDropInsertionDriverWidget_GlyphEngagedBrush";

        // ===========[ DockPanelToolbarView ]==================================
        // Shared keys, the WinUI3 toolbar's and buttons'

        public const string DockPanelToolbar_Background = "AJut_DockPanelToolbar_NormalBackground";
        public const string DockPanelToolbar_Border = "AJut_DockPanelToolbar_NormalBorderBrush";

        public const string DockPanelAddButton_NormalBackground = "AJut_DockPanelAddButton_NormalBackground";
        public const string DockPanelAddButton_NormalBorder = "AJut_DockPanelAddButton_NormalBorderBrush";
        public const string DockPanelAddButton_NormalForeground = "AJut_DockPanelAddButton_NormalForeground";
        public const string DockPanelAddButton_PointerOverBackground = "AJut_DockPanelAddButton_PointerOverBackground";
        public const string DockPanelAddButton_PressedBackground = "AJut_DockPanelAddButton_PressedBackground";

        /// <summary>The small plus marking an add button</summary>
        public const string DockPanelAddButton_PlusOverlayForeground = "AJut_DockPanelAddButton_PlusOverlayForeground";

        public const string DockPanelToggleButton_NormalBackground = "AJut_DockPanelToggleButton_NormalBackground";
        public const string DockPanelToggleButton_NormalBorder = "AJut_DockPanelToggleButton_NormalBorderBrush";
        public const string DockPanelToggleButton_NormalForeground = "AJut_DockPanelToggleButton_NormalForeground";
        public const string DockPanelToggleButton_PointerOverBackground = "AJut_DockPanelToggleButton_PointerOverBackground";
        public const string DockPanelToggleButton_PressedBackground = "AJut_DockPanelToggleButton_PressedBackground";
        public const string DockPanelToggleButton_CheckedBackground = "AJut_DockPanelToggleButton_CheckedBackground";
        public const string DockPanelToggleButton_CheckedBorder = "AJut_DockPanelToggleButton_CheckedBorderBrush";
        public const string DockPanelToggleButton_CheckedPointerOverBackground = "AJut_DockPanelToggleButton_CheckedPointerOverBackground";
        public const string DockPanelToggleButton_CheckedPointerOverBorder = "AJut_DockPanelToggleButton_CheckedPointerOverBorderBrush";
        public const string DockPanelToggleButton_CheckedPressedBackground = "AJut_DockPanelToggleButton_CheckedPressedBackground";
        public const string DockPanelToggleButton_CheckedPressedBorder = "AJut_DockPanelToggleButton_CheckedPressedBorderBrush";

        // ===========[ DockTabScrollButton ]===================================
        // The arrows at either end of a dock tab strip with more tabs than fit. The fills are shared keys, the WinUI3
        // DockTabScrollButton's; the arrow is AppKit only, since WinUI3's takes the dock's text color by inheritance.

        /// <summary>A tab strip scroll arrow. AppKit only: WinUI3's inherits the dock's normal text.</summary>
        public const string DockTabScrollButton_Glyph = "AJut_DockTabScrollButton_Glyph";

        /// <summary>A tab strip scroll arrow's fill with the pointer over it. Shared key.</summary>
        public const string DockTabScrollButton_HoverBackground = "AJut_DockTabScrollButton_HoverBackground";

        /// <summary>A tab strip scroll arrow's fill while pressed. Shared key.</summary>
        public const string DockTabScrollButton_PressedBackground = "AJut_DockTabScrollButton_PressedBackground";

        // ===========[ DockCloseButton ]=======================================
        // The X on a dock panel's header and on each closable tab. The fills are shared keys, the WinUI3 DockCloseButton's;
        // the X takes the header text, DockZone_HeaderForeground, as WinUI3's inherits the text beside it.

        /// <summary>A dock close X's fill with the pointer over it. Shared key.</summary>
        public const string DockCloseButton_HoverBackground = "AJut_DockCloseButton_HoverBackground";

        /// <summary>A dock close X's fill while pressed. Shared key.</summary>
        public const string DockCloseButton_PressedBackground = "AJut_DockCloseButton_PressedBackground";

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Registers every key above with <see cref="ThemedColors"/>. Runs once, from ThemedColors' static
        /// constructor; calling it again puts back any default or alias a consumer has replaced.
        /// </summary>
        public static void RegisterAll ()
        {
            // 1. ThemedWindow, shared keys
            ThemedColors.SetUnthemedDefault(ThemedWindow_InactiveBorder, NSColor.Separator);
            ThemedColors.SetUnthemedDefault(ThemedWindow_CloseHoverBorder, Srgb(0xFF892630));
            ThemedColors.SetUnthemedDefault(ThemedWindow_ActiveTitleBarBackground, NSColor.WindowBackground);
            ThemedColors.SetUnthemedDefault(ThemedWindow_InactiveTitleBarBackground, NSColor.WindowBackground);

            // 2. ThemedWindow, AppKit only
            RegisterAppKitOnly(ThemedWindow_ActiveBorder, NSColor.Separator, "AJut_Brush_WindowGlowBorder");
            RegisterAppKitOnly(ThemedWindow_TitleForeground, NSColor.Label, "AJut_Brush_TitleBarForeground");
            RegisterAppKitOnly(ThemedWindow_Background, NSColor.WindowBackground, "AJut_Brush_RootPanelBackground");

            // 3. ThemedSplitView, AppKit only
            RegisterAppKitOnly(Splitter_Divider, NSColor.Separator, "AJut_Brush_BorderCommon");

            // 4. GeometryButton, AppKit only fills and shared traces
            RegisterAppKitOnly(GeometryButton_Fill, NSColor.SecondaryLabel, "ButtonBackground");
            RegisterAppKitOnly(GeometryButton_FillPointerOver, NSColor.Label, "ButtonBackgroundPointerOver");
            RegisterAppKitOnly(GeometryButton_FillPressed, NSColor.ControlAccent, "ButtonBackgroundPressed");
            RegisterAppKitOnly(GeometryButton_FillDisabled, NSColor.TertiaryLabel, "ButtonBackgroundDisabled");
            RegisterAppKitOnly(GeometryButton_Foreground, NSColor.WindowBackground, "ButtonForeground");
            RegisterAppKitOnly(GeometryButton_ForegroundPointerOver, NSColor.WindowBackground, "ButtonForegroundPointerOver");
            RegisterAppKitOnly(GeometryButton_ForegroundPressed, NSColor.WindowBackground, "ButtonForegroundPressed");
            RegisterAppKitOnly(GeometryButton_ForegroundDisabled, NSColor.WindowBackground, "ButtonForegroundDisabled");
            ThemedColors.SetUnthemedDefault(GeometryButton_Trace, NSColor.Separator);
            ThemedColors.SetUnthemedDefault(GeometryButton_TracePointerOver, NSColor.SecondaryLabel);
            ThemedColors.SetUnthemedDefault(GeometryButton_TracePressed, NSColor.ControlAccent);
            ThemedColors.SetUnthemedDefault(GeometryButton_TraceDisabled, NSColor.QuaternaryLabel);

            // 5. BrushButton, AppKit only
            RegisterAppKitOnly(BrushButton_NormalBackground, NSColor.Control, "ButtonBackground");
            RegisterAppKitOnly(BrushButton_NormalForeground, NSColor.ControlText, "ButtonForeground");
            RegisterAppKitOnly(BrushButton_NormalBorder, NSColor.Separator, "ButtonBorderBrush");
            RegisterAppKitOnly(BrushButton_PointerOverBackground, NSColor.QuaternaryLabel, "ButtonBackgroundPointerOver");
            RegisterAppKitOnly(BrushButton_PointerOverForeground, NSColor.ControlText, "ButtonForegroundPointerOver");
            RegisterAppKitOnly(BrushButton_PointerOverBorder, NSColor.SecondaryLabel, "ButtonBorderBrushPointerOver");
            RegisterAppKitOnly(BrushButton_PressedBackground, NSColor.TertiaryLabel, "ButtonBackgroundPressed");
            RegisterAppKitOnly(BrushButton_PressedForeground, NSColor.ControlText, "ButtonForegroundPressed");
            RegisterAppKitOnly(BrushButton_PressedBorder, NSColor.SecondaryLabel, "ButtonBorderBrushPressed");
            RegisterAppKitOnly(BrushButton_DisabledBackground, NSColor.Control, "ButtonBackgroundDisabled");
            RegisterAppKitOnly(BrushButton_DisabledForeground, NSColor.DisabledControlText, "ButtonForegroundDisabled");
            RegisterAppKitOnly(BrushButton_DisabledBorder, NSColor.QuaternaryLabel, "ButtonBorderBrushDisabled");

            // 6. ToggleStrip, shared keys
            ThemedColors.SetUnthemedDefault(ToggleStripButton_PointerOverBackground, NSColor.QuaternaryLabel);
            ThemedColors.SetUnthemedDefault(ToggleStripButton_PressedBackground, NSColor.TertiaryLabel);
            ThemedColors.SetUnthemedDefault(ToggleStripButton_CheckedBackground, NSColor.ControlAccent);
            ThemedColors.SetUnthemedDefault(ToggleStripButton_CheckedPointerOverBackground, NSColor.ControlAccent);
            ThemedColors.SetUnthemedDefault(ToggleStripButton_CheckedPressedBackground, NSColor.ControlAccent);
            const double kDisabledSelectionOpacity = 0.4;
            ThemedColors.SetUnthemedDefault(ToggleStripButton_CheckedDisabledBackground, NSColor.ControlAccent.ColorWithAlphaComponent((nfloat)kDisabledSelectionOpacity));
            ThemedColors.SetUnthemedDefault(ToggleStripButton_HighlightForeground, NSColor.ControlText);
            ThemedColors.SetUnthemedDefault(ToggleStrip_DisabledForeground, NSColor.DisabledControlText);

            // 7. ToggleStrip, AppKit only
            RegisterAppKitOnly(ToggleStripButton_Foreground, NSColor.ControlText, "AJut_Brush_NormalText");
            RegisterAppKitOnly(ToggleStripButton_CheckedForeground, NSColor.AlternateSelectedControlText, "AJut_Brush_NormalText");
            RegisterAppKitOnly(ToggleStrip_Background, NSColor.Control, "AJut_Brush_PopOutControlBackground");
            RegisterAppKitOnly(ToggleStrip_Border, NSColor.Separator, "AJut_Brush_BorderCommon");
            RegisterAppKitOnly(ToggleStrip_DisabledBackground, NSColor.Control, MobileLook.ToggleStrip.StripDisabledSurface);
            RegisterAppKitOnly(ToggleStrip_DisabledBorder, NSColor.Separator, MobileLook.ToggleStrip.StripDisabledBorder);
            RegisterAppKitOnly(ToggleStrip_ScrollIndicator, NSColor.SecondaryLabel, "AJut_Brush_SymbolForeground");

            // 8. NumericEditor, shared keys
            ThemedColors.SetUnthemedDefault(NumericEditor_FocusBorder, NSColor.KeyboardFocusIndicator);
            ThemedColors.SetUnthemedDefault(NumericEditor_NormalBorder, NSColor.Separator);
            ThemedColors.SetUnthemedDefault(NumericEditor_PointerOverBorder, NSColor.ControlAccent);
            ThemedColors.SetUnthemedDefault(NumericEditor_ErrorBorder, NSColor.SystemRed);
            ThemedColors.SetUnthemedDefault(NumericEditor_StepPointerOverFill, NSColor.ControlAccent);

            // 9. FlatTreeListView, shared keys
            ThemedColors.SetUnthemedDefault(FlatTree_Selection, NSColor.ControlAccent);
            ThemedColors.SetUnthemedDefault(FlatTree_SelectionUnfocused, NSColor.SecondaryLabel);
            ThemedColors.SetUnthemedDefault(FlatTree_SelectionBackground, NSColor.UnemphasizedSelectedContentBackground);
            ThemedColors.SetUnthemedDefault(FlatTree_SelectionPointerOver, NSColor.SelectedContentBackground);
            ThemedColors.SetUnthemedDefault(FlatTree_HoverBackground, NSColor.QuaternaryLabel);
            ThemedColors.SetUnthemedDefault(FlatTree_HoverBorder, NSColor.Separator);
            ThemedColors.SetUnthemedDefault(FlatTree_DragInsertionLine, NSColor.ControlAccent);
            const double kDragParentConnectorOpacity = 0.5; // the WinUI3 connector brush's opacity
            ThemedColors.SetUnthemedDefault(FlatTree_DragParentConnector, NSColor.ControlAccent.ColorWithAlphaComponent((nfloat)kDragParentConnectorOpacity));
            ThemedColors.SetUnthemedDefault(FlatTree_DragTargetHighlight, NSColor.ControlAccent);
            RegisterAppKitOnly(FlatTree_ExpanderGlyph, NSColor.SecondaryLabel, "AJut_Brush_SymbolForeground");
            ThemedColors.SetUnthemedDefault(FlatTree_ExpanderGlyphPointerOver, NSColor.Label);
            ThemedColors.SetUnthemedDefault(FlatTree_ExpanderPointerOverBackground, NSColor.QuaternaryLabel);
            ThemedColors.SetUnthemedDefault(FlatTree_ExpanderPressedBackground, NSColor.TertiaryLabel);

            //     The list's fill and outline are WinUI3's theme style's, and clear unthemed as WinUI3's default style leaves
            //     them. The drag ghost takes the flagships' themed ghost keys; unthemed, a control face with a label.
            RegisterAppKitOnly(FlatTree_ListBackground, NSColor.Clear, "AJut_Brush_ListBackground");
            RegisterAppKitOnly(FlatTree_ListBorder, NSColor.Clear, "AJut_Brush_ListBorder");
            ThemedColors.SetUnthemedDefault(FlatTree_DragGhostBackground, NSColor.ControlBackground);
            ThemedColors.SetUnthemedDefault(FlatTree_DragGhostBorder, NSColor.Separator);
            ThemedColors.SetUnthemedDefault(FlatTree_DragGhostForeground, NSColor.Label);

            // 10. PropertyGrid. Unthemed, the null shade is WinUI3's literal, which macOS has no system color for.
            ThemedColors.SetUnthemedDefault(PropertyGridRow_LabelForeground, NSColor.Label);
            ThemedColors.SetUnthemedDefault(PropertyGridRow_SelectionBorder, NSColor.ControlAccent);
            ThemedColors.SetUnthemedDefault(PropertyGrid_LabelSplitter, NSColor.ControlAccent);
            ThemedColors.SetUnthemedDefault(NullableEditor_NotSetForeground, NSColor.SecondaryLabel);
            ThemedColors.SetUnthemedDefault(NullableEditor_OverlayBackground, Srgb(0x88000000));

            // 11. ColorEditIngressControl, shared keys
            ThemedColors.SetUnthemedDefault(ColorEditIngress_NormalBorder, NSColor.Separator);
            ThemedColors.SetUnthemedDefault(ColorEditIngress_HighlightBorder, NSColor.ControlAccent);

            // 12. DockZoneView. The panel, header and tab keys take the palette keys WinUI3's DockLeafLayout and DockTabItem
            //     theme styles set. Unthemed, the focused header is the system's focused selection with the text that reads
            //     on it, as WinUI3's unthemed one is a blue with white text.
            ThemedColors.SetUnthemedDefault(DockZone_EmptyBackground, NSColor.UnderPageBackground);
            ThemedColors.SetUnthemedDefault(DockZone_EmptyBorder, NSColor.Separator);
            ThemedColors.SetUnthemedDefault(DockZone_EmptyForeground, NSColor.SecondaryLabel);
            RegisterAppKitOnly(DockZone_PanelBackground, NSColor.WindowBackground, "AJut_Brush_PanelBackground");
            RegisterAppKitOnly(DockZone_PanelBorder, NSColor.Separator, "AJut_Brush_PanelBorder");
            RegisterAppKitOnly(DockZone_HeaderBackground, NSColor.UnderPageBackground, "AJut_Brush_NavigationPanelBackground");
            RegisterAppKitOnly(DockZone_HeaderForeground, NSColor.Label, "AJut_Brush_NormalText");
            RegisterAppKitOnly(DockZone_HeaderFocusedBackground, NSColor.SelectedContentBackground, "AJut_Brush_PrimaryHighlightDark");
            RegisterAppKitOnly(DockZone_HeaderFocusedForeground, NSColor.AlternateSelectedControlText, "AJut_Brush_HighContrastText");
            RegisterAppKitOnly(DockZone_TabBackground, NSColor.UnderPageBackground, "AJut_Brush_NavigationPanelBackground");
            RegisterAppKitOnly(DockZone_SelectedTabBackground, NSColor.WindowBackground, "AJut_Brush_PanelBackground");
            const double kTabPointerOverOpacity = 0.8; // WPF's unthemed tab hover fill, #C16F, is the same strength
            RegisterAppKitOnly(DockZone_TabPointerOverBackground, NSColor.ControlAccent.ColorWithAlphaComponent((nfloat)kTabPointerOverOpacity), "AJut_Brush_PrimaryHighlightDesat");
            ThemedColors.SetUnthemedDefault(DockZone_DragInsertLine, NSColor.ControlAccent);
            ThemedColors.SetUnthemedDefault(DockZone_DragGhostBackground, NSColor.WindowBackground);
            ThemedColors.SetUnthemedDefault(DockZone_DragGhostBorder, NSColor.Separator);

            //     The tab strip has no palette entry and no alias, so themed it falls back to this clear default too
            ThemedColors.SetUnthemedDefault(DockZone_TabStripBackground, NSColor.Clear);

            // 13. NumericEditor, AppKit only
            RegisterAppKitOnly(NumericEditor_FrameBackground, NSColor.ControlBackground, MobileLook.NumericEditor.FrameSurface);
            RegisterAppKitOnly(NumericEditor_TextBoxBackground, NSColor.TextBackground, MobileLook.NumericEditor.WellSurface);
            RegisterAppKitOnly(NumericEditor_Text, NSColor.Text, MobileLook.NumericEditor.Number);
            RegisterAppKitOnly(NumericEditor_ReadOnlyText, NSColor.DisabledControlText, MobileLook.NumericEditor.ReadOnlyText);
            RegisterAppKitOnly(NumericEditor_Label, NSColor.SecondaryLabel, MobileLook.NumericEditor.Label);
            RegisterAppKitOnly(NumericEditor_StepGlyph, NSColor.Label, MobileLook.NumericEditor.StepGlyph);

            // 14. DockPanelToolbarView, shared keys. Unthemed the buttons are macOS control colors,
            //     checked is the accent, and hover and pressed are translucent label grays.
            ThemedColors.SetUnthemedDefault(DockPanelToolbar_Background, NSColor.UnderPageBackground);
            ThemedColors.SetUnthemedDefault(DockPanelToolbar_Border, NSColor.Separator);
            ThemedColors.SetUnthemedDefault(DockPanelAddButton_NormalBackground, NSColor.Control);
            ThemedColors.SetUnthemedDefault(DockPanelAddButton_NormalBorder, NSColor.Separator);
            ThemedColors.SetUnthemedDefault(DockPanelAddButton_NormalForeground, NSColor.ControlText);
            ThemedColors.SetUnthemedDefault(DockPanelAddButton_PointerOverBackground, NSColor.QuaternaryLabel);
            ThemedColors.SetUnthemedDefault(DockPanelAddButton_PressedBackground, NSColor.TertiaryLabel);
            ThemedColors.SetUnthemedDefault(DockPanelAddButton_PlusOverlayForeground, NSColor.ControlAccent);
            ThemedColors.SetUnthemedDefault(DockPanelToggleButton_NormalBackground, NSColor.Control);
            ThemedColors.SetUnthemedDefault(DockPanelToggleButton_NormalBorder, NSColor.Separator);
            ThemedColors.SetUnthemedDefault(DockPanelToggleButton_NormalForeground, NSColor.ControlText);
            ThemedColors.SetUnthemedDefault(DockPanelToggleButton_PointerOverBackground, NSColor.QuaternaryLabel);
            ThemedColors.SetUnthemedDefault(DockPanelToggleButton_PressedBackground, NSColor.TertiaryLabel);
            ThemedColors.SetUnthemedDefault(DockPanelToggleButton_CheckedBackground, NSColor.ControlAccent);
            ThemedColors.SetUnthemedDefault(DockPanelToggleButton_CheckedBorder, NSColor.ControlAccent);
            ThemedColors.SetUnthemedDefault(DockPanelToggleButton_CheckedPointerOverBackground, NSColor.ControlAccent);
            ThemedColors.SetUnthemedDefault(DockPanelToggleButton_CheckedPointerOverBorder, NSColor.ControlAccent);
            ThemedColors.SetUnthemedDefault(DockPanelToggleButton_CheckedPressedBackground, NSColor.ControlAccent);
            ThemedColors.SetUnthemedDefault(DockPanelToggleButton_CheckedPressedBorder, NSColor.ControlAccent);

            // 15. DockDropOverlayView, shared keys. Unthemed a widget is a control face with a label
            //     glyph, and engaged it is the accent with the glyph that reads on the accent.
            ThemedColors.SetUnthemedDefault(DockDropWidget_BorderIdle, NSColor.SecondaryLabel);
            ThemedColors.SetUnthemedDefault(DockDropWidget_BackgroundIdle, NSColor.ControlBackground);
            ThemedColors.SetUnthemedDefault(DockDropWidget_GlyphIdle, NSColor.Label);
            ThemedColors.SetUnthemedDefault(DockDropWidget_BorderEngaged, NSColor.ControlAccent);
            ThemedColors.SetUnthemedDefault(DockDropWidget_BackgroundEngaged, NSColor.ControlAccent);
            ThemedColors.SetUnthemedDefault(DockDropWidget_GlyphEngaged, NSColor.AlternateSelectedControlText);

            // 16. AutoBrushButton, shared key. Unthemed it is the macOS accent, which is itself dynamic.
            ThemedColors.SetUnthemedDefault(AutoBrushButton_Accent, NSColor.ControlAccent);

            // 17. DockTabScrollButton. Unthemed the arrow is the label color, and hover and pressed are translucent
            //     label grays, as the toolbar buttons' are.
            RegisterAppKitOnly(DockTabScrollButton_Glyph, NSColor.Label, "AJut_Brush_NormalText");
            ThemedColors.SetUnthemedDefault(DockTabScrollButton_HoverBackground, NSColor.QuaternaryLabel);
            ThemedColors.SetUnthemedDefault(DockTabScrollButton_PressedBackground, NSColor.TertiaryLabel);

            // 18. DockCloseButton, shared keys. Unthemed hover and pressed are translucent label grays, as BrushButton's are.
            ThemedColors.SetUnthemedDefault(DockCloseButton_HoverBackground, NSColor.QuaternaryLabel);
            ThemedColors.SetUnthemedDefault(DockCloseButton_PressedBackground, NSColor.TertiaryLabel);
        }

        // ===========[ Helper Methods ]========================================
        private static void RegisterAppKitOnly (string key, NSColor unthemedDefault, string paletteKey)
        {
            ThemedColors.SetUnthemedDefault(key, unthemedDefault);
            ThemedColors.SetThemedAlias(key, paletteKey);
        }

        /// <summary>An sRGB color from 0xAARRGGBB, the same form as the WinUI3 literal defaults</summary>
        internal static NSColor Srgb (uint argb)
        {
            return new ColorArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb).ToNSColor();
        }
    }
}
