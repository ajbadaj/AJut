namespace AJut.UX.Tests
{
    using System;
    using System.Linq;
    using AJut.UX.Controls;
    using CoreGraphics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using UIKit;

    [TestClass]
    public class PageStackTests
    {
        private const float kStackWidth = 300f;
        private const float kStackHeight = 160f;

        [TestMethod]
        public void SelectedIndex_PastTheEnd_ShowsTheLastPage_AndKeepsTheIndexGiven ()
        {
            PageStack stack = _Stack(pageCount: 0);
            stack.SelectedIndex = 9;
            _AddPages(stack, 4);
            stack.LayoutIfNeeded();

            Assert.AreEqual(9, stack.SelectedIndex, "An index set before the pages are added is kept as given");
            Assert.IsFalse(_HostOf(stack, 3).Hidden, "The last page shows");
            Assert.AreEqual(1.0, (double)_HostOf(stack, 3).Alpha, 0.001);
            Assert.IsFalse(_ForwardButton(stack).Enabled, "There is no page past the last");
        }

        [TestMethod]
        public void InsertBeforeTheShownPage_KeepsShowingIt ()
        {
            PageStack stack = _Stack(pageCount: 5);
            stack.SelectedIndex = 2;
            int raised = 0;
            stack.SelectedIndexChanged += _OnSelectedIndexChanged;

            UIView shown = stack.Children[2];
            stack.Children.Insert(0, new UIView());
            stack.SelectedIndexChanged -= _OnSelectedIndexChanged;

            Assert.AreEqual(3, stack.SelectedIndex);
            Assert.AreSame(shown, stack.Children[stack.SelectedIndex]);
            Assert.AreEqual(1, raised, "The index moved, so SelectedIndexChanged is raised");

            void _OnSelectedIndexChanged (object sender, EventArgs e) => ++raised;
        }

        [TestMethod]
        public void RemoveTheShownPage_GoesToThePageThatTookItsPlace ()
        {
            PageStack stack = _Stack(pageCount: 5);
            stack.SelectedIndex = 4;
            stack.Children.RemoveAt(4);
            Assert.AreEqual(3, stack.SelectedIndex, "The last page removed, the new last page shows");

            stack.SelectedIndex = 1;
            UIView next = stack.Children[2];
            stack.Children.RemoveAt(1);
            Assert.AreEqual(1, stack.SelectedIndex);
            Assert.AreSame(next, stack.Children[stack.SelectedIndex]);
        }

        [TestMethod]
        public void PageButtons_EnableOnlyWhereThereIsAPageToGoTo_AndPageThatWay ()
        {
            PageStack stack = _Stack(pageCount: 3);
            using (TestHost.Attach(stack))
            {
                stack.LayoutIfNeeded();
                Assert.IsFalse(_BackButton(stack).Enabled);
                Assert.IsTrue(_ForwardButton(stack).Enabled);

                _ForwardButton(stack).SendActionForControlEvents(UIControlEvent.TouchUpInside);
                Assert.AreEqual(1, stack.SelectedIndex);
                _ForwardButton(stack).SendActionForControlEvents(UIControlEvent.TouchUpInside);
                stack.LayoutIfNeeded();
                Assert.AreEqual(2, stack.SelectedIndex);
                Assert.IsFalse(_ForwardButton(stack).Enabled, "On the last page");

                _BackButton(stack).SendActionForControlEvents(UIControlEvent.TouchUpInside);
                Assert.AreEqual(1, stack.SelectedIndex);
            }
        }

        [TestMethod]
        public void OnePage_HidesThePageButtons ()
        {
            PageStack stack = _Stack(pageCount: 1);
            stack.LayoutIfNeeded();
            Assert.IsTrue(_BackButton(stack).Hidden);
            Assert.IsTrue(_ForwardButton(stack).Hidden);
            Assert.AreEqual((double)kStackWidth, (double)_HostOf(stack, 0).Frame.Width, 0.001, "With nothing to page to, the page takes the whole stack");
        }

        [TestMethod]
        public void ADot_GoesToItsPage ()
        {
            PageStack stack = _Stack(pageCount: 12);
            stack.ShowDots = true;
            stack.DotReach = 2;
            using (TestHost.Attach(stack))
            {
                stack.LayoutIfNeeded();

                // Five dots around page 0 and a forward edge dot standing for page 5
                UIControl edge = _Dot(stack, "Page 6 of 12");
                edge.SendActionForControlEvents(UIControlEvent.TouchUpInside);
                Assert.AreEqual(5, stack.SelectedIndex, "The edge dot goes just past the range");

                stack.TransitionDuration = TimeSpan.Zero;
                stack.SelectedIndex = 6;
                _Dot(stack, "Page 5 of 12").SendActionForControlEvents(UIControlEvent.TouchUpInside);
                Assert.AreEqual(4, stack.SelectedIndex);
            }
        }

        [TestMethod]
        public void Layout_PlacesTheShownPageBetweenThePeeks_AndHidesPagesPastThem ()
        {
            // No dots or count: the unset page padding is 5 on the top and bottom, and nothing on the sides
            PageStack stack = _Stack(pageCount: 5);
            stack.SelectedIndex = 2;
            stack.LayoutIfNeeded();

            // A button is 4 + 12 + 4 plus a 1 point outline each side: 22, then the peek (16) and the gap (8)
            const double kClearance = 22.0 + 16.0 + 8.0;
            const double kPageLength = kStackWidth - (2.0 * kClearance);
            CGRect shown = _HostOf(stack, 2).Frame;
            Assert.AreEqual(kClearance, (double)shown.X, 0.001);
            Assert.AreEqual(5.0, (double)shown.Y, 0.001);
            Assert.AreEqual(kPageLength, (double)shown.Width, 0.001);
            Assert.AreEqual(kStackHeight - 10.0, (double)shown.Height, 0.001);

            CGRect next = _HostOf(stack, 3).Frame;
            Assert.AreEqual(kClearance + kPageLength + 8.0, (double)next.X, 0.001, "The neighbor sits a page and a gap along");
            Assert.AreEqual(0.25, (double)_HostOf(stack, 3).Alpha, 0.001, "Faded to InactivePageOpacity");
            Assert.IsFalse(_HostOf(stack, 1).Hidden);
            Assert.IsTrue(_HostOf(stack, 0).Hidden, "Two pages away can't be seen at all");
            Assert.IsTrue(_HostOf(stack, 4).Hidden);
        }

        [TestMethod]
        public void OnlyTheShownPage_TakesTouches ()
        {
            PageStack stack = _Stack(pageCount: 3);
            stack.SelectedIndex = 1;
            stack.LayoutIfNeeded();

            UIView peek = _HostOf(stack, 0);
            CGPoint inPeek = new CGPoint(peek.Bounds.GetMidX(), peek.Bounds.GetMidY());
            Assert.IsNull(peek.HitTest(inPeek, null), "A peek can't be tapped into");

            UIView shown = _HostOf(stack, 1);
            Assert.IsNotNull(shown.HitTest(new CGPoint(shown.Bounds.GetMidX(), shown.Bounds.GetMidY()), null));
        }

        [TestMethod]
        public void Swipe_CanOnlyBeginWhenOn_WithAnotherPageToGoTo ()
        {
            PageStack stack = _Stack(pageCount: 3);
            Assert.IsTrue(stack.IsSwipeEnabled, "On by default");
            Assert.IsTrue(_Swipe(stack).Enabled);

            stack.IsSwipeEnabled = false;
            Assert.IsFalse(_Swipe(stack).Enabled, "Off, a drag is left to whatever else wants it");

            stack.IsSwipeEnabled = true;
            stack.Children.RemoveAt(2);
            stack.Children.RemoveAt(1);
            Assert.IsFalse(_Swipe(stack).Enabled, "One page has nowhere to swipe to");

            stack.Children.Add(new UIView());
            Assert.IsTrue(_Swipe(stack).Enabled);
        }

        // A stack out of any window, so a pick jumps straight to its page
        private static PageStack _Stack (int pageCount)
        {
            var stack = new PageStack { Frame = new CGRect(0, 0, kStackWidth, kStackHeight) };
            _AddPages(stack, pageCount);
            return stack;
        }

        private static void _AddPages (PageStack stack, int pageCount)
        {
            for (int index = 0; index < pageCount; ++index)
            {
                stack.Children.Add(new UIView());
            }
        }

        // The page host the stack put the page in
        private static UIView _HostOf (PageStack stack, int pageIndex) => stack.Children[pageIndex].Superview;

        private static UIControl _BackButton (PageStack stack) => _ControlNamed(stack, "Previous page");

        private static UIControl _ForwardButton (PageStack stack) => _ControlNamed(stack, "Next page");

        private static UIControl _Dot (PageStack stack, string label) => _ControlNamed(stack, label);

        private static UIControl _ControlNamed (PageStack stack, string label)
        {
            return TestHost.Descendants<UIControl>(stack).Single(control => control.AccessibilityLabel == label);
        }

        private static UIPanGestureRecognizer _Swipe (PageStack stack) => stack.GestureRecognizers.OfType<UIPanGestureRecognizer>().Single();
    }
}
