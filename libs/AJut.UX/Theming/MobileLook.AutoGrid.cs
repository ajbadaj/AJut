namespace AJut.UX.Theming
{
    public static partial class MobileLook
    {
        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// AutoGrid: the grid itself draws nothing but its resizers, which take the color WinUI3's and WPF's GridSplitter
        /// take with the AJut theme loaded. Unthemed, Android draws them in WinUI3's unthemed GridSplitter gray and UIKit in
        /// the system separator color. A resizer is drawn as thick as the grid's SizerLength, but takes hold of a touch
        /// anywhere within a finger's width of it.
        /// </summary>
        public static class AutoGrid
        {
            // ===========[ Keys ]===================================
            /// <summary>The resizers between rows and columns</summary>
            public const string Resizer = "AJut_Brush_BorderCommon";

            /// <summary>The border around the grid, when it has a thickness and no color of its own</summary>
            public const string Border = "AJut_Brush_BorderCommon";

            // ===========[ Sizes ]===================================
            /// <summary>How wide a band around a resizer takes hold of a press, centered on it (Apple's minimum touch target)</summary>
            public const float ResizerGrabLength = 44f;
        }
    }
}
