namespace AJut.UX.Tests
{
    using System.Linq;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>The wrap panel's layout every stack shares: <see cref="SimpleWrapLayout"/></summary>
    [TestClass]
    public class SimpleWrapLayoutTests
    {
        private const double kHorizontalSpacing = 8.0;
        private const double kVerticalSpacing = 6.0;

        [TestMethod]
        public void Compute_ChildrenThatFit_SitOnOneLineWithSpacingBetween ()
        {
            SimpleWrapLayout layout = Compute(100, Size(20, 10), Size(30, 14), Size(10, 8));
            AssertSlot(layout, 0, 0, 0, 20, 10);
            AssertSlot(layout, 1, 28, 0, 30, 14);
            AssertSlot(layout, 2, 66, 0, 10, 8);
            Assert.AreEqual(76.0, layout.Width, "The line's width, not the space offered");
            Assert.AreEqual(14.0, layout.Height, "A line is as tall as its tallest child");
        }

        [TestMethod]
        public void Compute_ChildThatWouldRunPastTheWidth_StartsTheNextLine ()
        {
            // 20 + 8 + 20 = 48 fits in 50; another 8 + 20 does not
            SimpleWrapLayout layout = Compute(50, Size(20, 10), Size(20, 12), Size(20, 10), Size(20, 10));
            AssertSlot(layout, 0, 0, 0, 20, 10);
            AssertSlot(layout, 1, 28, 0, 20, 12);
            AssertSlot(layout, 2, 0, 18, 20, 10);
            AssertSlot(layout, 3, 28, 18, 20, 10);
            Assert.AreEqual(48.0, layout.Width);
            Assert.AreEqual(28.0, layout.Height, "12, then the spacing, then 10");
        }

        [TestMethod]
        public void Compute_FirstChildOnALine_NeverWrapsEvenWhenTooWide ()
        {
            SimpleWrapLayout layout = Compute(50, Size(80, 10), Size(80, 10));
            AssertSlot(layout, 0, 0, 0, 80, 10);
            AssertSlot(layout, 1, 0, 16, 80, 10);
            Assert.AreEqual(50.0, layout.Width, "No wider than the space, though the children overflow it");
        }

        [TestMethod]
        public void Compute_ChildWantingNoSize_TakesNoSlotAndNoSpacing ()
        {
            SimpleWrapLayout layout = Compute(100, Size(20, 10), Size(0, 0), Size(20, 10));
            Assert.IsFalse(layout.Slots[1].IsPlaced);
            AssertSlot(layout, 2, 28, 0, 20, 10);
            Assert.AreEqual(48.0, layout.Width);
        }

        [TestMethod]
        public void Compute_ChildWithNoWidthButSomeHeight_StillSitsInTheFlow ()
        {
            SimpleWrapLayout layout = Compute(100, Size(20, 10), Size(0, 30), Size(20, 10));
            Assert.IsTrue(layout.Slots[1].IsPlaced);
            AssertSlot(layout, 1, 28, 0, 0, 30);
            AssertSlot(layout, 2, 36, 0, 20, 10);
            Assert.AreEqual(30.0, layout.Height);
        }

        [TestMethod]
        public void Compute_UnboundedWidth_NeverWraps ()
        {
            foreach (double unbounded in new[] { double.PositiveInfinity, double.NaN })
            {
                SimpleWrapLayout layout = Compute(unbounded, Size(200, 10), Size(200, 10), Size(200, 10));
                Assert.IsTrue(layout.Slots.All(slot => slot.Y == 0.0));
                Assert.AreEqual(616.0, layout.Width, "The whole line");
            }
        }

        [TestMethod]
        public void Compute_NoChildren_WantsNoSize ()
        {
            SimpleWrapLayout layout = Compute(100);
            Assert.AreEqual(0.0, layout.Width);
            Assert.AreEqual(0.0, layout.Height);
            Assert.AreEqual(0, layout.Slots.Count);
        }

        // ===========[ Helpers ]===================================

        private static SimpleWrapLayout Compute (double width, params WrapItemSize[] sizes) => SimpleWrapLayout.Compute(sizes, width, kHorizontalSpacing, kVerticalSpacing);

        private static WrapItemSize Size (double width, double height) => new WrapItemSize(width, height);

        private static void AssertSlot (SimpleWrapLayout layout, int index, double x, double y, double width, double height)
        {
            Assert.AreEqual(new SimpleWrapLayout.Slot(x, y, width, height, true), layout.Slots[index], $"Child {index}");
        }
    }
}
