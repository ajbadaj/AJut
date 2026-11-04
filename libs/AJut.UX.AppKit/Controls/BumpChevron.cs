namespace AJut.UX.Controls
{
    using AppKit;
    using CoreGraphics;

    // ===========[ BumpChevron ]================================================
    // The arrow a bump button draws, shared by BumpStack's buttons and a Scroll mode ToggleStrip's: a thin chevron pointing
    // the way the button scrolls, and the strength the whole button draws at, the way WinUI3's bump buttons fade
    // (brighter under the pointer and while pressed, dimmed when it can't go further).
    internal static class BumpChevron
    {
        private const double kNormalOpacity = 0.8;
        private const double kHighlightedOpacity = 1.0;
        private const double kDisabledOpacity = 0.4;
        private const double kLineWidth = 1.5;

        // How far the chevron's point sits ahead of its middle, and how far its arms reach out, from the glyph size, as a
        // font glyph's would
        private const double kPointRatio = 0.2;
        private const double kSpreadRatio = 0.35;

        /// <summary>The opacity a bump button draws everything at in <paramref name="state"/></summary>
        public static double OpacityFor (eManagedButtonState state)
        {
            switch (state)
            {
                case eManagedButtonState.PointerOver:
                case eManagedButtonState.Pressed:
                    return kHighlightedOpacity;

                case eManagedButtonState.Disabled:
                    return kDisabledOpacity;

                default:
                    return kNormalOpacity;
            }
        }

        /// <summary>
        /// Strokes the chevron in <paramref name="color"/>, centered in <paramref name="bounds"/> of a flipped view (+y is
        /// down), pointing right or down when <paramref name="isForward"/>, left or up otherwise
        /// </summary>
        public static void Stroke (CGRect bounds, bool isForward, bool isHorizontal, double glyphSize, NSColor color)
        {
            double centerX = (double)bounds.X + ((double)bounds.Width / 2.0);
            double centerY = (double)bounds.Y + ((double)bounds.Height / 2.0);
            double pointLength = glyphSize * kPointRatio;
            double spread = glyphSize * kSpreadRatio;
            double direction = isForward ? 1.0 : -1.0;
            var chevron = new NSBezierPath
            {
                LineWidth = (nfloat)kLineWidth,
                LineCapStyle = NSLineCapStyle.Round,
                LineJoinStyle = NSLineJoinStyle.Round,
            };

            if (isHorizontal)
            {
                chevron.MoveTo(new CGPoint(centerX - (direction * pointLength), centerY - spread));
                chevron.LineTo(new CGPoint(centerX + (direction * pointLength), centerY));
                chevron.LineTo(new CGPoint(centerX - (direction * pointLength), centerY + spread));
            }
            else
            {
                chevron.MoveTo(new CGPoint(centerX - spread, centerY - (direction * pointLength)));
                chevron.LineTo(new CGPoint(centerX, centerY + (direction * pointLength)));
                chevron.LineTo(new CGPoint(centerX + spread, centerY - (direction * pointLength)));
            }

            color.SetStroke();
            chevron.Stroke();
        }
    }
}
