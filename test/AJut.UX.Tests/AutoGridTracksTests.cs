namespace AJut.UX.Tests
{
    using System.Collections.Generic;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>One AutoGrid's tracks and resizer drag, as the toolkits with no Grid of their own keep them: <see cref="AutoGridTracks"/></summary>
    [TestClass]
    public class AutoGridTracksTests
    {
        private const double kTolerance = 0.0001;
        private const double kGrabLength = 24.0;

        [TestMethod]
        public void UpdateTrackCounts_MoreChildren_AddsTracksAtTheAverageShare ()
        {
            var tracks = new AutoGridTracks { FixedColumnCount = 1 };
            Assert.IsTrue(tracks.UpdateTrackCounts(2));
            tracks.SetTargetSizePercentageFor(0, 0, 0.7, -1.0);

            Assert.IsTrue(tracks.UpdateTrackCounts(3));
            AssertValues(tracks.RowShares, 0.7 / 1.5, 0.3 / 1.5, 0.5 / 1.5);
            Assert.IsFalse(tracks.UpdateTrackCounts(3), "The same count changes nothing");
        }

        [TestMethod]
        public void GetCellBounds_IsWhereTheArrangedTracksPutIt ()
        {
            AutoGridTracks tracks = Arranged(columns: 2, children: 4, width: 200, height: 100);
            Assert.AreEqual(new AutoGridTracks.TrackBounds(100, 50, 100, 50), tracks.GetCellBounds(1, 1));
        }

        [TestMethod]
        public void GetResizerBounds_SitsOnTheFarEdgeAndRunsTheGrid ()
        {
            AutoGridTracks tracks = Arranged(columns: 2, children: 4, width: 200, height: 100);
            tracks.AddColumnResizers = true;
            tracks.AddRowResizers = true;

            Assert.AreEqual(new AutoGridTracks.TrackBounds(98, 0, 2, 100), tracks.GetResizerBounds(true, 0));
            Assert.AreEqual(new AutoGridTracks.TrackBounds(0, 48, 200, 2), tracks.GetResizerBounds(false, 0));
        }

        [TestMethod]
        public void Drag_ColumnResizer_TradesWidthBetweenTheTwoColumns ()
        {
            AutoGridTracks tracks = Arranged(columns: 2, children: 2, width: 200, height: 100);
            tracks.AddColumnResizers = true;

            Assert.IsTrue(tracks.TryBeginDrag(95, 40, kGrabLength), "A press beside the resizer takes hold of it");
            Assert.IsTrue(tracks.IsDragging);
            Assert.IsTrue(tracks.ContinueDrag(125, 60));
            AssertValues(tracks.ColumnShares, 0.65, 0.35);

            // From where the drag started, not from the last move, so going back undoes it
            Assert.IsTrue(tracks.ContinueDrag(95, 40));
            AssertValues(tracks.ColumnShares, 0.5, 0.5);

            tracks.EndDrag();
            Assert.IsFalse(tracks.IsDragging);
            Assert.IsFalse(tracks.ContinueDrag(150, 40), "No drag, no change");
        }

        [TestMethod]
        public void Drag_PastTheMinimum_StopsAtIt ()
        {
            AutoGridTracks tracks = Arranged(columns: 1, children: 2, width: 100, height: 200);
            tracks.AddRowResizers = true;
            tracks.MinElementHeight = 20;

            Assert.IsTrue(tracks.TryBeginDrag(50, 99, kGrabLength));
            tracks.ContinueDrag(50, 500);
            AssertValues(tracks.RowShares, 0.9, 0.1);
        }

        [TestMethod]
        public void TryBeginDrag_AwayFromAnyResizer_StartsNothing ()
        {
            AutoGridTracks tracks = Arranged(columns: 2, children: 2, width: 200, height: 100);
            tracks.AddColumnResizers = true;

            Assert.IsFalse(tracks.TryBeginDrag(50, 40, kGrabLength));
            Assert.IsFalse(tracks.TryBeginDrag(99, 140, kGrabLength), "Below the grid is not on its resizer");
            Assert.IsFalse(tracks.IsDragging);
        }

        [TestMethod]
        public void IsOnResizer_SaysWhereAPressWouldTakeHold_AndStartsNothing ()
        {
            AutoGridTracks tracks = Arranged(columns: 2, children: 4, width: 200, height: 100);
            tracks.AddColumnResizers = true;
            tracks.AddRowResizers = true;

            Assert.IsTrue(tracks.IsOnResizer(95, 20, kGrabLength, out bool isColumnResizer), "Beside the column resizer");
            Assert.IsTrue(isColumnResizer);
            Assert.IsTrue(tracks.IsOnResizer(150, 45, kGrabLength, out isColumnResizer), "Beside the row resizer");
            Assert.IsFalse(isColumnResizer);
            Assert.IsTrue(tracks.IsOnResizer(99, 49, kGrabLength, out isColumnResizer), "Where the two cross");
            Assert.IsTrue(isColumnResizer, "Where the two cross, the column resizer, as a press takes it");
            Assert.IsFalse(tracks.IsOnResizer(40, 20, kGrabLength, out _), "Inside a cell");
            Assert.IsFalse(tracks.IsOnResizer(99, 140, kGrabLength, out _), "Below the grid");
            Assert.IsFalse(tracks.IsDragging, "Asking takes hold of nothing");
        }

        [TestMethod]
        public void TryBeginDrag_NoResizers_StartsNothing ()
        {
            AutoGridTracks tracks = Arranged(columns: 2, children: 2, width: 200, height: 100);
            Assert.IsFalse(tracks.TryBeginDrag(99, 40, kGrabLength));
        }

        [TestMethod]
        public void UpdateTrackCounts_ATrackGoesMidDrag_EndsTheDrag ()
        {
            AutoGridTracks tracks = Arranged(columns: 3, children: 3, width: 300, height: 100);
            tracks.AddColumnResizers = true;
            Assert.IsTrue(tracks.TryBeginDrag(199, 40, kGrabLength));

            tracks.UpdateTrackCounts(2);
            Assert.IsFalse(tracks.IsDragging);
        }

        [TestMethod]
        public void ResizerCount_AnAxisArrangedByItsContent_HasNone ()
        {
            // Its tracks are as long as their children whatever their shares, so a drag would move nothing
            var tracks = new AutoGridTracks { FixedColumnCount = 1, AddRowResizers = true };
            tracks.UpdateTrackCounts(3);
            tracks.ArrangeColumns(100);
            tracks.ArrangeRows(double.PositiveInfinity, new[] { 20.0, 30.0, 40.0 });
            Assert.AreEqual(0, tracks.RowResizerCount);
            Assert.IsFalse(tracks.TryBeginDrag(50, 20, kGrabLength));

            tracks.ArrangeRows(300);
            Assert.AreEqual(2, tracks.RowResizerCount, "Given a height, the rows share it and can be resized");
        }

        [TestMethod]
        public void ResizerCount_TracksAddedSinceTheLastArrange_AreNotCountedUntilArranged ()
        {
            AutoGridTracks tracks = Arranged(columns: 1, children: 2, width: 100, height: 100);
            tracks.AddRowResizers = true;
            Assert.AreEqual(1, tracks.RowResizerCount);

            tracks.UpdateTrackCounts(4);
            Assert.AreEqual(1, tracks.RowResizerCount, "Two rows were arranged, so there is one resizer to place");
            Assert.AreEqual(default(AutoGridTracks.TrackBounds), tracks.GetResizerBounds(false, 2), "One past them is empty");

            tracks.ArrangeRows(100);
            Assert.AreEqual(3, tracks.RowResizerCount);
        }

        [TestMethod]
        public void SetTargetFixedSizeFor_GivesTheShareOfTheLastLayout ()
        {
            AutoGridTracks tracks = Arranged(columns: 2, children: 4, width: 200, height: 100);
            tracks.SetTargetFixedSizeFor(0, 0, 50, 30);
            AssertValues(tracks.ColumnShares, 0.25, 0.75);
            AssertValues(tracks.RowShares, 0.3, 0.7);
        }

        // ===========[ Helper Methods ]========================================
        private static AutoGridTracks Arranged (int columns, int children, double width, double height)
        {
            var tracks = new AutoGridTracks { FixedColumnCount = columns };
            tracks.UpdateTrackCounts(children);
            tracks.ArrangeColumns(width);
            tracks.ArrangeRows(height);
            return tracks;
        }

        private static void AssertValues (IReadOnlyList<double> actual, params double[] expected)
        {
            Assert.AreEqual(expected.Length, actual.Count, "track count");
            for (int index = 0; index < expected.Length; ++index)
            {
                Assert.AreEqual(expected[index], actual[index], kTolerance, $"track {index}");
            }
        }
    }
}
