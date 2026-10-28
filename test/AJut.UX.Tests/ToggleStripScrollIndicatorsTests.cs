namespace AJut.UX.Tests
{
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ToggleStripScrollIndicatorsTests
    {
        [TestMethod]
        public void TSSI_ContentThatFits_ShowsNoDots ()
        {
            Assert.AreEqual(new ToggleStripScrollIndicators(false, false), ToggleStripScrollIndicators.For(0, 200, 260), "Shorter than the strip");
            Assert.AreEqual(new ToggleStripScrollIndicators(false, false), ToggleStripScrollIndicators.For(0, 260, 260), "Exactly the strip's length");
        }

        [TestMethod]
        public void TSSI_AtTheLeadingEnd_ShowsOnlyTheForwardDot ()
        {
            Assert.AreEqual(new ToggleStripScrollIndicators(ShowBack: false, ShowForward: true), ToggleStripScrollIndicators.For(0, 557, 260));
        }

        [TestMethod]
        public void TSSI_InTheMiddle_ShowsBothDots ()
        {
            Assert.AreEqual(new ToggleStripScrollIndicators(ShowBack: true, ShowForward: true), ToggleStripScrollIndicators.For(150, 557, 260));
        }

        [TestMethod]
        public void TSSI_AtTheTrailingEnd_ShowsOnlyTheBackDot ()
        {
            Assert.AreEqual(new ToggleStripScrollIndicators(ShowBack: true, ShowForward: false), ToggleStripScrollIndicators.For(297, 557, 260));
        }

        [TestMethod]
        public void TSSI_WithinTheToleranceOfAnEnd_CountsAsThere ()
        {
            Assert.IsFalse(ToggleStripScrollIndicators.For(0.4, 557, 260).ShowBack, "0.4 from the leading end has reached it");
            Assert.IsTrue(ToggleStripScrollIndicators.For(0.6, 557, 260).ShowBack, "0.6 from the leading end can still scroll back");
            Assert.IsFalse(ToggleStripScrollIndicators.For(296.6, 557, 260).ShowForward, "0.4 from the trailing end has reached it");
            Assert.IsTrue(ToggleStripScrollIndicators.For(296.4, 557, 260).ShowForward, "0.6 from the trailing end can still scroll forward");
        }

        [TestMethod]
        public void TSSI_BouncedPastAnEnd_CountsAsThatEnd ()
        {
            Assert.AreEqual(new ToggleStripScrollIndicators(ShowBack: false, ShowForward: true), ToggleStripScrollIndicators.For(-20, 557, 260), "Pulled past the leading end");
            Assert.AreEqual(new ToggleStripScrollIndicators(ShowBack: true, ShowForward: false), ToggleStripScrollIndicators.For(320, 557, 260), "Pulled past the trailing end");
        }
    }
}
