// eAutoPopulationOrder moved to AJut.UX, in the same namespace, so every stack's AutoGrid shares it. This forwards
// anything built against it here to where it is now.
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(AJut.UX.Controls.eAutoPopulationOrder))]

namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using APUtils = AJut.UX.APUtils<AutoGrid>;
    using DPUtils = AJut.UX.DPUtils<AutoGrid>;

    // ===========[ AutoGrid ]=======================================================
    // WinUI3 port: pending.
    /// <summary>
    /// A grid which will automatically add rows and/or columns as child elements are inserted (and optionally grid resizers) based off of setup guidelines.
    /// Which cell each child takes, how many rows and columns there are, the share a new one comes in at, and where the
    /// resizers go are AJut.UX's <see cref="AutoGridLayout"/>, shared with every stack's AutoGrid.
    /// </summary>
    public class AutoGrid : Grid
    {
        public static int kUnlimited = AutoGridLayout.kUnlimited;

        private List<GridSplitter> m_generatedColumnSizers = new List<GridSplitter>();
        private List<GridSplitter> m_generatedRowSizers = new List<GridSplitter>();
        private bool m_isRemovingSizers = false;
        private bool m_isWidthUnbounded;
        private bool m_isHeightUnbounded;

        private static DependencyPropertyKey IsInRangePropertyKey = APUtils.RegisterReadOnly(GetIsInRange, SetIsInRange);
        public static DependencyProperty IsInRangeProperty = IsInRangePropertyKey.DependencyProperty;
        public static bool GetIsInRange (DependencyObject obj) => (bool)obj.GetValue(IsInRangeProperty);
        internal static void SetIsInRange (DependencyObject obj, bool value) => obj.SetValue(IsInRangePropertyKey, value);

        /// <summary>Space between the border and the cells, on each side</summary>
        public static readonly DependencyProperty PaddingProperty = DPUtils.Register(_ => _.Padding, new FrameworkPropertyMetadata(new Thickness(), FrameworkPropertyMetadataOptions.AffectsMeasure));
        public Thickness Padding
        {
            get => (Thickness)this.GetValue(PaddingProperty);
            set => this.SetValue(PaddingProperty, value);
        }

        /// <summary>The brush the border is drawn in, around the grid's edge, as <see cref="BorderThickness"/> thick</summary>
        public static readonly DependencyProperty BorderBrushProperty = DPUtils.Register(_ => _.BorderBrush, new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public Brush BorderBrush
        {
            get => (Brush)this.GetValue(BorderBrushProperty);
            set => this.SetValue(BorderBrushProperty, value);
        }

        /// <summary>How thick the border is on each side. The cells sit inside it, and inside the <see cref="Padding"/> within it.</summary>
        public static readonly DependencyProperty BorderThicknessProperty = DPUtils.Register(_ => _.BorderThickness, new FrameworkPropertyMetadata(new Thickness(), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
        public Thickness BorderThickness
        {
            get => (Thickness)this.GetValue(BorderThicknessProperty);
            set => this.SetValue(BorderThicknessProperty, value);
        }

        public static readonly DependencyProperty FixedColumnCountProperty = DPUtils.Register(_ => _.FixedColumnCount, AutoGrid.kUnlimited, (d, e) => d.EvaluateAndSetSetRowColumnInfo());
        public int FixedColumnCount
        {
            get => (int)this.GetValue(FixedColumnCountProperty);
            set => this.SetValue(FixedColumnCountProperty, value);
        }

        public static readonly DependencyProperty FixedRowCountProperty = DPUtils.Register(_ => _.FixedRowCount, AutoGrid.kUnlimited, (d, e) => d.EvaluateAndSetSetRowColumnInfo());
        public int FixedRowCount
        {
            get => (int)this.GetValue(FixedRowCountProperty);
            set => this.SetValue(FixedRowCountProperty, value);
        }

        public static readonly DependencyProperty AutoPopulationOrderProperty = DPUtils.Register(_ => _.AutoPopulationOrder, (d, e) => d.EvaluateAndSetSetRowColumnInfo());
        public eAutoPopulationOrder AutoPopulationOrder
        {
            get => (eAutoPopulationOrder)this.GetValue(AutoPopulationOrderProperty);
            set => this.SetValue(AutoPopulationOrderProperty, value);
        }

        public static readonly DependencyProperty AddColumnResizersProperty = DPUtils.Register(_ => _.AddColumnResizers, (d, e) => d.EvaluateAndSetSetRowColumnInfo());
        public bool AddColumnResizers
        {
            get => (bool)this.GetValue(AddColumnResizersProperty);
            set => this.SetValue(AddColumnResizersProperty, value);
        }

        public static readonly DependencyProperty AddRowResizersProperty = DPUtils.Register(_ => _.AddRowResizers, (d, e) => d.EvaluateAndSetSetRowColumnInfo());
        public bool AddRowResizers
        {
            get => (bool)this.GetValue(AddRowResizersProperty);
            set => this.SetValue(AddRowResizersProperty, value);
        }

        public static readonly DependencyProperty SizerLengthProperty = DPUtils.Register(_ => _.SizerLength, AutoGridLayout.kDefaultSizerLength);
        public double SizerLength
        {
            get => (double)this.GetValue(SizerLengthProperty);
            set => this.SetValue(SizerLengthProperty, value);
        }

        public static readonly DependencyProperty ShrinkRowColumnsToUtilizedElementCountProperty = DPUtils.Register(_ => _.ShrinkRowColumnsToUtilizedElementCount, (d, e) => d.EvaluateAndSetSetRowColumnInfo());
        public bool ShrinkRowColumnsToUtilizedElementCount
        {
            get => (bool)this.GetValue(ShrinkRowColumnsToUtilizedElementCountProperty);
            set => this.SetValue(ShrinkRowColumnsToUtilizedElementCountProperty, value);
        }

        public static readonly DependencyProperty MinElementWidthProperty = DPUtils.Register(_ => _.MinElementWidth, AutoGridLayout.kDefaultMinElementLength);
        public double MinElementWidth
        {
            get => (double)this.GetValue(MinElementWidthProperty);
            set => this.SetValue(MinElementWidthProperty, value);
        }

        public static readonly DependencyProperty MinElementHeightProperty = DPUtils.Register(_ => _.MinElementHeight, AutoGridLayout.kDefaultMinElementLength);
        public double MinElementHeight
        {
            get => (double)this.GetValue(MinElementHeightProperty);
            set => this.SetValue(MinElementHeightProperty, value);
        }

        protected override void OnVisualChildrenChanged (DependencyObject visualAdded, DependencyObject visualRemoved)
        {
            base.OnVisualChildrenChanged(visualAdded, visualRemoved);
            this.EvaluateAndSetSetRowColumnInfo();
        }

        // The cells are measured and arranged inside the border and the padding, which Grid knows nothing of
        protected override Size MeasureOverride (Size constraint)
        {
            m_isWidthUnbounded = double.IsInfinity(constraint.Width);
            m_isHeightUnbounded = double.IsInfinity(constraint.Height);
            Thickness inset = this.GetInset();
            Size desired = base.MeasureOverride(Deflate(constraint, inset));
            return new Size(desired.Width + inset.Left + inset.Right, desired.Height + inset.Top + inset.Bottom);
        }

        protected override Size ArrangeOverride (Size arrangeSize)
        {
            Thickness inset = this.GetInset();
            Size inner = Deflate(arrangeSize, inset);
            base.ArrangeOverride(inner);

            // Grid arranges each child in its cell from its own top left corner, so with a border or padding each child is
            //  arranged again in the same cell, moved in by the inset. The cell comes from the definitions' offsets and
            //  lengths, which Grid has just worked out, so this works for any child, not only a FrameworkElement.
            //
            //  The cost: with a left or top inset every child is arranged twice a pass, so grids nested inside grids that
            //  both have one multiply it. Grid keeps its cell math to itself, so there is no way to hand it the offset.
            //  ShowGridLines draws where Grid put the cells, not moved in by the inset.
            if ((inset.Left != 0.0) || (inset.Top != 0.0))
            {
                foreach (UIElement child in this.InternalChildren)
                {
                    if (child != null)
                    {
                        Rect cell = this.GetCellBounds(child, inner);
                        cell.Offset(inset.Left, inset.Top);
                        child.Arrange(cell);
                    }
                }
            }

            // Measured with no width or height to fill, Grid sizes its star tracks on that axis to their children, so a
            //  resizer there would change the stars and move nothing. Laid out with no size, it draws nothing and takes no
            //  press, as the phone and Mac grids have no resizers on such an axis. It is still there, ready for a layout
            //  that has a length to share.
            if (m_isWidthUnbounded)
            {
                _CollapseEach(m_generatedColumnSizers);
            }

            if (m_isHeightUnbounded)
            {
                _CollapseEach(m_generatedRowSizers);
            }

            return arrangeSize;

            static void _CollapseEach (List<GridSplitter> sizers)
            {
                foreach (GridSplitter sizer in sizers)
                {
                    sizer.Arrange(new Rect(0, 0, 0, 0));
                }
            }
        }

        protected override void OnRender (DrawingContext dc)
        {
            // The background, which Panel draws over the whole grid
            base.OnRender(dc);

            Brush brush = this.BorderBrush;
            Thickness border = this.BorderThickness;
            if ((brush == null) || ((border.Left <= 0.0) && (border.Top <= 0.0) && (border.Right <= 0.0) && (border.Bottom <= 0.0)))
            {
                return;
            }

            // Each side is its own band, so uneven sides draw the way WPF's Border draws them
            Size size = this.RenderSize;
            double middleHeight = Math.Max(0.0, size.Height - border.Top - border.Bottom);
            _DrawBand(0.0, 0.0, size.Width, border.Top);
            _DrawBand(0.0, size.Height - border.Bottom, size.Width, border.Bottom);
            _DrawBand(0.0, border.Top, border.Left, middleHeight);
            _DrawBand(size.Width - border.Right, border.Top, border.Right, middleHeight);

            void _DrawBand (double x, double y, double width, double height)
            {
                if ((width > 0.0) && (height > 0.0))
                {
                    dc.DrawRectangle(brush, null, new Rect(x, y, width, height));
                }
            }
        }

        protected virtual void EvaluateAndSetSetRowColumnInfo ()
        {
            if (m_isRemovingSizers)
            {
                return;
            }

            this.EnsureRowAndColumnDefinitionsAreUnitSized();

            // 1. Each child takes the next cell, and is marked out of range when there is none for it
            int usedRows = 0;
            int usedColumns = 0;
            int evaluationIndex = 0;
            for (int index = 0; index < this.Children.Count; ++index)
            {
                UIElement child = this.Children[index];
                if (child == null || child is GridSplitter)
                {
                    continue;
                }

                if (this.CalculateRowColumnForIndex(evaluationIndex, out int row, out int column))
                {
                    Grid.SetRow(child, row);
                    Grid.SetColumn(child, column);
                    AutoGrid.SetIsInRange(child, true);
                    usedRows = Math.Max(usedRows, row + 1);
                    usedColumns = Math.Max(usedColumns, column + 1);
                }
                else
                {
                    AutoGrid.SetIsInRange(child, false);
                }

                ++evaluationIndex;
            }

            // 2. As many rows and columns as the children use, or the fixed counts when both are fixed and not shrinking.
            //    A track added comes in at the average share, and the last ones go when there are too many.
            AutoGridLayout.TrackCounts counts = AutoGridLayout.ComputeTrackCounts(
                new AutoGridLayout.TrackCounts(usedRows, usedColumns),
                this.FixedRowCount,
                this.FixedColumnCount,
                this.ShrinkRowColumnsToUtilizedElementCount
            );

            this.EnsureRowColumnAvailable(counts.Rows - 1, counts.Columns - 1);
            while (this.RowDefinitions.Count > counts.Rows)
            {
                this.RowDefinitions.RemoveAt(this.RowDefinitions.Count - 1);
            }

            while (this.ColumnDefinitions.Count > counts.Columns)
            {
                this.ColumnDefinitions.RemoveAt(this.ColumnDefinitions.Count - 1);
            }

            // 3. One resizer between each pair of rows or columns there are, never one past the last. They go in and come
            //    out on the dispatcher, since this runs while the children are changing.
            var sizersToAdd = new List<GridSplitter>();
            var sizersToRemove = new List<GridSplitter>();
            this.MatchSizers(
                m_generatedColumnSizers,
                AutoGridLayout.ResizerCount(this.AddColumnResizers, counts.Columns),
                isForColumns: true,
                sizersToAdd,
                sizersToRemove
            );
            this.MatchSizers(
                m_generatedRowSizers,
                AutoGridLayout.ResizerCount(this.AddRowResizers, counts.Rows),
                isForColumns: false,
                sizersToAdd,
                sizersToRemove
            );
            for (int column = 0; column < m_generatedColumnSizers.Count; ++column)
            {
                Grid.SetRow(m_generatedColumnSizers[column], 0);
                Grid.SetColumn(m_generatedColumnSizers[column], column);
                Grid.SetRowSpan(m_generatedColumnSizers[column], Math.Max(1, counts.Rows));
            }

            for (int row = 0; row < m_generatedRowSizers.Count; ++row)
            {
                Grid.SetRow(m_generatedRowSizers[row], row);
                Grid.SetColumn(m_generatedRowSizers[row], 0);
                Grid.SetColumnSpan(m_generatedRowSizers[row], Math.Max(1, counts.Columns));
            }

            if (sizersToAdd.Any() || sizersToRemove.Any())
            {
                this.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        m_isRemovingSizers = true;
                        foreach (var sizer in sizersToRemove)
                        {
                            this.Children.Remove(sizer);
                        }

                        foreach (var sizer in sizersToAdd)
                        {
                            this.Children.Add(sizer);
                        }
                    }
                    finally
                    {
                        m_isRemovingSizers = false;
                    }
                });
            }
        }

        /// <remarks>
        /// Requires <see cref="EnsureRowAndColumnDefinitionsAreUnitSized"/> is called first
        /// </remarks>
        protected virtual void EnsureRowColumnAvailable (int row, int column)
        {
            if (row < 0 || column < 0)
            {
                return;
            }

            double newRowSize = AutoGridLayout.NewTrackShare(this.RowDefinitions.Count);
            double newColumnSize = AutoGridLayout.NewTrackShare(this.ColumnDefinitions.Count);

            bool addedAny = false;
            while (this.RowDefinitions.Count <= row)
            {
                var def = new RowDefinition { Height = new GridLength(newRowSize, GridUnitType.Star) };
                def.SetBinding(RowDefinition.MinHeightProperty, this.CreateBinding(nameof(MinElementHeight)));
                this.RowDefinitions.Add(def);
                addedAny = true;
            }
            while (this.ColumnDefinitions.Count <= column)
            {
                var def = new ColumnDefinition { Width = new GridLength(newColumnSize, GridUnitType.Star) };
                def.SetBinding(ColumnDefinition.MinWidthProperty, this.CreateBinding(nameof(MinElementWidth)));
                this.ColumnDefinitions.Add(def);
                addedAny = true;
            }

            if (addedAny)
            {
                this.EnsureRowAndColumnDefinitionsAreUnitSized();
            }
        }

        /// <summary>
        /// Scales the row heights and the column widths so each set's values sum to 1, each a share of the whole, as stars.
        /// This is how lengths given in pixels (a docking split's sizes, say) become shares. A set that already sums to 1
        /// is left exactly as it is, whatever unit its lengths are in.
        /// </summary>
        public void EnsureRowAndColumnDefinitionsAreUnitSized ()
        {
            double[] rowShares = this.GetRowShares();
            if (AutoGridLayout.Normalize(rowShares))
            {
                this.SetRowShares(rowShares);
            }

            double[] columnShares = this.GetColumnShares();
            if (AutoGridLayout.Normalize(columnShares))
            {
                this.SetColumnShares(columnShares);
            }
        }

        //#error this is still slightly flawed, if this is drag/drop where you're dropping to might be smaller and all of these won't work properly. Better to to store targetSize as percents (though this is auto-grid so maybe support both?)
        /// <summary>
        /// Gives one row and one column the share <paramref name="targetSize"/> is of the grid as it is laid out now (inside
        /// its border and padding), each capped at <see cref="AutoGridLayout.kMaxShare"/>, through
        /// <see cref="SetTargetSizePercentageFor"/>. A size with no area changes nothing, and neither does an axis the grid
        /// has no length on yet.
        /// </summary>
        public void SetTargetFixedSizeFor (int targetRow, int targetColumn, Size targetSize)
        {
            if (targetSize.HasZeroArea())
            {
                return;
            }

            // No matter what you set your target, it will be capped to the max percent. The tracks share what is left
            //  inside the border and the padding.
            Thickness inset = this.GetInset();
            double newRowUnitSize = AutoGridLayout.ShareOfLength(targetSize.Height, this.ActualHeight - inset.Top - inset.Bottom);
            double newColumnUnitSize = AutoGridLayout.ShareOfLength(targetSize.Width, this.ActualWidth - inset.Left - inset.Right);
            this.SetTargetSizePercentageFor(targetRow, targetColumn, newRowUnitSize, newColumnUnitSize);
        }

        /// <summary>
        /// Gives the row <paramref name="targetRow"/> and the column <paramref name="targetColumn"/> a share of the grid, from
        /// 0 to 1, and spreads the difference evenly over the other rows and columns, each kept within
        /// <see cref="AutoGridLayout.kMinShare"/> and <see cref="AutoGridLayout.kMaxShare"/>. -1 for either leaves that axis as
        /// it is. A share of 0 for either is not supported, and changes neither.
        /// </summary>
        public void SetTargetSizePercentageFor (int targetRow, int targetColumn, double newRowSizePercent, double newColumnWidthPercent)
        {
            // If neither are asking to be set... (supply -1 for that)
            if (newRowSizePercent < 0.0 && newColumnWidthPercent < 0.0)
            {
                return;
            }

            // If either have an error of 0.0 (not supported)
            if (newRowSizePercent == 0.0 || newColumnWidthPercent == 0.0)
            {
                return;
            }

            this.EnsureRowAndColumnDefinitionsAreUnitSized();

            // A -1 for one of them leaves that axis as it is
            double[] rowShares = this.GetRowShares();
            AutoGridLayout.SetShare(rowShares, targetRow, newRowSizePercent);
            this.SetRowShares(rowShares);

            double[] columnShares = this.GetColumnShares();
            AutoGridLayout.SetShare(columnShares, targetColumn, newColumnWidthPercent);
            this.SetColumnShares(columnShares);

            /*
             * How it should be...
             * 3 Rows: 200*|800*|400*
             * Total size: 1400
             *
             * New row added, and automatically before we can see...
             * 4 rows (still size 1400)...
             *      First Unit sized:
             *          > 14%  |57%  |29%
             *          > 0.14*|0.57*|0.29*
             *      Second add new element with init size:
             *          auto init size would resolve to 1/count, in this case 0.25*
             *          > 0.14*|0.57*|0.29*|0.25*
             *          Effectively this resolves to:
             *          > 0.11*|0.46*|0.23*|0.20
             *          > 154* |644* |322* |280*
             *      Third, outside of AutoGrid now acts - it says "hey I wanted the new guy to be 140 in size...
             *          Unit size again first...
             *          > 0.11*|0.46*|0.23*|0.20
             *          Then determine target new unit size
             *          > 0.10
             *          Find difference (0.2 - 0.1)
             *          > 0.10
             *          Determine distribution ammount, diff/(count-1)
             *          > 0.033
             *          Set everying
             *          > 0.14*|0.49*|0.26*|0.1*
             *          > 196* |686* |364* |140*
             *
             */
        }

        protected virtual bool CalculateRowColumnForIndex (int index, out int row, out int column)
        {
            return AutoGridLayout.TryGetCell(index, this.FixedRowCount, this.FixedColumnCount, this.AutoPopulationOrder, out row, out column);
        }

        // ===========[ Helper Methods ]========================================
        private Thickness GetInset ()
        {
            Thickness border = this.BorderThickness;
            Thickness padding = this.Padding;
            return new Thickness(border.Left + padding.Left, border.Top + padding.Top, border.Right + padding.Right, border.Bottom + padding.Bottom);
        }

        // Where Grid put a child: the cells it spans, from its row and column clamped into the definitions there are, as
        //  Grid clamps them. With no definitions on an axis, Grid has one track filling it.
        private Rect GetCellBounds (UIElement child, Size inner)
        {
            double x = 0.0;
            double width = inner.Width;
            if (this.ColumnDefinitions.Count > 0)
            {
                int first = Math.Min(Grid.GetColumn(child), this.ColumnDefinitions.Count - 1);
                int last = Math.Min(first + Math.Max(1, Grid.GetColumnSpan(child)) - 1, this.ColumnDefinitions.Count - 1);
                x = this.ColumnDefinitions[first].Offset;
                width = this.ColumnDefinitions[last].Offset + this.ColumnDefinitions[last].ActualWidth - x;
            }

            double y = 0.0;
            double height = inner.Height;
            if (this.RowDefinitions.Count > 0)
            {
                int first = Math.Min(Grid.GetRow(child), this.RowDefinitions.Count - 1);
                int last = Math.Min(first + Math.Max(1, Grid.GetRowSpan(child)) - 1, this.RowDefinitions.Count - 1);
                y = this.RowDefinitions[first].Offset;
                height = this.RowDefinitions[last].Offset + this.RowDefinitions[last].ActualHeight - y;
            }

            return new Rect(x, y, Math.Max(0.0, width), Math.Max(0.0, height));
        }

        // Makes or drops sizers until there are as many as wanted. The ones dropped are always the last, so the ones kept
        //  stay at the edges they were on.
        private void MatchSizers (List<GridSplitter> sizers, int wanted, bool isForColumns, List<GridSplitter> sizersToAdd, List<GridSplitter> sizersToRemove)
        {
            while (sizers.Count > wanted)
            {
                sizersToRemove.Add(sizers[sizers.Count - 1]);
                sizers.RemoveAt(sizers.Count - 1);
            }

            while (sizers.Count < wanted)
            {
                GridSplitter gs = isForColumns
                    ? new GridSplitter
                    {
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        ResizeDirection = GridResizeDirection.Columns,
                    }
                    : new GridSplitter
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        ResizeDirection = GridResizeDirection.Rows,
                    };

                gs.SetBinding(
                    isForColumns ? GridSplitter.WidthProperty : GridSplitter.HeightProperty,
                    this.CreateBinding(nameof(SizerLength), System.Windows.Data.BindingMode.OneWay)
                );
                sizers.Add(gs);
                sizersToAdd.Add(gs);
            }
        }

        private double[] GetRowShares () => this.RowDefinitions.Select(r => r.Height.Value).ToArray();
        private double[] GetColumnShares () => this.ColumnDefinitions.Select(c => c.Width.Value).ToArray();

        private void SetRowShares (double[] shares)
        {
            for (int index = 0; index < shares.Length; ++index)
            {
                this.RowDefinitions[index].SetCurrentValue(RowDefinition.HeightProperty, new GridLength(shares[index], GridUnitType.Star));
            }
        }

        private void SetColumnShares (double[] shares)
        {
            for (int index = 0; index < shares.Length; ++index)
            {
                this.ColumnDefinitions[index].SetCurrentValue(ColumnDefinition.WidthProperty, new GridLength(shares[index], GridUnitType.Star));
            }
        }

        private static Size Deflate (Size size, Thickness inset)
        {
            return new Size(
                Math.Max(0.0, size.Width - inset.Left - inset.Right),
                Math.Max(0.0, size.Height - inset.Top - inset.Bottom)
            );
        }
    }
}
