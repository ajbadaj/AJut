namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.UX.Theming;
    using AppKit;
    using CoreGraphics;
    using ObjCRuntime;

    // ===========[ AutoGrid ]===================================================
    // A grid that adds rows and columns as subviews come in, the AppKit twin of WPF's AutoGrid:
    // each subview takes the next cell, across a row or down a column, wrapping at FixedColumnCount
    // or FixedRowCount, and with AddColumnResizers or AddRowResizers a resizer between each pair of
    // columns or rows can be dragged to trade space between them. The rules are AJut.UX's
    // AutoGridLayout, shared with every stack's AutoGrid.
    //
    // Each subview fills its cell, placed by frame, so it keeps TranslatesAutoresizingMaskIntoConstraints
    // on. The columns and rows share the grid's bounds inside its Padding by their shares, each at least
    // MinElementWidth or MinElementHeight long. Its intrinsic height is each row as tall as its tallest
    // subview's fitting size, as star rows are in a WPF Grid given unbounded height. Laid out, though,
    // the rows share whatever height the grid has by their shares, unlike WPF's and Android's, whose
    // rows follow their children when the grid has no height to fill: AppKit has no measure pass to say
    // so. A hidden subview keeps its cell and wants no height, and one past the last cell of a grid with
    // both counts fixed gets an empty frame (GetIsInRange says which). In a right to left layout
    // column 0 is on the right.
    //
    // The resizers are drawn and dragged on the grid's topmost subview, which takes the mouse only on a
    // resizer and lets every other click through to the cells, with the resize cursor over each. They
    // take ThemedColorKeys.Splitter_Divider, the color ThemedSplitView's divider takes.
    //
    // BorderThickness and BorderColorKey draw a border at the grid's edge, as WPF's AutoGrid's
    // BorderThickness and BorderBrush do, in ThemedColorKeys.AutoGrid_Border unless another key is set,
    // with the cells inside it and the Padding within it.
    public class AutoGrid : NSView
    {
        private const double kResizerGrabLength = 6.0;

        private readonly AutoGridTracks m_tracks = new AutoGridTracks();
        private ResizerOverlay m_overlay;
        private NSEdgeInsets m_padding = new NSEdgeInsets(0, 0, 0, 0);
        private NSEdgeInsets m_borderThickness = new NSEdgeInsets(0, 0, 0, 0);
        private string m_borderColorKey = ThemedColorKeys.AutoGrid_Border;
        private bool m_isEnabled = true;
        private bool m_isRaisingOverlay;
        private nfloat m_heightAtLastLayout = -1;

        // ===========[ Setup/Construction/Teardown ]===========================
        public AutoGrid ()
        {
            this.Setup();
        }

        /// <summary>Used by the runtime when AppKit creates the managed peer for an existing native view.</summary>
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

        // ===========[ Properties ]============================================
        /// <summary>Top-left origin, as the grid's cells and the other AJut views have it</summary>
        public override bool IsFlipped => true;

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
                m_overlay.Refresh();
            }
        }

        /// <summary>Whether a resizer sits between each pair of rows</summary>
        public bool AddRowResizers
        {
            get => m_tracks.AddRowResizers;
            set
            {
                m_tracks.AddRowResizers = value;
                m_overlay.Refresh();
            }
        }

        /// <summary>How thick a resizer is drawn, in points. 2 unless set.</summary>
        public double SizerLength
        {
            get => m_tracks.SizerLength;
            set
            {
                m_tracks.SizerLength = Math.Max(0.0, value);
                m_overlay.Refresh();
            }
        }

        /// <summary>The narrowest a column is laid out or dragged to, in points. 10 unless set.</summary>
        public double MinElementWidth
        {
            get => m_tracks.MinElementWidth;
            set
            {
                m_tracks.MinElementWidth = Math.Max(0.0, value);
                this.InvalidateGrid();
            }
        }

        /// <summary>The shortest a row is laid out or dragged to, in points. 10 unless set.</summary>
        public double MinElementHeight
        {
            get => m_tracks.MinElementHeight;
            set
            {
                m_tracks.MinElementHeight = Math.Max(0.0, value);
                this.InvalidateGrid();
            }
        }

        /// <summary>Space between the border and the cells, on each side</summary>
        public NSEdgeInsets Padding
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
        public NSEdgeInsets BorderThickness
        {
            get => m_borderThickness;
            set
            {
                m_borderThickness = value;
                this.InvalidateGrid();
                m_overlay.Refresh();
            }
        }

        /// <summary>
        /// The <see cref="ThemedColors"/> key the border is drawn in, WPF's AutoGrid.BorderBrush:
        /// <see cref="ThemedColorKeys.AutoGrid_Border"/> by default. Null draws no border, whatever its thickness.
        /// </summary>
        public string BorderColorKey
        {
            get => m_borderColorKey;
            set
            {
                m_borderColorKey = value;
                m_overlay.Refresh();
            }
        }

        /// <summary>
        /// Whether the resizers can be dragged. An NSView has no Enabled of its own, and the grid leaves its subviews' to
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

                m_overlay.Refresh();
            }
        }

        /// <summary>Each row's share of the grid's height; they sum to 1. A drag or a target share changes them at once.</summary>
        public IReadOnlyList<double> RowShares => m_tracks.RowShares;

        /// <summary>Each column's share of the grid's width; they sum to 1. A drag or a target share changes them at once.</summary>
        public IReadOnlyList<double> ColumnShares => m_tracks.ColumnShares;

        /// <summary>The grid's cells' views, in cell order: its subviews, less the one it draws its resizers on</summary>
        public NSView[] GridChildren => this.Subviews.Where(this.IsGridChild).ToArray();

        public override CGSize IntrinsicContentSize => new CGSize(NSView.NoIntrinsicMetric, this.ComputeWantedHeight());

        private int ResizerCount => m_tracks.ColumnResizerCount + m_tracks.RowResizerCount;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Whether <paramref name="subview"/> has a cell; false for one past the last cell of a grid with both counts fixed, or for a view that is not one of the grid's</summary>
        public bool GetIsInRange (NSView subview)
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
            this.NeedsLayout = true;
        }

        /// <summary>Gives one row and one column the share a size in points is of the grid at its last layout, as WPF's AutoGrid does</summary>
        public void SetTargetFixedSizeFor (int targetRow, int targetColumn, double width, double height)
        {
            // A row or column a subview added since the last layout has needs to be there to take a share
            m_tracks.UpdateTrackCounts(this.GridChildren.Length);
            m_tracks.SetTargetFixedSizeFor(targetRow, targetColumn, width, height);
            this.NeedsLayout = true;
        }

        public override void Layout ()
        {
            base.Layout();
            NSView[] children = this.GridChildren;
            m_tracks.UpdateTrackCounts(children.Length);
            CGRect inner = this.GetInnerBounds();

            // The rows always share the height the grid has. AppKit has no measure pass to say whether that height was the
            //  one the grid asked for, and a subview's fitting size can be the frame it was last given, so rows following
            //  their subviews could never move again, by a drag or a target share.
            m_tracks.ArrangeColumns(inner.Width);
            m_tracks.ArrangeRows(inner.Height);
            double[] contentHeights = this.MeasureContentHeights(children);
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
            m_overlay.Refresh();

            var wanted = (nfloat)(wantedHeight + this.Inset.Top + this.Inset.Bottom);
            if (wanted != m_heightAtLastLayout)
            {
                m_heightAtLastLayout = wanted;
                this.InvalidateIntrinsicContentSize();
            }
        }

        // The resizers draw over the cells, so their view stays the topmost one as subviews come in
        public override void DidAddSubview (NSView subview)
        {
            base.DidAddSubview(subview);
            if (!m_isRaisingOverlay && (m_overlay != null) && !ReferenceEquals(subview, m_overlay))
            {
                m_isRaisingOverlay = true;
                this.AddSubview(m_overlay, NSWindowOrderingMode.Above, null);
                m_isRaisingOverlay = false;
            }

            this.InvalidateGrid();
        }

        public override void WillRemoveSubview (NSView subview)
        {
            base.WillRemoveSubview(subview);
            this.InvalidateGrid();
        }

        public override void ViewDidChangeEffectiveAppearance ()
        {
            base.ViewDidChangeEffectiveAppearance();
            m_overlay?.Refresh();
        }

        // ===========[ Helper Methods ]========================================
        private bool IsGridChild (NSView subview) => !ReferenceEquals(subview, m_overlay);

        private void InvalidateGrid ()
        {
            this.NeedsLayout = true;
            this.InvalidateIntrinsicContentSize();
        }

        // The cells sit inside the border and, within it, the padding, as in WPF's AutoGrid
        private NSEdgeInsets Inset => new NSEdgeInsets(
            m_padding.Top + m_borderThickness.Top,
            m_padding.Left + m_borderThickness.Left,
            m_padding.Bottom + m_borderThickness.Bottom,
            m_padding.Right + m_borderThickness.Right
        );

        // Each side of the border is its own band at the grid's edge, so uneven sides draw the way WPF's AutoGrid draws them
        private void DrawBorder (CGRect bounds)
        {
            NSEdgeInsets border = m_borderThickness;
            NSColor color = (m_borderColorKey != null) ? ThemedColors.Get(m_borderColorKey) : null;
            if ((color == null) || ((border.Left <= 0) && (border.Top <= 0) && (border.Right <= 0) && (border.Bottom <= 0)))
            {
                return;
            }

            color.SetFill();
            nfloat middleHeight = (nfloat)Math.Max(0.0, bounds.Height - border.Top - border.Bottom);
            _Band(0, 0, bounds.Width, border.Top);
            _Band(0, bounds.Height - border.Bottom, bounds.Width, border.Bottom);
            _Band(0, border.Top, border.Left, middleHeight);
            _Band(bounds.Width - border.Right, border.Top, border.Right, middleHeight);

            static void _Band (nfloat x, nfloat y, nfloat width, nfloat height)
            {
                if ((width > 0) && (height > 0))
                {
                    NSBezierPath.FillRect(new CGRect(x, y, width, height));
                }
            }
        }

        private CGRect GetInnerBounds () => new CGRect(
            this.Inset.Left,
            this.Inset.Top,
            (nfloat)Math.Max(0.0, this.Bounds.Width - this.Inset.Left - this.Inset.Right),
            (nfloat)Math.Max(0.0, this.Bounds.Height - this.Inset.Top - this.Inset.Bottom)
        );

        private bool IsRightToLeft () => this.UserInterfaceLayoutDirection == NSUserInterfaceLayoutDirection.RightToLeft;

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

        // The height the grid wants: each row as tall as its tallest subview's fitting size, which in AppKit doesn't depend
        //  on the width offered. The lengths are measured, not kept, so asking never moves the cells the grid has.
        private nfloat ComputeWantedHeight ()
        {
            NSView[] children = this.GridChildren;
            m_tracks.UpdateTrackCounts(children.Length);
            double[] rowHeights = m_tracks.MeasureRows(double.PositiveInfinity, this.MeasureContentHeights(children));
            return (nfloat)(rowHeights.Sum() + this.Inset.Top + this.Inset.Bottom);
        }

        // Each row's tallest subview. A hidden subview keeps its cell but wants no height, as a gone child does on Android.
        private double[] MeasureContentHeights (NSView[] children)
        {
            var contentHeights = new double[m_tracks.RowCount];
            for (int index = 0; index < children.Length; ++index)
            {
                if (!children[index].Hidden && m_tracks.TryGetCell(index, out int row, out _))
                {
                    contentHeights[row] = Math.Max(contentHeights[row], (double)children[index].FittingSize.Height);
                }
            }

            return contentHeights;
        }

        // Each resizer's grab band, in the grid's space: the resizer itself, widened along its axis to the grab length
        private IEnumerable<ResizerHandle> GetResizerHandles ()
        {
            CGRect inner = this.GetInnerBounds();
            for (int index = 0; index < m_tracks.ColumnResizerCount; ++index)
            {
                CGRect drawn = this.ToFrame(m_tracks.GetResizerBounds(true, index), inner);
                nfloat grab = (nfloat)Math.Max(drawn.Width, kResizerGrabLength);
                var grabBand = new CGRect(drawn.GetMidX() - (grab / 2), drawn.Y, grab, drawn.Height);
                yield return new ResizerHandle(drawn, grabBand, IsColumnResizer: true);
            }

            for (int index = 0; index < m_tracks.RowResizerCount; ++index)
            {
                CGRect drawn = this.ToFrame(m_tracks.GetResizerBounds(false, index), inner);
                nfloat grab = (nfloat)Math.Max(drawn.Height, kResizerGrabLength);
                var grabBand = new CGRect(drawn.X, drawn.GetMidY() - (grab / 2), drawn.Width, grab);
                yield return new ResizerHandle(drawn, grabBand, IsColumnResizer: false);
            }
        }

        private bool TryBeginResizerDrag (CGPoint point)
        {
            if (!m_isEnabled || (this.ResizerCount == 0))
            {
                return false;
            }

            CGPoint inTracks = this.ToTrackSpace(point, this.GetInnerBounds());
            return m_tracks.TryBeginDrag(inTracks.X, inTracks.Y, kResizerGrabLength);
        }

        private bool IsOnResizer (CGPoint point)
        {
            if (m_tracks.IsDragging)
            {
                return true;
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
                this.NeedsLayout = true;
            }
        }

        // ===========[ Subclasses/structs ]====================================
        // Where a resizer is drawn, where it takes hold of the mouse, and which way it drags
        private readonly record struct ResizerHandle (CGRect Drawn, CGRect Grab, bool IsColumnResizer);

        // The topmost subview, drawing the resizers and taking the mouse on one, with every other click passing through to
        //  the cells under it. It holds the grid weakly, so nothing the grid retains natively leads back to the grid.
        private sealed class ResizerOverlay : NSView
        {
            private readonly WeakReference<AutoGrid> m_owner;

            public ResizerOverlay (AutoGrid owner)
            {
                m_owner = new WeakReference<AutoGrid>(owner);
            }

            /// <summary>Used by the runtime when AppKit creates the managed peer for an existing native view.</summary>
            private ResizerOverlay (NativeHandle handle) : base(handle)
            {
                m_owner = new WeakReference<AutoGrid>(null);
            }

            public override bool IsFlipped => true;

            public void Refresh ()
            {
                this.NeedsDisplay = true;
                this.Window?.InvalidateCursorRectsForView(this);
            }

            public override NSView HitTest (CGPoint aPoint)
            {
                if ((this.Superview == null) || !m_owner.TryGetTarget(out AutoGrid owner))
                {
                    return null;
                }

                CGPoint inGrid = owner.ConvertPointFromView(aPoint, this.Superview);
                return owner.IsOnResizer(inGrid) ? this : null;
            }

            // A press on a resizer in a window that isn't key starts the drag at once, as NSSplitView's divider does
            public override bool AcceptsFirstMouse (NSEvent theEvent) => true;

            public override void MouseDown (NSEvent theEvent)
            {
                if (m_owner.TryGetTarget(out AutoGrid owner))
                {
                    owner.TryBeginResizerDrag(owner.ConvertPointFromView(theEvent.LocationInWindow, null));
                }
            }

            public override void MouseDragged (NSEvent theEvent)
            {
                if (m_owner.TryGetTarget(out AutoGrid owner))
                {
                    owner.ContinueResizerDrag(owner.ConvertPointFromView(theEvent.LocationInWindow, null));
                }
            }

            public override void MouseUp (NSEvent theEvent)
            {
                if (m_owner.TryGetTarget(out AutoGrid owner))
                {
                    owner.m_tracks.EndDrag();
                }
            }

            public override void ResetCursorRects ()
            {
                if (!m_owner.TryGetTarget(out AutoGrid owner) || !owner.m_isEnabled)
                {
                    return;
                }

                foreach (ResizerHandle handle in owner.GetResizerHandles())
                {
                    NSCursor cursor = handle.IsColumnResizer ? NSCursor.ResizeLeftRightCursor : NSCursor.ResizeUpDownCursor;
                    this.AddCursorRect(owner.ConvertRectToView(handle.Grab, this), cursor);
                }
            }

            public override void DrawRect (CGRect dirtyRect)
            {
                if (!m_owner.TryGetTarget(out AutoGrid owner))
                {
                    return;
                }

                // The overlay covers the grid exactly, so the grid's bounds are its own
                owner.DrawBorder(this.Bounds);
                if (owner.ResizerCount == 0)
                {
                    return;
                }

                // A null key would mean no line; the divider key always has a color, the system separator unthemed
                NSColor color = ThemedColors.Get(ThemedColorKeys.Splitter_Divider);
                if (color == null)
                {
                    return;
                }

                color.SetFill();
                foreach (ResizerHandle handle in owner.GetResizerHandles())
                {
                    NSBezierPath.FillRect(owner.ConvertRectToView(handle.Drawn, this));
                }
            }
        }
    }
}
