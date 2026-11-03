namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.UX.Theming;
    using Android.Content;
    using Android.Content.Res;
    using Android.Graphics;
    using Android.Runtime;
    using Android.Util;
    using Android.Views;
    using AndroidColor = Android.Graphics.Color;

    /// <summary>
    /// A grid that adds rows and columns as children come in, the Android twin of WPF's AutoGrid: each child takes the next
    /// cell, across a row or down a column (<see cref="AutoPopulationOrder"/>), wrapping at <see cref="FixedColumnCount"/> or
    /// <see cref="FixedRowCount"/>, and with <see cref="AddColumnResizers"/> or <see cref="AddRowResizers"/> a resizer between
    /// each pair of columns or rows can be dragged to trade space between them. The rules are AJut.UX's
    /// <see cref="AutoGridLayout"/>, shared with every stack's AutoGrid.
    /// </summary>
    /// <remarks>
    /// Each child fills its cell. Given a width or height of its own (match_parent, or an exact size), the grid's columns or
    /// rows share it by their shares, each at least <see cref="MinElementWidth"/> or <see cref="MinElementHeight"/> long.
    /// Without one (wrap_content, or the way a scroll view scrolls), each column is as wide as its widest child and each row
    /// as tall as its tallest, as star tracks are in a WPF Grid given unbounded space, and that axis has no resizers, since
    /// its tracks follow their children rather than their shares; a wrap_content grid whose children want more than the
    /// space there is shares the space after all. A child that is gone keeps its cell
    /// and draws nothing. A child with no cell (past the last one of a grid with both counts fixed) is laid out with no
    /// size; <see cref="GetIsInRange"/> says which those are. The grid's own padding insets the cells. In a right to left
    /// layout column 0 is on the right, as in a mirrored WPF Grid. The grid paints nothing but its resizers, which take
    /// <see cref="MobileLook.AutoGrid"/>'s key with the AJut theme loaded, and a resizer takes hold of a press within a
    /// finger's width of it. A disabled grid's resizers don't drag. <see cref="SetBorderThickness"/> and
    /// <see cref="BorderBrush"/> draw a border at the grid's edge, as WPF's AutoGrid's do, with the cells inside it and the
    /// padding within it. In a layout, the ajut attributes of AJutAutoGrid set
    /// each property.
    /// </remarks>
    [Register("ajut.ux.controls.AutoGrid")]
    public class AutoGrid : ViewGroup
    {
        private readonly AutoGridTracks m_tracks = new AutoGridTracks();
        private readonly Paint m_resizerPaint = new Paint();
        private readonly Paint m_borderPaint = new Paint();
        private int m_borderLeft;
        private int m_borderTop;
        private int m_borderRight;
        private int m_borderBottom;
        private AndroidColor? m_borderBrush;
        private float m_density = 1f;
        private int m_dragPointerId = -1;

        // ===========[ Setup/Construction/Teardown ]===================================
        public AutoGrid (Context context) : this(context, null)
        {
        }

        public AutoGrid (Context context, IAttributeSet? attrs) : this(context, attrs, 0)
        {
        }

        public AutoGrid (Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            this.SetDefaultSizes(context);
            if (attrs != null)
            {
                using TypedArray values = context.ObtainStyledAttributes(attrs, Resource.Styleable.AJutAutoGrid);
                this.ReadAttributes(values);
                values.Recycle();
            }
        }

        protected AutoGrid (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
            if (this.Context != null)
            {
                this.SetDefaultSizes(this.Context);
            }
        }

        // The sizes are dp, given in pixels as a view's sizes are
        private void SetDefaultSizes (Context context)
        {
            m_density = context.Resources?.DisplayMetrics?.Density ?? 1f;
            m_tracks.SizerLength = AutoGridLayout.kDefaultSizerLength * m_density;
            m_tracks.MinElementWidth = AutoGridLayout.kDefaultMinElementLength * m_density;
            m_tracks.MinElementHeight = AutoGridLayout.kDefaultMinElementLength * m_density;
        }

        private void ReadAttributes (TypedArray values)
        {
            m_tracks.FixedColumnCount = values.GetInt(Resource.Styleable.AJutAutoGrid_ajutFixedColumnCount, m_tracks.FixedColumnCount);
            m_tracks.FixedRowCount = values.GetInt(Resource.Styleable.AJutAutoGrid_ajutFixedRowCount, m_tracks.FixedRowCount);
            m_tracks.AutoPopulationOrder = (eAutoPopulationOrder)values.GetInt(
                Resource.Styleable.AJutAutoGrid_ajutAutoPopulationOrder,
                (int)m_tracks.AutoPopulationOrder
            );
            m_tracks.AddColumnResizers = values.GetBoolean(Resource.Styleable.AJutAutoGrid_ajutAddColumnResizers, false);
            m_tracks.AddRowResizers = values.GetBoolean(Resource.Styleable.AJutAutoGrid_ajutAddRowResizers, false);
            m_tracks.ShrinkRowColumnsToUtilizedElementCount = values.GetBoolean(
                Resource.Styleable.AJutAutoGrid_ajutShrinkRowColumnsToUtilizedElementCount,
                false
            );
            m_tracks.SizerLength = _Dimension(Resource.Styleable.AJutAutoGrid_ajutSizerLength, m_tracks.SizerLength);
            m_tracks.MinElementWidth = _Dimension(Resource.Styleable.AJutAutoGrid_ajutMinElementWidth, m_tracks.MinElementWidth);
            m_tracks.MinElementHeight = _Dimension(Resource.Styleable.AJutAutoGrid_ajutMinElementHeight, m_tracks.MinElementHeight);

            // One thickness for all four sides, as android:padding is
            int borderThickness = (int)Math.Round(_Dimension(Resource.Styleable.AJutAutoGrid_ajutBorderThickness, 0.0));
            this.SetBorderThickness(borderThickness, borderThickness, borderThickness, borderThickness);
            if (values.HasValue(Resource.Styleable.AJutAutoGrid_ajutBorderBrush))
            {
                m_borderBrush = values.GetColor(Resource.Styleable.AJutAutoGrid_ajutBorderBrush, 0);
            }

            // A ViewGroup ignores android:enabled from a layout, so the grid reads it the way a widget does
            this.Enabled = values.GetBoolean(Resource.Styleable.AJutAutoGrid_android_enabled, true);

            double _Dimension (int index, double fallback) => Math.Max(0f, values.GetDimension(index, (float)fallback));
        }

        // ===========[ Properties ]===================================
        /// <summary>How many columns a row holds before the next row starts. <see cref="AutoGridLayout.kUnlimited"/> (the default) never wraps.</summary>
        public int FixedColumnCount
        {
            get => m_tracks.FixedColumnCount;
            set
            {
                m_tracks.FixedColumnCount = value;
                this.RequestLayout();
            }
        }

        /// <summary>How many rows a column holds before the next column starts. <see cref="AutoGridLayout.kUnlimited"/> (the default) never wraps.</summary>
        public int FixedRowCount
        {
            get => m_tracks.FixedRowCount;
            set
            {
                m_tracks.FixedRowCount = value;
                this.RequestLayout();
            }
        }

        /// <summary>Whether children fill a row before the next (the default) or a column before the next</summary>
        public eAutoPopulationOrder AutoPopulationOrder
        {
            get => m_tracks.AutoPopulationOrder;
            set
            {
                m_tracks.AutoPopulationOrder = value;
                this.RequestLayout();
            }
        }

        /// <summary>With both counts fixed: whether the grid has only the rows and columns its children use, rather than all of them</summary>
        public bool ShrinkRowColumnsToUtilizedElementCount
        {
            get => m_tracks.ShrinkRowColumnsToUtilizedElementCount;
            set
            {
                m_tracks.ShrinkRowColumnsToUtilizedElementCount = value;
                this.RequestLayout();
            }
        }

        /// <summary>Whether a resizer sits between each pair of columns</summary>
        public bool AddColumnResizers
        {
            get => m_tracks.AddColumnResizers;
            set
            {
                m_tracks.AddColumnResizers = value;
                this.Invalidate();
            }
        }

        /// <summary>Whether a resizer sits between each pair of rows</summary>
        public bool AddRowResizers
        {
            get => m_tracks.AddRowResizers;
            set
            {
                m_tracks.AddRowResizers = value;
                this.Invalidate();
            }
        }

        /// <summary>How thick a resizer is drawn, in pixels, as a view's sizes are. 2dp unless set.</summary>
        public float SizerLength
        {
            get => (float)m_tracks.SizerLength;
            set
            {
                m_tracks.SizerLength = Math.Max(0f, value);
                this.Invalidate();
            }
        }

        /// <summary>The narrowest a column is laid out or dragged to, in pixels. 10dp unless set.</summary>
        public float MinElementWidth
        {
            get => (float)m_tracks.MinElementWidth;
            set
            {
                m_tracks.MinElementWidth = Math.Max(0f, value);
                this.RequestLayout();
            }
        }

        /// <summary>The shortest a row is laid out or dragged to, in pixels. 10dp unless set.</summary>
        public float MinElementHeight
        {
            get => (float)m_tracks.MinElementHeight;
            set
            {
                m_tracks.MinElementHeight = Math.Max(0f, value);
                this.RequestLayout();
            }
        }

        /// <summary>The border's left thickness in pixels, as a view's sizes are; <see cref="SetBorderThickness"/> sets it</summary>
        public int BorderThicknessLeft => m_borderLeft;

        /// <summary>The border's top thickness in pixels; <see cref="SetBorderThickness"/> sets it</summary>
        public int BorderThicknessTop => m_borderTop;

        /// <summary>The border's right thickness in pixels; <see cref="SetBorderThickness"/> sets it</summary>
        public int BorderThicknessRight => m_borderRight;

        /// <summary>The border's bottom thickness in pixels; <see cref="SetBorderThickness"/> sets it</summary>
        public int BorderThicknessBottom => m_borderBottom;

        /// <summary>
        /// The border's color, as WPF's AutoGrid.BorderBrush. Null (the default) draws it in <see cref="MobileLook.AutoGrid"/>'s
        /// border key with the AJut theme loaded, and the platform's divider gray without it.
        /// </summary>
        public AndroidColor? BorderBrush
        {
            get => m_borderBrush;
            set
            {
                m_borderBrush = value;
                this.Invalidate();
            }
        }

        /// <summary>Each row's share of the grid's height; they sum to 1. A drag or a target share changes them at once.</summary>
        public IReadOnlyList<double> RowShares => m_tracks.RowShares;

        /// <summary>Each column's share of the grid's width; they sum to 1. A drag or a target share changes them at once.</summary>
        public IReadOnlyList<double> ColumnShares => m_tracks.ColumnShares;

        // ===========[ Public Interface Methods ]===================================
        /// <summary>
        /// Sets the border's thickness on each side, in pixels, as <see cref="View.SetPadding"/> sets the padding. The cells
        /// sit inside the border, and inside the padding within it, as in WPF's AutoGrid. All zero (the default) is no border.
        /// </summary>
        public void SetBorderThickness (int left, int top, int right, int bottom)
        {
            m_borderLeft = Math.Max(0, left);
            m_borderTop = Math.Max(0, top);
            m_borderRight = Math.Max(0, right);
            m_borderBottom = Math.Max(0, bottom);
            this.RequestLayout();
            this.Invalidate();
        }

        /// <summary>Whether <paramref name="child"/> has a cell; false for one past the last cell of a grid with both counts fixed, or for a view that is not a child</summary>
        public bool GetIsInRange (View child)
        {
            int index = this.IndexOfChild(child);
            return (index >= 0) && m_tracks.TryGetCell(index, out _, out _);
        }

        /// <summary>
        /// Gives one row and one column a share of the grid, spreading the difference over the others, as WPF's AutoGrid
        /// does. -1 for either leaves that axis as it is.
        /// </summary>
        public void SetTargetSizePercentageFor (int targetRow, int targetColumn, double newRowSizePercent, double newColumnWidthPercent)
        {
            m_tracks.UpdateTrackCounts(this.ChildCount);
            m_tracks.SetTargetSizePercentageFor(targetRow, targetColumn, newRowSizePercent, newColumnWidthPercent);
            this.RequestLayout();
        }

        /// <summary>Gives one row and one column the share a size in pixels is of the grid at its last layout, as WPF's AutoGrid does</summary>
        public void SetTargetFixedSizeFor (int targetRow, int targetColumn, float width, float height)
        {
            // A row or column a child added since the last layout has needs to be there to take a share
            m_tracks.UpdateTrackCounts(this.ChildCount);
            m_tracks.SetTargetFixedSizeFor(targetRow, targetColumn, width, height);
            this.RequestLayout();
        }

        protected override void OnMeasure (int widthMeasureSpec, int heightMeasureSpec)
        {
            m_tracks.UpdateTrackCounts(this.ChildCount);
            int horizontalPadding = this.InsetLeft + this.InsetRight;
            int verticalPadding = this.InsetTop + this.InsetBottom;
            MeasureSpecMode widthMode = MeasureSpec.GetMode(widthMeasureSpec);
            MeasureSpecMode heightMode = MeasureSpec.GetMode(heightMeasureSpec);
            int innerWidth = Math.Max(0, MeasureSpec.GetSize(widthMeasureSpec) - horizontalPadding);
            int innerHeight = Math.Max(0, MeasureSpec.GetSize(heightMeasureSpec) - verticalPadding);
            int unspecified = MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified);

            // 1. The columns share a width the grid is given; without one, each is as wide as its widest child
            double[]? contentWidths = null;
            if (widthMode != MeasureSpecMode.Exactly)
            {
                contentWidths = new double[m_tracks.ColumnCount];
                int offered = (widthMode == MeasureSpecMode.AtMost)
                    ? MeasureSpec.MakeMeasureSpec(innerWidth, MeasureSpecMode.AtMost)
                    : unspecified;
                for (int index = 0; index < this.ChildCount; ++index)
                {
                    View child = this.GetChildAt(index)!;
                    if ((child.Visibility != ViewStates.Gone) && m_tracks.TryGetCell(index, out _, out int column))
                    {
                        // As it asks to be (wrap_content, an exact size) within what is offered
                        child.Measure(
                            GetChildMeasureSpec(offered, 0, _AskedWidth(child)),
                            GetChildMeasureSpec(unspecified, 0, _AskedHeight(child))
                        );
                        contentWidths[column] = Math.Max(contentWidths[column], child.MeasuredWidth);
                    }
                }
            }

            // A wrap_content grid whose children want more than the width there is shares that width instead, so the rows
            //  are measured at the widths the columns will have
            this.ArrangeTracks(isColumns: true, widthMode, innerWidth, contentWidths);

            // 2. The rows the same way, each child measured at its column's width
            double[]? contentHeights = null;
            if (heightMode != MeasureSpecMode.Exactly)
            {
                contentHeights = new double[m_tracks.RowCount];
                int offered = (heightMode == MeasureSpecMode.AtMost)
                    ? MeasureSpec.MakeMeasureSpec(innerHeight, MeasureSpecMode.AtMost)
                    : unspecified;
                for (int index = 0; index < this.ChildCount; ++index)
                {
                    View child = this.GetChildAt(index)!;
                    if ((child.Visibility != ViewStates.Gone) && m_tracks.TryGetCell(index, out int row, out int column))
                    {
                        CellPixels cell = this.GetCellPixels(row, column);
                        child.Measure(
                            MeasureSpec.MakeMeasureSpec(cell.Width, MeasureSpecMode.Exactly),
                            GetChildMeasureSpec(offered, 0, _AskedHeight(child))
                        );
                        contentHeights[row] = Math.Max(contentHeights[row], child.MeasuredHeight);
                    }
                }
            }

            this.ArrangeTracks(isColumns: false, heightMode, innerHeight, contentHeights);

            // 3. Each child at its cell, filling it whatever it asked for, as a WPF Grid stretches its children
            this.MeasureChildrenToCells();
            this.SetMeasuredDimension(
                ResolveSize((int)Math.Ceiling(m_tracks.ColumnLengths.Sum()) + horizontalPadding, widthMeasureSpec),
                ResolveSize((int)Math.Ceiling(m_tracks.RowLengths.Sum()) + verticalPadding, heightMeasureSpec)
            );

            int _AskedWidth (View child) => child.LayoutParameters?.Width ?? ViewGroup.LayoutParams.WrapContent;
            int _AskedHeight (View child) => child.LayoutParameters?.Height ?? ViewGroup.LayoutParams.WrapContent;
        }

        protected override void OnLayout (bool changed, int left, int top, int right, int bottom)
        {
            // A parent can lay the grid out at another size than it measured, and then the tracks share the size it has.
            //  The size is compared with the measured size, not with the tracks' lengths, since a measured size is whole
            //  pixels and the lengths need not be (a 10dp minimum is 26.25 pixels at 2.625 density).
            int innerWidth = Math.Max(0, (right - left) - this.InsetLeft - this.InsetRight);
            int innerHeight = Math.Max(0, (bottom - top) - this.InsetTop - this.InsetBottom);
            bool isResized = false;
            if ((right - left) != this.MeasuredWidth)
            {
                m_tracks.ArrangeColumns(innerWidth);
                isResized = true;
            }

            if ((bottom - top) != this.MeasuredHeight)
            {
                m_tracks.ArrangeRows(innerHeight);
                isResized = true;
            }

            if (isResized)
            {
                this.MeasureChildrenToCells();
            }

            for (int index = 0; index < this.ChildCount; ++index)
            {
                View child = this.GetChildAt(index)!;
                if (child.Visibility == ViewStates.Gone)
                {
                    continue;
                }

                if (m_tracks.TryGetCell(index, out int row, out int column))
                {
                    CellPixels cell = this.ToLaidOut(this.GetCellPixels(row, column), innerWidth);
                    child.Layout(cell.Left, cell.Top, cell.Left + cell.Width, cell.Top + cell.Height);
                }
                else
                {
                    child.Layout(this.InsetLeft, this.InsetTop, this.InsetLeft, this.InsetTop);
                }
            }

            this.Invalidate();
        }

        // A press on a resizer is the grid's, whatever child is under it, so it is taken before the children see it
        public override bool OnInterceptTouchEvent (MotionEvent? ev)
        {
            if ((ev != null) && (ev.ActionMasked == MotionEventActions.Down) && this.TryBeginResizerDrag(ev))
            {
                return true;
            }

            return base.OnInterceptTouchEvent(ev);
        }

        public override bool OnTouchEvent (MotionEvent? e)
        {
            if (e == null)
            {
                return false;
            }

            switch (e.ActionMasked)
            {
                case MotionEventActions.Down:
                    if (m_tracks.IsDragging || this.TryBeginResizerDrag(e))
                    {
                        return true;
                    }

                    break;

                // The finger that took hold of the resizer drags it; another finger coming or going changes nothing
                case MotionEventActions.Move:
                    if (m_tracks.IsDragging)
                    {
                        int pointerIndex = e.FindPointerIndex(m_dragPointerId);
                        if (!this.Enabled || (pointerIndex < 0))
                        {
                            m_tracks.EndDrag();
                        }
                        else if (m_tracks.ContinueDrag(
                            this.ToInnerX(e.GetX(pointerIndex)),
                            e.GetY(pointerIndex) - this.InsetTop
                        ))
                        {
                            this.RequestLayout();
                            this.Invalidate();
                        }

                        return true;
                    }

                    break;

                case MotionEventActions.PointerUp:
                    if (m_tracks.IsDragging && (e.GetPointerId(e.ActionIndex) == m_dragPointerId))
                    {
                        m_tracks.EndDrag();
                    }

                    return m_tracks.IsDragging || base.OnTouchEvent(e);

                case MotionEventActions.Up:
                case MotionEventActions.Cancel:
                    if (m_tracks.IsDragging)
                    {
                        m_tracks.EndDrag();
                        return true;
                    }

                    break;
            }

            return base.OnTouchEvent(e);
        }

        // Under a mouse or stylus a resizer shows the resize pointer, as a desktop splitter's cursor does, so it reads as
        //  something to drag before it is pressed. It answers for the same reach a press takes hold of.
        public override PointerIcon? OnResolvePointerIcon (MotionEvent? e, int pointerIndex)
        {
            if ((e != null)
                && this.Enabled
                && ((m_tracks.ColumnResizerCount + m_tracks.RowResizerCount) > 0)
                && m_tracks.IsOnResizer(
                    this.ToInnerX(e.GetX(pointerIndex)),
                    e.GetY(pointerIndex) - this.InsetTop,
                    MobileLook.AutoGrid.ResizerGrabLength * m_density,
                    out bool isColumnResizer
                ))
            {
                return PointerIcon.GetSystemIcon(this.Context!, isColumnResizer ? PointerIconType.HorizontalDoubleArrow : PointerIconType.VerticalDoubleArrow);
            }

            return base.OnResolvePointerIcon(e, pointerIndex);
        }

        protected override void DispatchDraw (Canvas canvas)
        {
            base.DispatchDraw(canvas);
            this.DrawBorder(canvas);
            if ((m_tracks.ColumnResizerCount + m_tracks.RowResizerCount) == 0)
            {
                return;
            }

            // Over the children, as a GridSplitter added after them draws over them in WPF
            m_resizerPaint.Color = ThemeColors.Resolve(this.Context!, GridColors.Resizer).ToAndroidColor();
            int innerWidth = Math.Max(0, this.Width - this.InsetLeft - this.InsetRight);
            for (int index = 0; index < m_tracks.ColumnResizerCount; ++index)
            {
                this.DrawResizer(canvas, m_tracks.GetResizerBounds(true, index), innerWidth);
            }

            for (int index = 0; index < m_tracks.RowResizerCount; ++index)
            {
                this.DrawResizer(canvas, m_tracks.GetResizerBounds(false, index), innerWidth);
            }
        }

        // ===========[ Helper Methods ]===================================
        // The cells sit inside the border and, within it, the padding, as in WPF's AutoGrid
        private int InsetLeft => this.PaddingLeft + m_borderLeft;
        private int InsetTop => this.PaddingTop + m_borderTop;
        private int InsetRight => this.PaddingRight + m_borderRight;
        private int InsetBottom => this.PaddingBottom + m_borderBottom;

        // Each side is its own band at the grid's edge, so uneven sides draw the way WPF's AutoGrid draws them
        private void DrawBorder (Canvas canvas)
        {
            if ((m_borderLeft + m_borderTop + m_borderRight + m_borderBottom) == 0)
            {
                return;
            }

            m_borderPaint.Color = m_borderBrush ?? ThemeColors.Resolve(this.Context!, GridColors.Border).ToAndroidColor();
            int width = this.Width;
            int height = this.Height;
            int middleBottom = height - m_borderBottom;
            _Band(0, 0, width, m_borderTop);
            _Band(0, middleBottom, width, height);
            _Band(0, m_borderTop, m_borderLeft, middleBottom);
            _Band(width - m_borderRight, m_borderTop, width, middleBottom);

            void _Band (int left, int top, int right, int bottom)
            {
                if ((right > left) && (bottom > top))
                {
                    canvas.DrawRect(left, top, right, bottom, m_borderPaint);
                }
            }
        }

        private void DrawResizer (Canvas canvas, AutoGridTracks.TrackBounds bounds, int innerWidth)
        {
            CellPixels pixels = this.ToLaidOut(ToPixelBounds(bounds), innerWidth);
            if ((pixels.Width > 0) && (pixels.Height > 0))
            {
                canvas.DrawRect(pixels.Left, pixels.Top, pixels.Left + pixels.Width, pixels.Top + pixels.Height, m_resizerPaint);
            }
        }

        private bool TryBeginResizerDrag (MotionEvent e)
        {
            if (!this.Enabled || ((m_tracks.ColumnResizerCount + m_tracks.RowResizerCount) == 0))
            {
                return false;
            }

            double grabLength = MobileLook.AutoGrid.ResizerGrabLength * m_density;
            if (!m_tracks.TryBeginDrag(this.ToInnerX(e.GetX()), e.GetY() - this.InsetTop, grabLength))
            {
                return false;
            }

            m_dragPointerId = e.GetPointerId(e.ActionIndex);

            // A scroll view around the grid would otherwise take the drag over once it moves far enough
            this.Parent?.RequestDisallowInterceptTouchEvent(true);
            return true;
        }

        // Exactly: the tracks share the length. Otherwise each is as long as its longest child, unless an AtMost length is
        //  too short for that, when they share the length after all (the cells overflow no further than the grid can)
        private void ArrangeTracks (bool isColumns, MeasureSpecMode mode, int length, double[]? contentLengths)
        {
            double available = double.PositiveInfinity;
            if (mode == MeasureSpecMode.Exactly)
            {
                available = length;
            }
            else if (mode == MeasureSpecMode.AtMost)
            {
                double[] wanted = isColumns
                    ? m_tracks.MeasureColumns(double.PositiveInfinity, contentLengths)
                    : m_tracks.MeasureRows(double.PositiveInfinity, contentLengths);
                if (wanted.Sum() > length)
                {
                    available = length;
                }
            }

            if (isColumns)
            {
                m_tracks.ArrangeColumns(available, contentLengths);
            }
            else
            {
                m_tracks.ArrangeRows(available, contentLengths);
            }
        }

        // Into the tracks' space: inside the padding and border, and mirrored in a right to left layout
        private double ToInnerX (float x)
        {
            double inner = x - this.InsetLeft;
            return this.IsRightToLeft() ? Math.Max(0, this.Width - this.InsetLeft - this.InsetRight) - inner : inner;
        }

        private void MeasureChildrenToCells ()
        {
            int nothing = MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Exactly);
            for (int index = 0; index < this.ChildCount; ++index)
            {
                View child = this.GetChildAt(index)!;
                if (child.Visibility == ViewStates.Gone)
                {
                    continue;
                }

                if (m_tracks.TryGetCell(index, out int row, out int column))
                {
                    CellPixels cell = this.GetCellPixels(row, column);
                    child.Measure(MeasureSpec.MakeMeasureSpec(cell.Width, MeasureSpecMode.Exactly), MeasureSpec.MakeMeasureSpec(cell.Height, MeasureSpecMode.Exactly));
                }
                else
                {
                    child.Measure(nothing, nothing);
                }
            }
        }

        private CellPixels GetCellPixels (int row, int column) => ToPixelBounds(m_tracks.GetCellBounds(row, column));

        // Each edge is rounded rather than each length, so neighboring cells meet with no gap or overlap
        private static CellPixels ToPixelBounds (AutoGridTracks.TrackBounds bounds)
        {
            int left = (int)Math.Round(bounds.X);
            int top = (int)Math.Round(bounds.Y);
            return new CellPixels(left, top, (int)Math.Round(bounds.X + bounds.Width) - left, (int)Math.Round(bounds.Y + bounds.Height) - top);
        }

        // From the tracks' space to the grid's: past the padding, and mirrored in a right to left layout
        private CellPixels ToLaidOut (CellPixels inner, int innerWidth)
        {
            int left = this.IsRightToLeft() ? innerWidth - inner.Left - inner.Width : inner.Left;
            return new CellPixels(left + this.InsetLeft, inner.Top + this.InsetTop, inner.Width, inner.Height);
        }

        private bool IsRightToLeft () => this.LayoutDirection == Android.Views.LayoutDirection.Rtl;

        // ===========[ Subclasses/structs ]===================================
        private readonly record struct CellPixels (int Left, int Top, int Width, int Height);

        // The resizer and border colors: the AJut theme's for the look key when it is loaded, otherwise the unthemed defaults
        private static class GridColors
        {
            public static readonly ThemedColor Resizer = ThemedColor.FromArgb(MobileLook.AutoGrid.Resizer, 0x40808080, 0x40808080);

            // Unthemed, the platform's divider gray in light and dark
            public static readonly ThemedColor Border = ThemedColor.FromArgb(MobileLook.AutoGrid.Border, 0x1F000000, 0x1FFFFFFF);
        }
    }
}
