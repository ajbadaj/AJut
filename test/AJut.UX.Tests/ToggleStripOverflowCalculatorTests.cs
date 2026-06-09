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
    }
}
