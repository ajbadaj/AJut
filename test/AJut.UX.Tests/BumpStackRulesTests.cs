namespace AJut.UX.Tests
{
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>What a BumpStack's bump buttons show and do, through <see cref="BumpStackRules"/></summary>
    [TestClass]
    public class BumpStackRulesTests
    {
        [TestMethod]
        public void For_ContentThatFits_ShowsNoButtons ()
        {
            Assert.AreEqual(new BumpStackRules(false, false, false), BumpStackRules.For(scrollingEnabled: true, offset: 0.0, scrollableLength: 0.0));
            Assert.AreEqual(new BumpStackRules(false, false, false), BumpStackRules.For(true, 0.0, BumpStackRules.kEndTolerance), "Overflow within the tolerance is layout rounding, not overflow");
        }

        [TestMethod]
        public void For_ScrollingOff_ShowsNoButtonsWhateverOverflows ()
        {
            Assert.AreEqual(new BumpStackRules(false, false, false), BumpStackRules.For(scrollingEnabled: false, offset: 40.0, scrollableLength: 200.0));
        }

        [TestMethod]
        public void For_AtTheStart_OnlyTheForwardButtonIsEnabled ()
        {
            Assert.AreEqual(new BumpStackRules(true, false, true), BumpStackRules.For(true, 0.0, 200.0));
            Assert.AreEqual(new BumpStackRules(true, false, true), BumpStackRules.For(true, 0.4, 200.0), "Within the tolerance of the start is at it");
        }

        [TestMethod]
        public void For_AtTheEnd_OnlyTheBackButtonIsEnabled ()
        {
            Assert.AreEqual(new BumpStackRules(true, true, false), BumpStackRules.For(true, 200.0, 200.0));
            Assert.AreEqual(new BumpStackRules(true, true, false), BumpStackRules.For(true, 199.6, 200.0), "Within the tolerance of the end is at it");
        }

        [TestMethod]
        public void For_InTheMiddle_BothButtonsAreEnabled ()
        {
            Assert.AreEqual(new BumpStackRules(true, true, true), BumpStackRules.For(true, 80.0, 200.0));
        }

        [TestMethod]
        public void NudgedOffset_StaysInsideTheScrollableRange ()
        {
            Assert.AreEqual(60.0, BumpStackRules.NudgedOffset(0.0, 60.0, 200.0));
            Assert.AreEqual(200.0, BumpStackRules.NudgedOffset(180.0, 60.0, 200.0));
            Assert.AreEqual(0.0, BumpStackRules.NudgedOffset(20.0, -60.0, 200.0));
            Assert.AreEqual(0.0, BumpStackRules.NudgedOffset(0.0, 60.0, -5.0), "Nothing to scroll is nowhere to go");
        }

        [TestMethod]
        public void ButtonClearance_IsTheButtonAndTheOffsetOnlyWhileShown ()
        {
            Assert.AreEqual(26.0, BumpStackRules.ButtonClearance(areShown: true, buttonLength: 20.0, edgeClearanceOffset: 6.0));
            Assert.AreEqual(0.0, BumpStackRules.ButtonClearance(areShown: false, buttonLength: 20.0, edgeClearanceOffset: 6.0));
        }

        [TestMethod]
        public void TryTurnWheelSideways_MostlyVerticalWheelOnAHorizontalStack_MovesAlongIt ()
        {
            // Wheel down (negative y) moves toward the end
            Assert.IsTrue(BumpStackRules.TryTurnWheelSideways(isHorizontal: true, deltaX: 0.0, deltaY: -2.0, isInPoints: false, out double lines));
            Assert.AreEqual(2.0 * BumpStackRules.kWheelLineStep, lines);

            Assert.IsTrue(BumpStackRules.TryTurnWheelSideways(true, 1.0, 12.0, isInPoints: true, out double points));
            Assert.AreEqual(-12.0, points, "A trackpad's points go through as they are");
        }

        [TestMethod]
        public void TryTurnWheelSideways_SidewaysSwipeOrVerticalStack_IsLeftToTheToolkit ()
        {
            Assert.IsFalse(BumpStackRules.TryTurnWheelSideways(isHorizontal: true, deltaX: -8.0, deltaY: 3.0, isInPoints: true, out _));
            Assert.IsFalse(BumpStackRules.TryTurnWheelSideways(isHorizontal: false, deltaX: 0.0, deltaY: -2.0, isInPoints: false, out _));
        }
    }
}
