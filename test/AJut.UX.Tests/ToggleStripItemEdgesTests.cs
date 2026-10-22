namespace AJut.UX.Tests
{
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ToggleStripItemEdgesTests
    {
        // ===[ Horizontal ]===

        [TestMethod]
        public void Horizontal_FirstMiddleLast_SeparatorsBetweenAndRoundedEnds ()
        {
            ToggleStripItemEdges first = ToggleStripItemEdges.For(0, 3, isVertical: false, trailingSeparator: false);
            ToggleStripItemEdges middle = ToggleStripItemEdges.For(1, 3, isVertical: false, trailingSeparator: false);
            ToggleStripItemEdges last = ToggleStripItemEdges.For(2, 3, isVertical: false, trailingSeparator: false);

            Assert.AreEqual(new ToggleStripItemEdges(false, false, false, false, true, false, false, true), first, "The first rounds its left corners, with no separator");
            Assert.AreEqual(new ToggleStripItemEdges(true, false, false, false, false, false, false, false), middle, "A middle one has a separator on its left and no rounding");
            Assert.AreEqual(new ToggleStripItemEdges(true, false, false, false, false, true, true, false), last, "The last has a left separator and rounds its right corners");
        }

        [TestMethod]
        public void Horizontal_LastBeforeAnOverflowButton_TrailingSeparatorAndSquare ()
        {
            ToggleStripItemEdges last = ToggleStripItemEdges.For(2, 3, isVertical: false, trailingSeparator: true);
            Assert.AreEqual(new ToggleStripItemEdges(true, false, true, false, false, false, false, false), last);
        }

        [TestMethod]
        public void Horizontal_OnlyButton_RoundsAllFour ()
        {
            ToggleStripItemEdges only = ToggleStripItemEdges.For(0, 1, isVertical: false, trailingSeparator: false);
            Assert.AreEqual(new ToggleStripItemEdges(false, false, false, false, true, true, true, true), only);
        }

        // ===[ Vertical ]===

        [TestMethod]
        public void Vertical_FirstMiddleLast_SeparatorsBetweenAndRoundedEnds ()
        {
            ToggleStripItemEdges first = ToggleStripItemEdges.For(0, 3, isVertical: true, trailingSeparator: false);
            ToggleStripItemEdges middle = ToggleStripItemEdges.For(1, 3, isVertical: true, trailingSeparator: false);
            ToggleStripItemEdges last = ToggleStripItemEdges.For(2, 3, isVertical: true, trailingSeparator: false);

            Assert.AreEqual(new ToggleStripItemEdges(false, false, false, false, true, true, false, false), first, "The first rounds its top corners, with no separator");
            Assert.AreEqual(new ToggleStripItemEdges(false, true, false, false, false, false, false, false), middle, "A middle one has a separator on its top and no rounding");
            Assert.AreEqual(new ToggleStripItemEdges(false, true, false, false, false, false, true, true), last, "The last has a top separator and rounds its bottom corners");
        }

        [TestMethod]
        public void Vertical_LastBeforeAnOverflowButton_TrailingSeparatorAndSquare ()
        {
            ToggleStripItemEdges last = ToggleStripItemEdges.For(2, 3, isVertical: true, trailingSeparator: true);
            Assert.AreEqual(new ToggleStripItemEdges(false, true, false, true, false, false, false, false), last);
        }

        [TestMethod]
        public void Vertical_OnlyButton_RoundsAllFour ()
        {
            ToggleStripItemEdges only = ToggleStripItemEdges.For(0, 1, isVertical: true, trailingSeparator: false);
            Assert.AreEqual(new ToggleStripItemEdges(false, false, false, false, true, true, true, true), only);
        }
    }
}
