namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Windows.Foundation;
    using DPUtils = AJut.UX.DPUtils<SimpleWrapPanel>;

    // ===========[ SimpleWrapPanel ]============================================
    // A no-frills horizontal wrap panel - WinUI3 ships no built-in one. Lays children out left to
    // right and wraps to the next line when the next child would overflow the available width.
    // HorizontalSpacing / VerticalSpacing gap the items. Children are arranged at their desired size
    // (never stretched), so each one stays content-sized and the flow can pack several per line.
    // Collapsed (or otherwise zero-size) children still get measured, so the layout system keeps
    // tracking them and a later Collapsed->Visible flip reflows the panel - but they take no slot in
    // the flow and leave no gap.
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
        // The flow itself is AJut.UX's SimpleWrapLayout, which every stack's wrap panel shares
        protected override Size MeasureOverride (Size availableSize)
        {
            // Measure EVERY child (collapsed ones return 0x0) so the layout system keeps tracking
            // them - that is what makes a child flipping Collapsed->Visible re-run this pass instead
            // of only updating on the next resize. The layout gives zero-size children no slot, so
            // a hidden group leaves no phantom spacing.
            foreach (UIElement child in this.Children)
            {
                child.Measure(availableSize);
            }

            SimpleWrapLayout layout = SimpleWrapLayout.Compute(this.GetChildSizes(), availableSize.Width, this.HorizontalSpacing, this.VerticalSpacing);
            return new Size(layout.Width, layout.Height);
        }

        protected override Size ArrangeOverride (Size finalSize)
        {
            SimpleWrapLayout layout = SimpleWrapLayout.Compute(this.GetChildSizes(), finalSize.Width, this.HorizontalSpacing, this.VerticalSpacing);
            for (int index = 0; index < this.Children.Count; ++index)
            {
                SimpleWrapLayout.Slot slot = layout.Slots[index];
                this.Children[index].Arrange(slot.IsPlaced ? new Rect(slot.X, slot.Y, slot.Width, slot.Height) : new Rect(0, 0, 0, 0));
            }

            return finalSize;
        }

        // ===========[ Helper Methods ]===========================================
        private WrapItemSize[] GetChildSizes ()
        {
            var sizes = new WrapItemSize[this.Children.Count];
            for (int index = 0; index < sizes.Length; ++index)
            {
                Size desired = this.Children[index].DesiredSize;
                sizes[index] = new WrapItemSize(desired.Width, desired.Height);
            }

            return sizes;
        }
    }
}
