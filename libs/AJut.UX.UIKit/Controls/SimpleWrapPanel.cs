namespace AJut.UX.Controls
{
    using System;
    using CoreGraphics;
    using ObjCRuntime;
    using UIKit;

    /// <summary>
    /// A no-frills horizontal wrap panel, the UIKit twin of WinUI3's SimpleWrapPanel: subviews flow left to right at the size
    /// they want and the next one drops to a new line when it would run past the width, with <see cref="HorizontalSpacing"/>
    /// between subviews on a line and <see cref="VerticalSpacing"/> between lines. The layout is AJut.UX's
    /// <see cref="SimpleWrapLayout"/>, shared with every stack's wrap panel. In a right to left layout the flow runs right
    /// to left, as a mirrored WinUI3 panel does. It paints nothing, so there is nothing to theme.
    /// </summary>
    /// <remarks>
    /// Subviews are placed by frame, so they keep TranslatesAutoresizingMaskIntoConstraints on, the way a stack view's are
    /// managed for them. A subview's size is what its SizeThatFits gives for the width there is (a label, a button, a
    /// switch), or its compressed fitting size for a view laid out by constraints. A hidden subview takes no place. UIKit
    /// tells no parent when a subview hides, shows or changes what it wants, so after any of those call SetNeedsLayout on
    /// the panel, which also updates its intrinsic height for a parent laid out by constraints.
    /// </remarks>
    public class SimpleWrapPanel : UIView
    {
        private const float kDefaultHorizontalSpacing = 8f;
        private const float kDefaultVerticalSpacing = 6f;

        private nfloat m_horizontalSpacing = kDefaultHorizontalSpacing;
        private nfloat m_verticalSpacing = kDefaultVerticalSpacing;
        private nfloat m_heightAtLastLayout = -1;

        // ===========[ Setup/Construction/Teardown ]===================================
        public SimpleWrapPanel () : base(CGRect.Empty)
        {
        }

        /// <summary>Used by the runtime when UIKit creates the managed peer for an existing native view.</summary>
        protected SimpleWrapPanel (NativeHandle handle) : base(handle)
        {
        }

        // ===========[ Properties ]===================================
        /// <summary>The gap between subviews on a line, in points. 8 unless set.</summary>
        public nfloat HorizontalSpacing
        {
            get => m_horizontalSpacing;
            set
            {
                m_horizontalSpacing = (nfloat)Math.Max(0.0, value);
                this.InvalidateFlow();
            }
        }

        /// <summary>The gap between lines, in points. 6 unless set.</summary>
        public nfloat VerticalSpacing
        {
            get => m_verticalSpacing;
            set
            {
                m_verticalSpacing = (nfloat)Math.Max(0.0, value);
                this.InvalidateFlow();
            }
        }

        // A parent laid out by constraints asks for the intrinsic size, and the height depends on the width, so the
        //  height is the one for the width the panel last had, and a layout at a new width updates it
        public override CGSize IntrinsicContentSize => new CGSize(UIView.NoIntrinsicMetric, this.ComputeLayout(this.Bounds.Width).Height);

        // ===========[ Public Interface Methods ]===================================
        public override CGSize SizeThatFits (CGSize size)
        {
            SimpleWrapLayout layout = this.ComputeLayout(size.Width);
            return new CGSize(layout.Width, layout.Height);
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews();
            nfloat width = this.Bounds.Width;
            SimpleWrapLayout layout = this.ComputeLayout(width);
            bool isRightToLeft = this.EffectiveUserInterfaceLayoutDirection == UIUserInterfaceLayoutDirection.RightToLeft;
            UIView[] subviews = this.Subviews;
            for (int index = 0; index < subviews.Length; ++index)
            {
                SimpleWrapLayout.Slot slot = layout.Slots[index];
                if (slot.IsPlaced)
                {
                    double x = isRightToLeft ? width - slot.X - slot.Width : slot.X;
                    subviews[index].Frame = new CGRect(x, slot.Y, slot.Width, slot.Height);
                }
            }

            if (layout.Height != m_heightAtLastLayout)
            {
                m_heightAtLastLayout = (nfloat)layout.Height;
                this.InvalidateIntrinsicContentSize();
            }
        }

        public override void SubviewAdded (UIView uiview)
        {
            base.SubviewAdded(uiview);
            this.InvalidateFlow();
        }

        public override void WillRemoveSubview (UIView uiview)
        {
            base.WillRemoveSubview(uiview);
            this.InvalidateFlow();
        }

        // ===========[ Helper Methods ]===================================
        private void InvalidateFlow ()
        {
            this.SetNeedsLayout();
            this.InvalidateIntrinsicContentSize();
        }

        // A width of zero or less (not laid out yet) is treated as unbounded, so the panel reports one line rather than
        //  a column of one subview each
        private SimpleWrapLayout ComputeLayout (nfloat width)
        {
            double available = width > 0 ? (double)width : double.PositiveInfinity;
            var offered = new CGSize(width > 0 ? width : nfloat.MaxValue, nfloat.MaxValue);
            UIView[] subviews = this.Subviews;
            var sizes = new WrapItemSize[subviews.Length];
            for (int index = 0; index < subviews.Length; ++index)
            {
                UIView subview = subviews[index];
                if (subview.Hidden)
                {
                    continue;
                }

                CGSize wanted = subview.TranslatesAutoresizingMaskIntoConstraints
                    ? subview.SizeThatFits(offered)
                    : subview.SystemLayoutSizeFittingSize(UIView.UILayoutFittingCompressedSize);
                sizes[index] = new WrapItemSize(wanted.Width, wanted.Height);
            }

            return SimpleWrapLayout.Compute(sizes, available, m_horizontalSpacing, m_verticalSpacing);
        }
    }
}
