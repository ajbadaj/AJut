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
        /// common border for its outline and the lines between items, and the understated text, faded, on every item. A
        /// selected item keeps a muted fill, so the selection still shows without reading as live. The overflow button is a
        /// button, set off from the last item by a line of the border: the button fills and text under a press or a
        /// pointer, and across a vertical strip the plain button fill at rest.
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

            /// <summary>Every item's text, and the overflow button's, while the strip is disabled: the flagships' disabled strip text, the understated text faded.</summary>
            public const string ItemDisabledText = AJutThemeKeys.ToggleStrip_DisabledForeground;
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

            /// <summary>A selected item's fill while the strip is disabled: the muted primary the flagships' disabled selection takes. Solid.</summary>
            public const string ItemSelectedDisabledFill = AJutThemeKeys.ToggleStripButton_CheckedDisabledBackground;

            /// <summary>The dots past a Scroll strip's ends, one at each end it can still scroll toward.</summary>
            public const string ScrollIndicator = "AJut_Brush_SymbolForeground";

            /// <summary>
            /// The overflow button's text. It is a button rather than an item, so it takes the button keys, as WinUI3's
            /// overflow button does; disabled, it takes <see cref="ItemDisabledText"/> with the items.
            /// </summary>
            public const string OverflowText = "ButtonForeground";
            public const string OverflowPointerOverText = "ButtonForegroundPointerOver";
            public const string OverflowPressedText = "ButtonForegroundPressed";
            public const string OverflowPointerOverFill = "ButtonBackgroundPointerOver";
            public const string OverflowPressedFill = "ButtonBackgroundPressed";

            /// <summary>
            /// The overflow button's fill at rest across a vertical strip: the plain button fill. The strip's surface shades
            /// top to bottom, so across a vertical strip's end the button would show a slice of it that reads as part of
            /// neither. Along a horizontal strip it has no fill at rest, so the strip's own surface shows.
            /// </summary>
            public const string OverflowVerticalFill = "ButtonBackground";

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

            /// <summary>
            /// The key an item's fill takes in its state, or null for none (unselected, and at rest or disabled). A disabled
            /// strip takes no press or pointer, so only a selected item there is filled, with the muted fill.
            /// </summary>
            public static string? ItemFill (bool isEnabled, bool isSelected, bool isPressed, bool isPointerOver)
            {
                if (!isEnabled)
                {
                    return isSelected ? ItemSelectedDisabledFill : null;
                }

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

            /// <summary>
            /// The key the overflow button's fill takes in its state, or null for none, so the strip's surface shows: at rest
            /// along a horizontal strip, and always while disabled, when the strip's flat disabled fill is the button's too.
            /// </summary>
            public static string? OverflowFill (bool isEnabled, bool isVertical, bool isPressed, bool isPointerOver)
            {
                if (!isEnabled)
                {
                    return null;
                }

                if (isPressed)
                {
                    return OverflowPressedFill;
                }

                if (isPointerOver)
                {
                    return OverflowPointerOverFill;
                }

                return isVertical ? OverflowVerticalFill : null;
            }

            /// <summary>The key the overflow button's text takes in its state: the items' disabled text while disabled, otherwise the button text for the state.</summary>
            public static string OverflowTextFor (bool isEnabled, bool isPressed, bool isPointerOver)
            {
                if (!isEnabled)
                {
                    return ItemDisabledText;
                }

                return isPressed ? OverflowPressedText : (isPointerOver ? OverflowPointerOverText : OverflowText);
            }
        }
    }
}
