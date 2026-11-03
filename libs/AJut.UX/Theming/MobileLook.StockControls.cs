namespace AJut.UX.Theming
{
    public static partial class MobileLook
    {
        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// The platforms' own widgets an app uses beside AJut's (Android's CheckBox, RadioButton, Switch and Spinner, and
        /// iOS's UISwitch and the window's tint), so they take the AJut accent and palette too. Each part takes the stock
        /// key the WinUI3 theme gives the same part of the same control, which the shared map aliases, so a palette change
        /// reaches them along with every other stack. A platform's ripple is left as it is.
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

            // ===========[ Sizes ]===================================
            /// <summary>The selection fill's opacity, WPF's text box selection opacity, so the selected text still reads.</summary>
            public const float TextSelectionOpacity = 0.4f;
        }
    }
}
