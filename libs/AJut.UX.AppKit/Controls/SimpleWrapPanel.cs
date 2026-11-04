namespace AJut.UX.Controls
{
    using System;
    using AppKit;
    using CoreGraphics;
    using ObjCRuntime;

    /// <summary>
    /// A no-frills horizontal wrap panel, the AppKit twin of WinUI3's SimpleWrapPanel: subviews flow left to right at the
    /// size they want and the next one drops to a new line when it would run past the width, with
    /// <see cref="HorizontalSpacing"/> between subviews on a line and <see cref="VerticalSpacing"/> between lines. The
    /// layout is AJut.UX's <see cref="SimpleWrapLayout"/>, shared with every stack's wrap panel. In a right to left layout
    /// the flow runs right to left, as a mirrored WinUI3 panel does. It paints nothing, so there is nothing to theme.
    /// </summary>
    /// <remarks>
    /// Subviews are placed by frame, so a subview added with TranslatesAutoresizingMaskIntoConstraints off has it turned
    /// back on, keeping the size its own constraints give it. A subview's size is the fitting size of the constraints it
    /// holds (a view built inside with constraints, or one given its own width and height), else its intrinsic size (a
    /// label, a button, a switch), else the size of the frame it was given, so a plain view needs a frame size. Sizes and
    /// spacing are measured the way auto layout measures them, by alignment rect, so controls are spaced by their
    /// visible edges, as in a stack view. A hidden subview takes no place. AppKit tells no parent when a subview hides,
    /// shows or changes what it wants, so after any of those set NeedsLayout on the panel, which also updates its height.
    ///
    /// The panel has a height for the width it has, and no width of its own, so it takes the width its parent gives it:
    /// stretched across a vertical FillStack (the default there), a grower in a horizontal one, or a width constraint.
    /// </remarks>
    public class SimpleWrapPanel : NSView
    {
        private const double kDefaultHorizontalSpacing = 8.0;
        private const double kDefaultVerticalSpacing = 6.0;

        private double m_horizontalSpacing = kDefaultHorizontalSpacing;
        private double m_verticalSpacing = kDefaultVerticalSpacing;
        private double m_heightAtLastLayout = -1.0;

        // ===========[ Setup/Construction/Teardown ]===================================
        public SimpleWrapPanel ()
        {
        }

        /// <summary>
        /// Used by the runtime when AppKit needs a managed peer for a native view whose peer is gone (one disposed while
        /// still in a window, say). It only answers AppKit's calls, and lays out the subviews the native view has, at the
        /// default spacing. None of its managed state survives a dispose, so a call that needs that state can still fail:
        /// dispose it only once AppKit is done with it.
        /// </summary>
        protected SimpleWrapPanel (NativeHandle handle) : base(handle)
        {
        }

        // ===========[ Properties ]===================================
        /// <summary>Top-left origin, so the lines run down from the top</summary>
        public override bool IsFlipped => true;

        /// <summary>The gap between subviews on a line, in points. 8 unless set.</summary>
        public double HorizontalSpacing
        {
            get => m_horizontalSpacing;
            set
            {
                m_horizontalSpacing = Math.Max(0.0, value);
                this.NeedsLayout = true;
            }
        }

        /// <summary>The gap between lines, in points. 6 unless set.</summary>
        public double VerticalSpacing
        {
            get => m_verticalSpacing;
            set
            {
                m_verticalSpacing = Math.Max(0.0, value);
                this.NeedsLayout = true;
            }
        }

        /// <summary>
        /// No width, and the height the lines took at the last layout. Before the first layout it is an estimate at the
        /// width the panel has (one line when it has none), from what its subviews know of themselves.
        /// </summary>
        public override CGSize IntrinsicContentSize
        {
            get
            {
                // The height depends on the width, which auto layout has no way to ask for, so the panel lays out at the
                //  width it is given and then reports the height that took, which brings a second pass at the right
                //  height (the way a wrapping label settles its own). It is not measured here in full: AppKit asks for this
                //  while it gathers constraints, and a subview's fitting size runs a layout pass of its own, which does not
                //  belong inside that. So until the first layout the estimate leaves fitting sizes out.
                double height = m_heightAtLastLayout >= 0.0
                    ? m_heightAtLastLayout
                    : this.ComputeLayout(this.Subviews, (double)this.Bounds.Width, canAskFittingSize: false).Height;
                return new CGSize(NSView.NoIntrinsicMetric, (nfloat)height);
            }
        }

        // ===========[ Public Interface Methods ]===================================
        /// <summary>
        /// The size the panel wants within <paramref name="size"/>'s width: its widest line, and every line's height. For a
        /// parent that places the panel by frame; under auto layout it gives its height through its intrinsic size.
        /// </summary>
        public CGSize SizeThatFits (CGSize size)
        {
            SimpleWrapLayout layout = this.ComputeLayout(this.Subviews, (double)size.Width, canAskFittingSize: true);
            return new CGSize((nfloat)layout.Width, (nfloat)layout.Height);
        }

        public override void Layout ()
        {
            base.Layout();
            double width = (double)this.Bounds.Width;
            NSView[] subviews = this.Subviews;
            SimpleWrapLayout layout = this.ComputeLayout(subviews, width, canAskFittingSize: true);
            bool isRightToLeft = this.UserInterfaceLayoutDirection == NSUserInterfaceLayoutDirection.RightToLeft;
            for (int index = 0; index < subviews.Length; ++index)
            {
                SimpleWrapLayout.Slot slot = layout.Slots[index];
                if (slot.IsPlaced)
                {
                    // The slot is where the subview's alignment rect goes; its frame reaches past that by its alignment
                    //  insets (a push button's bezel shadow), as auto layout would place it
                    double x = isRightToLeft ? width - slot.X - slot.Width : slot.X;
                    var alignmentRect = new CGRect((nfloat)x, (nfloat)slot.Y, (nfloat)slot.Width, (nfloat)slot.Height);
                    subviews[index].Frame = subviews[index].GetFrameForAlignmentRect(alignmentRect);
                }
            }

            if (layout.Height != m_heightAtLastLayout)
            {
                m_heightAtLastLayout = layout.Height;
                this.InvalidateIntrinsicContentSize();
            }
        }

        public override void DidAddSubview (NSView subview)
        {
            base.DidAddSubview(subview);
            if (!subview.TranslatesAutoresizingMaskIntoConstraints)
            {
                // Sized first, so the constraints its frame makes agree with the ones it holds from the start
                CGRect fitted = subview.GetFrameForAlignmentRect(new CGRect(CGPoint.Empty, subview.FittingSize));
                subview.SetFrameSize(fitted.Size);
                subview.TranslatesAutoresizingMaskIntoConstraints = true;
            }

            this.NeedsLayout = true;
        }

        public override void WillRemoveSubview (NSView subview)
        {
            base.WillRemoveSubview(subview);
            this.NeedsLayout = true;
        }

        // ===========[ Helper Methods ]===================================
        // A width of zero or less (not laid out yet) is treated as unbounded, so the panel reports one line rather than a
        //  column of one subview each
        private SimpleWrapLayout ComputeLayout (NSView[] subviews, double width, bool canAskFittingSize)
        {
            double available = width > 0.0 ? width : double.PositiveInfinity;
            var sizes = new WrapItemSize[subviews.Length];
            for (int index = 0; index < subviews.Length; ++index)
            {
                NSView subview = subviews[index];
                if (!subview.Hidden)
                {
                    sizes[index] = WantedSize(subview, canAskFittingSize);
                }
            }

            return SimpleWrapLayout.Compute(sizes, available, m_horizontalSpacing, m_verticalSpacing);
        }

        // What a subview wants, as an alignment rect size. A subview that holds constraints of its own is measured by the
        //  fitting size they give, which counts its intrinsic size too and leaves out the constraints its frame makes
        //  (its parent holds those), so a view placed by frame still reports what it holds. Anything else is measured by
        //  its intrinsic size (NoIntrinsicMetric is negative), and an axis with neither takes the frame's. FittingSize
        //  is only asked for when the subview holds constraints, since it runs a layout pass over the subview.
        private static WrapItemSize WantedSize (NSView subview, bool canAskFittingSize)
        {
            CGSize frame = subview.GetAlignmentRectForFrame(subview.Frame).Size;
            if (canAskFittingSize && (subview.Constraints.Length > 0))
            {
                CGSize fitting = subview.FittingSize;
                return new WrapItemSize(_Axis(fitting.Width, frame.Width), _Axis(fitting.Height, frame.Height));
            }

            CGSize intrinsic = subview.IntrinsicContentSize;
            return new WrapItemSize(_Axis(intrinsic.Width, frame.Width), _Axis(intrinsic.Height, frame.Height));

            // A measured length when there is one, else the frame's
            static double _Axis (nfloat measured, nfloat frameLength)
            {
                return measured > 0 ? (double)measured : Math.Max(0.0, (double)frameLength);
            }
        }
    }
}
