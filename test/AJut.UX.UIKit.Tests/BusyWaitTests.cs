namespace AJut.UX.Tests
{
    using System.Linq;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using UIKit;

    [TestClass]
    public class BusyWaitTests
    {
        // ===[ Inline ]===

        [TestMethod]
        public void BusyWait_SpinsByDefault ()
        {
            var busy = new BusyWait();

            Assert.IsTrue(busy.IsSpinning);
            Assert.IsTrue(SpinnerOf(busy).IsAnimating);
            Assert.IsFalse(SpinnerOf(busy).Hidden);
        }

        [TestMethod]
        public void BusyWait_Stopped_HidesTheSpinner_ButKeepsItsRoom ()
        {
            var busy = new BusyWait { Text = "Loading" };

            busy.IsSpinning = false;

            Assert.IsTrue(SpinnerOf(busy).Hidden, "The spinner hides inside its box, which keeps its room in the stack");
            Assert.IsFalse(SpinnerOf(busy).IsAnimating);
            Assert.IsFalse(StackOf(busy).ArrangedSubviews.Any(view => view.Hidden && (view is not UILabel)), "The spinner's box stays laid out");
        }

        [TestMethod]
        public void BusyWait_Inline_TakesNoTouches ()
        {
            var busy = new BusyWait();

            Assert.IsFalse(busy.UserInteractionEnabled);
        }

        [TestMethod]
        public void BusyWait_NoText_ShowsTheSpinnerAlone ()
        {
            var busy = new BusyWait();

            Assert.IsTrue(busy.TextLabel.Hidden);
        }

        [TestMethod]
        public void BusyWait_SpinnerDock_PutsTheSpinnerOnThatSide ()
        {
            var busy = new BusyWait { Text = "Loading" };
            UIStackView stack = StackOf(busy);

            busy.SpinnerDock = eBusyWaitSpinnerDock.Left;
            Assert.AreEqual(UILayoutConstraintAxis.Horizontal, stack.Axis);
            Assert.IsNotInstanceOfType(stack.ArrangedSubviews[0], typeof(UILabel));

            busy.SpinnerDock = eBusyWaitSpinnerDock.Right;
            Assert.AreEqual(UILayoutConstraintAxis.Horizontal, stack.Axis);
            Assert.IsInstanceOfType(stack.ArrangedSubviews[0], typeof(UILabel));

            busy.SpinnerDock = eBusyWaitSpinnerDock.Top;
            Assert.AreEqual(UILayoutConstraintAxis.Vertical, stack.Axis);
            Assert.IsNotInstanceOfType(stack.ArrangedSubviews[0], typeof(UILabel));

            busy.SpinnerDock = eBusyWaitSpinnerDock.Bottom;
            Assert.AreEqual(UILayoutConstraintAxis.Vertical, stack.Axis);
            Assert.IsInstanceOfType(stack.ArrangedSubviews[0], typeof(UILabel));
        }

        // ===[ Overlay ]===

        [TestMethod]
        public void BusyWait_Overlay_Spinning_CoversAndTakesTouches ()
        {
            var busy = new BusyWait { IsOverlay = true };

            Assert.IsTrue(busy.UserInteractionEnabled, "A view that takes touches keeps the ones that land on it from what is under it");
            Assert.IsNotNull(busy.BackgroundColor);
            Assert.IsFalse(StackOf(busy).Hidden);
        }

        [TestMethod]
        public void BusyWait_Overlay_Stopped_DrawsNothing_AndLetsTouchesThrough ()
        {
            var busy = new BusyWait { IsOverlay = true };

            busy.IsSpinning = false;

            Assert.IsFalse(busy.UserInteractionEnabled);
            Assert.IsNull(busy.BackgroundColor);
            Assert.IsTrue(StackOf(busy).Hidden);
        }

        [TestMethod]
        public void BusyWait_Overlay_Stopped_GivesBackTheBackgroundItReplaced ()
        {
            var busy = new BusyWait { BackgroundColor = UIColor.SystemPurple };

            busy.IsOverlay = true;
            Assert.AreNotEqual(UIColor.SystemPurple, busy.BackgroundColor, "Spinning, the overlay shows its own fill");

            busy.IsSpinning = false;
            Assert.AreEqual(UIColor.SystemPurple, busy.BackgroundColor);
        }

        [TestMethod]
        public void BusyWait_Inline_KeepsABackgroundTheAppGaveIt ()
        {
            var busy = new BusyWait { BackgroundColor = UIColor.SystemPurple };

            busy.IsSpinning = false;
            busy.IsSpinning = true;

            Assert.AreEqual(UIColor.SystemPurple, busy.BackgroundColor);
        }

        // ===[ Helpers ]===

        private static UIStackView StackOf (BusyWait busy) => busy.Subviews.OfType<UIStackView>().Single();

        private static UIActivityIndicatorView SpinnerOf (BusyWait busy) => TestHost.Descendants<UIActivityIndicatorView>(busy).Single();
    }
}
