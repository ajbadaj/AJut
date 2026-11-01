namespace AJut.UX.Theming
{
    using UIKit;

    // ===========[ ThemedColorKeys ]============================================
    // Every color key AJut's UIKit controls paint with, and its unthemed default: the UIKit counterpart to WinUI3's
    // AJutCustomControlStylingDefaults.xaml. The keys are the WinUI3 ones, so a key means the same thing on every
    // stack, and the themed value for each comes from AJut.UX's palette and key map through ThemedColors.
    //
    // Unthemed defaults are UIKit system colors wherever one fits, so an unthemed AJut control sits among stock UIKit
    // controls without standing out, in light and dark alike. A null default means "the view's TintColor", for the
    // accent colored keys, the way UIKit's own controls take their accent.

    public static class ThemedColorKeys
    {
        // ===========[ Stock button keys (BrushButton, GeometryButton) ]======================
        public const string ButtonBackground = "ButtonBackground";
        public const string ButtonBackgroundPointerOver = "ButtonBackgroundPointerOver";
        public const string ButtonBackgroundPressed = "ButtonBackgroundPressed";
        public const string ButtonBackgroundDisabled = "ButtonBackgroundDisabled";
        public const string ButtonForeground = "ButtonForeground";
        public const string ButtonForegroundPointerOver = "ButtonForegroundPointerOver";
        public const string ButtonForegroundPressed = "ButtonForegroundPressed";
        public const string ButtonForegroundDisabled = "ButtonForegroundDisabled";
        public const string ButtonBorderBrush = "ButtonBorderBrush";
        public const string ButtonBorderBrushPointerOver = "ButtonBorderBrushPointerOver";
        public const string ButtonBorderBrushPressed = "ButtonBorderBrushPressed";
        public const string ButtonBorderBrushDisabled = "ButtonBorderBrushDisabled";

        /// <summary>The accent AutoBrushButton derives from when none is set</summary>
        public const string SystemAccentColor = "SystemAccentColor";

        // ===========[ Stock toggle button keys (ToggleStrip, EnumToggleStrip) ]==============
        // The strip's base and text. Its state overlays (pointer over, pressed and the checked gradients) are the
        // AJut_ToggleStripButton_* keys, which ToggleStrip resolves as ThemedGradients.
        public const string ToggleButtonBackground = "ToggleButtonBackground";
        public const string ToggleButtonForeground = "ToggleButtonForeground";
        public const string ToggleButtonForegroundDisabled = "ToggleButtonForegroundDisabled";

        /// <summary>
        /// A ToggleStrip item's text over its pressed and pointer-over fills, which with the AJut theme are the primary
        /// highlight's solid colors: the light text that reads on them, as on WinUI3, AppKit and Android. Unthemed the
        /// fills are faint washes, so it is the normal toggle text.
        /// </summary>
        public const string ToggleStripButton_HighlightForeground = "AJut_ToggleStripButton_HighlightForeground";

        /// <summary>
        /// The common border, which ToggleStrip draws its outline and separators with. Not ToggleButtonBorderBrush, which
        /// is a button's own border: the touch palettes take that off, while a strip keeps its outline and separators.
        /// </summary>
        public const string Brush_BorderCommon = "AJut_Brush_BorderCommon";

        // ===========[ Stock combo box keys (EnumComboBox) ]==================================
        public const string ComboBoxBackground = "ComboBoxBackground";
        public const string ComboBoxForeground = "ComboBoxForeground";

        // ===========[ Stock text keys (NumericEditor) ]=====================================
        public const string TextControlForeground = "TextControlForeground";
        public const string TextControlForegroundDisabled = "TextControlForegroundDisabled";

        /// <summary>The text box color, which NumericEditor's number sits in a well of, as WinUI3's TextBoxBackground does.</summary>
        public const string Brush_TextBoxBackground = "AJut_Brush_TextBoxBackground";

        // ===========[ AJut control keys ]===================================================
        public const string NumericEditor_NormalBorderBrush = "AJut_NumericEditor_NormalBorderBrush";
        public const string NumericEditor_FocusBorderBrush = "AJut_NumericEditor_FocusBorderBrush";
        public const string NumericEditor_ErrorBrush = "AJut_NumericEditor_ErrorBrush";

        public const string ColorEditIngressControl_NormalBorderBrush = "AJut_ColorEditIngressControl_NormalBorderBrush";
        public const string ColorEditIngressControl_HighlightBorderBrush = "AJut_ColorEditIngressControl_HighlightBorderBrush";

        /// <summary>
        /// GeometryButton's traced outline, at rest. The trace keys take the common borders rather than the button border
        /// keys, so a shape's outline (and a shape drawn as an outline alone) stays on touch, where the touch palettes
        /// take button borders off. Unthemed they match the stock button border keys.
        /// </summary>
        public const string GeometryButton_Trace = "AJut_GeometryButton_Trace";
        public const string GeometryButton_TracePointerOver = "AJut_GeometryButton_TracePointerOver";
        public const string GeometryButton_TracePressed = "AJut_GeometryButton_TracePressed";
        public const string GeometryButton_TraceDisabled = "AJut_GeometryButton_TraceDisabled";

        // ===========[ Public Interface Methods ]===================================
        /// <summary>
        /// Registers every key above with its unthemed default. Runs once, from ThemedColors' static constructor;
        /// calling it again puts back any default a consumer has replaced.
        /// </summary>
        public static void RegisterAll ()
        {
            // 1. Buttons: a bordered system button
            ThemedColors.SetUnthemedDefault(ButtonBackground, UIColor.SecondarySystemFill);
            ThemedColors.SetUnthemedDefault(ButtonBackgroundPointerOver, UIColor.SystemFill);
            ThemedColors.SetUnthemedDefault(ButtonBackgroundPressed, UIColor.TertiarySystemFill);
            ThemedColors.SetUnthemedDefault(ButtonBackgroundDisabled, UIColor.QuaternarySystemFill);
            ThemedColors.SetUnthemedDefault(ButtonForeground, UIColor.Label);
            ThemedColors.SetUnthemedDefault(ButtonForegroundPointerOver, UIColor.Label);
            ThemedColors.SetUnthemedDefault(ButtonForegroundPressed, UIColor.SecondaryLabel);
            ThemedColors.SetUnthemedDefault(ButtonForegroundDisabled, UIColor.TertiaryLabel);
            ThemedColors.SetUnthemedDefault(ButtonBorderBrush, UIColor.Separator);
            ThemedColors.SetUnthemedDefault(ButtonBorderBrushPointerOver, UIColor.Separator);
            ThemedColors.SetUnthemedDefault(ButtonBorderBrushPressed, UIColor.Separator);
            ThemedColors.SetUnthemedDefault(ButtonBorderBrushDisabled, UIColor.QuaternarySystemFill);
            ThemedColors.SetUnthemedDefault(SystemAccentColor, null);

            // 2. Toggles: opaque, because a ToggleStrip's separators are its row showing through the gaps
            ThemedColors.SetUnthemedDefault(ToggleButtonBackground, UIColor.SecondarySystemBackground);
            ThemedColors.SetUnthemedDefault(ToggleButtonForeground, UIColor.Label);
            ThemedColors.SetUnthemedDefault(ToggleButtonForegroundDisabled, UIColor.TertiaryLabel);
            ThemedColors.SetUnthemedDefault(ToggleStripButton_HighlightForeground, UIColor.Label);
            ThemedColors.SetUnthemedDefault(Brush_BorderCommon, UIColor.Separator);

            // 3. Combo box
            ThemedColors.SetUnthemedDefault(ComboBoxBackground, UIColor.SecondarySystemFill);
            ThemedColors.SetUnthemedDefault(ComboBoxForeground, UIColor.Label);

            // 4. Text
            ThemedColors.SetUnthemedDefault(TextControlForeground, UIColor.Label);
            ThemedColors.SetUnthemedDefault(TextControlForegroundDisabled, UIColor.TertiaryLabel);
            ThemedColors.SetUnthemedDefault(Brush_TextBoxBackground, UIColor.SystemBackground);

            // 5. NumericEditor and ColorEditIngressControl
            ThemedColors.SetUnthemedDefault(NumericEditor_NormalBorderBrush, UIColor.Separator);
            ThemedColors.SetUnthemedDefault(NumericEditor_FocusBorderBrush, null);
            ThemedColors.SetUnthemedDefault(NumericEditor_ErrorBrush, UIColor.SystemRed);
            ThemedColors.SetUnthemedDefault(ColorEditIngressControl_NormalBorderBrush, UIColor.Separator);
            ThemedColors.SetUnthemedDefault(ColorEditIngressControl_HighlightBorderBrush, null);

            // 6. The MobileLook keys no other UIKit key above already covers: what NumericEditor's label, step glyphs and
            //  pressed step buttons, a Scroll ToggleStrip's end dots, a disabled ToggleStrip's outline, and a pressed
            //  GeometryButton's outline fall back to with no theme loaded
            ThemedColors.SetUnthemedDefault(MobileLook.NumericEditor.Label, UIColor.Label);
            ThemedColors.SetUnthemedDefault(MobileLook.NumericEditor.StepGlyph, UIColor.Label);
            ThemedColors.SetUnthemedDefault(MobileLook.NumericEditor.StepPressedFill, UIColor.SystemBlue);
            ThemedColors.SetUnthemedDefault(MobileLook.ToggleStrip.ScrollIndicator, UIColor.SecondaryLabel);
            ThemedColors.SetUnthemedDefault(MobileLook.ToggleStrip.StripDisabledBorder, UIColor.Separator);
            ThemedColors.SetUnthemedDefault(MobileLook.GeometryButton.PressedTrace, UIColor.SystemBlue);

            // 7. GeometryButton's trace, unthemed the same as the stock button borders above
            ThemedColors.SetUnthemedDefault(GeometryButton_Trace, UIColor.Separator);
            ThemedColors.SetUnthemedDefault(GeometryButton_TracePointerOver, UIColor.Separator);
            ThemedColors.SetUnthemedDefault(GeometryButton_TracePressed, UIColor.Separator);
            ThemedColors.SetUnthemedDefault(GeometryButton_TraceDisabled, UIColor.QuaternarySystemFill);
        }
    }
}
