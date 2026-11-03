namespace AJut.UX.Theming
{
    public static partial class MobileLook
    {
        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// EnumComboBox: with the AJut theme loaded, a rounded face on the pop-out surface outlined with the text box border,
        /// the selected value in the interactive text and a chevron in the symbol color at its trailing end, as the
        /// flagships' themed ComboBox draws it. The stock ComboBox keys, so an app that restyles ComboBox restyles this too.
        /// Under a pointer (a stylus or a mouse) the face takes the hover fill and the bright primary outline; pressed, the
        /// pressed button fill and the primary outline; disabled, the flat disabled fill, the common border, and the
        /// understated text and chevron. Where a toolkit draws the open list itself, the list is outlined with the list
        /// selection, the selected option is filled with it, and options under a pointer or pressed take the list's hover
        /// and pressed fills. Unthemed, each toolkit keeps its own platform drop-down.
        /// </summary>
        public static class EnumComboBox
        {
            // ===========[ Keys ]===================================
            /// <summary>The face. A gradient (the pop-out surface).</summary>
            public const string Surface = "ComboBoxBackground";
            public const string SurfacePointerOver = "ComboBoxBackgroundPointerOver";
            public const string SurfacePressed = "ComboBoxBackgroundPressed";
            public const string SurfaceDisabled = "ComboBoxBackgroundDisabled";
            public const string Border = "ComboBoxBorderBrush";
            public const string BorderPointerOver = "ComboBoxBorderBrushPointerOver";
            public const string BorderPressed = "ComboBoxBorderBrushPressed";
            public const string BorderDisabled = "ComboBoxBorderBrushDisabled";

            /// <summary>The selected value's text on the face.</summary>
            public const string Text = "ComboBoxForeground";
            public const string TextPointerOver = "ComboBoxForegroundPointerOver";
            public const string TextPressed = "ComboBoxForegroundPressed";
            public const string TextDisabled = "ComboBoxForegroundDisabled";

            /// <summary>The chevron at the face's trailing end.</summary>
            public const string Chevron = "ComboBoxDropDownGlyphForeground";
            public const string ChevronDisabled = "ComboBoxDropDownGlyphForegroundDisabled";

            /// <summary>The open list's surface, where the toolkit draws the list itself.</summary>
            public const string DropDownSurface = "ComboBoxDropDownBackground";

            /// <summary>The open list's outline, where the toolkit draws the list itself.</summary>
            public const string DropDownBorder = "ComboBoxDropDownBorderBrush";

            /// <summary>An option's text in the open list.</summary>
            public const string DropDownText = "ComboBoxItemForeground";
            public const string DropDownTextPointerOver = "ComboBoxItemForegroundPointerOver";
            public const string DropDownTextPressed = "ComboBoxItemForegroundPressed";
            public const string DropDownTextSelected = "ComboBoxItemForegroundSelected";
            public const string DropDownTextSelectedPointerOver = "ComboBoxItemForegroundSelectedPointerOver";
            public const string DropDownTextSelectedPressed = "ComboBoxItemForegroundSelectedPressed";

            /// <summary>An option's fill in the open list. At rest an unselected option has none.</summary>
            public const string DropDownItemPointerOverFill = "ComboBoxItemBackgroundPointerOver";
            public const string DropDownItemPressedFill = "ComboBoxItemBackgroundPressed";
            public const string DropDownItemSelectedFill = "ComboBoxItemBackgroundSelected";
            public const string DropDownItemSelectedPointerOverFill = "ComboBoxItemBackgroundSelectedPointerOver";
            public const string DropDownItemSelectedPressedFill = "ComboBoxItemBackgroundSelectedPressed";

            // ===========[ Sizes ]===================================
            public const float CornerRadius = 4f;
            public const float BorderThickness = 1f;
            public const float PaddingHorizontal = 12f;
            public const float PaddingVertical = 8f;

            /// <summary>The chevron's width; it is half as tall.</summary>
            public const float ChevronWidth = 10f;
            public const float ChevronThickness = 1.5f;

            // ===========[ Public Interface Methods ]===================================
            /// <summary>The key the face takes in its state. Disabled wins, then pressed, then a pointer over it.</summary>
            public static string SurfaceFor (bool isEnabled, bool isPressed, bool isPointerOver) => Pick(isEnabled, isPressed, isPointerOver, Surface, SurfacePointerOver, SurfacePressed, SurfaceDisabled);

            /// <summary>The key the face's outline takes in its state, in the same order as <see cref="SurfaceFor"/>.</summary>
            public static string BorderFor (bool isEnabled, bool isPressed, bool isPointerOver) => Pick(isEnabled, isPressed, isPointerOver, Border, BorderPointerOver, BorderPressed, BorderDisabled);

            /// <summary>The key the selected value's text takes in its state, in the same order as <see cref="SurfaceFor"/>.</summary>
            public static string TextFor (bool isEnabled, bool isPressed, bool isPointerOver) => Pick(isEnabled, isPressed, isPointerOver, Text, TextPointerOver, TextPressed, TextDisabled);

            /// <summary>The key the chevron takes: understated while disabled, as the text is.</summary>
            public static string ChevronFor (bool isEnabled) => isEnabled ? Chevron : ChevronDisabled;

            /// <summary>The key an option's fill takes in the open list, or null for none (unselected, at rest).</summary>
            public static string? DropDownItemFill (bool isSelected, bool isPressed, bool isPointerOver)
            {
                if (isSelected)
                {
                    return isPressed ? DropDownItemSelectedPressedFill : (isPointerOver ? DropDownItemSelectedPointerOverFill : DropDownItemSelectedFill);
                }

                return isPressed ? DropDownItemPressedFill : (isPointerOver ? DropDownItemPointerOverFill : null);
            }

            /// <summary>The key an option's text takes in the open list, in the same order as <see cref="DropDownItemFill"/>.</summary>
            public static string DropDownTextFor (bool isSelected, bool isPressed, bool isPointerOver)
            {
                if (isSelected)
                {
                    return isPressed ? DropDownTextSelectedPressed : (isPointerOver ? DropDownTextSelectedPointerOver : DropDownTextSelected);
                }

                return isPressed ? DropDownTextPressed : (isPointerOver ? DropDownTextPointerOver : DropDownText);
            }

            // ===========[ Helper Methods ]===================================
            private static string Pick (bool isEnabled, bool isPressed, bool isPointerOver, string rest, string pointerOver, string pressed, string disabled)
            {
                if (!isEnabled)
                {
                    return disabled;
                }

                return isPressed ? pressed : (isPointerOver ? pointerOver : rest);
            }
        }
    }
}
