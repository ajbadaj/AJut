namespace AJut.UX.Tests
{
    using AJut.UX;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ScrollIntoViewMathTests
    {
        // Signature: ResolveAxisOffset(positionInViewport, size, currentOffset, viewport, scrollable, leadingInset, trailingInset)

        [TestMethod]
        public void SIVM_NotScrollable_ReturnsNull ()
        {
            Assert.IsNull(ScrollIntoViewMath.ResolveAxisOffset(50, 30, 0, 200, 0, 0, 0));
        }

        [TestMethod]
        public void SIVM_AlreadyFullyVisible_ReturnsNull ()
        {
            Assert.IsNull(ScrollIntoViewMath.ResolveAxisOffset(50, 30, 10, 200, 100, 0, 0));
        }

        [TestMethod]
        public void SIVM_CutOffAtEnd_AlignsEndEdge ()
        {
            // Element at viewport-x 190, width 30 -> end 220 > 200. Bring its end to the viewport end.
            double? result = ScrollIntoViewMath.ResolveAxisOffset(190, 30, 10, 200, 100, 0, 0);
            Assert.AreEqual(30.0, result.Value, 0.001);
        }

        [TestMethod]
        public void SIVM_CutOffAtStart_AlignsStartEdge ()
        {
            // Element starts 20px left of the viewport -> bring its start to the viewport start.
            double? result = ScrollIntoViewMath.ResolveAxisOffset(-20, 30, 50, 200, 100, 0, 0);
            Assert.AreEqual(30.0, result.Value, 0.001);
        }

        [TestMethod]
        public void SIVM_TrailingInset_KeepsElementClearOfReservedEdge ()
        {
            // 20px reserved at the end (e.g. a scroll button); the element's end must clear viewport-20.
            double? result = ScrollIntoViewMath.ResolveAxisOffset(185, 30, 0, 200, 100, 0, 20);
            Assert.AreEqual(35.0, result.Value, 0.001);
        }

        [TestMethod]
        public void SIVM_BiggerThanViewport_AlignsStart ()
        {
            // Element wider than the clear viewport - prioritize showing its start.
            double? result = ScrollIntoViewMath.ResolveAxisOffset(40, 300, 0, 200, 500, 0, 0);
            Assert.AreEqual(40.0, result.Value, 0.001);
        }
    }
}
