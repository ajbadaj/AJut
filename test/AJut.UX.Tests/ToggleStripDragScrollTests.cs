namespace AJut.UX.Tests
{
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ToggleStripDragScrollTests
    {
        [TestMethod]
        public void TSDS_MoveWithinTheThreshold_IsStillAClick ()
        {
            var drag = new ToggleStripDragScroll(threshold: 4);
            drag.Begin(100);

            Assert.AreEqual(0.0, drag.Move(103), "No scroll before the threshold");
            Assert.IsFalse(drag.IsDragging);
            Assert.IsFalse(drag.End(), "The release clicks the item");
        }

        [TestMethod]
        public void TSDS_MovePastTheThreshold_ScrollsByEachMove_AndCancelsTheClick ()
        {
            var drag = new ToggleStripDragScroll(threshold: 4);
            drag.Begin(100);

            Assert.AreEqual(-10.0, drag.Move(90), "Crossing the threshold scrolls by the whole move so far");
            Assert.IsTrue(drag.IsDragging);
            Assert.AreEqual(-5.0, drag.Move(85), "Then by each move");
            Assert.AreEqual(7.0, drag.Move(92), "Either way along the strip");
            Assert.IsTrue(drag.End(), "The release must not click the item");
            Assert.IsFalse(drag.IsDragging);
        }

        [TestMethod]
        public void TSDS_MoveWithoutAPress_DoesNothing ()
        {
            var drag = new ToggleStripDragScroll();

            Assert.AreEqual(0.0, drag.Move(500));
            Assert.IsFalse(drag.End());
        }

        [TestMethod]
        public void TSDS_ANewPress_StartsOverAsAClick ()
        {
            var drag = new ToggleStripDragScroll(threshold: 4);
            drag.Begin(0);
            drag.Move(50);
            drag.End();

            drag.Begin(50);
            Assert.AreEqual(0.0, drag.Move(52));
            Assert.IsFalse(drag.End());
        }
    }
}
