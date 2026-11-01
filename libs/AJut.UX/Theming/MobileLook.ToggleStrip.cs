namespace AJut.UX.Theming
{
    public static partial class MobileLook
    {
        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// ToggleStrip: one rounded strip on the pop-out surface, outlined with the common border, with a line of the
        /// same color between its items, as WinUI3's ToggleStripButtons take the strip's own border. A selected item is
        /// shaded in under the checked gradient. Pressed or under a pointer (a stylus or a mouse), an item fills with the
        /// primary highlight and its text goes to the light highlight text that reads on it. A disabled strip looks like a
        /// disabled ComboBox, as on WinUI3 and WPF: the flat disabled control fill in place of the pop-out surface, the
        /// common border for its outline and the lines between items, and the disabled text on every item. The items
        /// keep their fills, so the selection still shows.
        /// </summary>
        public static class ToggleStrip
        {
            // ===========[ Keys ]===================================
            /// <summary>The strip's surface. A gradient.</summary>
            public const string StripSurface = "AJut_Brush_PopOutControlBackground";

            /// <summary>The strip's outline, and the line between its items.</summary>
            public const string StripBorder = "AJut_Brush_BorderCommon";

            /// <summary>The strip's surface while it is disabled: the flat fill a disabled ComboBox takes. Solid.</summary>
            public const string StripDisabledSurface = "AJut_Brush_ControlBackgroundDisabled";

            /// <summary>The strip's outline, and the line between its items, while it is disabled: a disabled ComboBox's border.</summary>
            public const string StripDisabledBorder = "AJut_Brush_BorderCommon";
            public const string ItemText = "TextFillColorPrimaryBrush";

            /// <summary>An item's text over the pressed and pointer-over fills.</summary>
            public const string ItemHighlightText = AJutThemeKeys.ToggleStripButton_HighlightForeground;

            /// <summary>Every item's text, and the overflow button's, while the strip is disabled.</summary>
            public const string ItemDisabledText = "ToggleButtonForegroundDisabled";
            public const string ItemPointerOverFill = AJutThemeKeys.ToggleStripButton_PointerOverBackground;
            public const string ItemPressedFill = AJutThemeKeys.ToggleStripButton_PressedBackground;

            /// <summary>A selected item's shade. A gradient.</summary>
            public const string ItemSelectedFill = AJutThemeKeys.ToggleStripButton_CheckedBackground;

            /// <summary>
            /// A selected item's fill under a pointer: themed the primary highlight's solid fill, as an unselected item's,
            /// and unthemed a deeper checked gradient, so a toolkit paints it on its gradient path, which takes either.
            /// </summary>
            public const string ItemSelectedPointerOverFill = AJutThemeKeys.ToggleStripButton_CheckedPointerOverBackground;

            /// <summary>A selected item's fill while pressed, themed solid and unthemed a gradient, as <see cref="ItemSelectedPointerOverFill"/>.</summary>
            public const string ItemSelectedPressedFill = AJutThemeKeys.ToggleStripButton_CheckedPressedBackground;

            /// <summary>The dots past a Scroll strip's ends, one at each end it can still scroll toward.</summary>
            public const string ScrollIndicator = "AJut_Brush_SymbolForeground";

            // ===========[ Sizes ]===================================
            public const float CornerRadius = 4f;
            public const float BorderThickness = 1f;

            /// <summary>The line between items.</summary>
            public const float SeparatorThickness = 1f;
            public const float ItemPaddingHorizontal = 12f;
            public const float ItemPaddingVertical = 8f;

            /// <summary>An item's least height, so it stays a comfortable touch target.</summary>
            public const float ItemMinHeight = 40f;

            // ===========[ Public Interface Methods ]===================================
            /// <summary>The key the strip's surface takes: the pop-out surface, or the flat disabled fill while disabled.</summary>
            public static string StripSurfaceFor (bool isEnabled) => isEnabled ? StripSurface : StripDisabledSurface;

            /// <summary>The key the strip's outline and the lines between its items take, enabled or disabled.</summary>
            public static string StripBorderFor (bool isEnabled) => isEnabled ? StripBorder : StripDisabledBorder;

            /// <summary>The key an item's fill takes in its state, or null for none (unselected and at rest).</summary>
            public static string? ItemFill (bool isSelected, bool isPressed, bool isPointerOver)
            {
                if (isSelected)
                {
                    return isPressed ? ItemSelectedPressedFill : (isPointerOver ? ItemSelectedPointerOverFill : ItemSelectedFill);
                }

                return isPressed ? ItemPressedFill : (isPointerOver ? ItemPointerOverFill : null);
            }

            /// <summary>The key an item's text takes in its state: the disabled text while disabled, otherwise the highlight text over the pressed and pointer-over fills.</summary>
            public static string ItemTextFor (bool isEnabled, bool isPressed, bool isPointerOver)
            {
                if (!isEnabled)
                {
                    return ItemDisabledText;
                }

                return (isPressed || isPointerOver) ? ItemHighlightText : ItemText;
            }
        }
    }
}
