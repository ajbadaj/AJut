namespace AJut.UX.Theming
{
    public static partial class MobileLook
    {
        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// BusyWait: the platform's own spinner beside a line of text. Inline it takes the text color, as WPF's BusyWait
        /// spinner takes its foreground. As an overlay it covers what is under it with the blockout fill and draws its
        /// spinner and text in the high contrast text color, as WPF's stack navigation busy overlay does. With no theme
        /// loaded the spinner keeps the platform's own color.
        /// </summary>
        public static class BusyWait
        {
            // ===========[ Keys ]===================================
            /// <summary>The spinner and the text, inline: the normal text color.</summary>
            public const string Foreground = "AJut_Brush_NormalText";

            /// <summary>An overlay's fill over everything under it.</summary>
            public const string OverlayBackground = "AJut_Brush_OverlayBlockoutBackground";

            /// <summary>An overlay's spinner and text.</summary>
            public const string OverlayForeground = "AJut_Brush_HighContrastText";

            // ===========[ Sizes ]===================================
            /// <summary>The spinner's width and height, the size of WPF's spinner glyph.</summary>
            public const float SpinnerSize = 28f;

            /// <summary>The space between the spinner and the text.</summary>
            public const float SpinnerSpacing = 8f;

            /// <summary>The least space an overlay keeps between its spinner and text and its own edges.</summary>
            public const float OverlayPadding = 24f;
        }
    }
}
