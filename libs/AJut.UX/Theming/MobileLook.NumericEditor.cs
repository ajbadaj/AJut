namespace AJut.UX.Theming
{
    using System;
    using AJut.UX.Helpers;

    public static partial class MobileLook
    {
        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// NumericEditor: one rounded frame on the pop-out surface, with the decrease button, the number and the increase
        /// button side by side in it. The number sits in a well of the text box color, inset from the frame's top and
        /// bottom and shaded at its edges the way WinUI3's two nested borders shade it. The step buttons are the frame's
        /// own surface until pressed, when they fill with the bright highlight, since touch has no hover to lead into a
        /// press. An optional label sits in front of the frame.
        /// </summary>
        public static class NumericEditor
        {
            // ===========[ Keys ]===================================
            /// <summary>The frame's surface. A gradient.</summary>
            public const string FrameSurface = "AJut_Brush_PopOutControlBackground";
            public const string FrameBorder = AJutThemeKeys.NumericEditor_NormalBorderBrush;

            /// <summary>The frame's border while the number is being edited.</summary>
            public const string FrameBorderFocused = AJutThemeKeys.NumericEditor_FocusBorderBrush;

            /// <summary>The frame's border while the text is out of bounds, which wins over focused.</summary>
            public const string FrameBorderError = AJutThemeKeys.NumericEditor_ErrorBrush;

            /// <summary>The well the number sits in.</summary>
            public const string WellSurface = "AJut_Brush_TextBoxBackground";

            /// <summary>The optional label in front of the frame, which reads as a field's header does.</summary>
            public const string Label = "TextControlHeaderForeground";
            public const string Number = "TextControlForeground";

            /// <summary>The number, and the step buttons' glyphs, while the editor is read-only.</summary>
            public const string ReadOnlyText = "TextControlForegroundDisabled";
            public const string StepGlyph = "TextFillColorPrimaryBrush";

            /// <summary>
            /// A step button's fill while pressed: WinUI3's step highlight under the pointer, the bright primary, since a
            /// press is the only highlight touch has.
            /// </summary>
            public const string StepPressedFill = AJutThemeKeys.NumericEditor_IncreasePointerOverBackground;

            // ===========[ Sizes ]===================================
            public const float CornerRadius = 4f;
            public const float BorderThickness = 1f;

            /// <summary>Each step button's width, Apple's smallest comfortable touch target.</summary>
            public const float StepButtonWidth = 44f;

            /// <summary>How far the well sits in from the frame's top and bottom, inside its border.</summary>
            public const float WellInset = 3f;

            /// <summary>The well's shadow along its top and left.</summary>
            public const float WellShadowThickness = 1.4f;

            /// <summary>The well's light line along its bottom and right.</summary>
            public const float WellLightThickness = 1f;

            /// <summary>The space between the well's edges and the number.</summary>
            public const float WellTextPadding = 4f;

            /// <summary>The space between the label and the frame.</summary>
            public const float LabelSpacing = 8f;

            // ===========[ Shading and timing ]===================================
            /// <summary>The well's shadow color. The same over any theme, as on WinUI3.</summary>
            public static readonly ColorArgb WellShadow = new ColorArgb(0x80, 0x00, 0x00, 0x00);

            /// <summary>The well's light line color. The same over any theme, as on WinUI3.</summary>
            public static readonly ColorArgb WellLight = new ColorArgb(0x5F, 0xFF, 0xFF, 0xFF);

            /// <summary>How long a step button is held before it starts to repeat.</summary>
            public static readonly TimeSpan RepeatDelay = TimeSpan.FromMilliseconds(400);

            /// <summary>The time between repeats while a step button is held.</summary>
            public static readonly TimeSpan RepeatInterval = TimeSpan.FromMilliseconds(80);
        }
    }
}
