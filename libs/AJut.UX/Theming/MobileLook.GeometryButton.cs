namespace AJut.UX.Theming
{
    public static partial class MobileLook
    {
        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// GeometryButton: a shape filled with the stock button colors, so it sits with an untouched BrushButton, and traced
        /// with its own outline keys, which take the common borders so the outline stays on touch. WinUI3's keys in every
        /// state but one: pressed, the outline goes to the bright highlight, since on touch a press is the only highlight
        /// there is and the fill alone barely changed.
        /// </summary>
        public static class GeometryButton
        {
            // ===========[ Keys ]===================================
            public const string Fill = "ButtonBackground";
            public const string Glyph = "ButtonForeground";
            public const string Trace = AJutThemeKeys.GeometryButton_Trace;

            public const string PointerOverFill = "ButtonBackgroundPointerOver";
            public const string PointerOverGlyph = "ButtonForegroundPointerOver";
            public const string PointerOverTrace = AJutThemeKeys.GeometryButton_TracePointerOver;

            public const string PressedFill = "ButtonBackgroundPressed";
            public const string PressedGlyph = "ButtonForegroundPressed";

            /// <summary>The outline while pressed: the bright highlight, where WinUI3 keeps the common border.</summary>
            public const string PressedTrace = "AJut_Brush_PrimaryHighlightBright";

            public const string DisabledFill = "ButtonBackgroundDisabled";
            public const string DisabledGlyph = "ButtonForegroundDisabled";
            public const string DisabledTrace = AJutThemeKeys.GeometryButton_TraceDisabled;
        }
    }
}
