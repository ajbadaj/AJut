namespace AJut.UX
{
    using System;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using Windows.Foundation;

    public static class ScrollViewerXT
    {
        /// <summary>
        /// Scrolls so the first element under the <see cref="ScrollViewer"/> matching
        /// <paramref name="predicate"/> is brought fully into view - both its start and end edges where
        /// the element fits, otherwise its start. Optional leading/trailing insets keep the element clear
        /// of anything parked over the viewport edges (e.g. scroll buttons).
        /// </summary>
        /// <returns>True if a matching element was found (whether or not a scroll was needed).</returns>
        public static bool ScrollFirstElementIntoView (this ScrollViewer scrollViewer, Func<FrameworkElement, bool> predicate, double leadingInset = 0.0, double trailingInset = 0.0)
        {
            if (scrollViewer == null || predicate == null)
            {
                return false;
            }

            FrameworkElement target = FindFirstMatch(scrollViewer, predicate);
            if (target == null)
            {
                return false;
            }

            // Element's top-left relative to the current viewport - converting to content offsets below.
            GeneralTransform toViewport = target.TransformToVisual(scrollViewer);
            Point topLeft = toViewport.TransformPoint(new Point(0.0, 0.0));

            // Geometry is platform specific; the offset math is the shared AJut.UX spine.
            double? newHorizontal = ScrollIntoViewMath.ResolveAxisOffset(
                topLeft.X, target.ActualWidth, scrollViewer.HorizontalOffset,
                scrollViewer.ViewportWidth, scrollViewer.ScrollableWidth, leadingInset, trailingInset);

            double? newVertical = ScrollIntoViewMath.ResolveAxisOffset(
                topLeft.Y, target.ActualHeight, scrollViewer.VerticalOffset,
                scrollViewer.ViewportHeight, scrollViewer.ScrollableHeight, leadingInset, trailingInset);

            if (newHorizontal == null && newVertical == null)
            {
                return true;
            }

            scrollViewer.ChangeView(newHorizontal, newVertical, null);
            return true;
        }

        private static FrameworkElement FindFirstMatch (DependencyObject root, Func<FrameworkElement, bool> predicate)
        {
            foreach (DependencyObject child in root.GetVisualChildren())
            {
                if (child is FrameworkElement fe && predicate(fe))
                {
                    return fe;
                }

                FrameworkElement nested = FindFirstMatch(child, predicate);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
