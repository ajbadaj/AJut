namespace AJut.UX.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>Which pages a PageStack's dot strip shows, through <see cref="PageStackDotWindow"/></summary>
    [TestClass]
    public class PageStackDotWindowTests
    {
        [TestMethod]
        public void For_NoPages_ShowsNoDots ()
        {
            PageStackDotWindow window = PageStackDotWindow.For(pageCount: 0, currentPage: 3, dotReach: 5);
            Assert.AreEqual(0, window.DotCount);
            Assert.AreEqual(-1, window.CurrentPage);
        }

        [TestMethod]
        public void For_FewerPagesThanTheReach_ShowsEveryPageAndNoEdges ()
        {
            PageStackDotWindow window = PageStackDotWindow.For(pageCount: 4, currentPage: 2, dotReach: 5);
            Assert.AreEqual(new PageStackDotWindow(2, 0, 3, false, false), window);
            CollectionAssert.AreEqual(
                new[] { "0 Page", "1 Page", "2 Current", "3 Page" },
                Describe(window)
            );
        }

        [TestMethod]
        public void For_InTheMiddle_ShowsTheReachEachSidePlusBothEdges ()
        {
            // A reach of 5 is 11 dots, with a faded edge dot at each end: 13 in all
            PageStackDotWindow window = PageStackDotWindow.For(pageCount: 40, currentPage: 20, dotReach: 5);
            Assert.AreEqual(new PageStackDotWindow(20, 15, 25, true, true), window);
            Assert.AreEqual(13, window.DotCount);
            Assert.AreEqual(new PageStackDotWindow.Dot(14, ePageStackDotKind.BackEdge), window.DotAt(0));
            Assert.AreEqual(new PageStackDotWindow.Dot(20, ePageStackDotKind.Current), window.DotAt(6));
            Assert.AreEqual(new PageStackDotWindow.Dot(26, ePageStackDotKind.ForwardEdge), window.DotAt(12));
        }

        [TestMethod]
        public void For_NearTheStart_SlidesTheWindowRatherThanShrinkingIt ()
        {
            PageStackDotWindow window = PageStackDotWindow.For(pageCount: 40, currentPage: 2, dotReach: 5);
            Assert.AreEqual(new PageStackDotWindow(2, 0, 10, false, true), window, "Still 11 dots, with the current one off center");
            Assert.AreEqual(12, window.DotCount);
            Assert.AreEqual(new PageStackDotWindow.Dot(2, ePageStackDotKind.Current), window.DotAt(2));
            Assert.AreEqual(new PageStackDotWindow.Dot(11, ePageStackDotKind.ForwardEdge), window.DotAt(11), "The edge dot goes just past the window");
        }

        [TestMethod]
        public void For_NearTheEnd_SlidesTheWindowRatherThanShrinkingIt ()
        {
            PageStackDotWindow window = PageStackDotWindow.For(pageCount: 40, currentPage: 38, dotReach: 5);
            Assert.AreEqual(new PageStackDotWindow(38, 29, 39, true, false), window);
            Assert.AreEqual(new PageStackDotWindow.Dot(28, ePageStackDotKind.BackEdge), window.DotAt(0));
        }

        [TestMethod]
        public void For_ExactlyAWindowOfPages_HasNoEdges ()
        {
            Assert.AreEqual(new PageStackDotWindow(0, 0, 10, false, false), PageStackDotWindow.For(11, 0, 5));
            Assert.AreEqual(new PageStackDotWindow(0, 0, 10, false, true), PageStackDotWindow.For(12, 0, 5), "One page more is one edge dot");
        }

        [TestMethod]
        public void For_ZeroOrNegativeReach_ShowsOnlyTheCurrentDotAndItsEdges ()
        {
            Assert.AreEqual(new PageStackDotWindow(3, 3, 3, true, true), PageStackDotWindow.For(10, 3, 0));
            Assert.AreEqual(new PageStackDotWindow(3, 3, 3, true, true), PageStackDotWindow.For(10, 3, -4));
        }

        [TestMethod]
        public void For_AHugeReach_ShowsEveryPage ()
        {
            Assert.AreEqual(new PageStackDotWindow(5, 0, 9, false, false), PageStackDotWindow.For(10, 5, int.MaxValue));
        }

        [TestMethod]
        public void For_ACurrentPageOutsideThePages_IsKeptInside ()
        {
            Assert.AreEqual(9, PageStackDotWindow.For(10, 50, 2).CurrentPage);
            Assert.AreEqual(0, PageStackDotWindow.For(10, -3, 2).CurrentPage);
        }

        [TestMethod]
        public void DotAt_OutsideTheStrip_Throws ()
        {
            PageStackDotWindow window = PageStackDotWindow.For(4, 0, 5);
            Assert.ThrowsExactly<System.ArgumentOutOfRangeException>(() => window.DotAt(4));
            Assert.ThrowsExactly<System.ArgumentOutOfRangeException>(() => window.DotAt(-1));
        }

        [TestMethod]
        public void EdgeDot_ClickedRepeatedly_WalksPastTheWindow ()
        {
            // Following the forward edge dot from the start crosses more pages each click than the window shows
            int current = 0;
            var visited = new List<int>();
            for (int click = 0; click < 3; ++click)
            {
                PageStackDotWindow window = PageStackDotWindow.For(40, current, 5);
                current = window.DotAt(window.DotCount - 1).PageIndex;
                visited.Add(current);
            }

            CollectionAssert.AreEqual(new[] { 11, 17, 23 }, visited);
        }

        [TestMethod]
        public void FirstAndLastDotPage_IncludeTheEdgeDots ()
        {
            PageStackDotWindow middle = PageStackDotWindow.For(40, 20, 5);
            Assert.AreEqual(14, middle.FirstDotPage);
            Assert.AreEqual(26, middle.LastDotPage);

            PageStackDotWindow start = PageStackDotWindow.For(40, 2, 5);
            Assert.AreEqual(0, start.FirstDotPage, "No back edge dot at the start");
            Assert.AreEqual(11, start.LastDotPage);
        }

        [TestMethod]
        public void KindOf_PagesOutsideTheWindow_LookLikeTheEdgeOnTheirSide ()
        {
            // A strip sliding to a new window draws the dots it slides out of view this way
            PageStackDotWindow window = PageStackDotWindow.For(40, 20, 5);
            Assert.AreEqual(ePageStackDotKind.Current, window.KindOf(20));
            Assert.AreEqual(ePageStackDotKind.Page, window.KindOf(15));
            Assert.AreEqual(ePageStackDotKind.BackEdge, window.KindOf(14));
            Assert.AreEqual(ePageStackDotKind.BackEdge, window.KindOf(9));
            Assert.AreEqual(ePageStackDotKind.ForwardEdge, window.KindOf(31));
        }

        private static string[] Describe (PageStackDotWindow window)
        {
            return Enumerable.Range(0, window.DotCount)
                .Select(window.DotAt)
                .Select(dot => $"{dot.PageIndex} {dot.Kind}")
                .ToArray();
        }
    }
}
