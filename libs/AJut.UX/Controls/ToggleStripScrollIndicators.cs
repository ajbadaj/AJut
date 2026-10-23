namespace AJut.UX.Controls
{
    using System;

    /// <summary>
    /// Which ends of a ToggleStrip in Scroll mode show a scroll indicator: a small dot just outside each end the strip
    /// can still scroll toward, and none at an end it has reached. A Scroll strip has no scroll bar, so the dots are what
    /// say there is more past an end. The same rule and sizes on every stack, for either orientation: lengths and the
    /// offset are measured along the strip, widths and x for a horizontal strip, heights and y for a vertical one.
    /// </summary>
    /// <remarks>
    /// The dots sit outside the strip's bounds, so they take no layout space and the strip doesn't move as they come and
    /// go. They take no input either. Each is drawn in the strip's scroll indicator color at <see cref="kDotOpacity"/>.
    /// </remarks>
    /// <param name="ShowBack">Show the dot before the strip's leading end (left, or top): it can scroll back</param>
    /// <param name="ShowForward">Show the dot past the strip's trailing end (right, or bottom): it can scroll forward</param>
    public readonly record struct ToggleStripScrollIndicators (bool ShowBack, bool ShowForward)
    {
        /// <summary>The dot's diameter.</summary>
        public const double kDotDiameter = 6.0;

        /// <summary>The space between the strip's end and the near edge of its dot.</summary>
        public const double kDotGap = 4.0;

        /// <summary>How far a dot's center sits outside the strip's end: the gap plus half the dot.</summary>
        public const double kDotCenterOffset = kDotGap + (kDotDiameter / 2.0);

        /// <summary>The dot's opacity, applied over its color.</summary>
        public const double kDotOpacity = 0.8;

        /// <summary>
        /// How close to an end the strip has to be to count as having reached it, so a fraction left over from layout
        /// rounding doesn't keep a dot showing at an end the strip is already at.
        /// </summary>
        public const double kEndTolerance = 0.5;

        /// <summary>The dots a Scroll strip shows for where it is scrolled to.</summary>
        /// <param name="offset">How far the strip is scrolled from its leading end</param>
        /// <param name="contentLength">The length of everything in the strip</param>
        /// <param name="viewportLength">The length of the strip that shows it</param>
        public static ToggleStripScrollIndicators For (double offset, double contentLength, double viewportLength)
        {
            // Content that fits has nowhere to scroll, and an offset past either end (a bounce) has reached that end
            double maxOffset = Math.Max(0.0, contentLength - viewportLength);
            return new ToggleStripScrollIndicators(
                ShowBack: offset > kEndTolerance,
                ShowForward: offset < (maxOffset - kEndTolerance)
            );
        }
    }
}
