namespace AJut.UX
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;

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

            GeneralTransform toViewport = target.TransformToVisual(scrollViewer);
            Point topLeft = toViewport.Transform(new Point(0.0, 0.0));

            // Geometry is platform specific; the offset math is the shared AJut.UX spine.
            double? newHorizontal = ScrollIntoViewMath.ResolveAxisOffset(
                topLeft.X, target.ActualWidth, scrollViewer.HorizontalOffset,
                scrollViewer.ViewportWidth, scrollViewer.ScrollableWidth, leadingInset, trailingInset);

            double? newVertical = ScrollIntoViewMath.ResolveAxisOffset(
                topLeft.Y, target.ActualHeight, scrollViewer.VerticalOffset,
                scrollViewer.ViewportHeight, scrollViewer.ScrollableHeight, leadingInset, trailingInset);

            if (newHorizontal.HasValue)
            {
                scrollViewer.ScrollToHorizontalOffset(newHorizontal.Value);
            }

            if (newVertical.HasValue)
            {
                scrollViewer.ScrollToVerticalOffset(newVertical.Value);
            }

            return true;
        }

        private static FrameworkElement FindFirstMatch (DependencyObject root, Func<FrameworkElement, bool> predicate)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; ++i)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
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
