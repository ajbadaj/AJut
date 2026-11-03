namespace AJut.UX.Theming
{
    public static partial class MobileLook
    {
        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// How AJut's buttons answer a press on UIKit (BrushButton, AutoBrushButton and GeometryButton): they grow a
        /// little while held and spring back when let go, as iOS's own buttons do, on top of the pressed colors. Android
        /// shows a press with its colors and ripple, so it takes none of this.
        /// </summary>
        public static class PressGrowth
        {
            // ===========[ Sizes ]===================================
            /// <summary>The scale a held button grows to, kept small so a row of buttons doesn't jostle.</summary>
            public const float PressedScale = 1.05f;

            /// <summary>How long the grow and the spring back take, in seconds.</summary>
            public const double Duration = 0.2;

            /// <summary>The spring's damping ratio: below 1 it overshoots a little before it settles.</summary>
            public const float SpringDamping = 0.6f;
        }
    }
}
