namespace AJut.UX.Theming
{
    public static partial class MobileLook
    {
        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// PopupMenuButton: with the AJut theme loaded, the stock ToggleButton look WinUI3's PART_Button takes, its face, text
        /// and border each following the state, and the button shown checked while its menu is open, as WinUI3 shows it.
        /// The chevron takes its own key in every state, as WinUI3's chevron glyph does. The sizes are a phone's, the same as
        /// EnumComboBox's, so the two pull-down buttons sit together. Unthemed, each toolkit keeps its own platform button.
        /// The menu itself is the platform's on both toolkits.
        /// </summary>
        public static class PopupMenuButton
        {
            // ===========[ Keys ]===================================
            public const string Fill = "ToggleButtonBackground";
            public const string Text = "ToggleButtonForeground";
            public const string Border = "ToggleButtonBorderBrush";

            /// <summary>The chevron, in every state</summary>
            public const string Chevron = AJutThemeKeys.PopupMenuButton_ChevronForeground;

            /// <summary>Hover, which only a pointer gives (an iPad with a trackpad)</summary>
            public const string PointerOverFill = "ToggleButtonBackgroundPointerOver";
            public const string PointerOverText = "ToggleButtonForegroundPointerOver";
            public const string PointerOverBorder = "ToggleButtonBorderBrushPointerOver";

            public const string PressedFill = "ToggleButtonBackgroundPressed";
            public const string PressedText = "ToggleButtonForegroundPressed";
            public const string PressedBorder = "ToggleButtonBorderBrushPressed";

            public const string DisabledFill = "ToggleButtonBackgroundDisabled";
            public const string DisabledText = "ToggleButtonForegroundDisabled";
            public const string DisabledBorder = "ToggleButtonBorderBrushDisabled";

            /// <summary>While the menu is open: the checked look</summary>
            public const string OpenFill = "ToggleButtonBackgroundChecked";
            public const string OpenText = "ToggleButtonForegroundChecked";
            public const string OpenBorder = "ToggleButtonBorderBrushChecked";

            public const string OpenPointerOverFill = "ToggleButtonBackgroundCheckedPointerOver";
            public const string OpenPointerOverText = "ToggleButtonForegroundCheckedPointerOver";
            public const string OpenPointerOverBorder = "ToggleButtonBorderBrushCheckedPointerOver";

            public const string OpenPressedFill = "ToggleButtonBackgroundCheckedPressed";
            public const string OpenPressedText = "ToggleButtonForegroundCheckedPressed";
            public const string OpenPressedBorder = "ToggleButtonBorderBrushCheckedPressed";

            // ===========[ Sizes ]===================================
            public const float CornerRadius = 4f;
            public const float BorderThickness = 1f;
            public const float PaddingHorizontal = 12f;
            public const float PaddingVertical = 8f;

            /// <summary>The chevron's width; it is half as tall.</summary>
            public const float ChevronWidth = 10f;
            public const float ChevronThickness = 1.5f;

            /// <summary>The space between the text and the chevron</summary>
            public const float ChevronSpacing = 6f;
        }
    }
}
