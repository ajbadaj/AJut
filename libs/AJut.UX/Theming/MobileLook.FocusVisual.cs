namespace AJut.UX.Theming
{
    public static partial class MobileLook
    {
        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// The keyboard focus look of every control a mobile stack draws itself: a thin dotted rectangle in the bright
        /// primary just inside the control, the same as AJut's WPF focus visual (AJut_Style_FocusVisual). A phone shows it
        /// only while a key, not a touch, put focus there, which is when the platform shows any focus at all.
        /// </summary>
        public static class FocusVisual
        {
            // ===========[ Keys ]===================================
            /// <summary>The dotted rectangle's color.</summary>
            public const string Stroke = "AJut_Brush_PrimaryHighlightBright";

            // ===========[ Sizes ]===================================
            /// <summary>The rectangle's line thickness.</summary>
            public const float StrokeThickness = 1f;

            /// <summary>The length of each dot along the line.</summary>
            public const float DashLength = 1f;

            /// <summary>The space between the dots.</summary>
            public const float DashGap = 2f;

            /// <summary>How far inside the control's edges the rectangle runs.</summary>
            public const float Inset = 2f;
        }
    }
}
