namespace AJut.UX.Theming
{
    public static partial class MobileLook
    {
        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// EnumComboBox: with the AJut theme loaded, a rounded face on the pop-out surface outlined with the text box border,
        /// the selected value in the interactive text and a chevron at its trailing end, as WPF's themed ComboBox draws it.
        /// The stock ComboBox keys, so an app that restyles ComboBox restyles this too. Pressed, the face takes the pressed
        /// button fill. Unthemed, each toolkit keeps its own platform drop-down.
        /// </summary>
        public static class EnumComboBox
        {
            // ===========[ Keys ]===================================
            /// <summary>The face. A gradient (the pop-out surface).</summary>
            public const string Surface = "ComboBoxBackground";
            public const string SurfacePressed = "ComboBoxBackgroundPressed";
            public const string Border = "ComboBoxBorderBrush";

            /// <summary>The selected value's text, and the chevron.</summary>
            public const string Text = "ComboBoxForeground";

            /// <summary>The open list's surface, where the toolkit draws the list itself.</summary>
            public const string DropDownSurface = "ComboBoxDropDownBackground";

            /// <summary>An option's text in the open list.</summary>
            public const string DropDownText = "TextFillColorPrimaryBrush";

            // ===========[ Sizes ]===================================
            public const float CornerRadius = 4f;
            public const float BorderThickness = 1f;
            public const float PaddingHorizontal = 12f;
            public const float PaddingVertical = 8f;

            /// <summary>The chevron's width; it is half as tall.</summary>
            public const float ChevronWidth = 10f;
            public const float ChevronThickness = 1.5f;
        }
    }
}
