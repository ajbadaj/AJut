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
        // AppKit only, each aliased to the Button* key the WinUI3 GeometryButton uses for that state.
        // They are separate keys because unthemed, a Button* key means a button face, and a shape
        // filled with a button face color barely shows; unthemed, these are label colors instead.

        /// <summary>The shape's fill at rest</summary>
        public const string GeometryButton_Fill = "AJut_GeometryButton_Fill";

        /// <summary>The shape's fill with the pointer over it</summary>
        public const string GeometryButton_FillPointerOver = "AJut_GeometryButton_FillPointerOver";

        /// <summary>The shape's fill while pressed</summary>
        public const string GeometryButton_FillPressed = "AJut_GeometryButton_FillPressed";

        /// <summary>The shape's fill while disabled</summary>
        public const string GeometryButton_FillDisabled = "AJut_GeometryButton_FillDisabled";

        /// <summary>The traced outline at rest</summary>
        public const string GeometryButton_Trace = "AJut_GeometryButton_Trace";

        /// <summary>The traced outline with the pointer over it</summary>
        public const string GeometryButton_TracePointerOver = "AJut_GeometryButton_TracePointerOver";

        /// <summary>The traced outline while pressed</summary>
        public const string GeometryButton_TracePressed = "AJut_GeometryButton_TracePressed";

        /// <summary>The traced outline while disabled</summary>
        public const string GeometryButton_TraceDisabled = "AJut_GeometryButton_TraceDisabled";

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

        // ===========[ ToggleStrip ]===========================================
        // The button backgrounds are shared keys (WinUI3's AJutThemeMapAliasing has them, and the
        // checked ones are gradients). The strip's own background and border, and the name colors,
        // are AppKit only: WinUI3 sets those through style setters rather than keys.

        public const string ToggleStripButton_PointerOverBackground = "AJut_ToggleStripButton_PointerOverBackground";
        public const string ToggleStripButton_PressedBackground = "AJut_ToggleStripButton_PressedBackground";
        public const string ToggleStripButton_CheckedBackground = "AJut_ToggleStripButton_CheckedBackground";
        public const string ToggleStripButton_CheckedPointerOverBackground = "AJut_ToggleStripButton_CheckedPointerOverBackground";
        public const string ToggleStripButton_CheckedPressedBackground = "AJut_ToggleStripButton_CheckedPressedBackground";

        /// <summary>An item's name while it is not selected. AppKit only.</summary>
        public const string ToggleStripButton_Foreground = "AJut_ToggleStripButton_Foreground";

        /// <summary>An item's name while it is selected. AppKit only: unthemed, it sits on the accent color, so it is white.</summary>
        public const string ToggleStripButton_CheckedForeground = "AJut_ToggleStripButton_CheckedForeground";

        /// <summary>The strip's background behind its items. AppKit only.</summary>
        public const string ToggleStrip_Background = "AJut_ToggleStrip_Background";

        /// <summary>The strip's outline and the separators between items. AppKit only.</summary>
        public const string ToggleStrip_Border = "AJut_ToggleStrip_Border";

        // ===========[ NumericEditor ]=========================================
        /// <summary>The border while the field has focus. Shared key.</summary>
        public const string NumericEditor_FocusBorder = "AJut_NumericEditor_FocusBorderBrush";

        /// <summary>The border at rest. Shared key.</summary>
        public const string NumericEditor_NormalBorder = "AJut_NumericEditor_NormalBorderBrush";

        /// <summary>The border while the text is out of range. Shared key.</summary>
        public const string NumericEditor_ErrorBorder = "AJut_NumericEditor_ErrorBrush";

        /// <summary>The field's background. AppKit only: WinUI3 sets it through a style setter.</summary>
        public const string NumericEditor_TextBoxBackground = "AJut_NumericEditor_TextBoxBackground";

        /// <summary>The typed text. AppKit only.</summary>
        public const string NumericEditor_Text = "AJut_NumericEditor_Text";

        /// <summary>The label before the field (the "#" by default). AppKit only.</summary>
        public const string NumericEditor_Label = "AJut_NumericEditor_Label";

        // ===========[ FlatTreeListView ]======================================
        /// <summary>A selected row's leading strip and outline while its window is key. Shared key.</summary>
        public const string FlatTree_Selection = "AJut_FlatTree_SelectionBrush";

        /// <summary>A selected row's leading strip and outline while another window is key. Shared key.</summary>
        public const string FlatTree_SelectionUnfocused = "AJut_FlatTree_SelectionBrushUnfocused";

        /// <summary>A selected row's fill. Shared key.</summary>
        public const string FlatTree_SelectionBackground = "AJut_FlatTree_SelectionBackground";

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

        // ===========[ PropertyGrid ]==========================================
        /// <summary>A property row's name. Shared key.</summary>
        public const string PropertyGridRow_LabelForeground = "AJut_PropertyGridItemRow_LabelForeground";

        /// <summary>The line the label splitter shows over the label and editor boundary. Shared key.</summary>
        public const string PropertyGrid_LabelSplitter = "AJut_PropertyGrid_LabelSplitterBrush";

        /// <summary>A property row's subtitle and a list element's header text. AppKit only.</summary>
        public const string PropertyGridRow_SubtitleForeground = "AJut_PropertyGridItemRow_SubtitleForeground";

        /// <summary>The "Not set" text of a null nullable value. Shared key.</summary>
        public const string NullableEditor_NotSetForeground = "AJut_NullableEditor_NotSetForeground";

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

        /// <summary>A panel zone's outline and tab outlines. AppKit only.</summary>
        public const string DockZone_PanelBorder = "AJut_DockZone_PanelBorderBrush";

        /// <summary>The header bar above the shown panel. AppKit only.</summary>
        public const string DockZone_HeaderBackground = "AJut_DockZone_HeaderBackground";

        /// <summary>Header and tab titles. AppKit only.</summary>
        public const string DockZone_HeaderForeground = "AJut_DockZone_HeaderForeground";

        /// <summary>The tab strip behind the tabs. AppKit only.</summary>
        public const string DockZone_TabStripBackground = "AJut_DockZone_TabStripBackground";

        /// <summary>An unselected tab. AppKit only.</summary>
        public const string DockZone_TabBackground = "AJut_DockZone_TabBackground";

        /// <summary>The selected tab, which is the panel's own fill so the tab reads as part of the panel. AppKit only.</summary>
        public const string DockZone_SelectedTabBackground = "AJut_DockZone_SelectedTabBackground";

        /// <summary>The line marking where a dragged tab will land. Shared key.</summary>
        public const string DockZone_DragInsertLine = "AJut_DockZone_DragInsertLineBrush";

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

            // 4. GeometryButton, AppKit only
            RegisterAppKitOnly(GeometryButton_Fill, NSColor.SecondaryLabel, "ButtonBackground");
            RegisterAppKitOnly(GeometryButton_FillPointerOver, NSColor.Label, "ButtonBackgroundPointerOver");
            RegisterAppKitOnly(GeometryButton_FillPressed, NSColor.ControlAccent, "ButtonBackgroundPressed");
            RegisterAppKitOnly(GeometryButton_FillDisabled, NSColor.TertiaryLabel, "ButtonBackgroundDisabled");
            RegisterAppKitOnly(GeometryButton_Trace, NSColor.Separator, "ButtonBorderBrush");
            RegisterAppKitOnly(GeometryButton_TracePointerOver, NSColor.SecondaryLabel, "ButtonBorderBrushPointerOver");
            RegisterAppKitOnly(GeometryButton_TracePressed, NSColor.ControlAccent, "ButtonBorderBrushPressed");
            RegisterAppKitOnly(GeometryButton_TraceDisabled, NSColor.QuaternaryLabel, "ButtonBorderBrushDisabled");

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

            // 7. ToggleStrip, AppKit only
            RegisterAppKitOnly(ToggleStripButton_Foreground, NSColor.ControlText, "AJut_Brush_NormalText");
            RegisterAppKitOnly(ToggleStripButton_CheckedForeground, NSColor.AlternateSelectedControlText, "AJut_Brush_NormalText");
            RegisterAppKitOnly(ToggleStrip_Background, NSColor.Control, "AJut_Brush_PopOutControlBackground");
            RegisterAppKitOnly(ToggleStrip_Border, NSColor.Separator, "AJut_Brush_BorderCommon");

            // 8. NumericEditor, shared keys
            ThemedColors.SetUnthemedDefault(NumericEditor_FocusBorder, NSColor.KeyboardFocusIndicator);
            ThemedColors.SetUnthemedDefault(NumericEditor_NormalBorder, NSColor.Separator);
            ThemedColors.SetUnthemedDefault(NumericEditor_ErrorBorder, NSColor.SystemRed);

            // 9. FlatTreeListView, shared keys
            ThemedColors.SetUnthemedDefault(FlatTree_Selection, NSColor.ControlAccent);
            ThemedColors.SetUnthemedDefault(FlatTree_SelectionUnfocused, NSColor.SecondaryLabel);
            ThemedColors.SetUnthemedDefault(FlatTree_SelectionBackground, NSColor.UnemphasizedSelectedContentBackground);
            ThemedColors.SetUnthemedDefault(FlatTree_HoverBackground, NSColor.QuaternaryLabel);
            ThemedColors.SetUnthemedDefault(FlatTree_HoverBorder, NSColor.Separator);
            ThemedColors.SetUnthemedDefault(FlatTree_DragInsertionLine, NSColor.ControlAccent);
            const double kDragParentConnectorOpacity = 0.5; // the WinUI3 connector brush's opacity
            ThemedColors.SetUnthemedDefault(FlatTree_DragParentConnector, NSColor.ControlAccent.ColorWithAlphaComponent((nfloat)kDragParentConnectorOpacity));
            ThemedColors.SetUnthemedDefault(FlatTree_DragTargetHighlight, NSColor.ControlAccent);
            RegisterAppKitOnly(FlatTree_ExpanderGlyph, NSColor.SecondaryLabel, "AJut_Brush_SymbolForeground");

            // 10. PropertyGrid
            ThemedColors.SetUnthemedDefault(PropertyGridRow_LabelForeground, NSColor.Label);
            ThemedColors.SetUnthemedDefault(PropertyGrid_LabelSplitter, NSColor.ControlAccent);
            ThemedColors.SetUnthemedDefault(NullableEditor_NotSetForeground, NSColor.SecondaryLabel);
            RegisterAppKitOnly(PropertyGridRow_SubtitleForeground, NSColor.SecondaryLabel, "AJut_Brush_UnderstatedText");

            // 11. ColorEditIngressControl, shared keys
            ThemedColors.SetUnthemedDefault(ColorEditIngress_NormalBorder, NSColor.Separator);
            ThemedColors.SetUnthemedDefault(ColorEditIngress_HighlightBorder, NSColor.ControlAccent);

            // 12. DockZoneView
            ThemedColors.SetUnthemedDefault(DockZone_EmptyBackground, NSColor.UnderPageBackground);
            ThemedColors.SetUnthemedDefault(DockZone_EmptyBorder, NSColor.Separator);
            ThemedColors.SetUnthemedDefault(DockZone_EmptyForeground, NSColor.SecondaryLabel);
            RegisterAppKitOnly(DockZone_PanelBackground, NSColor.WindowBackground, "AJut_Brush_PanelBackground");
            RegisterAppKitOnly(DockZone_PanelBorder, NSColor.Separator, "AJut_Brush_BorderCommon");
            RegisterAppKitOnly(DockZone_HeaderBackground, NSColor.UnderPageBackground, "AJut_Brush_InnerPanelBackground");
            RegisterAppKitOnly(DockZone_HeaderForeground, NSColor.Label, "AJut_Brush_NormalText");
            RegisterAppKitOnly(DockZone_TabStripBackground, NSColor.WindowBackground, "AJut_Brush_PanelBackground");
            RegisterAppKitOnly(DockZone_TabBackground, NSColor.UnderPageBackground, "AJut_Brush_PanelBackground");
            RegisterAppKitOnly(DockZone_SelectedTabBackground, NSColor.WindowBackground, "AJut_Brush_PanelBackground");
            ThemedColors.SetUnthemedDefault(DockZone_DragInsertLine, NSColor.ControlAccent);

            // 13. NumericEditor, AppKit only
            RegisterAppKitOnly(NumericEditor_TextBoxBackground, NSColor.TextBackground, "AJut_Brush_TextBoxBackground");
            RegisterAppKitOnly(NumericEditor_Text, NSColor.Text, "AJut_Brush_InteractiveText");
            RegisterAppKitOnly(NumericEditor_Label, NSColor.SecondaryLabel, "AJut_Brush_UnderstatedText");

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
