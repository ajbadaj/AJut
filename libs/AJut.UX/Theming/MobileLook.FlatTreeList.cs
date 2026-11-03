namespace AJut.UX.Theming
{
    using System;

    public static partial class MobileLook
    {
        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// FlatTreeListView: a tree shown as a flat list of rows, each one an indent, an expand chevron, and the row's own
        /// content. A selected row takes the selection fill with an outline around it and a strip down its leading edge, as
        /// a WinUI3 and WPF tree row does. A pressed row takes the hover tint, since a press is the only highlight touch
        /// has. While rows are dragged, a line shows where they will land and a dashed connector runs to the new parent's
        /// chevron, which takes the drag highlight.
        /// </summary>
        public static class FlatTreeList
        {
            // ===========[ Keys ]===================================
            /// <summary>A selected row's fill.</summary>
            public const string SelectionFill = AJutThemeKeys.FlatTree_SelectionBackground;

            /// <summary>A selected row's outline, and the strip down its leading edge.</summary>
            public const string SelectionAccent = AJutThemeKeys.FlatTree_SelectionBrush;

            /// <summary>A pressed row's fill, WinUI3's hover tint.</summary>
            public const string PressedFill = AJutThemeKeys.FlatTree_HoverBackground;

            /// <summary>A pressed row's outline, WinUI3's hover border.</summary>
            public const string PressedBorder = AJutThemeKeys.FlatTree_HoverBorderBrush;

            /// <summary>The expand and collapse chevron.</summary>
            public const string ExpanderGlyph = "TextFillColorSecondaryBrush";

            /// <summary>The chevron while the tree is disabled.</summary>
            public const string ExpanderGlyphDisabled = "TextControlForegroundDisabled";

            /// <summary>The fill behind the chevron while it is pressed.</summary>
            public const string ExpanderPressedFill = AJutThemeKeys.FlatTreeItemRow_ExpanderPressedBackground;

            /// <summary>A row's text when the tree shows its own content (the source's ToString).</summary>
            public const string RowText = "TextFillColorPrimaryBrush";

            /// <summary>A row's own text while the tree is disabled.</summary>
            public const string RowTextDisabled = "TextControlForegroundDisabled";

            /// <summary>The line where dragged rows will land.</summary>
            public const string DragInsertionLine = AJutThemeKeys.FlatTree_DragInsertionLine;

            /// <summary>The dashed connector from the insertion line to the new parent's chevron.</summary>
            public const string DragParentConnector = AJutThemeKeys.FlatTree_DragParentConnector;

            /// <summary>The new parent's chevron while a drop would land under it.</summary>
            public const string DragTargetHighlight = AJutThemeKeys.FlatTree_DragTargetHighlight;

            // ===========[ Sizes ]===================================
            /// <summary>The indent per tree depth, as on WinUI3.</summary>
            public const float TreeDepthIndentSize = 16f;

            /// <summary>
            /// The chevron's column. Wider than WinUI3's 18, for a finger; a tap anywhere across a row's indent and this
            /// column toggles the row, so a deep row's target is wider still.
            /// </summary>
            public const float ExpanderColumnWidth = 32f;

            /// <summary>Half the length of each of the chevron's two strokes.</summary>
            public const float ExpanderGlyphHalfLength = 4f;
            public const float ExpanderGlyphStrokeThickness = 1.5f;
            public const float ExpanderPressedCornerRadius = 4f;

            /// <summary>A row is never shorter than this, Apple's smallest comfortable touch target.</summary>
            public const float MinRowHeight = 44f;

            /// <summary>The space between rows, as on WinUI3.</summary>
            public const float RowSpacing = 2f;

            /// <summary>The selected row's strip down its leading edge.</summary>
            public const float SelectionStripWidth = 4f;

            /// <summary>The selected and pressed rows' outlines.</summary>
            public const float OutlineThickness = 1f;

            /// <summary>The space between the selection strip and the row's indent, WinUI3's row content margin.</summary>
            public const float ContentLeadingInset = 6f;

            /// <summary>The space after the row's content.</summary>
            public const float ContentTrailingInset = 8f;

            public const float DragInsertionLineThickness = 3f;
            public const float DragParentConnectorThickness = 1f;

            /// <summary>The dashes of the parent connector, on then off.</summary>
            public const float DragParentConnectorDash = 4f;
            public const float DragParentConnectorGap = 3f;

            /// <summary>The space left between the insertion line's end and the list's trailing edge.</summary>
            public const float DragInsertionLineEndInset = 4f;

            /// <summary>The opacity of the rows being dragged, so they read as lifted out of the list.</summary>
            public const float DraggedRowOpacity = 0.5f;

            /// <summary>How near the top or bottom edge a dragging finger starts the list scrolling.</summary>
            public const float DragAutoScrollEdge = 48f;

            /// <summary>How far the list scrolls per frame with the finger at the very edge; less the farther in it is.</summary>
            public const float DragAutoScrollMaxStep = 12f;

            // ===========[ Timing ]===================================
            /// <summary>How long a row is held before it lifts to be dragged.</summary>
            public static readonly TimeSpan DragLongPressDelay = TimeSpan.FromMilliseconds(400);
        }
    }
}
