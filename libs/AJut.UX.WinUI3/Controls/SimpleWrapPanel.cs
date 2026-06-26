namespace AJut.UX.Controls
{
    using System;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Windows.Foundation;
    using DPUtils = AJut.UX.DPUtils<SimpleWrapPanel>;

    // ===========[ SimpleWrapPanel ]============================================
    // A no-frills horizontal wrap panel - WinUI3 ships no built-in one. Lays children out left to
    // right and wraps to the next line when the next child would overflow the available width.
    // HorizontalSpacing / VerticalSpacing gap the items. Children are arranged at their desired size
    // (never stretched), so each one stays content-sized and the flow can pack several per line.
    // Collapsed children are skipped entirely - they take no slot and leave no gap.
    //
    // "Simple" because it deliberately does the one thing: it doesn't balance lines, reflow columns,
    // or flip orientation - it just measures, wraps, and arranges in flow order.
    // =============================================================================

    public class SimpleWrapPanel : Panel
    {
        // ===========[ Const-like ]===============================================
        private const double kDefaultHorizontalSpacing = 8.0;
        private const double kDefaultVerticalSpacing = 6.0;

        // ===========[ Dependency Properties ]====================================
        public static readonly DependencyProperty HorizontalSpacingProperty = DPUtils.Register(_ => _.HorizontalSpacing, kDefaultHorizontalSpacing, (d, e) => d.InvalidateMeasure());
        public double HorizontalSpacing
        {
            get => (double)this.GetValue(HorizontalSpacingProperty);
            set => this.SetValue(HorizontalSpacingProperty, value);
        }

        public static readonly DependencyProperty VerticalSpacingProperty = DPUtils.Register(_ => _.VerticalSpacing, kDefaultVerticalSpacing, (d, e) => d.InvalidateMeasure());
        public double VerticalSpacing
        {
            get => (double)this.GetValue(VerticalSpacingProperty);
            set => this.SetValue(VerticalSpacingProperty, value);
        }

        // ===========[ Layout ]===================================================
        protected override Size MeasureOverride (Size availableSize)
        {
            double available = availableSize.Width;
            bool unbounded = double.IsInfinity(available) || double.IsNaN(available);

            double lineWidth = 0;
            double lineHeight = 0;
            double widest = 0;
            double totalHeight = 0;

            foreach (UIElement child in this.Children)
            {
                if (child.Visibility == Visibility.Collapsed)
                {
                    continue;
                }

                child.Measure(availableSize);
                Size desired = child.DesiredSize;

                if (!unbounded && (lineWidth > 0) && (lineWidth + this.HorizontalSpacing + desired.Width > available))
                {
                    // Doesn't fit - close the current line and start a new one.
                    widest = Math.Max(widest, lineWidth);
                    totalHeight += lineHeight + this.VerticalSpacing;
                    lineWidth = desired.Width;
                    lineHeight = desired.Height;
                }
                else
                {
                    lineWidth += (lineWidth > 0) ? this.HorizontalSpacing + desired.Width : desired.Width;
                    lineHeight = Math.Max(lineHeight, desired.Height);
                }
            }

            widest = Math.Max(widest, lineWidth);
            totalHeight += lineHeight;

            return new Size(unbounded ? widest : Math.Min(widest, available), totalHeight);
        }

        protected override Size ArrangeOverride (Size finalSize)
        {
            double available = finalSize.Width;
            double x = 0;
            double y = 0;
            double lineHeight = 0;

            foreach (UIElement child in this.Children)
            {
                if (child.Visibility == Visibility.Collapsed)
                {
                    continue;
                }

                Size desired = child.DesiredSize;

                if ((x > 0) && (x + this.HorizontalSpacing + desired.Width > available))
                {
                    // Wrap to the next line.
                    x = 0;
                    y += lineHeight + this.VerticalSpacing;
                    lineHeight = 0;
                }

                if (x > 0)
                {
                    x += this.HorizontalSpacing;
                }

                child.Arrange(new Rect(x, y, desired.Width, desired.Height));
                x += desired.Width;
                lineHeight = Math.Max(lineHeight, desired.Height);
            }

            return finalSize;
        }
    }
}
