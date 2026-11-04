namespace AJut.UX.Tests
{
    using System.Collections.Generic;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>The AutoGrid rules every stack shares: <see cref="AutoGridLayout"/></summary>
    [TestClass]
    public class AutoGridLayoutTests
    {
        private const int kUnlimited = AutoGridLayout.kUnlimited;
        private const double kTolerance = 0.0001;

        [TestMethod]
        public void TryGetCell_ColumnsFirstWithFixedColumns_FillsEachRowThenWraps ()
        {
            AssertCell(0, kUnlimited, 3, eAutoPopulationOrder.ColumnsFirst, 0, 0);
            AssertCell(2, kUnlimited, 3, eAutoPopulationOrder.ColumnsFirst, 0, 2);
            AssertCell(3, kUnlimited, 3, eAutoPopulationOrder.ColumnsFirst, 1, 0);
            AssertCell(7, kUnlimited, 3, eAutoPopulationOrder.ColumnsFirst, 2, 1);
        }

        [TestMethod]
        public void TryGetCell_RowsFirstWithFixedRows_FillsEachColumnThenWraps ()
        {
            AssertCell(1, 2, kUnlimited, eAutoPopulationOrder.RowsFirst, 1, 0);
            AssertCell(2, 2, kUnlimited, eAutoPopulationOrder.RowsFirst, 0, 1);
            AssertCell(5, 2, kUnlimited, eAutoPopulationOrder.RowsFirst, 1, 2);
        }

        [TestMethod]
        public void TryGetCell_ColumnsFirstWithNoColumnCount_StaysOnTheFirstRow ()
        {
            // The explicit order wins over the fixed row count: with nothing to wrap at, every child goes along row 0
            AssertCell(4, 2, kUnlimited, eAutoPopulationOrder.ColumnsFirst, 0, 4);
        }

        [TestMethod]
        public void TryGetCell_Auto_WrapsAtWhicheverCountIsFixed ()
        {
            AssertCell(1, 2, kUnlimited, eAutoPopulationOrder.Auto, 1, 0);
            AssertCell(1, kUnlimited, 2, eAutoPopulationOrder.Auto, 0, 1);
            AssertCell(3, 2, 2, eAutoPopulationOrder.Auto, 1, 1);
            AssertCell(3, kUnlimited, kUnlimited, eAutoPopulationOrder.Auto, 0, 3);
        }

        [TestMethod]
        public void TryGetCell_PastTheLastCellOfAFixedGrid_HasNoCell ()
        {
            Assert.IsTrue(AutoGridLayout.TryGetCell(3, 2, 2, eAutoPopulationOrder.ColumnsFirst, out _, out _));
            Assert.IsFalse(AutoGridLayout.TryGetCell(4, 2, 2, eAutoPopulationOrder.ColumnsFirst, out int row, out int column));
            Assert.AreEqual(-1, row);
            Assert.AreEqual(-1, column);
        }

        [TestMethod]
        public void TryGetCell_ACountOfZero_HasNoCellForAnyChild ()
        {
            Assert.IsFalse(AutoGridLayout.TryGetCell(0, 0, kUnlimited, eAutoPopulationOrder.ColumnsFirst, out _, out _));
            Assert.IsFalse(AutoGridLayout.TryGetCell(0, kUnlimited, 0, eAutoPopulationOrder.RowsFirst, out _, out _));
        }

        [TestMethod]
        public void TryGetCell_ANegativeCountOtherThanUnlimited_IsReadAsUnlimited ()
        {
            AssertCell(5, kUnlimited, -4, eAutoPopulationOrder.ColumnsFirst, 0, 5);
            AssertCell(5, -2, kUnlimited, eAutoPopulationOrder.RowsFirst, 5, 0);
        }

        [TestMethod]
        public void ComputeTrackCounts_BothFixedAndNotShrinking_KeepsEveryTrack ()
        {
            Assert.AreEqual(new AutoGridLayout.TrackCounts(3, 2), AutoGridLayout.ComputeTrackCounts(1, 3, 2, eAutoPopulationOrder.ColumnsFirst, false));
            Assert.AreEqual(new AutoGridLayout.TrackCounts(3, 2), AutoGridLayout.ComputeTrackCounts(0, 3, 2, eAutoPopulationOrder.ColumnsFirst, false));
        }

        [TestMethod]
        public void ComputeTrackCounts_ShrinkingOrOneCountUnlimited_HasWhatTheChildrenUse ()
        {
            Assert.AreEqual(new AutoGridLayout.TrackCounts(1, 2), AutoGridLayout.ComputeTrackCounts(2, 3, 2, eAutoPopulationOrder.ColumnsFirst, true));
            Assert.AreEqual(new AutoGridLayout.TrackCounts(1, 2), AutoGridLayout.ComputeTrackCounts(2, kUnlimited, 3, eAutoPopulationOrder.ColumnsFirst, false));
            Assert.AreEqual(new AutoGridLayout.TrackCounts(2, 3), AutoGridLayout.ComputeTrackCounts(5, kUnlimited, 3, eAutoPopulationOrder.ColumnsFirst, false));
            Assert.AreEqual(new AutoGridLayout.TrackCounts(0, 0), AutoGridLayout.ComputeTrackCounts(0, kUnlimited, 3, eAutoPopulationOrder.ColumnsFirst, false));
        }

        [TestMethod]
        public void ComputeTrackCounts_ACountOfZero_HasNoTracks ()
        {
            Assert.AreEqual(new AutoGridLayout.TrackCounts(0, 0), AutoGridLayout.ComputeTrackCounts(3, 3, 0, eAutoPopulationOrder.ColumnsFirst, false));
            Assert.AreEqual(new AutoGridLayout.TrackCounts(0, 0), AutoGridLayout.ComputeTrackCounts(3, 0, kUnlimited, eAutoPopulationOrder.RowsFirst, false));
            Assert.AreEqual(0, AutoGridLayout.ResizerCount(true, 0), "No tracks, so no resizers between them");
        }

        [TestMethod]
        public void ComputeTrackCounts_MoreChildrenThanCells_CountsOnlyTheCells ()
        {
            Assert.AreEqual(new AutoGridLayout.TrackCounts(2, 2), AutoGridLayout.ComputeTrackCounts(9, 2, 2, eAutoPopulationOrder.ColumnsFirst, true));
        }

        [TestMethod]
        public void ResizeShares_AddedTrack_ComesInAtTheAverageThenAllAreRescaled ()
        {
            var shares = new List<double> { 0.7, 0.3 };
            AutoGridLayout.ResizeShares(shares, 3);
            AssertShares(shares, 0.7 / 1.5, 0.3 / 1.5, 0.5 / 1.5);
        }

        [TestMethod]
        public void ResizeShares_FromNone_SharesEvenly ()
        {
            var shares = new List<double>();
            AutoGridLayout.ResizeShares(shares, 4);
            AssertShares(shares, 0.25, 0.25, 0.25, 0.25);
        }

        [TestMethod]
        public void ResizeShares_SharesNotSummingToOne_AreScaledBeforeATrackIsAdded ()
        {
            // Lengths given in pixels, say: the new track still comes in at the average
            var shares = new List<double> { 200.0, 200.0 };
            AutoGridLayout.ResizeShares(shares, 3);
            AssertShares(shares, 1.0 / 3.0, 1.0 / 3.0, 1.0 / 3.0);
        }

        [TestMethod]
        public void ResizeShares_FewerTracks_DropsTheLastAndRescales ()
        {
            var shares = new List<double> { 0.2, 0.2, 0.6 };
            AutoGridLayout.ResizeShares(shares, 2);
            AssertShares(shares, 0.5, 0.5);
        }

        [TestMethod]
        public void SetShare_SpreadsTheDifferenceEvenlyOverTheOthers ()
        {
            var shares = new List<double> { 0.5, 0.25, 0.25 };
            AutoGridLayout.SetShare(shares, 0, 0.3);
            AssertShares(shares, 0.3, 0.35, 0.35);
        }

        [TestMethod]
        public void SetShare_KeepsTheOthersWithinTheLimits ()
        {
            var shares = new List<double> { 1.0 / 3.0, 1.0 / 3.0, 1.0 / 3.0 };
            AutoGridLayout.SetShare(shares, 0, 0.99);
            AssertShares(shares, 0.99, AutoGridLayout.kMinShare, AutoGridLayout.kMinShare);
        }

        [TestMethod]
        public void SetShare_NoShareOrOneTrack_ChangesNothing ()
        {
            var shares = new List<double> { 0.5, 0.5 };
            AutoGridLayout.SetShare(shares, 0, -1.0);
            AssertShares(shares, 0.5, 0.5);
            AutoGridLayout.SetShare(shares, 0, double.NaN);
            AssertShares(shares, 0.5, 0.5);

            var single = new List<double> { 1.0 };
            AutoGridLayout.SetShare(single, 0, 0.4);
            AssertShares(single, 1.0);
        }

        [TestMethod]
        public void ShareOfLength_IsTheFractionCappedAtTheMaximum ()
        {
            Assert.AreEqual(0.25, AutoGridLayout.ShareOfLength(50, 200), kTolerance);
            Assert.AreEqual(AutoGridLayout.kMaxShare, AutoGridLayout.ShareOfLength(300, 200), kTolerance);
            Assert.AreEqual(-1.0, AutoGridLayout.ShareOfLength(50, 0), "A grid with no length gives no share to ask for");
        }

        [TestMethod]
        public void ComputeTrackLengths_SplitsTheLengthByShare ()
        {
            AssertShares(AutoGridLayout.ComputeTrackLengths(new[] { 0.25, 0.75 }, 200, 10), 50, 150);
        }

        [TestMethod]
        public void ComputeTrackLengths_TrackUnderTheMinimum_TakesItAndTheRestSplitWhatIsLeft ()
        {
            AssertShares(AutoGridLayout.ComputeTrackLengths(new[] { 0.05, 0.475, 0.475 }, 100, 10), 10, 45, 45);
        }

        [TestMethod]
        public void ComputeTrackLengths_MinimumsPastTheLength_EachTakesTheMinimum ()
        {
            AssertShares(AutoGridLayout.ComputeTrackLengths(new[] { 0.2, 0.3, 0.5 }, 20, 10), 10, 10, 10);
        }

        [TestMethod]
        public void ComputeTrackLengths_NoLengthToFill_EachIsItsLongestChildAndAtLeastTheMinimum ()
        {
            AssertShares(AutoGridLayout.ComputeTrackLengths(new[] { 0.5, 0.5 }, double.PositiveInfinity, 10, new[] { 30.0, 4.0 }), 30, 10);
        }

        [TestMethod]
        public void ResizerCount_IsOneBetweenEachPairOfTracks ()
        {
            Assert.AreEqual(2, AutoGridLayout.ResizerCount(true, 3));
            Assert.AreEqual(0, AutoGridLayout.ResizerCount(true, 1), "One track has no pair to resize");
            Assert.AreEqual(0, AutoGridLayout.ResizerCount(false, 3));
        }

        [TestMethod]
        public void ResizerStart_IsOnTheFarEdgeOfItsTrack ()
        {
            var lengths = new[] { 50.0, 150.0, 100.0 };
            Assert.AreEqual(48.0, AutoGridLayout.ResizerStart(lengths, 0, 2), kTolerance);
            Assert.AreEqual(198.0, AutoGridLayout.ResizerStart(lengths, 1, 2), kTolerance);
            Assert.AreEqual(0.0, AutoGridLayout.ResizerStart(new[] { 1.0, 10.0 }, 0, 2), kTolerance, "No thicker than its track");
        }

        [TestMethod]
        public void FindResizerAt_WithinHalfTheGrabLength_FindsTheNearest ()
        {
            var lengths = new[] { 100.0, 100.0, 100.0 };

            // The first resizer's middle is at 99, the second's at 199
            Assert.AreEqual(0, AutoGridLayout.FindResizerAt(lengths, 2, 99, 2, 24));
            Assert.AreEqual(0, AutoGridLayout.FindResizerAt(lengths, 2, 111, 2, 24));
            Assert.AreEqual(-1, AutoGridLayout.FindResizerAt(lengths, 2, 112, 2, 24));
            Assert.AreEqual(1, AutoGridLayout.FindResizerAt(lengths, 2, 190, 2, 24));
            Assert.AreEqual(-1, AutoGridLayout.FindResizerAt(lengths, 2, 101, 2, 0), "With no grab band, only the resizer itself");
        }

        [TestMethod]
        public void ComputeDraggedShares_TradesLengthBetweenTheTwoTracksBeside ()
        {
            double[] shares = AutoGridLayout.ComputeDraggedShares(new[] { 0.25, 0.25, 0.5 }, new[] { 50.0, 50.0, 100.0 }, 0, 20, 10);
            AssertShares(shares, 0.35, 0.15, 0.5);
        }

        [TestMethod]
        public void ComputeDraggedShares_PastTheMinimum_StopsAtIt ()
        {
            double[] shares = AutoGridLayout.ComputeDraggedShares(new[] { 0.25, 0.25, 0.5 }, new[] { 50.0, 50.0, 100.0 }, 0, 45, 10);
            AssertShares(shares, 0.45, 0.05, 0.5);

            shares = AutoGridLayout.ComputeDraggedShares(new[] { 0.25, 0.25, 0.5 }, new[] { 50.0, 50.0, 100.0 }, 0, -500, 10);
            AssertShares(shares, 0.05, 0.45, 0.5);
        }

        [TestMethod]
        public void ComputeDraggedShares_NoRoomToTrade_LeavesTheSharesAsTheyWere ()
        {
            double[] shares = AutoGridLayout.ComputeDraggedShares(new[] { 0.5, 0.5 }, new[] { 8.0, 8.0 }, 0, 4, 10);
            AssertShares(shares, 0.5, 0.5);
        }

        // ===========[ Helper Methods ]========================================
        private static void AssertCell (int childIndex, int fixedRows, int fixedColumns, eAutoPopulationOrder order, int expectedRow, int expectedColumn)
        {
            Assert.IsTrue(AutoGridLayout.TryGetCell(childIndex, fixedRows, fixedColumns, order, out int row, out int column), $"child {childIndex} has a cell");
            Assert.AreEqual(expectedRow, row, $"child {childIndex}'s row");
            Assert.AreEqual(expectedColumn, column, $"child {childIndex}'s column");
        }

        private static void AssertShares (IReadOnlyList<double> actual, params double[] expected)
        {
            Assert.AreEqual(expected.Length, actual.Count, "track count");
            for (int index = 0; index < expected.Length; ++index)
            {
                Assert.AreEqual(expected[index], actual[index], kTolerance, $"track {index}");
            }
        }
    }
}
