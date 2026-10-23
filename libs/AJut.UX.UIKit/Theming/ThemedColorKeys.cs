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

        // ===========[ AJut control keys ]===================================================
        public const string NumericEditor_NormalBorderBrush = "AJut_NumericEditor_NormalBorderBrush";
        public const string NumericEditor_FocusBorderBrush = "AJut_NumericEditor_FocusBorderBrush";
        public const string NumericEditor_ErrorBrush = "AJut_NumericEditor_ErrorBrush";

        /// <summary>
        /// NumericEditor's default "#" label. A key only the UIKit package has, which the shared palette doesn't know, so
        /// RegisterAll gives it a themed alias (understated text) as well as its unthemed default.
        /// </summary>
        public const string NumericEditor_Label = "AJut_NumericEditor_Label";

        /// <summary>
        /// A NumericEditor nudge button's fill while it is pressed, so a press reads clearly with no hover leading into
        /// it. A key only the UIKit package has, so RegisterAll gives it a themed alias (the primary highlight) as well as
        /// its unthemed default.
        /// </summary>
        public const string NumericEditor_NudgePressedBackground = "AJut_NumericEditor_NudgePressedBackground";

        /// <summary>
        /// The dots just outside a Scroll ToggleStrip's ends, one at each end it can still scroll toward. A key only the
        /// UIKit package has, so RegisterAll gives it a themed alias (the symbol color) as well as its unthemed default.
        /// </summary>
        public const string ToggleStrip_ScrollIndicator = "AJut_ToggleStrip_ScrollIndicator";

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
            ThemedColors.SetUnthemedDefault(Brush_BorderCommon, UIColor.Separator);

            // 3. Combo box
            ThemedColors.SetUnthemedDefault(ComboBoxBackground, UIColor.SecondarySystemFill);
            ThemedColors.SetUnthemedDefault(ComboBoxForeground, UIColor.Label);

            // 4. Text
            ThemedColors.SetUnthemedDefault(TextControlForeground, UIColor.Label);
            ThemedColors.SetUnthemedDefault(TextControlForegroundDisabled, UIColor.TertiaryLabel);

            // 5. NumericEditor, ColorEditIngressControl and the ToggleStrip scroll indicators
            ThemedColors.SetUnthemedDefault(NumericEditor_NormalBorderBrush, UIColor.Separator);
            ThemedColors.SetUnthemedDefault(NumericEditor_FocusBorderBrush, null);
            ThemedColors.SetUnthemedDefault(NumericEditor_ErrorBrush, UIColor.SystemRed);
            ThemedColors.SetUnthemedDefault(NumericEditor_Label, UIColor.SecondaryLabel);
            ThemedColors.SetThemedAlias(NumericEditor_Label, "AJut_Brush_UnderstatedText");
            ThemedColors.SetUnthemedDefault(NumericEditor_NudgePressedBackground, UIColor.SystemBlue);
            ThemedColors.SetThemedAlias(NumericEditor_NudgePressedBackground, "AJut_Brush_PrimaryHighlight");
            ThemedColors.SetUnthemedDefault(ColorEditIngressControl_NormalBorderBrush, UIColor.Separator);
            ThemedColors.SetUnthemedDefault(ColorEditIngressControl_HighlightBorderBrush, null);
            ThemedColors.SetUnthemedDefault(ToggleStrip_ScrollIndicator, UIColor.SecondaryLabel);
            ThemedColors.SetThemedAlias(ToggleStrip_ScrollIndicator, "AJut_Brush_SymbolForeground");

            // 6. GeometryButton's trace, unthemed the same as the stock button borders above
            ThemedColors.SetUnthemedDefault(GeometryButton_Trace, UIColor.Separator);
            ThemedColors.SetUnthemedDefault(GeometryButton_TracePointerOver, UIColor.Separator);
            ThemedColors.SetUnthemedDefault(GeometryButton_TracePressed, UIColor.Separator);
            ThemedColors.SetUnthemedDefault(GeometryButton_TraceDisabled, UIColor.QuaternarySystemFill);
        }
    }
}
