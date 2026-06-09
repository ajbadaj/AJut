namespace AJut.UX
{
    using System;

    // ===========[ ScrollIntoViewMath ]=========================================
    // Platform-agnostic math shared by the WPF and WinUI3 scroll-into-view helpers. Given where an
    // element sits relative to the current viewport, decide the new scroll offset that brings it fully
    // into view (start and end where it fits, otherwise the start), keeping it clear of any insets
    // reserved at the viewport edges (e.g. for overlaid scroll buttons). The platform-specific helpers
    // supply the measured geometry and apply the result; this is the shared spine.

    public static class ScrollIntoViewMath
    {
        /// <summary>
        /// Resolves the new scroll offset along one axis, or null if the element is already fully in
        /// view (or the axis cannot scroll).
        /// </summary>
        /// <param name="positionInViewport">The element's start edge relative to the visible viewport (already reflecting the current scroll).</param>
        /// <param name="size">The element's extent along this axis.</param>
        /// <param name="currentOffset">The current scroll offset.</param>
        /// <param name="viewport">The viewport extent.</param>
        /// <param name="scrollable">The total scrollable extent (the max offset).</param>
        /// <param name="leadingInset">Reserved space at the start edge to keep the element clear of.</param>
        /// <param name="trailingInset">Reserved space at the end edge to keep the element clear of.</param>
        public static double? ResolveAxisOffset (double positionInViewport, double size, double currentOffset, double viewport, double scrollable, double leadingInset, double trailingInset)
        {
            if (scrollable <= 0.0)
            {
                return null;
            }

            double contentStart = currentOffset + positionInViewport;
            double clearViewport = viewport - leadingInset - trailingInset;

            double target;
            if (size >= clearViewport || positionInViewport < leadingInset)
            {
                // Too big to fully fit, or cut off at the start - align the start edge.
                target = contentStart - leadingInset;
            }
            else if (positionInViewport + size > viewport - trailingInset)
            {
                // Cut off at the end - align the end edge.
                target = contentStart + size - (viewport - trailingInset);
            }
            else
            {
                return null;
            }

            target = Math.Max(0.0, Math.Min(scrollable, target));
            if (Math.Abs(target - currentOffset) < 0.5)
            {
                return null;
            }

            return target;
        }
    }
}
