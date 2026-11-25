namespace AJut.UX.Theming
{
    public static partial class MobileLook
    {
        // ===========[ Subclasses/structs ]===================================
        /// <summary>
        /// PageStack: the page buttons take BumpStack's themed surfaces, as on WinUI3, WPF and AppKit (the control fill,
        /// opaque since the neighbor pages peek in under them, the symbol color for the chevron, and the button border to
        /// set them apart from the peeks). The dots and the page count take the symbol color, and the current page's dot
        /// the primary highlight. An edge dot (more pages past the dot range) draws smaller and faded, and grows to full
        /// size while pressed, the way the desktop stacks grow it under the pointer. The count's numbers draw larger than
        /// the format's own words, which are fainter.
        /// </summary>
        public static class PageStack
        {
            // ===========[ Keys ]===================================
            /// <summary>A page button's fill. Opaque, since the neighbor pages peek in under it.</summary>
            public const string ButtonBackground = "AJut_Brush_ControlBackground";

            /// <summary>A page button's chevron.</summary>
            public const string ButtonForeground = "AJut_Brush_SymbolForeground";

            /// <summary>A page button's outline, which sets it apart from the page peeking in under it.</summary>
            public const string ButtonBorder = "AJut_Brush_ButtonBorder";

            /// <summary>Every dot in the strip but the current page's, and the page count's text.</summary>
            public const string Dot = "AJut_Brush_SymbolForeground";

            /// <summary>The current page's dot.</summary>
            public const string CurrentDot = "AJut_Brush_PrimaryHighlight";

            // ===========[ Sizes ]===================================
            /// <summary>How opaque a dot draws at rest.</summary>
            public const double DotOpacity = 0.85;

            /// <summary>How opaque a dot draws while pressed.</summary>
            public const double DotPressedOpacity = 1.0;

            /// <summary>How opaque a dot draws while the stack is disabled.</summary>
            public const double DotDisabledOpacity = 0.4;

            /// <summary>How much an edge dot fades on top of the dot opacity, pressed or not.</summary>
            public const double EdgeDotFade = 0.45;

            /// <summary>How big an edge dot draws next to a full dot, until it is pressed.</summary>
            public const double EdgeDotScale = 0.6;

            /// <summary>How long a dot takes to ease to a new size and fade, in seconds.</summary>
            public const double DotEaseSeconds = 0.15;

            /// <summary>The page count's numbers' font size.</summary>
            public const double CountFontSize = 13.0;

            /// <summary>The font size of the page count format's own words ("of").</summary>
            public const double CountWordFontSize = 10.0;

            /// <summary>How opaque the page count format's own words draw next to the numbers.</summary>
            public const double CountWordOpacity = 0.8;
        }
    }
}
