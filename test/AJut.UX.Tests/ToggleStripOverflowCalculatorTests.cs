namespace AJut.UX.Tests
{
    using System.Linq;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ToggleStripOverflowCalculatorTests
    {
        // All items 100 wide, button reserves 30, for easy mental math.
        private const double kItemWidth = 100.0;
        private const double kButtonWidth = 30.0;

        // ===[ Nothing to do ]===

        [TestMethod]
        public void TSOC_Empty_NoVisibleNoOverflow ()
        {
            var result = ToggleStripOverflowCalculator.Compute(new double[0], new bool[0], 500, kButtonWidth);

            Assert.AreEqual(0, result.VisibleIndices.Count);
            Assert.IsFalse(result.HasOverflow);
        }

        [TestMethod]
        public void TSOC_EverythingFits_AllVisible_ButtonSpaceIgnored ()
        {
            // 3 * 100 = 300 fits in 320 even though 320 - 30(button) = 290 would not.
            var widths = new[] { kItemWidth, kItemWidth, kItemWidth };
            var selected = new[] { false, false, false };

            var result = ToggleStripOverflowCalculator.Compute(widths, selected, 320, kButtonWidth);

            Assert.IsFalse(result.HasOverflow, "When all items fit, the reserved button space must not be subtracted");
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, result.VisibleIndices.ToArray());
        }

        // ===[ Basic overflow, no selection ]===

        [TestMethod]
        public void TSOC_Overflow_NoSelection_FillsLeadingItems ()
        {
            // 5 items * 100 = 500 > 250. Budget = 250 - 30 = 220 -> 2 items fit.
            var widths = Enumerable.Repeat(kItemWidth, 5).ToArray();
            var selected = Enumerable.Repeat(false, 5).ToArray();

            var result = ToggleStripOverflowCalculator.Compute(widths, selected, 250, kButtonWidth);

            CollectionAssert.AreEqual(new[] { 0, 1 }, result.VisibleIndices.ToArray());
            CollectionAssert.AreEqual(new[] { 2, 3, 4 }, result.OverflowIndices.ToArray());
        }

        // ===[ Selected item is kept visible (leading-window) ]===

        [TestMethod]
        public void TSOC_Overflow_SelectedLateItem_StaysVisible_OrderPreserved ()
        {
            // 5 items, budget fits 2. Item 4 is selected -> it must be visible, and one leading
            // unselected fills the other slot. Order is preserved (no reordering).
            var widths = Enumerable.Repeat(kItemWidth, 5).ToArray();
            var selected = new[] { false, false, false, false, true };

            var result = ToggleStripOverflowCalculator.Compute(widths, selected, 250, kButtonWidth);

            Assert.IsTrue(result.VisibleIndices.Contains(4), "The selected item must remain visible");
            CollectionAssert.AreEqual(new[] { 0, 4 }, result.VisibleIndices.ToArray(), "Visible items keep positional order");
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, result.OverflowIndices.ToArray());
        }

        // ===[ Multi-select where more are selected than fit ]===

        [TestMethod]
        public void TSOC_Overflow_MoreSelectedThanFit_KeepsAsManyAsFitInOrder ()
        {
            // Budget fits 2. Items 1, 3, 4 selected -> first two selected (1, 3) fit, 4 overflows.
            var widths = Enumerable.Repeat(kItemWidth, 5).ToArray();
            var selected = new[] { false, true, false, true, true };

            var result = ToggleStripOverflowCalculator.Compute(widths, selected, 250, kButtonWidth);

            CollectionAssert.AreEqual(new[] { 1, 3 }, result.VisibleIndices.ToArray());
            CollectionAssert.AreEqual(new[] { 0, 2, 4 }, result.OverflowIndices.ToArray());
        }

        // ===[ The item just picked claims first ]===

        [TestMethod]
        public void TSOC_Overflow_PickedItem_ClaimsFirst_AheadOfEarlierSelections ()
        {
            // Budget fits 2. Items 0, 1, 2 were selected, then 4 was picked: 4 shows, with the first selected one.
            var widths = Enumerable.Repeat(kItemWidth, 5).ToArray();
            var selected = new[] { true, true, true, false, true };

            var result = ToggleStripOverflowCalculator.Compute(widths, selected, 250, kButtonWidth, pickedIndex: 4);

            CollectionAssert.AreEqual(new[] { 0, 4 }, result.VisibleIndices.ToArray(), "The pick shows, and the order stays positional");
        }

        [TestMethod]
        public void TSOC_Overflow_PickedItemNotSelected_IsIgnored ()
        {
            // A picked index that is not selected (a deselect) takes no special claim
            var widths = Enumerable.Repeat(kItemWidth, 5).ToArray();
            var selected = new[] { true, true, false, false, false };

            var result = ToggleStripOverflowCalculator.Compute(widths, selected, 250, kButtonWidth, pickedIndex: 4);

            CollectionAssert.AreEqual(new[] { 0, 1 }, result.VisibleIndices.ToArray());
        }

        [TestMethod]
        public void TSOC_BudgetTooSmallForAny_ShowsThePickedItemRatherThanTheShortest ()
        {
            var widths = new[] { 40.0, 100.0, 120.0 };
            var selected = new[] { false, false, true };

            var result = ToggleStripOverflowCalculator.Compute(widths, selected, 50, kButtonWidth, pickedIndex: 2);

            CollectionAssert.AreEqual(new[] { 2 }, result.VisibleIndices.ToArray());
        }

        // ===[ Never empty ]===

        [TestMethod]
        public void TSOC_BudgetTooSmallForAny_StillShowsOne ()
        {
            // Available smaller than a single item + button -> budget can fit nothing, but we
            // never show an empty strip.
            var widths = new[] { kItemWidth, kItemWidth, kItemWidth };
            var selected = new[] { false, false, false };

            var result = ToggleStripOverflowCalculator.Compute(widths, selected, 50, kButtonWidth);

            Assert.AreEqual(1, result.VisibleIndices.Count, "At least one item is always shown");
            Assert.IsTrue(result.HasOverflow);
        }

        [TestMethod]
        public void TSOC_BudgetTooSmallForAny_ShowsTheShortest ()
        {
            var lengths = new[] { 90.0, 40.0, 70.0 };
            var selected = new[] { false, false, false };

            var result = ToggleStripOverflowCalculator.Compute(lengths, selected, 50, kButtonWidth);

            CollectionAssert.AreEqual(new[] { 1 }, result.VisibleIndices.ToArray(), "The shortest item along the strip is the one shown");
        }

        // ===[ Vertical strips ]===

        [TestMethod]
        public void TSOC_VerticalStrip_SplitsByHeight ()
        {
            // A vertical strip passes heights: five 40 tall buttons in a 130 tall strip, with a 30 tall overflow
            // button, leaves a budget of 100, so two buttons stay and the selected late one is one of them.
            var heights = Enumerable.Repeat(40.0, 5).ToArray();
            var selected = new[] { false, false, false, true, false };

            var result = ToggleStripOverflowCalculator.Compute(heights, selected, 130, 30);

            CollectionAssert.AreEqual(new[] { 0, 3 }, result.VisibleIndices.ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 4 }, result.OverflowIndices.ToArray());
        }

        [TestMethod]
        public void TSOC_VerticalStrip_TallEnough_NothingOverflows ()
        {
            var heights = new[] { 40.0, 40.0, 40.0 };
            var selected = new[] { false, true, false };

            var result = ToggleStripOverflowCalculator.Compute(heights, selected, 120, 30);

            Assert.IsFalse(result.HasOverflow, "Three 40 tall buttons fit a 120 tall strip exactly");
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, result.VisibleIndices.ToArray());
        }
    }
}
