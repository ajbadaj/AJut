namespace AJut.UX.Theming
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using static AJut.UX.Theming.AJutThemeEntry;

    /// <summary>
    /// The themed color keys AJut's controls paint with, named the WinUI3 way (AJut_&lt;Control&gt;_&lt;Concept&gt;) so a
    /// key means the same thing on every stack, and what each one aliases in the AJut theme. A toolkit package
    /// registers its own unthemed default per key; with the AJut theme loaded, <see cref="AJutPalette"/> resolves the
    /// key through the alias here to the theme's value.
    /// </summary>
    /// <remarks>
    /// The aliases mirror three files in AJut.UX.WinUI3's Resources folder, which stay the source of truth and which the
    /// palette test checks this against: AJutThemeMapAliasing.xaml (the control keys), AJut.UX_Overrides.xaml (the
    /// ThemedWindowRootControl brushes, keyed here as AJut_ThemedWindowRootControl_&lt;Property&gt; from the style setter
    /// each one comes from), and SystemColorAliasing.xaml (the stock control keys the AJut theme overrides, such as
    /// ButtonBackground, which have no constants here since they are not AJut's names).
    /// </remarks>
    public static class AJutThemeKeys
    {
        public const string DockDropInsertionDriverWidget_BorderIdleBrush = "AJut_DockDropInsertionDriverWidget_BorderIdleBrush";
        public const string DockDropInsertionDriverWidget_BackgroundIdleBrush = "AJut_DockDropInsertionDriverWidget_BackgroundIdleBrush";
        public const string DockDropInsertionDriverWidget_GlyphIdleBrush = "AJut_DockDropInsertionDriverWidget_GlyphIdleBrush";
        public const string DockDropInsertionDriverWidget_BorderEngagedBrush = "AJut_DockDropInsertionDriverWidget_BorderEngagedBrush";
        public const string DockDropInsertionDriverWidget_BackgroundEngagedBrush = "AJut_DockDropInsertionDriverWidget_BackgroundEngagedBrush";
        public const string DockDropInsertionDriverWidget_GlyphEngagedBrush = "AJut_DockDropInsertionDriverWidget_GlyphEngagedBrush";

        public const string DockTearoffContainerPanel_TitleBarBackground = "AJut_DockTearoffContainerPanel_TitleBarBackground";
        public const string DockTearoffContainerPanel_TitleBarForeground = "AJut_DockTearoffContainerPanel_TitleBarForeground";
        public const string DockTearoffContainerPanel_CloseButtonHoverBackground = "AJut_DockTearoffContainerPanel_CloseButtonHoverBackground";
        public const string DockTearoffContainerPanel_CloseButtonPressedBackground = "AJut_DockTearoffContainerPanel_CloseButtonPressedBackground";

        public const string PropertyGridItemRow_LabelForeground = "AJut_PropertyGridItemRow_LabelForeground";
        public const string PropertyGridItemRow_SelectionBorderBrush = "AJut_PropertyGridItemRow_SelectionBorderBrush";
        public const string PropertyGridItemRow_HoverBackground = "AJut_PropertyGridItemRow_HoverBackground";
        public const string PropertyGridItemRow_HoverBorderBrush = "AJut_PropertyGridItemRow_HoverBorderBrush";
        public const string PropertyGrid_LabelSplitterBrush = "AJut_PropertyGrid_LabelSplitterBrush";

        public const string PopupMenuButton_ChevronForeground = "AJut_PopupMenuButton_ChevronForeground";

        public const string PathSelectionControl_NormalBorderBrush = "AJut_PathSelectionControl_NormalBorderBrush";
        public const string PathSelectionControl_FocusedBorderBrush = "AJut_PathSelectionControl_FocusedBorderBrush";
        public const string PathSelectionControl_WarningIconForeground = "AJut_PathSelectionControl_WarningIconForeground";
        public const string PathSelectionControl_ButtonBorderBrush = "AJut_PathSelectionControl_ButtonBorderBrush";
        public const string PathSelectionControl_ButtonIconForeground = "AJut_PathSelectionControl_ButtonIconForeground";
        public const string PathSelectionControl_FocusedTextBoxBackgroundBrush = "AJut_PathSelectionControl_FocusedTextBoxBackgroundBrush";

        public const string FlatTreeItemRow_ExpanderPointerOverBackground = "AJut_FlatTreeItemRow_ExpanderPointerOverBackground";
        public const string FlatTreeItemRow_ExpanderPressedBackground = "AJut_FlatTreeItemRow_ExpanderPressedBackground";

        public const string FlatTree_DragInsertionLine = "AJut_FlatTree_DragInsertionLine";
        public const string FlatTree_DragParentConnector = "AJut_FlatTree_DragParentConnector";
        public const string FlatTree_DragTargetHighlight = "AJut_FlatTree_DragTargetHighlight";
        public const string FlatTree_SelectionBrush = "AJut_FlatTree_SelectionBrush";
        public const string FlatTree_SelectionBackground = "AJut_FlatTree_SelectionBackground";
        public const string FlatTree_SelectionBrushUnfocused = "AJut_FlatTree_SelectionBrushUnfocused";
        public const string FlatTree_HoverBackground = "AJut_FlatTree_HoverBackground";
        public const string FlatTree_HoverBorderBrush = "AJut_FlatTree_HoverBorderBrush";

        public const string ColorEditIngressControl_HighlightBorderBrush = "AJut_ColorEditIngressControl_HighlightBorderBrush";
        public const string ColorEditIngressControl_NormalBorderBrush = "AJut_ColorEditIngressControl_NormalBorderBrush";

        public const string DockCloseButton_HoverBackground = "AJut_DockCloseButton_HoverBackground";
        public const string DockCloseButton_PressedBackground = "AJut_DockCloseButton_PressedBackground";

        public const string DockTabScrollButton_HoverBackground = "AJut_DockTabScrollButton_HoverBackground";
        public const string DockTabScrollButton_PressedBackground = "AJut_DockTabScrollButton_PressedBackground";

        public const string NullableEditor_NotSetForeground = "AJut_NullableEditor_NotSetForeground";
        public const string NullableEditor_OverlayBackground = "AJut_NullableEditor_OverlayBackground";

        public const string NumericEditor_IncreasePointerOverBackground = "AJut_NumericEditor_IncreasePointerOverBackground";
        public const string NumericEditor_DecreasePointerOverBackground = "AJut_NumericEditor_DecreasePointerOverBackground";
        public const string NumericEditor_FocusBorderBrush = "AJut_NumericEditor_FocusBorderBrush";
        public const string NumericEditor_NormalBorderBrush = "AJut_NumericEditor_NormalBorderBrush";
        public const string NumericEditor_ErrorBrush = "AJut_NumericEditor_ErrorBrush";

        public const string DockPanelToggleButton_NormalBackground = "AJut_DockPanelToggleButton_NormalBackground";
        public const string DockPanelToggleButton_NormalBorderBrush = "AJut_DockPanelToggleButton_NormalBorderBrush";
        public const string DockPanelToggleButton_NormalForeground = "AJut_DockPanelToggleButton_NormalForeground";
        public const string DockPanelToggleButton_PointerOverBackground = "AJut_DockPanelToggleButton_PointerOverBackground";
        public const string DockPanelToggleButton_PressedBackground = "AJut_DockPanelToggleButton_PressedBackground";
        public const string DockPanelToggleButton_CheckedBackground = "AJut_DockPanelToggleButton_CheckedBackground";
        public const string DockPanelToggleButton_CheckedBorderBrush = "AJut_DockPanelToggleButton_CheckedBorderBrush";
        public const string DockPanelToggleButton_CheckedPointerOverBackground = "AJut_DockPanelToggleButton_CheckedPointerOverBackground";
        public const string DockPanelToggleButton_CheckedPointerOverBorderBrush = "AJut_DockPanelToggleButton_CheckedPointerOverBorderBrush";
        public const string DockPanelToggleButton_CheckedPressedBackground = "AJut_DockPanelToggleButton_CheckedPressedBackground";
        public const string DockPanelToggleButton_CheckedPressedBorderBrush = "AJut_DockPanelToggleButton_CheckedPressedBorderBrush";

        public const string DockPanelAddButton_NormalBackground = "AJut_DockPanelAddButton_NormalBackground";
        public const string DockPanelAddButton_NormalBorderBrush = "AJut_DockPanelAddButton_NormalBorderBrush";
        public const string DockPanelAddButton_NormalForeground = "AJut_DockPanelAddButton_NormalForeground";
        public const string DockPanelAddButton_PointerOverBackground = "AJut_DockPanelAddButton_PointerOverBackground";
        public const string DockPanelAddButton_PressedBackground = "AJut_DockPanelAddButton_PressedBackground";
        public const string DockPanelAddButton_PlusOverlayForeground = "AJut_DockPanelAddButton_PlusOverlayForeground";

        public const string DockPanelToolbar_NormalBackground = "AJut_DockPanelToolbar_NormalBackground";
        public const string DockPanelToolbar_NormalBorderBrush = "AJut_DockPanelToolbar_NormalBorderBrush";

        public const string DockZone_EmptyBackground = "AJut_DockZone_EmptyBackground";
        public const string DockZone_EmptyBorderBrush = "AJut_DockZone_EmptyBorderBrush";
        public const string DockZone_EmptyForeground = "AJut_DockZone_EmptyForeground";
        public const string DockZone_DragInsertLineBrush = "AJut_DockZone_DragInsertLineBrush";

        public const string ToggleStripButton_PointerOverBackground = "AJut_ToggleStripButton_PointerOverBackground";
        public const string ToggleStripButton_PressedBackground = "AJut_ToggleStripButton_PressedBackground";
        public const string ToggleStripButton_CheckedBackground = "AJut_ToggleStripButton_CheckedBackground";
        public const string ToggleStripButton_CheckedPointerOverBackground = "AJut_ToggleStripButton_CheckedPointerOverBackground";
        public const string ToggleStripButton_CheckedPressedBackground = "AJut_ToggleStripButton_CheckedPressedBackground";

        public const string ThemedWindowRootControl_ActiveTitleBarBackground = "AJut_ThemedWindowRootControl_ActiveTitleBarBackground";
        public const string ThemedWindowRootControl_InactiveTitlebarBackground = "AJut_ThemedWindowRootControl_InactiveTitlebarBackground";
        public const string ThemedWindowRootControl_InactiveBorderBrush = "AJut_ThemedWindowRootControl_InactiveBorderBrush";
        public const string ThemedWindowRootControl_CloseHoverBorderBrush = "AJut_ThemedWindowRootControl_CloseHoverBorderBrush";
        public const string ThemedWindowRootControl_CaptionButtonHoverBackground = "AJut_ThemedWindowRootControl_CaptionButtonHoverBackground";
        public const string ThemedWindowRootControl_CaptionButtonPressedBackground = "AJut_ThemedWindowRootControl_CaptionButtonPressedBackground";
        public const string ThemedWindowRootControl_CloseButtonHoverBackground = "AJut_ThemedWindowRootControl_CloseButtonHoverBackground";
        public const string ThemedWindowRootControl_CloseButtonPressedBackground = "AJut_ThemedWindowRootControl_CloseButtonPressedBackground";

        private static readonly Dictionary<string, AJutThemeEntry> g_entries = BuildEntries();

        // ===========[ Properties ]===================================
        /// <summary>Every key with an alias or value here: the AJut control keys and the stock control keys the AJut theme overrides.</summary>
        public static IEnumerable<string> All => g_entries.Keys;

        // ===========[ Public Interface Methods ]===================================
        /// <summary>
        /// The key that <paramref name="key"/> points at in the AJut theme, a palette key (AJut_Color_*, AJut_Brush_*) in
        /// almost every case. Returns false for a key that is not here, or that the theme gives a value directly rather
        /// than an alias (the ToggleStripButton checked gradients, for one); resolve those with AJutPalette.
        /// </summary>
        public static bool TryGetAlias (string key, [NotNullWhen(true)] out string? paletteKey)
        {
            paletteKey = g_entries.TryGetValue(key, out AJutThemeEntry? entry) ? entry.AliasKey : null;
            return paletteKey != null;
        }

        internal static bool TryGetEntry (string key, [NotNullWhen(true)] out AJutThemeEntry? entry) => g_entries.TryGetValue(key, out entry);

        // ===========[ Helper Methods ]===================================
        // Change the XAML first; the palette test names every entry here that no longer matches it.
        private static Dictionary<string, AJutThemeEntry> BuildEntries ()
        {
            return new Dictionary<string, AJutThemeEntry>
            {
                // ------ AJutThemeMapAliasing.xaml
                { "AJut_DockDropInsertionDriverWidget_BorderIdleBrush", Alias("AJut_Color_BorderCommon") },
                { "AJut_DockDropInsertionDriverWidget_BackgroundIdleBrush", Alias("AJut_Color_PanelBackground") },
                { "AJut_DockDropInsertionDriverWidget_GlyphIdleBrush", Alias("AJut_Color_SymbolForeground") },
                { "AJut_DockDropInsertionDriverWidget_BorderEngagedBrush", Alias("AJut_Color_BorderCommon") },
                { "AJut_DockDropInsertionDriverWidget_BackgroundEngagedBrush", Alias("AJut_Color_PrimaryHighlight") },
                { "AJut_DockDropInsertionDriverWidget_GlyphEngagedBrush", Alias("AJut_Color_SymbolForegroundHighlight") },
                { "AJut_DockTearoffContainerPanel_TitleBarBackground", Alias("AJut_Color_TitleBarBackground") },
                { "AJut_DockTearoffContainerPanel_TitleBarForeground", Alias("AJut_Color_TitleBarForeground") },
                { "AJut_DockTearoffContainerPanel_CloseButtonHoverBackground", Alias("AJut_Color_ChromeCloseButtonHoverBackground") },
                { "AJut_DockTearoffContainerPanel_CloseButtonPressedBackground", Alias("AJut_Color_ChromeCloseButtonPressedBackground") },
                { "AJut_PropertyGridItemRow_LabelForeground", Alias("AJut_Color_NormalText") },
                { "AJut_PropertyGridItemRow_SelectionBorderBrush", Alias("AJut_Color_ListSelection") },
                { "AJut_PropertyGridItemRow_HoverBackground", Alias("AJut_Color_ListSelectionBackground") },
                { "AJut_PropertyGridItemRow_HoverBorderBrush", Alias("AJut_Color_BorderCommon") },
                { "AJut_PropertyGrid_LabelSplitterBrush", Alias("AJut_Color_ListSelection") },
                { "AJut_PopupMenuButton_ChevronForeground", Alias("AJut_Color_SymbolForeground") },
                { "AJut_PathSelectionControl_NormalBorderBrush", Alias("AJut_Color_TextBoxBorder") },
                { "AJut_PathSelectionControl_FocusedBorderBrush", Alias("AJut_Color_PrimaryHighlight") },
                { "AJut_PathSelectionControl_WarningIconForeground", Alias("AJut_Color_NormalText") },
                { "AJut_PathSelectionControl_ButtonBorderBrush", Alias("AJut_Color_BorderCommon") },
                { "AJut_PathSelectionControl_ButtonIconForeground", Alias("AJut_Color_SymbolForeground") },
                { "AJut_PathSelectionControl_FocusedTextBoxBackgroundBrush", Alias("AJut_Color_TextBoxBackground") },
                { "AJut_FlatTreeItemRow_ExpanderPointerOverBackground", Alias("AJut_Color_MouseInteractionHighlight") },
                { "AJut_FlatTreeItemRow_ExpanderPressedBackground", Alias("AJut_Color_PrimaryHighlightDark") },
                { "AJut_FlatTree_DragInsertionLine", Alias("AJut_Color_PrimaryHighlight") },
                { "AJut_FlatTree_DragParentConnector", Alias("AJut_Color_PrimaryHighlight", 0.5) },
                { "AJut_FlatTree_DragTargetHighlight", Alias("AJut_Color_PrimaryHighlight") },
                { "AJut_FlatTree_SelectionBrush", Alias("AJut_Color_ListSelection") },
                { "AJut_FlatTree_SelectionBackground", Alias("AJut_Color_ListSelectionBackground") },
                { "AJut_FlatTree_SelectionBrushUnfocused", Alias("AJut_Color_PrimaryHighlightDesat") },
                { "AJut_FlatTree_HoverBackground", Alias("AJut_Color_ListSelectionBackground") },
                { "AJut_FlatTree_HoverBorderBrush", Alias("AJut_Color_BorderCommon") },
                { "AJut_ColorEditIngressControl_HighlightBorderBrush", Alias("AJut_Color_PrimaryHighlight") },
                { "AJut_ColorEditIngressControl_NormalBorderBrush", Alias("AJut_Color_BorderCommon") },
                { "AJut_DockCloseButton_HoverBackground", Alias("AJut_Color_MouseInteractionHighlight") },
                { "AJut_DockCloseButton_PressedBackground", Alias("AJut_Color_PrimaryHighlightDark") },
                { "AJut_DockTabScrollButton_HoverBackground", Alias("AJut_Color_MouseInteractionHighlight") },
                { "AJut_DockTabScrollButton_PressedBackground", Alias("AJut_Color_PrimaryHighlightDark") },
                { "AJut_NullableEditor_NotSetForeground", Alias("AJut_Color_NormalText") },
                { "AJut_NullableEditor_OverlayBackground", Solid(0x88000000) },
                { "AJut_NumericEditor_IncreasePointerOverBackground", Alias("AJut_Color_PrimaryHighlightBright") },
                { "AJut_NumericEditor_DecreasePointerOverBackground", Alias("AJut_Color_PrimaryHighlightBright") },
                { "AJut_NumericEditor_FocusBorderBrush", Alias("AJut_Color_PrimaryHighlight") },
                { "AJut_NumericEditor_NormalBorderBrush", Alias("AJut_Color_BorderCommon") },
                { "AJut_NumericEditor_ErrorBrush", Alias("AJut_Color_BadStuff") },
                { "AJut_DockPanelToggleButton_NormalBackground", Alias("AJut_Color_InnerPanelBackground") },
                { "AJut_DockPanelToggleButton_NormalBorderBrush", Alias("AJut_Color_BorderCommon") },
                { "AJut_DockPanelToggleButton_NormalForeground", Alias("AJut_Color_NormalText") },
                { "AJut_DockPanelToggleButton_PointerOverBackground", Alias("AJut_Color_MouseInteractionHighlight") },
                { "AJut_DockPanelToggleButton_PressedBackground", Alias("AJut_Color_PrimaryHighlightDark") },
                { "AJut_DockPanelToggleButton_CheckedBackground", Alias("AJut_Color_PrimaryHighlight") },
                { "AJut_DockPanelToggleButton_CheckedBorderBrush", Alias("AJut_Color_PrimaryHighlight") },
                { "AJut_DockPanelToggleButton_CheckedPointerOverBackground", Alias("AJut_Color_PrimaryHighlightBright") },
                { "AJut_DockPanelToggleButton_CheckedPointerOverBorderBrush", Alias("AJut_Color_PrimaryHighlightBright") },
                { "AJut_DockPanelToggleButton_CheckedPressedBackground", Alias("AJut_Color_PrimaryHighlightDark") },
                { "AJut_DockPanelToggleButton_CheckedPressedBorderBrush", Alias("AJut_Color_PrimaryHighlightDark") },
                { "AJut_DockPanelAddButton_NormalBackground", Alias("AJut_Color_InnerPanelBackground") },
                { "AJut_DockPanelAddButton_NormalBorderBrush", Alias("AJut_Color_BorderCommon") },
                { "AJut_DockPanelAddButton_NormalForeground", Alias("AJut_Color_NormalText") },
                { "AJut_DockPanelAddButton_PointerOverBackground", Alias("AJut_Color_MouseInteractionHighlight") },
                { "AJut_DockPanelAddButton_PressedBackground", Alias("AJut_Color_PrimaryHighlightDark") },
                { "AJut_DockPanelAddButton_PlusOverlayForeground", Alias("AJut_Color_PrimaryHighlightBright") },
                { "AJut_DockPanelToolbar_NormalBackground", Alias("AJut_Color_PanelBackground") },
                { "AJut_DockPanelToolbar_NormalBorderBrush", Alias("AJut_Color_BorderCommon") },
                { "AJut_DockZone_EmptyBackground", Alias("AJut_Color_PanelBackground") },
                { "AJut_DockZone_EmptyBorderBrush", Alias("AJut_Color_BorderCommon") },
                { "AJut_DockZone_EmptyForeground", Alias("AJut_Color_NormalText") },
                { "AJut_DockZone_DragInsertLineBrush", Alias("AJut_Color_PrimaryHighlight") },
                { "AJut_ToggleStripButton_PointerOverBackground", Alias("AJut_Color_PrimaryHighlightBright") },
                { "AJut_ToggleStripButton_PressedBackground", Alias("AJut_Color_PrimaryHighlightDark") },
                { "AJut_ToggleStripButton_CheckedBackground", Gradient(Stop(0.0, 0xCC000000), Stop(0.2, 0x801090A0), Stop(1.0, 0x331090A0)) },
                { "AJut_ToggleStripButton_CheckedPointerOverBackground", Gradient(Stop(0.0, 0xCC000000), Stop(0.2, 0x991090A0), Stop(1.0, 0x551090A0)) },
                { "AJut_ToggleStripButton_CheckedPressedBackground", Gradient(Stop(0.0, 0xEE000000), Stop(0.2, 0xAA1090A0), Stop(1.0, 0x661090A0)) },

                // ------ AJut.UX_Overrides.xaml
                { "AJut_ThemedWindowRootControl_ActiveTitleBarBackground", Alias("AJut_Brush_TitleBarBackground") },
                { "AJut_ThemedWindowRootControl_InactiveTitlebarBackground", Alias("AJut_Brush_TitleBarBackgroundInactive") },
                { "AJut_ThemedWindowRootControl_InactiveBorderBrush", Alias("AJut_Brush_WindowGlowBorderInactive") },
                { "AJut_ThemedWindowRootControl_CloseHoverBorderBrush", Alias("AJut_Brush_ChromeCloseButtonComplement") },
                { "AJut_ThemedWindowRootControl_CaptionButtonHoverBackground", Alias("AJut_Brush_ChromeButtonHoverBackground") },
                { "AJut_ThemedWindowRootControl_CaptionButtonPressedBackground", Alias("AJut_Brush_ChromeButtonPressedBackground") },
                { "AJut_ThemedWindowRootControl_CloseButtonHoverBackground", Alias("AJut_Brush_ChromeCloseButtonHoverBackground") },
                { "AJut_ThemedWindowRootControl_CloseButtonPressedBackground", Alias("AJut_Brush_ChromeCloseButtonPressedBackground") },

                // ------ SystemColorAliasing.xaml
                { "SystemAccentColor", Alias("AJut_Color_PrimaryHighlight") },
                { "SystemAccentColorDark1", Alias("AJut_Color_PrimaryHighlightDark") },
                { "SystemAccentColorLight1", Alias("AJut_Color_PrimaryHighlightBright") },
                { "SystemAccentColorLight2", Alias("AJut_Color_PrimaryHighlightDesat") },
                { "AccentFillColorDefaultBrush", Alias("AJut_Color_PrimaryHighlight") },
                { "SystemAccentColor2", Alias("AJut_Color_SecondaryHighlight") },
                { "SystemAccentColor2Dark1", Alias("AJut_Color_SecondaryHighlightDark") },
                { "SystemAccentColor2Light1", Alias("AJut_Color_SecondaryHighlightBright") },
                { "SystemAccentColor2Light2", Alias("AJut_Color_SecondaryHighlightDesat") },
                { "TextFillColorPrimaryBrush", Alias("AJut_Color_NormalText") },
                { "TextBoxForegroundThemeBrush", Alias("AJut_Color_NormalText") },
                { "TextFillColorSecondaryBrush", Alias("AJut_Color_InteractiveText") },
                { "SubtleFillColorSecondaryBrush", Alias("AJut_Color_HighlightText") },
                { "DefaultTextForegroundThemeBrush", Alias("AJut_Color_NormalText") },
                { "ControlStrokeColorDefaultBrush", Alias("AJut_Color_NormalText") },
                { "SystemListAccentLowColor", Alias("AJut_Color_PrimaryHighlightDesat") },
                { "SystemListAccentMediumColor", Alias("AJut_Color_PrimaryHighlight") },
                { "SystemListAccentHighColor", Alias("AJut_Color_PrimaryHighlightBright") },
                { "SystemListLowBrush", Alias("AJut_Brush_ListBackground") },
                { "SystemListMediumBrush", Alias("AJut_Brush_ListBorder") },
                { "SystemListAccentMediumBrush", Alias("AJut_Brush_ListSelection") },
                { "SystemChromeBlackHighBrush", Alias("AJut_Brush_ChromeButtonHoverBackground") },
                { "TitleBarCaptionButtonHoverForegroundColor", Alias("AJut_Color_ChromeButtonHoverForeground") },
                { "TitleBarCaptionButtonPressedForegroundColor", Alias("AJut_Color_ChromeButtonPressedForeground") },
                { "TitleBarCaptionButtonInactiveForegroundColor", Alias("AJut_Color_ChromeButtonInactiveForeground") },
                { "TitleBarCaptionButtonForegroundColor", Alias("AJut_Brush_TitleBarForeground") },
                { "TitleBarForegroundBrush", Alias("AJut_Brush_TitleBarForeground") },
                { "TitleBarDeactivatedForegroundBrush", Alias("AJut_Brush_TitleBarForegroundDesat") },
                { "TabViewBackground", Solid(0x00FFFFFF) },
                { "TabViewItemHeaderBackground", Solid(0x00FFFFFF) },
                { "TabViewItemHeaderBackgroundPointerOver", Alias("AJut_Color_InnerPanelBackgroundHover") },
                { "TabViewItemHeaderBackgroundSelected", Alias("AJut_Color_InnerPanelBackground") },
                { "TabViewItemHeaderDragBackground", Alias("AJut_Brush_PopupBackground") },
                { "TabViewItemHeaderBackgroundPressed", Alias("AJut_Brush_PopupBackground") },
                { "TabViewItemHeaderBackgroundDisabled", Alias("LayerOnMicaBaseAltFillColorTransparentBrush") },
                { "WindowCaptionBackground", Alias("AJut_Color_ChromeButtonBackground") },
                { "WindowCaptionBackgroundDisabled", Solid(0x00FFFFFF) },
                { "WindowCaptionForeground", Alias("AJut_Color_ChromeButtonForeground") },
                { "WindowCaptionForegroundDisabled", Alias("SystemControlDisabledBaseMediumLowBrush") },
                { "WindowCaptionButtonBackgroundPointerOver", Alias("AJut_Color_ChromeButtonHoverBackground") },
                { "WindowCaptionButtonBackgroundPressed", Alias("SystemControlBackgroundBaseMediumLowBrush") },
                { "WindowCaptionButtonStroke", Alias("SystemControlForegroundBaseHighBrush") },
                { "WindowCaptionButtonStrokePointerOver", Alias("SystemControlForegroundBaseHighBrush") },
                { "WindowCaptionButtonStrokePressed", Alias("SystemControlForegroundBaseHighBrush") },
                { "WindowCaptionButtonBackground", Solid(0x00FFFFFF) },
                { "CloseButtonBackgroundPointerOver", Alias("AJut_Color_ChromeCloseButtonHighlight") },
                { "CloseButtonStrokePointerOver", Alias("AJut_Color_ChromeButtonHoverForeground") },
                { "CloseButtonBackgroundPressed", Alias("AJut_Color_ChromeCloseButtonComplement") },
                { "CloseButtonStrokePressed", Alias("AJut_Color_ChromeCloseButtonHoverForeground") },
                { "ControlFillColorDefaultBrush", Alias("AJut_Brush_ControlBackground") },
                { "ControlFillColorSecondaryBrush", Alias("AJut_Brush_ControlBackgroundHover") },
                { "ControlFillColorInputActiveBrush", Alias("AJut_Brush_ControlBackground") },
                { "TextControlForeground", Alias("AJut_Color_NormalText") },
                { "TextControlForegroundPointerOver", Alias("AJut_Color_HighlightText") },
                { "TextControlForegroundFocused", Alias("AJut_Color_InteractiveText") },
                { "TextControlForegroundDisabled", Alias("AJut_Color_UnderstatedText") },
                { "TextControlBackground", Alias("AJut_Brush_TextBoxBackground") },
                { "TextControlBackgroundPointerOver", Alias("AJut_Brush_TextBoxBackground") },
                { "TextControlBackgroundFocused", Alias("AJut_Brush_TextBoxBackground") },
                { "TextControlBorderBrush", Alias("AJut_Brush_TextBoxBorder") },
                { "TextControlBorderBrushPointerOver", Alias("AJut_Color_PrimaryHighlightBright") },
                { "TextControlBorderBrushFocused", Alias("AJut_Brush_PrimaryHighlight") },
                { "TextControlPlaceholderForeground", Alias("AJut_Brush_UnderstatedText") },
                { "TextControlPlaceholderForegroundPointerOver", Alias("AJut_Brush_InteractiveTextUnfocused") },
                { "TextControlPlaceholderForegroundFocused", Alias("AJut_Brush_UnderstatedText") },
                { "ButtonForeground", Alias("AJut_Brush_NormalText") },
                { "ButtonForegroundPointerOver", Alias("AJut_Color_HighlightText") },
                { "ButtonForegroundPressed", Alias("AJut_Color_ImportantText") },
                { "ButtonForegroundDisabled", Alias("AJut_Color_UnderstatedText") },
                { "ButtonBackground", Alias("AJut_Brush_ControlBackground") },
                { "ButtonBackgroundPointerOver", Alias("AJut_Brush_ControlBackgroundHover") },
                { "ButtonBackgroundPressed", Alias("AJut_Brush_ButtonBackgroundPressed") },
                { "ButtonBorderBrush", Alias("AJut_Brush_BorderCommon") },
                { "ButtonBorderBrushPointerOver", Alias("AJut_Brush_BorderCommonHover") },
                { "ButtonBorderBrushPressed", Alias("AJut_Brush_BorderCommon") },
                { "ButtonBorderBrushDisabled", Alias("AJut_Brush_BorderCommon") },
                { "ToggleButtonBackground", Alias("AJut_Brush_ControlBackground") },
                { "ToggleButtonBackgroundPointerOver", Alias("AJut_Brush_ControlBackgroundHover") },
                { "ToggleButtonBackgroundPressed", Alias("AJut_Brush_ButtonBackgroundPressed") },
                { "ToggleButtonBackgroundChecked", Alias("AJut_Brush_PrimaryHighlight") },
                { "ToggleButtonBackgroundCheckedPointerOver", Alias("AJut_Brush_PrimaryHighlightBright") },
                { "ToggleButtonBackgroundCheckedPressed", Alias("AJut_Brush_PrimaryHighlightDark") },
                { "ToggleButtonForeground", Alias("AJut_Brush_NormalText") },
                { "ToggleButtonForegroundPointerOver", Alias("AJut_Color_HighlightText") },
                { "ToggleButtonForegroundChecked", Alias("AJut_Color_HighContrastText") },
                { "ToggleButtonForegroundCheckedPointerOver", Alias("AJut_Color_ImportantText") },
                { "ToggleButtonForegroundDisabled", Alias("AJut_Color_UnderstatedText") },
                { "ToggleButtonBorderBrush", Alias("AJut_Brush_BorderCommon") },
                { "ToggleButtonBorderBrushPointerOver", Alias("AJut_Brush_BorderCommonHover") },
                { "ToggleButtonBorderBrushChecked", Alias("AJut_Brush_BorderCommonPress") },
                { "ToggleButtonBorderBrushCheckedPointerOver", Alias("AJut_Brush_BorderCommonPress") },
                { "ToggleButtonBorderBrushPressed", Alias("AJut_Brush_BorderCommon") },
                { "ComboBoxBackground", Alias("AJut_Brush_PopOutControlBackground") },
                { "ComboBoxBackgroundPointerOver", Alias("AJut_Brush_ControlBackgroundHover") },
                { "ComboBoxBackgroundPressed", Alias("AJut_Brush_ButtonBackgroundPressed") },
                { "ComboBoxForeground", Alias("AJut_Brush_InteractiveTextUnfocused") },
                { "ComboBoxForegroundPointerOver", Alias("AJut_Color_HighlightText") },
                { "ComboBoxForegroundFocused", Alias("AJut_Color_InteractiveText") },
                { "ComboBoxForegroundDisabled", Alias("AJut_Color_UnderstatedText") },
                { "ComboBoxBorderBrush", Alias("AJut_Brush_TextBoxBorder") },
                { "ComboBoxBorderBrushPointerOver", Alias("AJut_Color_PrimaryHighlightBright") },
                { "ComboBoxBorderBrushFocused", Alias("AJut_Brush_PrimaryHighlight") },
                { "ComboBoxDropDownBackground", Alias("AJut_Brush_InnerPanelBackground") },
                { "ComboBoxDropDownBorderBrush", Alias("AJut_Brush_ListSelection") },
                { "ComboBoxItemForeground", Alias("AJut_Brush_NormalText") },
                { "ComboBoxItemForegroundPointerOver", Alias("AJut_Color_HighlightText") },
                { "ComboBoxItemBackgroundPointerOver", Alias("AJut_Brush_ListSelectionBackground") },
                { "ComboBoxItemBackgroundSelected", Alias("AJut_Brush_ListSelection") },
                { "ComboBoxItemForegroundSelected", Alias("AJut_Color_ImportantText") },
                { "NumberBoxForeground", Alias("AJut_Brush_NormalText") },
                { "NumberBoxBorderBrush", Alias("AJut_Brush_TextBoxBorder") },
                { "NumberBoxBorderBrushFocused", Alias("AJut_Brush_PrimaryHighlight") },
                { "NumberBoxButtonBackground", Solid(0x00FFFFFF) },
                { "NumberBoxButtonBackgroundPointerOver", Alias("AJut_Brush_MouseInteractionHighlight") },
                { "NumberBoxButtonBackgroundPressed", Alias("AJut_Brush_PrimaryHighlightDark") },
                { "NumberBoxButtonForeground", Alias("AJut_Brush_SymbolForeground") },
                { "NumberBoxButtonForegroundPointerOver", Alias("AJut_Brush_SymbolForegroundHighlight") },
                { "NumberBoxButtonForegroundPressed", Alias("AJut_Brush_SymbolForegroundHighlight") },
                { "ToggleSwitchFillOn", Alias("AJut_Brush_PrimaryHighlight") },
                { "ToggleSwitchFillOnPointerOver", Alias("AJut_Brush_PrimaryHighlightBright") },
                { "ToggleSwitchFillOnPressed", Alias("AJut_Brush_PrimaryHighlightDark") },
                { "ToggleSwitchKnobFillOn", Solid(0xFFFFFFFF) },
                { "ToggleSwitchKnobFillOnPointerOver", Solid(0xFFFFFFFF) },
                { "ToggleSwitchKnobFillOnPressed", Solid(0xFFEEEEEE) },
                { "ToggleSwitchFillOff", Alias("AJut_Brush_InnerPanelBackground") },
                { "ToggleSwitchFillOffPointerOver", Alias("AJut_Brush_ControlBackgroundHover") },
                { "ToggleSwitchFillOffPressed", Alias("AJut_Brush_ButtonBackgroundPressed") },
                { "ToggleSwitchStrokeOff", Alias("AJut_Brush_BorderCommon") },
                { "ToggleSwitchStrokeOffPointerOver", Alias("AJut_Brush_BorderCommonHover") },
                { "ToggleSwitchStrokeOn", Alias("AJut_Brush_PrimaryHighlightDark") },
                { "ToggleSwitchKnobFillOff", Alias("AJut_Color_SymbolForeground") },
                { "ToggleSwitchHeaderForeground", Alias("AJut_Color_NormalText") },
                { "ExpanderBackground", Alias("AJut_Brush_InnerPanelBackground") },
                { "ExpanderBorderBrush", Alias("AJut_Brush_BorderCommon") },
                { "ExpanderHeaderBackground", Alias("AJut_Brush_BannerBackground") },
                { "ExpanderHeaderBackgroundPointerOver", Alias("AJut_Brush_ControlBackgroundHover") },
                { "ExpanderHeaderBackgroundPressed", Alias("AJut_Brush_ButtonBackgroundPressed") },
                { "ExpanderHeaderForeground", Alias("AJut_Brush_NormalText") },
                { "ExpanderHeaderForegroundPointerOver", Alias("AJut_Color_HighlightText") },
                { "ExpanderHeaderBorderBrush", Alias("AJut_Brush_BorderCommon") },
                { "ExpanderHeaderBorderBrushPointerOver", Alias("AJut_Brush_BorderCommonHover") },
                { "ExpanderContentBackground", Alias("AJut_Brush_PanelBackground") },
                { "ExpanderContentBorderBrush", Alias("AJut_Brush_BorderCommon") },
                { "ExpanderChevronBackground", Solid(0x00FFFFFF) },
                { "ExpanderChevronBackgroundPointerOver", Alias("AJut_Brush_MouseInteractionHighlight") },
                { "ExpanderChevronBackgroundPressed", Alias("AJut_Brush_PrimaryHighlightDark") },
                { "ExpanderChevronForeground", Alias("AJut_Brush_SymbolForeground") },
                { "ExpanderChevronForegroundPointerOver", Alias("AJut_Brush_SymbolForegroundHighlight") },
                { "ExpanderChevronForegroundPressed", Alias("AJut_Brush_SymbolForegroundHighlight") },
                { "MenuFlyoutPresenterBackground", Alias("AJut_Brush_InnerPanelBackground") },
                { "MenuFlyoutPresenterBorderBrush", Alias("AJut_Brush_ListSelection") },
                { "MenuFlyoutItemBackground", Solid(0x00FFFFFF) },
                { "MenuFlyoutItemBackgroundPointerOver", Alias("AJut_Brush_ListSelectionBackground") },
                { "MenuFlyoutItemBackgroundPressed", Alias("AJut_Brush_PrimaryHighlightDark") },
                { "MenuFlyoutItemForeground", Alias("AJut_Brush_NormalText") },
                { "MenuFlyoutItemForegroundPointerOver", Alias("AJut_Color_HighlightText") },
                { "MenuFlyoutSeparatorBrush", Alias("AJut_Brush_BorderCommon") },
                { "MenuFlyoutSubItemChevron", Alias("AJut_Brush_SymbolForeground") },
                { "MenuBarBackground", Alias("AJut_Brush_BannerBackground") },
                { "MenuBarBorderBrush", Alias("AJut_Brush_BorderCommon") },
                { "MenuBarItemBackground", Solid(0x00FFFFFF) },
                { "MenuBarItemBackgroundPointerOver", Alias("AJut_Brush_MouseInteractionHighlight") },
                { "MenuBarItemBackgroundSelected", Alias("AJut_Brush_PrimaryHighlight") },
                { "MenuBarItemBackgroundPressed", Alias("AJut_Brush_PrimaryHighlightDark") },
                { "MenuBarItemForeground", Alias("AJut_Brush_NormalText") },
                { "MenuBarItemForegroundPointerOver", Alias("AJut_Color_HighlightText") },
                { "MenuBarItemForegroundSelected", Alias("AJut_Color_ImportantText") },
                { "DropDownButtonBackground", Alias("AJut_Brush_ControlBackground") },
                { "DropDownButtonBackgroundPointerOver", Alias("AJut_Brush_ControlBackgroundHover") },
                { "DropDownButtonBackgroundPressed", Alias("AJut_Brush_ButtonBackgroundPressed") },
                { "DropDownButtonForeground", Alias("AJut_Brush_NormalText") },
                { "DropDownButtonForegroundPointerOver", Alias("AJut_Color_HighlightText") },
                { "DropDownButtonBorderBrush", Alias("AJut_Brush_BorderCommon") },
                { "DropDownButtonBorderBrushPointerOver", Alias("AJut_Brush_BorderCommonHover") },
                { "CheckBoxForeground", Alias("AJut_Brush_NormalText") },
                { "CheckBoxCheckBackgroundFillChecked", Alias("AJut_Brush_PrimaryHighlight") },
                { "CheckBoxCheckBackgroundFillCheckedPointerOver", Alias("AJut_Brush_PrimaryHighlightBright") },
                { "CheckBoxCheckBackgroundFillCheckedPressed", Alias("AJut_Brush_PrimaryHighlightDark") },
                { "CheckBoxCheckBackgroundStrokeUnchecked", Alias("AJut_Brush_BorderCommon") },
                { "CheckBoxCheckBackgroundStrokeUncheckedPointerOver", Alias("AJut_Brush_BorderCommonHover") },
                { "CheckBoxCheckGlyphForegroundChecked", Alias("AJut_Brush_ImportantText") },
                { "RadioButtonForeground", Alias("AJut_Brush_NormalText") },
                { "RadioButtonOuterEllipseStroke", Alias("AJut_Brush_BorderCommon") },
                { "RadioButtonOuterEllipseStrokePointerOver", Alias("AJut_Brush_BorderCommonHover") },
                { "RadioButtonOuterEllipseCheckedStroke", Alias("AJut_Brush_PrimaryHighlight") },
                { "RadioButtonOuterEllipseCheckedFill", Alias("AJut_Brush_PrimaryHighlight") },
                { "RadioButtonCheckGlyphFill", Alias("AJut_Brush_ImportantText") },
                { "ScrollBarThumbBackgroundColor", Alias("AJut_Color_BorderCommon") },
                { "ScrollBarThumbBackgroundColorPointerOver", Alias("AJut_Color_BorderCommonTransparent") },
                { "ScrollBarPanningThumbBackgroundColor", Alias("AJut_Color_BorderCommon") },
            };
        }
    }
}
