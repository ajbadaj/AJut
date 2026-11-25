namespace AJut.UX.Tests
{
    using System;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>How a PageStack picks, lays out and moves between its pages, through <see cref="PageStackRules"/></summary>
    [TestClass]
    public class PageStackRulesTests
    {
        // ===========[ Selection ]=============================================
        [TestMethod]
        public void EffectiveSelectedIndex_IsKeptInsideThePages ()
        {
            Assert.AreEqual(-1, PageStackRules.EffectiveSelectedIndex(0, 0), "No pages, nothing shown");
            Assert.AreEqual(2, PageStackRules.EffectiveSelectedIndex(2, 5));
            Assert.AreEqual(4, PageStackRules.EffectiveSelectedIndex(9, 5));
            Assert.AreEqual(0, PageStackRules.EffectiveSelectedIndex(-3, 5));
        }

        [TestMethod]
        public void PageButtons_EnableOnlyWhereThereIsAPageToGoTo ()
        {
            Assert.IsFalse(PageStackRules.CanGoBack(0));
            Assert.IsTrue(PageStackRules.CanGoBack(1));
            Assert.IsTrue(PageStackRules.CanGoForward(0, 3));
            Assert.IsFalse(PageStackRules.CanGoForward(2, 3));
            Assert.IsFalse(PageStackRules.CanGoForward(-1, 0), "An empty stack goes nowhere");
        }

        [TestMethod]
        public void PageButtonsAndDots_ShowOnlyWithMoreThanOnePage ()
        {
            Assert.IsFalse(PageStackRules.ArePageButtonsShown(1));
            Assert.IsTrue(PageStackRules.ArePageButtonsShown(2));
            Assert.IsFalse(PageStackRules.AreDotsShown(showDots: true, pageCount: 1));
            Assert.IsTrue(PageStackRules.AreDotsShown(showDots: true, pageCount: 2));
            Assert.IsFalse(PageStackRules.AreDotsShown(showDots: false, pageCount: 8));
        }

        [TestMethod]
        public void PageCount_ShowsOnlyWithMoreThanOnePage ()
        {
            Assert.IsTrue(PageStackRules.IsPageCountShown(showPageCount: true, pageCount: 2));
            Assert.IsFalse(PageStackRules.IsPageCountShown(true, 1));
            Assert.IsFalse(PageStackRules.IsPageCountShown(false, 7));
        }

        [TestMethod]
        public void PageCountText_CountsFromOne_AndStacksItsWordsWhenVertical ()
        {
            Assert.AreEqual("3 of 7", PageStackRules.PageCountText(PageStackRules.kDefaultPageCountFormat, effectiveSelectedIndex: 2, pageCount: 7, isVertical: false));
            Assert.AreEqual("3\nof\n7", PageStackRules.PageCountText("{0} of {1}", 2, 7, isVertical: true));
            Assert.AreEqual("3/7", PageStackRules.PageCountText("{0}/{1}", 2, 7, isVertical: true), "A format with no spaces stays on one line");
            Assert.AreEqual("1 of 4", PageStackRules.PageCountText(null, 0, 4, false), "No format is the default one");
        }

        [TestMethod]
        public void SelectedIndexAfterInsert_KeepsShowingTheSamePage ()
        {
            Assert.AreEqual(4, PageStackRules.SelectedIndexAfterInsert(selectedIndex: 2, oldPageCount: 5, insertedAt: 0, insertedCount: 2));
            Assert.AreEqual(3, PageStackRules.SelectedIndexAfterInsert(2, 5, 2, 1), "Inserting at the shown page pushes it on");
            Assert.AreEqual(2, PageStackRules.SelectedIndexAfterInsert(2, 5, 3, 1), "Inserting after it changes nothing");
        }

        [TestMethod]
        public void SelectedIndexAfterInsert_WhileFillingTheStack_LeavesAnIndexSetAhead ()
        {
            // XAML sets SelectedIndex before it adds the pages one by one: the index must still land on page 2
            int selected = 2;
            for (int count = 0; count < 4; ++count)
            {
                selected = PageStackRules.SelectedIndexAfterInsert(selected, oldPageCount: count, insertedAt: count, insertedCount: 1);
            }

            Assert.AreEqual(2, selected);
        }

        [TestMethod]
        public void SelectedIndexAfterRemoval_KeepsShowingTheSamePage ()
        {
            Assert.AreEqual(1, PageStackRules.SelectedIndexAfterRemoval(selectedIndex: 3, oldPageCount: 6, removedAt: 0, removedCount: 2));
            Assert.AreEqual(3, PageStackRules.SelectedIndexAfterRemoval(3, 6, 4, 2), "Removing after it changes nothing");
        }

        [TestMethod]
        public void SelectedIndexAfterRemoval_OfTheShownPage_GoesToTheOneThatTookItsPlace ()
        {
            Assert.AreEqual(2, PageStackRules.SelectedIndexAfterRemoval(3, 6, 2, 2));
            Assert.AreEqual(3, PageStackRules.SelectedIndexAfterRemoval(5, 6, 4, 2), "Or the new last page, when it was at the end");
            Assert.AreEqual(0, PageStackRules.SelectedIndexAfterRemoval(0, 1, 0, 1), "Removing the only page leaves the index at the start");
        }

        [TestMethod]
        public void SelectedIndexAfterRemoval_OfAnIndexNamingNoPage_LeavesIt ()
        {
            Assert.AreEqual(7, PageStackRules.SelectedIndexAfterRemoval(7, 3, 0, 1));
        }

        // ===========[ Layout ]================================================
        [TestMethod]
        public void PageClearance_IsTheButtonPeekAndGap_OnlyWithPageButtons ()
        {
            Assert.AreEqual(44.0, PageStackRules.PageClearance(arePageButtonsShown: true, buttonLength: 20.0, pagePeek: 16.0, pageSpacing: 8.0));
            Assert.AreEqual(0.0, PageStackRules.PageClearance(false, 20.0, 16.0, 8.0), "A lone page takes the whole stack");
            Assert.AreEqual(20.0, PageStackRules.PageClearance(true, 20.0, -5.0, -1.0), "A negative peek or gap counts as none");
        }

        [TestMethod]
        public void PageOffset_IsAPageAndAGapPerPageAway ()
        {
            Assert.AreEqual(0.0, PageStackRules.PageOffset(pageIndex: 3, position: 3.0, pageLength: 200.0, pageSpacing: 10.0));
            Assert.AreEqual(210.0, PageStackRules.PageOffset(4, 3.0, 200.0, 10.0));
            Assert.AreEqual(-420.0, PageStackRules.PageOffset(1, 3.0, 200.0, 10.0));
            Assert.AreEqual(105.0, PageStackRules.PageOffset(4, 3.5, 200.0, 10.0), "Mid transition, halfway");
        }

        [TestMethod]
        public void PageOpacity_FadesToInactiveOnePageAway ()
        {
            Assert.AreEqual(1.0, PageStackRules.PageOpacity(pageIndex: 2, position: 2.0, inactivePageOpacity: 0.25));
            Assert.AreEqual(0.25, PageStackRules.PageOpacity(3, 2.0, 0.25));
            Assert.AreEqual(0.25, PageStackRules.PageOpacity(7, 2.0, 0.25), "Further away stays at inactive");
            Assert.AreEqual(0.625, PageStackRules.PageOpacity(3, 2.5, 0.25), 1e-9);
            Assert.AreEqual(0.0, PageStackRules.PageOpacity(3, 2.0, -1.0), "An inactive opacity outside zero to one is kept inside");
        }

        // ===========[ Transitions ]===========================================
        [TestMethod]
        public void EaseOut_StartsAndEndsInPlace_AndLeadsTheClock ()
        {
            Assert.AreEqual(0.0, PageStackRules.EaseOut(0.0));
            Assert.AreEqual(1.0, PageStackRules.EaseOut(1.0));
            Assert.AreEqual(0.875, PageStackRules.EaseOut(0.5), 1e-9);
            Assert.AreEqual(1.0, PageStackRules.EaseOut(4.0), "Past the end stays at the end");
        }

        [TestMethod]
        public void TransitionPosition_MovesFromStartToTarget ()
        {
            var duration = TimeSpan.FromMilliseconds(200);
            Assert.AreEqual(1.0, PageStackRules.TransitionPosition(1.0, 3.0, TimeSpan.Zero, duration));
            Assert.AreEqual(2.75, PageStackRules.TransitionPosition(1.0, 3.0, TimeSpan.FromMilliseconds(100), duration), 1e-9);
            Assert.AreEqual(3.0, PageStackRules.TransitionPosition(1.0, 3.0, TimeSpan.FromMilliseconds(250), duration));
            Assert.IsTrue(PageStackRules.IsTransitionDone(TimeSpan.FromMilliseconds(200), duration));
            Assert.IsFalse(PageStackRules.IsTransitionDone(TimeSpan.FromMilliseconds(199), duration));
        }

        [TestMethod]
        public void ShouldSlideDots_OnlyWhileTheTwoStripsShareADot ()
        {
            Assert.IsTrue(PageStackRules.ShouldSlideDots(fromFirstDotPage: 14.0, toFirstDotPage: 17, toDotCount: 13), "A dot three away slides three");
            Assert.IsTrue(PageStackRules.ShouldSlideDots(14.0, 2, 13));
            Assert.IsFalse(PageStackRules.ShouldSlideDots(14.0, 27, 13), "A strip's length away shares nothing");
            Assert.IsFalse(PageStackRules.ShouldSlideDots(0.0, 28, 12), "End from the start jumps");
        }

        [TestMethod]
        public void DotSlideSpan_HoldsBothStripsDots ()
        {
            // Clicking the dot three right of center: the dots leaving on the left and arriving on the right are both held
            Assert.AreEqual(new PageStackRules.DotSpan(14, 29), PageStackRules.DotSlideSpan(fromFirstDotPage: 14.0, toFirstDotPage: 17, toDotCount: 13, pageCount: 40));
            Assert.AreEqual(16, PageStackRules.DotSlideSpan(14.0, 17, 13, 40).Count);

            // Mid slide, between two pages, both neighbors are held
            Assert.AreEqual(new PageStackRules.DotSpan(15, 30), PageStackRules.DotSlideSpan(15.4, 18, 13, 40));
        }

        [TestMethod]
        public void DotSlideSpan_IsKeptInsideThePages ()
        {
            Assert.AreEqual(new PageStackRules.DotSpan(0, 11), PageStackRules.DotSlideSpan(0.0, 0, 12, 40));
            Assert.AreEqual(new PageStackRules.DotSpan(26, 39), PageStackRules.DotSlideSpan(26.0, 28, 12, 40));
        }

        [TestMethod]
        public void TransitionPosition_WithNoDuration_JumpsToTheTarget ()
        {
            Assert.AreEqual(5.0, PageStackRules.TransitionPosition(0.0, 5.0, TimeSpan.Zero, TimeSpan.Zero));
            Assert.IsTrue(PageStackRules.IsTransitionDone(TimeSpan.Zero, TimeSpan.FromMilliseconds(-10)));
        }
    }
}
