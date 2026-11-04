namespace AJut.UX.Theming
{
    public static partial class MobileLook
    {
        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// The platforms' own widgets an app uses beside AJut's (Android's CheckBox, RadioButton, Switch, Spinner, plain
        /// Button and EditText; iOS's UISwitch, plain buttons, text fields and the window's tint), so they take the AJut accent and
        /// palette too. Each part takes the stock key the WinUI3 theme gives the same part of the same control, which the
        /// shared map aliases, so a palette change reaches them along with every other stack. A platform's ripple is left
        /// as it is, except on a plain button, which shows a press with color as BrushButton does.
        /// </summary>
        public static class StockControls
        {
            // ===========[ Keys ]===================================
            /// <summary>The accent: Android's colorAccent and colorControlActivated, and the window tint on iOS.</summary>
            public const string Accent = "SystemAccentColor";

            /// <summary>A checked switch's track, and on iOS the switch's on tint.</summary>
            public const string SwitchTrackOn = "ToggleSwitchFillOn";

            /// <summary>A checked switch's thumb.</summary>
            public const string SwitchThumbOn = "ToggleSwitchKnobFillOn";

            /// <summary>An unchecked switch's track. A stock track takes one color and draws no outline, so it takes the outline's.</summary>
            public const string SwitchTrackOff = "ToggleSwitchStrokeOff";

            /// <summary>An unchecked switch's thumb.</summary>
            public const string SwitchThumbOff = "ToggleSwitchKnobFillOff";

            public const string SwitchTrackOnDisabled = "ToggleSwitchFillOnDisabled";
            public const string SwitchThumbOnDisabled = "ToggleSwitchKnobFillOnDisabled";
            public const string SwitchTrackOffDisabled = "ToggleSwitchStrokeOffDisabled";
            public const string SwitchThumbOffDisabled = "ToggleSwitchKnobFillOffDisabled";

            /// <summary>
            /// A checked check box. It is the check's color, not the box fill's: a stock check box is one color with the
            /// check cut out of it, so the fill's key would hide the check.
            /// </summary>
            public const string CheckBoxChecked = "CheckBoxCheckGlyphForegroundChecked";

            /// <summary>
            /// An unchecked check box's outline: the understated text, which reads on a dark panel and a light one. WinUI3's
            /// own outline key, CheckBoxCheckBackgroundStrokeUnchecked, takes the text box border, a near black that all but
            /// vanishes on a stock box in Dark, where WinUI3's own box sits on a lighter fill.
            /// </summary>
            public const string CheckBoxUnchecked = "AJut_Brush_UnderstatedText";

            /// <summary>A check box while it is pressed.</summary>
            public const string CheckBoxPressed = "CheckBoxCheckGlyphForegroundCheckedPressed";

            public const string CheckBoxCheckedDisabled = "CheckBoxCheckGlyphForegroundCheckedDisabled";
            public const string CheckBoxUncheckedDisabled = "CheckBoxCheckBackgroundStrokeUncheckedDisabled";

            /// <summary>A checked radio button.</summary>
            public const string RadioButtonChecked = "RadioButtonCheckGlyphFill";

            /// <summary>An unchecked radio button's ring.</summary>
            public const string RadioButtonUnchecked = "RadioButtonOuterEllipseStroke";

            /// <summary>A radio button while it is pressed.</summary>
            public const string RadioButtonPressed = "RadioButtonCheckGlyphFillPressed";

            public const string RadioButtonCheckedDisabled = "RadioButtonCheckGlyphFillDisabled";
            public const string RadioButtonUncheckedDisabled = "RadioButtonOuterEllipseStrokeDisabled";

            /// <summary>A drop-down's arrow.</summary>
            public const string SpinnerArrow = "ComboBoxDropDownGlyphForeground";

            /// <summary>A drop-down's open list.</summary>
            public const string SpinnerPopupBackground = "ComboBoxDropDownBackground";

            /// <summary>The text in a drop-down's open list.</summary>
            public const string SpinnerPopupText = "TextFillColorPrimaryBrush";

            /// <summary>Selected text in a text field, laid under the text at <see cref="TextSelectionOpacity"/>.</summary>
            public const string TextSelection = "TextControlSelectionHighlightColor";

            // A plain button (Android's Button, a UIKit system button): the stock button keys WinUI3's AJut theme gives
            // its Button, so a stock button looks like an untouched BrushButton and like a flagship button
            public const string ButtonBackground = "ButtonBackground";
            public const string ButtonBackgroundPointerOver = "ButtonBackgroundPointerOver";
            public const string ButtonBackgroundPressed = "ButtonBackgroundPressed";
            public const string ButtonBackgroundDisabled = "ButtonBackgroundDisabled";
            public const string ButtonForeground = "ButtonForeground";
            public const string ButtonForegroundPointerOver = "ButtonForegroundPointerOver";
            public const string ButtonForegroundPressed = "ButtonForegroundPressed";
            public const string ButtonForegroundDisabled = "ButtonForegroundDisabled";
            public const string ButtonBorder = "ButtonBorderBrush";
            public const string ButtonBorderPointerOver = "ButtonBorderBrushPointerOver";
            public const string ButtonBorderPressed = "ButtonBorderBrushPressed";
            public const string ButtonBorderDisabled = "ButtonBorderBrushDisabled";

            // A text field (Android's EditText, a UIKit text field): the normal AJut text box, as WPF's TextBox style has
            // it. The border takes the highlight while the field has focus and the bright highlight under a pointer, and
            // the pointer wins over focus, as the later of WPF's two triggers.
            public const string TextFieldBackground = "AJut_Brush_TextBoxBackground";
            public const string TextFieldBorder = "AJut_Brush_TextBoxBorder";
            public const string TextFieldBorderFocused = "AJut_Brush_PrimaryHighlight";
            public const string TextFieldBorderPointerOver = "AJut_Brush_PrimaryHighlightBright";
            public const string TextFieldForeground = "AJut_Brush_InteractiveTextUnfocused";
            public const string TextFieldForegroundFocused = "AJut_Brush_InteractiveText";
            public const string TextFieldForegroundDisabled = "TextControlForegroundDisabled";
            public const string TextFieldPlaceholder = "TextControlPlaceholderForeground";

            /// <summary>The caret, where the platform lets it be colored apart from the selection (Android 10 and later).</summary>
            public const string TextFieldCaret = "AJut_Brush_InteractiveText";

            // ===========[ Sizes ]===================================
            /// <summary>The selection fill's opacity, WPF's text box selection opacity, so the selected text still reads.</summary>
            public const float TextSelectionOpacity = 0.4f;

            /// <summary>A plain button's corners, the same as BrushButton's default.</summary>
            public const float ButtonCornerRadius = 4f;

            /// <summary>A plain button's outline, the same as BrushButton's default.</summary>
            public const float ButtonBorderThickness = 1f;

            /// <summary>A text field's corners, as the NumericEditor's frame has them.</summary>
            public const float TextFieldCornerRadius = 4f;

            /// <summary>A text field's outline.</summary>
            public const float TextFieldBorderThickness = 1f;

            /// <summary>
            /// The least space between a text field's outline and its text, above and below, on top of the outline's
            /// thickness: WPF's text box pads 3, and a phone's text sits better a little further in.
            /// </summary>
            public const float TextFieldVerticalPadding = 4f;

            /// <summary>A text field's caret width, where the platform takes a drawn caret.</summary>
            public const float TextFieldCaretWidth = 2f;

            /// <summary>
            /// The space between a UIKit text field's outline and its text. WPF's text box pads 4 by 3; a phone keeps its
            /// own touch height, so only the sides take a padding, wide enough that the text clears the rounded corners.
            /// </summary>
            public const float TextFieldHorizontalPadding = 8f;

            /// <summary>
            /// A UIKit text field's least height: what the system's rounded field stands at, which a field drawn by AJut
            /// would otherwise lose, since a borderless field is only as tall as its text.
            /// </summary>
            public const float TextFieldMinHeight = 34f;
        }
    }
}
