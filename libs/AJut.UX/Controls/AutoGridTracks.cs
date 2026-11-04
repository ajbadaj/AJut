namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // ===========[ AutoGridTracks ]==============================================
    // One AutoGrid's rows and columns, for a toolkit with no Grid of its own to keep them: how many
    // there are, their shares, their lengths at the last layout, where the resizers are, and a resizer
    // drag in progress. The rules are AutoGridLayout's. The toolkit measures its children, sets their
    // frames from the cells here, draws the resizers where this says, and hands presses and drags in.
    //
    // Everything here is in the grid's inner space (inside its padding), left to right and top down. A
    // toolkit in a right to left layout mirrors x on the way in and on the way out, so column 0 sits on
    // the right, as it does in a mirrored WPF Grid.
    //
    // An axis laid out by its content (no length to fill) has no resizers: its tracks are as long as
    // their children whatever their shares, so dragging one would move nothing.
    public sealed class AutoGridTracks
    {
        private readonly List<double> m_rowShares = new List<double>();
        private readonly List<double> m_columnShares = new List<double>();
        private double[] m_rowLengths = Array.Empty<double>();
        private double[] m_columnLengths = Array.Empty<double>();
        private bool m_areRowsByContent;
        private bool m_areColumnsByContent;
        private ResizerDrag? m_drag;

        // ===========[ Properties ]============================================
        /// <summary>How many columns a row holds before the next row starts, or <see cref="AutoGridLayout.kUnlimited"/></summary>
        public int FixedColumnCount { get; set; } = AutoGridLayout.kUnlimited;

        /// <summary>How many rows a column holds before the next column starts, or <see cref="AutoGridLayout.kUnlimited"/></summary>
        public int FixedRowCount { get; set; } = AutoGridLayout.kUnlimited;

        /// <summary>Whether children fill a row before the next (the default) or a column before the next</summary>
        public eAutoPopulationOrder AutoPopulationOrder { get; set; } = eAutoPopulationOrder.ColumnsFirst;

        /// <summary>With both counts fixed: whether the grid keeps only the rows and columns its children use, rather than all of them</summary>
        public bool ShrinkRowColumnsToUtilizedElementCount { get; set; }

        /// <summary>Whether a resizer sits between each pair of columns</summary>
        public bool AddColumnResizers { get; set; }

        /// <summary>Whether a resizer sits between each pair of rows</summary>
        public bool AddRowResizers { get; set; }

        /// <summary>How thick a resizer is, and how much of its track's far edge it covers</summary>
        public double SizerLength { get; set; } = AutoGridLayout.kDefaultSizerLength;

        /// <summary>The narrowest a column is laid out or dragged to</summary>
        public double MinElementWidth { get; set; } = AutoGridLayout.kDefaultMinElementLength;

        /// <summary>The shortest a row is laid out or dragged to</summary>
        public double MinElementHeight { get; set; } = AutoGridLayout.kDefaultMinElementLength;

        /// <summary>Each row's share of the grid's height; they sum to 1. A drag or a target share changes them at once.</summary>
        public IReadOnlyList<double> RowShares => m_rowShares;

        /// <summary>Each column's share of the grid's width; they sum to 1. A drag or a target share changes them at once.</summary>
        public IReadOnlyList<double> ColumnShares => m_columnShares;

        /// <summary>Each row's height at the last <see cref="ArrangeRows"/></summary>
        public IReadOnlyList<double> RowLengths => m_rowLengths;

        /// <summary>Each column's width at the last <see cref="ArrangeColumns"/></summary>
        public IReadOnlyList<double> ColumnLengths => m_columnLengths;

        /// <summary>How many rows there are, as of the last <see cref="UpdateTrackCounts"/></summary>
        public int RowCount => m_rowShares.Count;

        /// <summary>How many columns there are, as of the last <see cref="UpdateTrackCounts"/></summary>
        public int ColumnCount => m_columnShares.Count;

        /// <summary>
        /// How many column resizers there are to draw and drag: one between each pair of columns arranged at the last
        /// <see cref="ArrangeColumns"/>, and none when that arranged them by their content
        /// </summary>
        public int ColumnResizerCount => GetResizerCount(this.AddColumnResizers, this.ColumnCount, m_columnLengths, m_areColumnsByContent);

        /// <summary>How many row resizers there are to draw and drag, the same way as <see cref="ColumnResizerCount"/></summary>
        public int RowResizerCount => GetResizerCount(this.AddRowResizers, this.RowCount, m_rowLengths, m_areRowsByContent);

        /// <summary>Whether a resizer is being dragged</summary>
        public bool IsDragging => m_drag != null;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>The cell the child at <paramref name="childIndex"/> takes, false when it has none (it is out of range)</summary>
        public bool TryGetCell (int childIndex, out int row, out int column)
        {
            return AutoGridLayout.TryGetCell(childIndex, this.FixedRowCount, this.FixedColumnCount, this.AutoPopulationOrder, out row, out column);
        }

        /// <summary>
        /// Sets how many rows and columns there are for <paramref name="childCount"/> children, a track added coming in at
        /// the average share. Returns whether either count changed. A drag under way ends when one does.
        /// </summary>
        public bool UpdateTrackCounts (int childCount)
        {
            AutoGridLayout.TrackCounts counts = AutoGridLayout.ComputeTrackCounts(
                childCount,
                this.FixedRowCount,
                this.FixedColumnCount,
                this.AutoPopulationOrder,
                this.ShrinkRowColumnsToUtilizedElementCount
            );

            bool changed = (counts.Rows != this.RowCount) || (counts.Columns != this.ColumnCount);
            AutoGridLayout.ResizeShares(m_rowShares, counts.Rows);
            AutoGridLayout.ResizeShares(m_columnShares, counts.Columns);
            if (changed)
            {
                m_drag = null;
            }

            return changed;
        }

        /// <summary>
        /// Works out each column's width: their shares of <paramref name="width"/>, or with no width to fill (infinite or
        /// not a number) each column's widest child from <paramref name="contentWidths"/>, and never under
        /// <see cref="MinElementWidth"/>
        /// </summary>
        public IReadOnlyList<double> ArrangeColumns (double width, IReadOnlyList<double>? contentWidths = null)
        {
            m_columnLengths = this.MeasureColumns(width, contentWidths);
            m_areColumnsByContent = !IsLength(width);
            return m_columnLengths;
        }

        /// <summary>The rows' heights, the same way as <see cref="ArrangeColumns"/></summary>
        public IReadOnlyList<double> ArrangeRows (double height, IReadOnlyList<double>? contentHeights = null)
        {
            m_rowLengths = this.MeasureRows(height, contentHeights);
            m_areRowsByContent = !IsLength(height);
            return m_rowLengths;
        }

        /// <summary>
        /// The columns' widths as <see cref="ArrangeColumns"/> works them out, without keeping them: for asking what size the
        /// grid wants without moving the cells it has
        /// </summary>
        public double[] MeasureColumns (double width, IReadOnlyList<double>? contentWidths = null)
        {
            return AutoGridLayout.ComputeTrackLengths(m_columnShares, width, this.MinElementWidth, contentWidths);
        }

        /// <summary>The rows' heights as <see cref="ArrangeRows"/> works them out, without keeping them</summary>
        public double[] MeasureRows (double height, IReadOnlyList<double>? contentHeights = null)
        {
            return AutoGridLayout.ComputeTrackLengths(m_rowShares, height, this.MinElementHeight, contentHeights);
        }

        /// <summary>Where the cell at <paramref name="row"/> and <paramref name="column"/> is, from the last arranged lengths</summary>
        public TrackBounds GetCellBounds (int row, int column)
        {
            return new TrackBounds(
                StartOf(m_columnLengths, column),
                StartOf(m_rowLengths, row),
                LengthOf(m_columnLengths, column),
                LengthOf(m_rowLengths, row)
            );
        }

        /// <summary>
        /// Where a resizer is, from the last arranged lengths: a column resizer on its column's right edge running the
        /// grid's full height, or a row resizer on its row's bottom edge running its full width. Empty for one past the
        /// resizers there are.
        /// </summary>
        public TrackBounds GetResizerBounds (bool isColumnResizer, int index)
        {
            if (isColumnResizer)
            {
                if ((index < 0) || (index >= this.ColumnResizerCount))
                {
                    return default;
                }

                double x = AutoGridLayout.ResizerStart(m_columnLengths, index, this.SizerLength);
                double thickness = LengthOf(m_columnLengths, index) - (x - StartOf(m_columnLengths, index));
                return new TrackBounds(x, 0.0, thickness, m_rowLengths.Sum());
            }

            if ((index < 0) || (index >= this.RowResizerCount))
            {
                return default;
            }

            double y = AutoGridLayout.ResizerStart(m_rowLengths, index, this.SizerLength);
            double rowThickness = LengthOf(m_rowLengths, index) - (y - StartOf(m_rowLengths, index));
            return new TrackBounds(0.0, y, m_columnLengths.Sum(), rowThickness);
        }

        /// <summary>
        /// Starts dragging the resizer a press at (<paramref name="x"/>, <paramref name="y"/>) takes hold of, within half of
        /// <paramref name="grabLength"/> of its middle, column resizers first where the two cross. Returns false, and
        /// starts nothing, when the press is on no resizer.
        /// </summary>
        public bool TryBeginDrag (double x, double y, double grabLength)
        {
            m_drag = null;
            int index = this.FindResizer(x, y, grabLength, out bool isColumnResizer);
            if (index < 0)
            {
                return false;
            }

            m_drag = isColumnResizer
                ? new ResizerDrag(true, index, x, m_columnShares.ToArray(), m_columnLengths.ToArray())
                : new ResizerDrag(false, index, y, m_rowShares.ToArray(), m_rowLengths.ToArray());
            return true;
        }

        /// <summary>
        /// Whether a point at (<paramref name="x"/>, <paramref name="y"/>) is on a resizer, by the same rule a press goes
        /// by in <see cref="TryBeginDrag"/>, and which kind: a toolkit shows its resize pointer there under a mouse or
        /// stylus, so a resizer reads as something to drag before it is pressed. Starts nothing.
        /// </summary>
        public bool IsOnResizer (double x, double y, double grabLength, out bool isColumnResizer)
        {
            return this.FindResizer(x, y, grabLength, out isColumnResizer) >= 0;
        }

        /// <summary>
        /// Moves the drag under way to (<paramref name="x"/>, <paramref name="y"/>), trading length between the two tracks
        /// beside its resizer. Returns whether any share changed, so the toolkit knows to lay out again.
        /// </summary>
        public bool ContinueDrag (double x, double y)
        {
            if (m_drag == null)
            {
                return false;
            }

            List<double> shares = m_drag.IsColumns ? m_columnShares : m_rowShares;
            double delta = (m_drag.IsColumns ? x : y) - m_drag.StartPosition;
            double[] dragged = AutoGridLayout.ComputeDraggedShares(
                m_drag.StartShares,
                m_drag.StartLengths,
                m_drag.Index,
                delta,
                m_drag.IsColumns ? this.MinElementWidth : this.MinElementHeight
            );

            bool changed = false;
            for (int index = 0; (index < dragged.Length) && (index < shares.Count); ++index)
            {
                if (shares[index] != dragged[index])
                {
                    shares[index] = dragged[index];
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>Ends the drag under way, if there is one, leaving the shares where it put them</summary>
        public void EndDrag ()
        {
            m_drag = null;
        }

        /// <summary>
        /// Gives one row and one column a share of the grid, as WPF's AutoGrid.SetTargetSizePercentageFor does: the
        /// difference is spread over the others. -1 for either leaves that axis as it is, and a share of 0 changes nothing.
        /// </summary>
        public void SetTargetSizePercentageFor (int targetRow, int targetColumn, double newRowSizePercent, double newColumnWidthPercent)
        {
            if (((newRowSizePercent < 0.0) && (newColumnWidthPercent < 0.0))
                || (newRowSizePercent == 0.0)
                || (newColumnWidthPercent == 0.0))
            {
                return;
            }

            AutoGridLayout.SetShare(m_rowShares, targetRow, newRowSizePercent);
            AutoGridLayout.SetShare(m_columnShares, targetColumn, newColumnWidthPercent);
        }

        /// <summary>
        /// Gives one row and one column the share a length is of the grid at its last layout, as WPF's
        /// AutoGrid.SetTargetFixedSizeFor does, capped at <see cref="AutoGridLayout.kMaxShare"/>
        /// </summary>
        public void SetTargetFixedSizeFor (int targetRow, int targetColumn, double width, double height)
        {
            if ((width == 0.0) || (height == 0.0))
            {
                return;
            }

            double rowShare = AutoGridLayout.ShareOfLength(height, m_rowLengths.Sum());
            double columnShare = AutoGridLayout.ShareOfLength(width, m_columnLengths.Sum());
            this.SetTargetSizePercentageFor(targetRow, targetColumn, rowShare, columnShare);
        }

        // ===========[ Helper Methods ]========================================
        private static bool IsLength (double length) => !double.IsNaN(length) && !double.IsInfinity(length);

        // One between each pair of the tracks there are and were last arranged, so a resizer is never asked for past the
        //  lengths it is placed from, and none on an axis arranged by its content
        private static int GetResizerCount (bool hasResizers, int trackCount, double[] lengths, bool isByContent)
        {
            if (isByContent)
            {
                return 0;
            }

            return Math.Min(AutoGridLayout.ResizerCount(hasResizers, trackCount), Math.Max(0, lengths.Length - 1));
        }

        private static double StartOf (double[] lengths, int index)
        {
            double start = 0.0;
            for (int earlier = 0; (earlier < index) && (earlier < lengths.Length); ++earlier)
            {
                start += lengths[earlier];
            }

            return start;
        }

        private static double LengthOf (double[] lengths, int index) => ((index >= 0) && (index < lengths.Length)) ? lengths[index] : 0.0;

        // The resizer a point is within half of grabLength of the middle of, column resizers first where the two cross, or
        //  -1 for none. A column resizer runs the grid's height and a row resizer its width, so a point past either end of
        //  one is not on it.
        private int FindResizer (double x, double y, double grabLength, out bool isColumnResizer)
        {
            isColumnResizer = false;
            if ((y >= 0.0) && (y <= m_rowLengths.Sum()))
            {
                int column = AutoGridLayout.FindResizerAt(m_columnLengths, this.ColumnResizerCount, x, this.SizerLength, grabLength);
                if (column >= 0)
                {
                    isColumnResizer = true;
                    return column;
                }
            }

            if ((x >= 0.0) && (x <= m_columnLengths.Sum()))
            {
                return AutoGridLayout.FindResizerAt(m_rowLengths, this.RowResizerCount, y, this.SizerLength, grabLength);
            }

            return -1;
        }

        // ===========[ Subclasses/structs ]====================================
        /// <summary>A rectangle in the grid's inner space, left to right and top down</summary>
        public readonly record struct TrackBounds (double X, double Y, double Width, double Height);

        // The drag under way: which resizer, where the press started along its axis, and the shares and lengths then
        private sealed record ResizerDrag (bool IsColumns, int Index, double StartPosition, double[] StartShares, double[] StartLengths);
    }
}
