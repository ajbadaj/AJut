namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.MathUtilities;

    // ===========[ AutoGridLayout ]==============================================
    // AutoGrid's rules, the same on every stack that has one. An AutoGrid puts each child in the next
    // cell of a grid that grows to fit them, and can put a resizer between each pair of rows or columns.
    //
    //  - Cells: children take cells in order, across a row or down a column (eAutoPopulationOrder),
    //    wrapping at a fixed count. A count of kUnlimited never wraps, and any other negative count is
    //    read the same way. With both counts fixed, a child past the last cell has none and is out of
    //    range; so is every child when either count is zero.
    //  - Tracks: with both counts fixed the grid keeps every row and column, used or not, unless it is
    //    shrinking to its children. Otherwise it has as many as its children use. With a count of zero it
    //    has none.
    //  - Shares: each row and column has a share of the grid's length, and the shares sum to 1. A track
    //    added comes in at the average share (1 over how many there were) and then all of them are scaled
    //    to sum to 1 again, so a track somebody made bigger stays bigger than the rest.
    //  - Lengths: with a length to fill, the tracks split it by share, and none goes under the minimum: one
    //    that would takes the minimum, and the rest split what is left. With no length to fill (the axis a
    //    scroll view scrolls), each track is as long as its longest child, the way star tracks behave in a
    //    WPF Grid given unbounded space.
    //  - Resizers: one between each pair of tracks there are, never one past the last, on the far edge of
    //    the earlier track (a column's right edge, a row's bottom), running the grid's whole length the
    //    other way. Dragging one trades length between the two tracks beside it and no others, and never
    //    takes either under the minimum.
    //
    // WPF's AutoGrid takes the cells, the tracks, the shares and the resizer count from here, and leaves
    // lengths and dragging to its Grid and GridSplitters. A toolkit with no Grid takes all of it.
    public static class AutoGridLayout
    {
        /// <summary>The fixed row or column count that means no limit</summary>
        public const int kUnlimited = -1;

        /// <summary>The largest share <see cref="SetShare"/> leaves any track other than the one it sets</summary>
        public const double kMaxShare = 0.975;

        /// <summary>The smallest share <see cref="SetShare"/> leaves any track other than the one it sets</summary>
        public const double kMinShare = 0.015;

        /// <summary>How thick an AutoGrid's resizers are unless set, in device independent units</summary>
        public const double kDefaultSizerLength = 2.0;

        /// <summary>The least an AutoGrid's rows and columns are unless set, in device independent units</summary>
        public const double kDefaultMinElementLength = 10.0;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Is <paramref name="count"/> a fixed row or column count, rather than unlimited (any negative count)</summary>
        public static bool IsFixed (int count) => count >= 0;

        /// <summary>The order cells really fill in: <paramref name="order"/>, with <see cref="eAutoPopulationOrder.Auto"/> resolved by which counts are fixed</summary>
        public static eAutoPopulationOrder ResolveOrder (eAutoPopulationOrder order, int fixedRowCount, int fixedColumnCount)
        {
            if (order != eAutoPopulationOrder.Auto)
            {
                return order;
            }

            return (IsFixed(fixedRowCount) && !IsFixed(fixedColumnCount)) ? eAutoPopulationOrder.RowsFirst : eAutoPopulationOrder.ColumnsFirst;
        }

        /// <summary>
        /// The cell the child at <paramref name="childIndex"/> takes. False, with -1 for both, when it has none: past the
        /// last cell of a grid with both counts fixed, or in a grid with a count of zero.
        /// </summary>
        public static bool TryGetCell (int childIndex, int fixedRowCount, int fixedColumnCount, eAutoPopulationOrder order, out int row, out int column)
        {
            bool rowsFixed = IsFixed(fixedRowCount);
            bool columnsFixed = IsFixed(fixedColumnCount);
            if ((childIndex < 0)
                || (fixedRowCount == 0)
                || (fixedColumnCount == 0)
                || (rowsFixed && columnsFixed && (childIndex >= ((long)fixedRowCount * fixedColumnCount))))
            {
                row = -1;
                column = -1;
                return false;
            }

            if (ResolveOrder(order, fixedRowCount, fixedColumnCount) == eAutoPopulationOrder.ColumnsFirst)
            {
                row = columnsFixed ? childIndex / fixedColumnCount : 0;
                column = columnsFixed ? childIndex % fixedColumnCount : childIndex;
            }
            else
            {
                row = rowsFixed ? childIndex % fixedRowCount : childIndex;
                column = rowsFixed ? childIndex / fixedRowCount : 0;
            }

            return true;
        }

        /// <summary>
        /// How many rows and columns a grid of <paramref name="childCount"/> children has: the fixed counts when both are
        /// fixed and it is not shrinking to its children, otherwise as many as its children's cells use
        /// </summary>
        public static TrackCounts ComputeTrackCounts (int childCount, int fixedRowCount, int fixedColumnCount, eAutoPopulationOrder order, bool shrinkToChildren)
        {
            int rows = 0;
            int columns = 0;
            for (int index = 0; index < childCount; ++index)
            {
                // Once one child is past the last cell, every child after it is too
                if (!TryGetCell(index, fixedRowCount, fixedColumnCount, order, out int row, out int column))
                {
                    break;
                }

                rows = Math.Max(rows, row + 1);
                columns = Math.Max(columns, column + 1);
            }

            return ComputeTrackCounts(new TrackCounts(rows, columns), fixedRowCount, fixedColumnCount, shrinkToChildren);
        }

        /// <summary>
        /// How many rows and columns a grid has, given how many its children's cells use: the fixed counts when both are
        /// fixed and it is not shrinking to its children, otherwise <paramref name="used"/>. For a grid that places its
        /// children some other way than <see cref="TryGetCell"/>.
        /// </summary>
        public static TrackCounts ComputeTrackCounts (TrackCounts used, int fixedRowCount, int fixedColumnCount, bool shrinkToChildren)
        {
            // A count of zero has no cells, so the grid has no tracks either way, and no resizers between them
            if ((fixedRowCount == 0) || (fixedColumnCount == 0))
            {
                return new TrackCounts(0, 0);
            }

            return (!shrinkToChildren && IsFixed(fixedRowCount) && IsFixed(fixedColumnCount))
                ? new TrackCounts(fixedRowCount, fixedColumnCount)
                : used;
        }

        /// <summary>The share a track added to <paramref name="existingCount"/> others comes in at, before all are scaled to sum to 1 again</summary>
        public static double NewTrackShare (int existingCount) => 1.0 / Math.Max(1, existingCount);

        /// <summary>
        /// Grows or shrinks <paramref name="shares"/> to <paramref name="count"/> tracks: each one added comes in at
        /// <see cref="NewTrackShare"/>, the last ones go when there are too many, and then all are scaled to sum to 1
        /// </summary>
        public static void ResizeShares (List<double> shares, int count)
        {
            // The average share is 1 over the count only once the shares sum to 1
            count = Math.Max(0, count);
            Normalize(shares);
            double added = NewTrackShare(shares.Count);
            while (shares.Count < count)
            {
                shares.Add(added);
            }

            if (shares.Count > count)
            {
                shares.RemoveRange(count, shares.Count - count);
            }

            Normalize(shares);
        }

        /// <summary>
        /// Scales <paramref name="shares"/> to sum to 1, unless they already do (within a thousandth) or sum to nothing.
        /// Returns whether it scaled them.
        /// </summary>
        public static bool Normalize (IList<double> shares)
        {
            const double kUnitShareTolerance = 0.001;

            double sum = 0.0;
            for (int index = 0; index < shares.Count; ++index)
            {
                sum += shares[index];
            }

            if ((sum == 0.0) || sum.IsApproximatelyEqualTo(1.0, kUnitShareTolerance))
            {
                return false;
            }

            for (int index = 0; index < shares.Count; ++index)
            {
                shares[index] /= sum;
            }

            return true;
        }

        /// <summary>
        /// Gives the track at <paramref name="index"/> the share <paramref name="share"/>, and spreads the difference evenly
        /// over the others, each kept within <see cref="kMinShare"/> and <see cref="kMaxShare"/>. A share of zero or less
        /// asks for no change (-1 is the usual way to say so), and so does an axis with one track or none.
        /// </summary>
        public static void SetShare (IList<double> shares, int index, double share)
        {
            if (!(share > 0.0) || (shares.Count < 2) || (index < 0) || (index >= shares.Count))
            {
                return;
            }

            Normalize(shares);
            double spread = (shares[index] - share) / (shares.Count - 1);
            for (int other = 0; other < shares.Count; ++other)
            {
                shares[other] = (other == index) ? share : Cap.Within(kMinShare, kMaxShare, shares[other] + spread);
            }
        }

        /// <summary>
        /// The share a track <paramref name="length"/> long has of <paramref name="totalLength"/>, at most
        /// <see cref="kMaxShare"/>: what a fixed size asks <see cref="SetShare"/> for. -1 (no change) when either is not
        /// more than zero.
        /// </summary>
        public static double ShareOfLength (double length, double totalLength)
        {
            if (!(length > 0.0) || !(totalLength > 0.0))
            {
                return -1.0;
            }

            return Math.Min(length, kMaxShare * totalLength) / totalLength;
        }

        /// <summary>
        /// How long each track is. With a length <paramref name="available"/> to fill, the tracks split it by share and none
        /// goes under <paramref name="minLength"/>. With none (infinite, or not a number), each is as long as its entry in
        /// <paramref name="contentLengths"/> (its longest child), and never under the minimum either.
        /// </summary>
        public static double[] ComputeTrackLengths (IReadOnlyList<double> shares, double available, double minLength, IReadOnlyList<double>? contentLengths = null)
        {
            int count = shares.Count;
            var lengths = new double[count];
            minLength = (minLength > 0.0) ? minLength : 0.0;
            if (double.IsNaN(available) || double.IsInfinity(available))
            {
                for (int index = 0; index < count; ++index)
                {
                    double content = ((contentLengths != null) && (index < contentLengths.Count)) ? contentLengths[index] : 0.0;
                    lengths[index] = Math.Max(minLength, content);
                }

                return lengths;
            }

            // 1. Any track whose share of what is left would be under the minimum takes the minimum, which leaves the others
            //    less, so look again until none does
            var isAtMinimum = new bool[count];
            double remaining = Math.Max(0.0, available);
            double poolShare = 0.0;
            for (int index = 0; index < count; ++index)
            {
                poolShare += Math.Max(0.0, shares[index]);
            }

            int poolCount = count;
            bool tookAny = true;
            while (tookAny && (poolCount > 0))
            {
                tookAny = false;
                for (int index = 0; index < count; ++index)
                {
                    if (!isAtMinimum[index] && (_ShareOfRemaining(index) < minLength))
                    {
                        isAtMinimum[index] = true;
                        lengths[index] = minLength;
                        remaining -= minLength;
                        poolShare -= Math.Max(0.0, shares[index]);
                        --poolCount;
                        tookAny = true;
                    }
                }
            }

            // 2. The rest split what is left by share
            for (int index = 0; index < count; ++index)
            {
                if (!isAtMinimum[index])
                {
                    lengths[index] = _ShareOfRemaining(index);
                }
            }

            return lengths;

            double _ShareOfRemaining (int index)
            {
                if ((remaining <= 0.0) || (poolCount == 0))
                {
                    return 0.0;
                }

                return (poolShare > 0.0) ? remaining * Math.Max(0.0, shares[index]) / poolShare : remaining / poolCount;
            }
        }

        /// <summary>How many resizers an axis of <paramref name="trackCount"/> tracks has: one between each pair, when it has resizers at all</summary>
        public static int ResizerCount (bool hasResizers, int trackCount) => (hasResizers && (trackCount > 1)) ? trackCount - 1 : 0;

        /// <summary>
        /// Where the resizer after track <paramref name="resizerIndex"/> starts along its axis: on that track's far edge,
        /// <paramref name="sizerLength"/> thick, or the track's own length when that is less
        /// </summary>
        public static double ResizerStart (IReadOnlyList<double> lengths, int resizerIndex, double sizerLength)
        {
            double trackEnd = 0.0;
            for (int index = 0; index <= resizerIndex; ++index)
            {
                trackEnd += lengths[index];
            }

            return trackEnd - Math.Min(Math.Max(0.0, sizerLength), lengths[resizerIndex]);
        }

        /// <summary>
        /// Which of the first <paramref name="resizerCount"/> resizers a press at <paramref name="position"/> along the axis
        /// takes hold of: the nearest whose middle is within half of <paramref name="grabLength"/> (or half its own
        /// thickness, when that is more). A touch screen grabs with a band wider than the line it draws. -1 when none is
        /// in reach.
        /// </summary>
        public static int FindResizerAt (IReadOnlyList<double> lengths, int resizerCount, double position, double sizerLength, double grabLength)
        {
            int found = -1;
            double nearest = double.MaxValue;
            double trackEnd = 0.0;
            for (int index = 0; (index < resizerCount) && (index < lengths.Count); ++index)
            {
                trackEnd += lengths[index];
                double thickness = Math.Min(Math.Max(0.0, sizerLength), lengths[index]);
                double middle = trackEnd - (thickness / 2.0);
                double reach = Math.Max(thickness, grabLength) / 2.0;
                double distance = Math.Abs(position - middle);
                if ((distance <= reach) && (distance < nearest))
                {
                    found = index;
                    nearest = distance;
                }
            }

            return found;
        }

        /// <summary>
        /// The shares after the resizer after track <paramref name="resizerIndex"/> is dragged <paramref name="delta"/>
        /// along its axis from where the drag started, given the shares and lengths at the start. The two tracks beside
        /// it trade length and their shares trade to match, neither going under <paramref name="minLength"/>, and every
        /// other share stays as it was. Measuring from the start, rather than move by move, keeps a drag that runs into
        /// the minimum from creeping. The start shares come back unchanged when the two tracks have no room to trade.
        /// </summary>
        public static double[] ComputeDraggedShares (IReadOnlyList<double> startShares, IReadOnlyList<double> startLengths, int resizerIndex, double delta, double minLength)
        {
            double[] shares = startShares.ToArray();
            int before = resizerIndex;
            int after = resizerIndex + 1;
            if ((before < 0) || (after >= shares.Length) || (after >= startLengths.Count) || double.IsNaN(delta))
            {
                return shares;
            }

            minLength = (minLength > 0.0) ? minLength : 0.0;
            double combined = startLengths[before] + startLengths[after];
            if (!(combined > 0.0) || (combined < (2.0 * minLength)))
            {
                return shares;
            }

            double beforeLength = Cap.Within(minLength, combined - minLength, startLengths[before] + delta);
            double combinedShare = shares[before] + shares[after];
            shares[before] = combinedShare * beforeLength / combined;
            shares[after] = combinedShare - shares[before];
            return shares;
        }

        // ===========[ Subclasses/structs ]====================================
        /// <summary>How many rows and columns a grid has</summary>
        public readonly record struct TrackCounts (int Rows, int Columns);
    }
}
