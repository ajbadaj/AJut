namespace AJut.UX.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>Which pages a PageStack's dot strip shows, through <see cref="PageStackDotRange"/></summary>
    [TestClass]
    public class PageStackDotRangeTests
    {
        [TestMethod]
        public void For_NoPages_ShowsNoDots ()
        {
            PageStackDotRange range = PageStackDotRange.For(pageCount: 0, currentPage: 3, dotReach: 5);
            Assert.AreEqual(0, range.DotCount);
            Assert.AreEqual(-1, range.CurrentPage);
        }

        [TestMethod]
        public void For_FewerPagesThanTheReach_ShowsEveryPageAndNoEdges ()
        {
            PageStackDotRange range = PageStackDotRange.For(pageCount: 4, currentPage: 2, dotReach: 5);
            Assert.AreEqual(new PageStackDotRange(2, 0, 3, false, false), range);
            CollectionAssert.AreEqual(
                new[] { "0 Page", "1 Page", "2 Current", "3 Page" },
                Describe(range)
            );
        }

        [TestMethod]
        public void For_InTheMiddle_ShowsTheReachEachSidePlusBothEdges ()
        {
            // A reach of 5 is 11 dots, with a faded edge dot at each end: 13 in all
            PageStackDotRange range = PageStackDotRange.For(pageCount: 40, currentPage: 20, dotReach: 5);
            Assert.AreEqual(new PageStackDotRange(20, 15, 25, true, true), range);
            Assert.AreEqual(13, range.DotCount);
            Assert.AreEqual(new PageStackDotRange.Dot(14, ePageStackDotKind.BackEdge), range.DotAt(0));
            Assert.AreEqual(new PageStackDotRange.Dot(20, ePageStackDotKind.Current), range.DotAt(6));
            Assert.AreEqual(new PageStackDotRange.Dot(26, ePageStackDotKind.ForwardEdge), range.DotAt(12));
        }

        [TestMethod]
        public void For_NearTheStart_SlidesTheWindowRatherThanShrinkingIt ()
        {
            PageStackDotRange range = PageStackDotRange.For(pageCount: 40, currentPage: 2, dotReach: 5);
            Assert.AreEqual(new PageStackDotRange(2, 0, 10, false, true), range, "Still 11 dots, with the current one off center");
            Assert.AreEqual(12, range.DotCount);
            Assert.AreEqual(new PageStackDotRange.Dot(2, ePageStackDotKind.Current), range.DotAt(2));
            Assert.AreEqual(new PageStackDotRange.Dot(11, ePageStackDotKind.ForwardEdge), range.DotAt(11), "The edge dot goes just past the range");
        }

        [TestMethod]
        public void For_NearTheEnd_SlidesTheWindowRatherThanShrinkingIt ()
        {
            PageStackDotRange range = PageStackDotRange.For(pageCount: 40, currentPage: 38, dotReach: 5);
            Assert.AreEqual(new PageStackDotRange(38, 29, 39, true, false), range);
            Assert.AreEqual(new PageStackDotRange.Dot(28, ePageStackDotKind.BackEdge), range.DotAt(0));
        }

        [TestMethod]
        public void For_ExactlyAWindowOfPages_HasNoEdges ()
        {
            Assert.AreEqual(new PageStackDotRange(0, 0, 10, false, false), PageStackDotRange.For(11, 0, 5));
            Assert.AreEqual(new PageStackDotRange(0, 0, 10, false, true), PageStackDotRange.For(12, 0, 5), "One page more is one edge dot");
        }

        [TestMethod]
        public void For_ZeroOrNegativeReach_ShowsOnlyTheCurrentDotAndItsEdges ()
        {
            Assert.AreEqual(new PageStackDotRange(3, 3, 3, true, true), PageStackDotRange.For(10, 3, 0));
            Assert.AreEqual(new PageStackDotRange(3, 3, 3, true, true), PageStackDotRange.For(10, 3, -4));
        }

        [TestMethod]
        public void For_AHugeReach_ShowsEveryPage ()
        {
            Assert.AreEqual(new PageStackDotRange(5, 0, 9, false, false), PageStackDotRange.For(10, 5, int.MaxValue));
        }

        [TestMethod]
        public void For_ACurrentPageOutsideThePages_IsKeptInside ()
        {
            Assert.AreEqual(9, PageStackDotRange.For(10, 50, 2).CurrentPage);
            Assert.AreEqual(0, PageStackDotRange.For(10, -3, 2).CurrentPage);
        }

        [TestMethod]
        public void DotAt_OutsideTheStrip_Throws ()
        {
            PageStackDotRange range = PageStackDotRange.For(4, 0, 5);
            Assert.ThrowsExactly<System.ArgumentOutOfRangeException>(() => range.DotAt(4));
            Assert.ThrowsExactly<System.ArgumentOutOfRangeException>(() => range.DotAt(-1));
        }

        [TestMethod]
        public void EdgeDot_ClickedRepeatedly_WalksPastTheWindow ()
        {
            // Following the forward edge dot from the start crosses more pages each click than the range shows
            int current = 0;
            var visited = new List<int>();
            for (int click = 0; click < 3; ++click)
            {
                PageStackDotRange range = PageStackDotRange.For(40, current, 5);
                current = range.DotAt(range.DotCount - 1).PageIndex;
                visited.Add(current);
            }

            CollectionAssert.AreEqual(new[] { 11, 17, 23 }, visited);
        }

        [TestMethod]
        public void FirstAndLastDotPage_IncludeTheEdgeDots ()
        {
            PageStackDotRange middle = PageStackDotRange.For(40, 20, 5);
            Assert.AreEqual(14, middle.FirstDotPage);
            Assert.AreEqual(26, middle.LastDotPage);

            PageStackDotRange start = PageStackDotRange.For(40, 2, 5);
            Assert.AreEqual(0, start.FirstDotPage, "No back edge dot at the start");
            Assert.AreEqual(11, start.LastDotPage);
        }

        [TestMethod]
        public void KindOf_PagesOutsideTheWindow_LookLikeTheEdgeOnTheirSide ()
        {
            // A strip sliding to a new range draws the dots it slides out of view this way
            PageStackDotRange range = PageStackDotRange.For(40, 20, 5);
            Assert.AreEqual(ePageStackDotKind.Current, range.KindOf(20));
            Assert.AreEqual(ePageStackDotKind.Page, range.KindOf(15));
            Assert.AreEqual(ePageStackDotKind.BackEdge, range.KindOf(14));
            Assert.AreEqual(ePageStackDotKind.BackEdge, range.KindOf(9));
            Assert.AreEqual(ePageStackDotKind.ForwardEdge, range.KindOf(31));
        }

        private static string[] Describe (PageStackDotRange range)
        {
            return Enumerable.Range(0, range.DotCount)
                .Select(range.DotAt)
                .Select(dot => $"{dot.PageIndex} {dot.Kind}")
                .ToArray();
        }
    }
}
