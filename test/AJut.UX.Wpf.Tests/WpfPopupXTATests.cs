namespace AJut.UX.Tests
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using AJut.UX.AttachedProperties;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// What <see cref="PopupXTA.StaysOpenUnlessClickAwayProperty"/> counts as a click away from its popup. WPF objects
    /// need an STA thread, so each test runs on one, and nothing is ever shown.
    /// </summary>
    [TestClass]
    public class WpfPopupXTATests
    {
        private const double kPopupSize = 100.0;

        [TestMethod]
        public void ClickAway_ClickOutsideThePopup_ClosesIt ()
        {
            StaTestThread.Run(() =>
            {
                // A click outside the popup lands on something outside it
                Popup popup = BuildPopup(out _);
                var outside = new Button();

                Assert.IsTrue(PopupXTA.IsClickAway(popup, outside, new Point(kPopupSize * 3, kPopupSize * 3)));
            });
        }

        [TestMethod]
        public void ClickAway_ClickInsideThePopup_LeavesItOpen ()
        {
            StaTestThread.Run(() =>
            {
                Popup popup = BuildPopup(out Border child);

                Assert.IsFalse(PopupXTA.IsClickAway(popup, child, new Point(kPopupSize / 2, kPopupSize / 2)));
            });
        }

        [TestMethod]
        public void ClickAway_ClickInAPopupOpenedFromInsideIt_LeavesItOpenWhereverThatPopupSits ()
        {
            StaTestThread.Run(() =>
            {
                // A menu opened from a button inside the popup, hanging below it, as a calendar's month menu does
                Popup popup = BuildPopup(out Border child);
                var menuItem = new Button();
                var innerPopup = new Popup { Child = menuItem };
                child.Child = innerPopup;

                Assert.IsFalse(PopupXTA.IsClickAway(popup, menuItem, new Point(kPopupSize / 2, kPopupSize * 3)));
            });
        }

        // A popup whose child is laid out at a known size, so a point can be inside or outside it
        private static Popup BuildPopup (out Border child)
        {
            child = new Border { Width = kPopupSize, Height = kPopupSize };
            var popup = new Popup { Child = child };
            child.Measure(new Size(kPopupSize, kPopupSize));
            child.Arrange(new Rect(0, 0, kPopupSize, kPopupSize));
            return popup;
        }
    }
}
