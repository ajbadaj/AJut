namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.UX.Theming;
    using CoreGraphics;
    using ObjCRuntime;
    using UIKit;

    /// <summary>
    /// A grid that adds rows and columns as subviews come in, the UIKit twin of WPF's AutoGrid: each subview takes the next
    /// cell, across a row or down a column (<see cref="AutoPopulationOrder"/>), wrapping at <see cref="FixedColumnCount"/> or
    /// <see cref="FixedRowCount"/>, and with <see cref="AddColumnResizers"/> or <see cref="AddRowResizers"/> a resizer between
    /// each pair of columns or rows can be dragged to trade space between them. The rules are AJut.UX's
    /// <see cref="AutoGridLayout"/>, shared with every stack's AutoGrid.
    /// </summary>
    /// <remarks>
    /// Each subview fills its cell, placed by frame, so it keeps TranslatesAutoresizingMaskIntoConstraints on, the way a
    /// stack view's are managed for it. The columns and rows share the grid's bounds inside its <see cref="Padding"/> by
    /// their shares, each at least <see cref="MinElementWidth"/> or <see cref="MinElementHeight"/> long. A parent that asks
    /// what size the grid wants (SizeThatFits, or the intrinsic height a parent laid out by constraints reads) gets each row
    /// as tall as its tallest subview at its column's width, as star tracks are in a WPF Grid given unbounded height. Laid
    /// out, though, the rows share whatever height the grid has by their shares, unlike WPF's and Android's, whose rows
    /// follow their children when the grid has no height to fill: UIKit has no measure pass to say so. A hidden subview
    /// keeps its cell and wants no height. A subview with no cell (past the last one of a grid with both counts fixed) gets an
    /// empty frame; <see cref="GetIsInRange"/> says which those are. In a right to left layout column 0 is on the right, as
    /// in a mirrored WPF Grid. <see cref="BorderThickness"/> and <see cref="BorderBrush"/> draw a border at the grid's edge, as WPF's AutoGrid's do. The resizers draw in <see cref="MobileLook.AutoGrid"/>'s key with the
    /// AJut theme loaded and the system separator color without it, and a resizer takes hold of a touch within a finger's
    /// width of it, ahead of a scroll view around the grid. A grid that is not <see cref="Enabled"/> doesn't drag them.
    /// </remarks>
    public class AutoGrid : UIView
    {
        private readonly AutoGridTracks m_tracks = new AutoGridTracks();
        private ResizerOverlay m_overlay;
        private UIEdgeInsets m_padding = UIEdgeInsets.Zero;
        private UIEdgeInsets m_borderThickness = UIEdgeInsets.Zero;
        private UIColor m_borderBrush;
        private bool m_isEnabled = true;
        private bool m_isRaisingOverlay;
        private nfloat m_heightAtLastLayout = -1;

        // ===========[ Setup/Construction/Teardown ]===================================
        public AutoGrid () : base(CGRect.Empty)
        {
            this.Setup();
        }

        /// <summary>Used by the runtime when UIKit creates the managed peer for an existing native view.</summary>
        protected AutoGrid (NativeHandle handle) : base(handle)
        {
            this.Setup();
        }

        // A peer made again for a native grid finds the native resizer view still there, holding no grid, and replaces it
        private void Setup ()
        {
            foreach (ResizerOverlay stale in this.Subviews.OfType<ResizerOverlay>())
            {
                stale.RemoveFromSuperview();
            }

            m_overlay = new ResizerOverlay(this);
            this.AddSubview(m_overlay);
        }

        // ===========[ Properties ]===================================
        /// <summary>How many columns a row holds before the next row starts. <see cref="AutoGridLayout.kUnlimited"/> (the default) never wraps.</summary>
        public int FixedColumnCount
        {
            get => m_tracks.FixedColumnCount;
            set
            {
                m_tracks.FixedColumnCount = value;
                this.InvalidateGrid();
            }
        }

        /// <summary>How many rows a column holds before the next column starts. <see cref="AutoGridLayout.kUnlimited"/> (the default) never wraps.</summary>
        public int FixedRowCount
        {
            get => m_tracks.FixedRowCount;
            set
            {
                m_tracks.FixedRowCount = value;
                this.InvalidateGrid();
            }
        }

        /// <summary>Whether subviews fill a row before the next (the default) or a column before the next</summary>
        public eAutoPopulationOrder AutoPopulationOrder
        {
            get => m_tracks.AutoPopulationOrder;
            set
            {
                m_tracks.AutoPopulationOrder = value;
                this.InvalidateGrid();
            }
        }

        /// <summary>With both counts fixed: whether the grid has only the rows and columns its subviews use, rather than all of them</summary>
        public bool ShrinkRowColumnsToUtilizedElementCount
        {
            get => m_tracks.ShrinkRowColumnsToUtilizedElementCount;
            set
            {
                m_tracks.ShrinkRowColumnsToUtilizedElementCount = value;
                this.InvalidateGrid();
            }
        }

        /// <summary>Whether a resizer sits between each pair of columns</summary>
        public bool AddColumnResizers
        {
            get => m_tracks.AddColumnResizers;
            set
            {
                m_tracks.AddColumnResizers = value;
                m_overlay.SetNeedsDisplay();
            }
        }

        /// <summary>Whether a resizer sits between each pair of rows</summary>
        public bool AddRowResizers
        {
            get => m_tracks.AddRowResizers;
            set
            {
                m_tracks.AddRowResizers = value;
                m_overlay.SetNeedsDisplay();
            }
        }

        /// <summary>How thick a resizer is drawn, in points. 2 unless set.</summary>
        public nfloat SizerLength
        {
            get => (nfloat)m_tracks.SizerLength;
            set
            {
                m_tracks.SizerLength = Math.Max(0.0, value);
                m_overlay.SetNeedsDisplay();
            }
        }

        /// <summary>The narrowest a column is laid out or dragged to, in points. 10 unless set.</summary>
        public nfloat MinElementWidth
        {
            get => (nfloat)m_tracks.MinElementWidth;
            set
            {
                m_tracks.MinElementWidth = Math.Max(0.0, value);
                this.InvalidateGrid();
            }
        }

        /// <summary>The shortest a row is laid out or dragged to, in points. 10 unless set.</summary>
        public nfloat MinElementHeight
        {
            get => (nfloat)m_tracks.MinElementHeight;
            set
            {
                m_tracks.MinElementHeight = Math.Max(0.0, value);
                this.InvalidateGrid();
            }
        }

        /// <summary>Space between the border and the cells, on each side</summary>
        public UIEdgeInsets Padding
        {
            get => m_padding;
            set
            {
                m_padding = value;
                this.InvalidateGrid();
            }
        }

        /// <summary>
        /// How thick the border at the grid's edge is on each side, in points. The cells sit inside it, and inside the
        /// <see cref="Padding"/> within it, as in WPF's AutoGrid. Zero (the default) is no border.
        /// </summary>
        public UIEdgeInsets BorderThickness
        {
            get => m_borderThickness;
            set
            {
                m_borderThickness = value;
                this.InvalidateGrid();
                m_overlay.SetNeedsDisplay();
            }
        }

        /// <summary>
        /// The border's color, as WPF's AutoGrid.BorderBrush. Null (the default) draws it in <see cref="MobileLook.AutoGrid"/>'s
        /// border key with the AJut theme loaded, and the system separator color without it.
        /// </summary>
        public UIColor BorderBrush
        {
            get => m_borderBrush;
            set
            {
                m_borderBrush = value;
                m_overlay.SetNeedsDisplay();
            }
        }

        /// <summary>
        /// Whether the resizers can be dragged. A UIView has no Enabled of its own, and the grid leaves its subviews' to
        /// them, as a layout does.
        /// </summary>
        public bool Enabled
        {
            get => m_isEnabled;
            set
            {
                m_isEnabled = value;
                if (!value)
                {
                    m_tracks.EndDrag();
                }
            }
        }

        /// <summary>Each row's share of the grid's height; they sum to 1. A drag or a target share changes them at once.</summary>
        public IReadOnlyList<double> RowShares => m_tracks.RowShares;

        /// <summary>Each column's share of the grid's width; they sum to 1. A drag or a target share changes them at once.</summary>
        public IReadOnlyList<double> ColumnShares => m_tracks.ColumnShares;

        /// <summary>The grid's cells' views, in cell order: its subviews, less the one it draws its resizers on</summary>
        public UIView[] GridChildren => this.Subviews.Where(this.IsGridChild).ToArray();

        // A parent laid out by constraints asks for the intrinsic size, and the height depends on the width, so the height
        //  is the one for the width the grid last had, and a layout at a new width updates it
        public override CGSize IntrinsicContentSize => new CGSize(UIView.NoIntrinsicMetric, this.ComputeWantedSize(this.Bounds.Width).Height);

        // ===========[ Public Interface Methods ]===================================
        /// <summary>Whether <paramref name="subview"/> has a cell; false for one past the last cell of a grid with both counts fixed, or for a view that is not one of the grid's</summary>
        public bool GetIsInRange (UIView subview)
        {
            int index = Array.IndexOf(this.GridChildren, subview);
            return (index >= 0) && m_tracks.TryGetCell(index, out _, out _);
        }

        /// <summary>
        /// Gives one row and one column a share of the grid, spreading the difference over the others, as WPF's AutoGrid
        /// does. -1 for either leaves that axis as it is.
        /// </summary>
        public void SetTargetSizePercentageFor (int targetRow, int targetColumn, double newRowSizePercent, double newColumnWidthPercent)
        {
            m_tracks.UpdateTrackCounts(this.GridChildren.Length);
            m_tracks.SetTargetSizePercentageFor(targetRow, targetColumn, newRowSizePercent, newColumnWidthPercent);
            this.SetNeedsLayout();
        }

        /// <summary>Gives one row and one column the share a size in points is of the grid at its last layout, as WPF's AutoGrid does</summary>
        public void SetTargetFixedSizeFor (int targetRow, int targetColumn, nfloat width, nfloat height)
        {
            // A row or column a subview added since the last layout has needs to be there to take a share
            m_tracks.UpdateTrackCounts(this.GridChildren.Length);
            m_tracks.SetTargetFixedSizeFor(targetRow, targetColumn, width, height);
            this.SetNeedsLayout();
        }

        public override CGSize SizeThatFits (CGSize size) => this.ComputeWantedSize(size.Width);

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews();
            UIView[] children = this.GridChildren;
            m_tracks.UpdateTrackCounts(children.Length);
            CGRect inner = this.GetInnerBounds();

            // The rows always share the height the grid has. UIKit has no measure pass to say whether that height was the
            //  one the grid asked for, and the rows can't tell from their subviews either: a plain view wants the cell it was
            //  last given, so rows following their subviews would never move again, by a drag or a target share.
            m_tracks.ArrangeColumns(inner.Width);
            m_tracks.ArrangeRows(inner.Height);
            double[] contentHeights = this.MeasureContentHeights(children, m_tracks.ColumnLengths);
            double wantedHeight = m_tracks.MeasureRows(double.PositiveInfinity, contentHeights).Sum();
            for (int index = 0; index < children.Length; ++index)
            {
                children[index].Frame = m_tracks.TryGetCell(index, out int row, out int column)
                    ? this.ToFrame(m_tracks.GetCellBounds(row, column), inner)
                    : new CGRect(inner.X, inner.Y, 0, 0);
            }

            // Its view stays on top, and comes back if something took every subview away
            if (!ReferenceEquals(m_overlay.Superview, this))
            {
                this.AddSubview(m_overlay);
            }

            m_overlay.Frame = this.Bounds;
            m_overlay.SetNeedsDisplay();

            var wanted = (nfloat)(wantedHeight + this.Inset.Top + this.Inset.Bottom);
            if (wanted != m_heightAtLastLayout)
            {
                m_heightAtLastLayout = wanted;
                this.InvalidateIntrinsicContentSize();
            }
        }

        // The resizers draw over the cells, so their view stays the topmost one as subviews come in
        public override void SubviewAdded (UIView uiview)
        {
            base.SubviewAdded(uiview);
            if (!m_isRaisingOverlay && (m_overlay != null) && !ReferenceEquals(uiview, m_overlay))
            {
                m_isRaisingOverlay = true;
                this.BringSubviewToFront(m_overlay);
                m_isRaisingOverlay = false;
            }

            this.InvalidateGrid();
        }

        public override void WillRemoveSubview (UIView uiview)
        {
            base.WillRemoveSubview(uiview);
            this.InvalidateGrid();
        }

        // ===========[ Helper Methods ]===================================
        private bool IsGridChild (UIView subview) => !ReferenceEquals(subview, m_overlay);

        private void InvalidateGrid ()
        {
            this.SetNeedsLayout();
            this.InvalidateIntrinsicContentSize();
        }

        // The cells sit inside the border and, within it, the padding, as in WPF's AutoGrid
        private UIEdgeInsets Inset => new UIEdgeInsets(
            m_padding.Top + m_borderThickness.Top,
            m_padding.Left + m_borderThickness.Left,
            m_padding.Bottom + m_borderThickness.Bottom,
            m_padding.Right + m_borderThickness.Right
        );

        private CGRect GetInnerBounds () => new CGRect(
            this.Inset.Left,
            this.Inset.Top,
            (nfloat)Math.Max(0.0, this.Bounds.Width - this.Inset.Left - this.Inset.Right),
            (nfloat)Math.Max(0.0, this.Bounds.Height - this.Inset.Top - this.Inset.Bottom)
        );

        private bool IsRightToLeft () => this.EffectiveUserInterfaceLayoutDirection == UIUserInterfaceLayoutDirection.RightToLeft;

        // From the tracks' space to the grid's: past the padding, and mirrored in a right to left layout
        private CGRect ToFrame (AutoGridTracks.TrackBounds bounds, CGRect inner)
        {
            double x = this.IsRightToLeft() ? inner.Width - bounds.X - bounds.Width : bounds.X;
            return new CGRect(inner.X + x, inner.Y + bounds.Y, bounds.Width, bounds.Height);
        }

        // From a point in the grid to the tracks' space
        private CGPoint ToTrackSpace (CGPoint point, CGRect inner)
        {
            nfloat x = point.X - inner.X;
            return new CGPoint(this.IsRightToLeft() ? inner.Width - x : x, point.Y - inner.Y);
        }

        // What the grid wants at a width: with one, the columns share it and each row is as tall as its tallest subview at
        //  its column's width; with none (zero or less, not laid out yet), each column is as wide as its widest subview too.
        //  The lengths are measured, not kept, so asking never moves the cells the grid has.
        private CGSize ComputeWantedSize (nfloat width)
        {
            UIView[] children = this.GridChildren;
            m_tracks.UpdateTrackCounts(children.Length);

            double innerWidth = width - this.Inset.Left - this.Inset.Right;
            double[] contentWidths = null;
            if (!(innerWidth > 0.0))
            {
                innerWidth = double.PositiveInfinity;
                contentWidths = new double[m_tracks.ColumnCount];
                for (int index = 0; index < children.Length; ++index)
                {
                    if (!children[index].Hidden && m_tracks.TryGetCell(index, out _, out int column))
                    {
                        double childWidth = WantedSize(children[index], nfloat.MaxValue).Width;
                        contentWidths[column] = Math.Max(contentWidths[column], childWidth);
                    }
                }
            }

            double[] columnWidths = m_tracks.MeasureColumns(innerWidth, contentWidths);
            double[] rowHeights = m_tracks.MeasureRows(double.PositiveInfinity, this.MeasureContentHeights(children, columnWidths));
            return new CGSize(
                columnWidths.Sum() + this.Inset.Left + this.Inset.Right,
                rowHeights.Sum() + this.Inset.Top + this.Inset.Bottom
            );
        }

        // Each row's tallest subview at its column's width. A hidden subview keeps its cell but wants no height, as a gone
        //  child does on Android.
        private double[] MeasureContentHeights (UIView[] children, IReadOnlyList<double> columnWidths)
        {
            var contentHeights = new double[m_tracks.RowCount];
            for (int index = 0; index < children.Length; ++index)
            {
                if (!children[index].Hidden && m_tracks.TryGetCell(index, out int row, out int column) && (column < columnWidths.Count))
                {
                    double height = WantedSize(children[index], (nfloat)columnWidths[column]).Height;
                    contentHeights[row] = Math.Max(contentHeights[row], height);
                }
            }

            return contentHeights;
        }

        // A label, a button or a switch says what it wants for the width there is. A plain view's SizeThatFits is the frame
        //  it has, so a plain view in a grid wants the cell it was last given: the rows grow to fit it, but don't shrink
        //  back on their own. A view laid out by constraints gives its compressed fitting size.
        private static CGSize WantedSize (UIView view, nfloat width)
        {
            var offered = new CGSize(width, nfloat.MaxValue);
            return view.TranslatesAutoresizingMaskIntoConstraints
                ? view.SizeThatFits(offered)
                : view.SystemLayoutSizeFittingSize(UIView.UILayoutFittingCompressedSize);
        }

        private bool TryBeginResizerDrag (CGPoint point)
        {
            if (!m_isEnabled || ((m_tracks.ColumnResizerCount + m_tracks.RowResizerCount) == 0))
            {
                return false;
            }

            CGPoint inTracks = this.ToTrackSpace(point, this.GetInnerBounds());
            return m_tracks.TryBeginDrag(inTracks.X, inTracks.Y, MobileLook.AutoGrid.ResizerGrabLength);
        }

        private bool IsOnResizer (CGPoint point)
        {
            if (!m_isEnabled || m_tracks.IsDragging || ((m_tracks.ColumnResizerCount + m_tracks.RowResizerCount) == 0))
            {
                return m_tracks.IsDragging;
            }

            // A trial start, taken back at once, says whether a press here would take hold of one
            bool isOnResizer = this.TryBeginResizerDrag(point);
            m_tracks.EndDrag();
            return isOnResizer;
        }

        private void ContinueResizerDrag (CGPoint point)
        {
            CGPoint inTracks = this.ToTrackSpace(point, this.GetInnerBounds());
            if (m_tracks.ContinueDrag(inTracks.X, inTracks.Y))
            {
                this.SetNeedsLayout();
            }
        }

        private void EndResizerDrag () => m_tracks.EndDrag();

        // Each side of the border is its own band at the grid's edge, so uneven sides draw the way WPF's AutoGrid draws them
        private void DrawBorder (CGContext context)
        {
            UIEdgeInsets border = m_borderThickness;
            if ((border.Left <= 0) && (border.Top <= 0) && (border.Right <= 0) && (border.Bottom <= 0))
            {
                return;
            }

            (m_borderBrush ?? ThemedColors.Get(MobileLook.AutoGrid.Border) ?? UIColor.Separator).SetFill();
            CGRect bounds = this.Bounds;
            nfloat middleHeight = (nfloat)Math.Max(0.0, bounds.Height - border.Top - border.Bottom);
            _Band(0, 0, bounds.Width, border.Top);
            _Band(0, bounds.Height - border.Bottom, bounds.Width, border.Bottom);
            _Band(0, border.Top, border.Left, middleHeight);
            _Band(bounds.Width - border.Right, border.Top, border.Right, middleHeight);

            void _Band (nfloat x, nfloat y, nfloat width, nfloat height)
            {
                if ((width > 0) && (height > 0))
                {
                    context.FillRect(new CGRect(x, y, width, height));
                }
            }
        }

        private void DrawResizers (CGContext context)
        {
            this.DrawBorder(context);
            int columnResizers = m_tracks.ColumnResizerCount;
            int rowResizers = m_tracks.RowResizerCount;
            if ((columnResizers + rowResizers) == 0)
            {
                return;
            }

            CGRect inner = this.GetInnerBounds();
            (ThemedColors.Get(MobileLook.AutoGrid.Resizer) ?? UIColor.Separator).SetFill();
            for (int index = 0; index < columnResizers; ++index)
            {
                context.FillRect(this.ToFrame(m_tracks.GetResizerBounds(true, index), inner));
            }

            for (int index = 0; index < rowResizers; ++index)
            {
                context.FillRect(this.ToFrame(m_tracks.GetResizerBounds(false, index), inner));
            }
        }

        // ===========[ Subclasses/structs ]===================================
        // The topmost subview, drawing the resizers and taking the touches that land on one. Every other touch passes
        //  through to the cells under it. It holds the grid weakly, and its gesture handler is static and finds the grid
        //  through the recognizer's view, so nothing the grid retains natively leads back to the grid.
        private sealed class ResizerOverlay : UIView
        {
            private static readonly ResizerPanDelegate g_panDelegate = new ResizerPanDelegate();
            private readonly WeakReference<AutoGrid> m_owner;

            public ResizerOverlay (AutoGrid owner) : base(CGRect.Empty)
            {
                m_owner = new WeakReference<AutoGrid>(owner);
                this.Opaque = false;
                this.BackgroundColor = UIColor.Clear;
                this.ContentMode = UIViewContentMode.Redraw;
                this.AddGestureRecognizer(new UIPanGestureRecognizer(OnPan) { Delegate = g_panDelegate, MaximumNumberOfTouches = 1 });
            }

            private ResizerOverlay (NativeHandle handle) : base(handle)
            {
                m_owner = new WeakReference<AutoGrid>(null);
            }

            public override bool PointInside (CGPoint point, UIEvent uievent)
            {
                return m_owner.TryGetTarget(out AutoGrid owner) && owner.IsOnResizer(this.ConvertPointToView(point, owner));
            }

            // UIKit draws a view that draws itself again on a light or dark switch, so the resizer color follows it
            public override void Draw (CGRect rect)
            {
                if (m_owner.TryGetTarget(out AutoGrid owner))
                {
                    using CGContext context = UIGraphics.GetCurrentContext();
                    if (context != null)
                    {
                        owner.DrawResizers(context);
                    }
                }
            }

            private static void OnPan (UIPanGestureRecognizer recognizer)
            {
                if ((recognizer.View is not ResizerOverlay overlay) || !overlay.m_owner.TryGetTarget(out AutoGrid owner))
                {
                    return;
                }

                CGPoint location = recognizer.LocationInView(owner);
                switch (recognizer.State)
                {
                    case UIGestureRecognizerState.Began:
                        {
                            // Where the touch came down, which the recognizer only knows once it has moved
                            CGPoint moved = recognizer.TranslationInView(owner);
                            owner.TryBeginResizerDrag(new CGPoint(location.X - moved.X, location.Y - moved.Y));
                            owner.ContinueResizerDrag(location);
                            break;
                        }

                    case UIGestureRecognizerState.Changed:
                        owner.ContinueResizerDrag(location);
                        break;

                    case UIGestureRecognizerState.Ended:
                    case UIGestureRecognizerState.Cancelled:
                    case UIGestureRecognizerState.Failed:
                        owner.EndResizerDrag();
                        break;
                }
            }
        }

        // A scroll view around the grid waits for a resizer drag to fail before it scrolls, so a drag that starts on a
        //  resizer resizes. A touch anywhere else never reaches the resizers, so it scrolls as ever. It holds nothing.
        private sealed class ResizerPanDelegate : UIGestureRecognizerDelegate
        {
            public override bool ShouldBeRequiredToFailBy (UIGestureRecognizer gestureRecognizer, UIGestureRecognizer otherGestureRecognizer)
            {
                return otherGestureRecognizer.View is UIScrollView;
            }
        }
    }
}
