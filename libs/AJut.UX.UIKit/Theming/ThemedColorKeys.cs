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
        // The strip's base, text and separators. Its state overlays (pointer over, pressed and the checked gradients)
        // are the AJut_ToggleStripButton_* keys, which ToggleStrip resolves as ThemedGradients.
        public const string ToggleButtonBackground = "ToggleButtonBackground";
        public const string ToggleButtonForeground = "ToggleButtonForeground";
        public const string ToggleButtonForegroundDisabled = "ToggleButtonForegroundDisabled";
        public const string ToggleButtonBorderBrush = "ToggleButtonBorderBrush";

        // ===========[ Stock combo box keys (EnumComboBox) ]==================================
        public const string ComboBoxBackground = "ComboBoxBackground";
        public const string ComboBoxForeground = "ComboBoxForeground";

        // ===========[ Stock text keys (NumericEditor) ]=====================================
        public const string TextControlForeground = "TextControlForeground";

        // ===========[ AJut control keys ]===================================================
        public const string NumericEditor_NormalBorderBrush = "AJut_NumericEditor_NormalBorderBrush";
        public const string NumericEditor_FocusBorderBrush = "AJut_NumericEditor_FocusBorderBrush";
        public const string NumericEditor_ErrorBrush = "AJut_NumericEditor_ErrorBrush";
        public const string ColorEditIngressControl_NormalBorderBrush = "AJut_ColorEditIngressControl_NormalBorderBrush";
        public const string ColorEditIngressControl_HighlightBorderBrush = "AJut_ColorEditIngressControl_HighlightBorderBrush";

        /// <summary>
        /// A NumericEditor nudge button while it is pressed. Themed, it is the AJut palette's primary highlight, named
        /// here by its palette key since the WinUI3 theme has no UIKit-shaped control key for it.
        /// </summary>
        public const string NumericEditor_NudgePressedBackground = "AJut_Color_PrimaryHighlight";

        /// <summary>
        /// The dots at the ends of a Scroll ToggleStrip that show it scrolls further that way. Themed, it is the AJut
        /// palette's symbol color, named here by its palette key.
        /// </summary>
        public const string ToggleStrip_ScrollIndicator = "AJut_Color_SymbolForeground";

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
            ThemedColors.SetUnthemedDefault(ToggleButtonBorderBrush, UIColor.Separator);

            // 3. Combo box
            ThemedColors.SetUnthemedDefault(ComboBoxBackground, UIColor.SecondarySystemFill);
            ThemedColors.SetUnthemedDefault(ComboBoxForeground, UIColor.Label);

            // 4. Text
            ThemedColors.SetUnthemedDefault(TextControlForeground, UIColor.Label);

            // 5. NumericEditor and ColorEditIngressControl
            ThemedColors.SetUnthemedDefault(NumericEditor_NormalBorderBrush, UIColor.Separator);
            ThemedColors.SetUnthemedDefault(NumericEditor_FocusBorderBrush, null);
            ThemedColors.SetUnthemedDefault(NumericEditor_ErrorBrush, UIColor.SystemRed);
            ThemedColors.SetUnthemedDefault(ColorEditIngressControl_NormalBorderBrush, UIColor.Separator);
            ThemedColors.SetUnthemedDefault(ColorEditIngressControl_HighlightBorderBrush, null);
            ThemedColors.SetUnthemedDefault(NumericEditor_NudgePressedBackground, UIColor.SystemBlue);
            ThemedColors.SetUnthemedDefault(ToggleStrip_ScrollIndicator, UIColor.SecondaryLabel);
        }
    }
}
