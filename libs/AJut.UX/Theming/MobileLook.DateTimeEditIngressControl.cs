namespace AJut.UX.Theming
{
    public static partial class MobileLook
    {
        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// DateTimeEditIngressControl: one rounded frame on the pop-out surface, holding the text in a well of the text box
        /// color, the warning glyph while the text is invalid, and the picker button at the trailing end. The frame's border
        /// takes the focused color while the text is being edited and the invalid color while the text can't be read, which
        /// wins. The picker button is the frame's own surface at rest, and fills with the highlight under a mouse or stylus
        /// and while pressed, with its glyph lit on it, as WinUI3's does.
        /// </summary>
        public static class DateTimeEditIngressControl
        {
            // ===========[ Keys ]===================================
            /// <summary>The frame's surface.</summary>
            public const string FrameSurface = "AJut_Brush_PopOutControlBackground";

            /// <summary>The frame's border at rest.</summary>
            public const string FrameBorder = AJutThemeKeys.DateTimeEditIngressControl_NormalBorderBrush;

            /// <summary>The frame's border while the text is being edited.</summary>
            public const string FrameBorderFocused = AJutThemeKeys.DateTimeEditIngressControl_FocusedBorderBrush;

            /// <summary>The frame's border while the text can't be read, which wins over focus.</summary>
            public const string FrameBorderInvalid = AJutThemeKeys.DateTimeEditIngressControl_InvalidBorderBrush;

            /// <summary>The well the text sits in.</summary>
            public const string WellSurface = AJutThemeKeys.DateTimeEditIngressControl_FocusedTextBoxBackgroundBrush;

            /// <summary>The text, read-only or not: read-only changes only its style, to italics a touch faded.</summary>
            public const string Text = "TextControlForeground";

            /// <summary>The prompt shown while there is no value and nothing typed.</summary>
            public const string PlaceholderText = "TextControlPlaceholderForeground";

            /// <summary>The warning glyph beside text that can't be read.</summary>
            public const string WarningGlyph = AJutThemeKeys.DateTimeEditIngressControl_WarningIconForeground;

            /// <summary>The picker button's glyph at rest.</summary>
            public const string ButtonGlyph = AJutThemeKeys.DateTimeEditIngressControl_ButtonIconForeground;

            /// <summary>The picker button's fill under a mouse or stylus.</summary>
            public const string ButtonPointerOverFill = AJutThemeKeys.DateTimeEditIngressControl_ButtonPointerOverBackground;

            /// <summary>The picker button's fill while pressed, and while its pickers are open.</summary>
            public const string ButtonPressedFill = AJutThemeKeys.DateTimeEditIngressControl_ButtonPressedBackground;

            /// <summary>The picker button's glyph over its pointer-over and pressed fills.</summary>
            public const string ButtonHighlightGlyph = AJutThemeKeys.DateTimeEditIngressControl_ButtonHighlightForeground;

            // ===========[ Sizes ]===================================
            public const float CornerRadius = 4f;
            public const float BorderThickness = 1f;

            /// <summary>The picker button's width, Apple's smallest comfortable touch target.</summary>
            public const float PickerButtonWidth = 44f;

            /// <summary>The picker button's glyph, square.</summary>
            public const float PickerGlyphSize = 18f;

            /// <summary>How far the well sits in from the frame, inside its border.</summary>
            public const float WellInset = 3f;

            /// <summary>The space between the well's edges and the text.</summary>
            public const float WellTextPadding = 6f;

            /// <summary>The warning glyph, square.</summary>
            public const float WarningGlyphSize = 16f;

            /// <summary>The space either side of the warning glyph.</summary>
            public const float WarningGlyphSpacing = 4f;

            /// <summary>The control is never shorter than this, Apple's smallest comfortable touch target.</summary>
            public const float MinTouchHeight = 44f;

            /// <summary>The space between the calendar and the time picker, when both are offered.</summary>
            public const float PickerSpacing = 8f;

            // ===========[ Public Interface Methods ]===================================
            /// <summary>
            /// The frame's border key for the control's state: text that can't be read takes the invalid border, then editing
            /// takes the focused one. Read-only takes the same borders as editable.
            /// </summary>
            public static string FrameBorderFor (bool isTextInvalid, bool isEditing)
            {
                if (isTextInvalid)
                {
                    return FrameBorderInvalid;
                }

                return isEditing ? FrameBorderFocused : FrameBorder;
            }
        }
    }
}
