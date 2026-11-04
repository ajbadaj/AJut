namespace AJut.UX.Tests
{
    using System.Collections.ObjectModel;
    using System.Linq;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using UIKit;

    [TestClass]
    public class UIElementRepeaterTests
    {
        [TestMethod]
        public void DisplayChildren_GoAheadOfTheContainersOwn ()
        {
            var own = new UIView();
            var container = new UIView();
            container.AddSubview(own);
            ObservableCollection<UIView> views = _Views(2);
            var repeater = new UIElementRepeater { Container = container, DisplayChildren = views };

            _AssertSubviews(container.Subviews, views[0], views[1], own);
            Assert.AreSame(container, repeater.Subviews.Single(), "The container is the repeater's one subview");
        }

        [TestMethod]
        public void InAWindow_EveryChangeIsFollowed ()
        {
            var container = new UIView();
            ObservableCollection<UIView> views = _Views(3);
            var repeater = new UIElementRepeater { Container = container, DisplayChildren = views };
            using (TestHost.Attach(repeater))
            {
                views.Add(new UIView());
                views.Insert(1, new UIView());
                views.Move(0, 3);
                views.RemoveAt(2);
                _AssertSubviews(container.Subviews, views.ToArray());

                views.Clear();
                Assert.AreEqual(0, container.Subviews.Length);
            }
        }

        [TestMethod]
        public void OutOfAWindow_ChangesWaitUntilItIsBack ()
        {
            var container = new UIView();
            ObservableCollection<UIView> views = _Views(1);
            var repeater = new UIElementRepeater { Container = container, DisplayChildren = views };
            using (TestHost.Attach(repeater))
            {
            }

            // Out of a window it no longer follows, and it lets go of its container, which keeps its views
            views.Add(new UIView());
            _AssertSubviews(container.Subviews, views[0]);
            Assert.IsNull(container.Superview, "The repeater lets go of its container");

            using (TestHost.Attach(repeater))
            {
                Assert.AreSame(repeater, container.Superview, "Back in a window, the container is back");
                _AssertSubviews(container.Subviews, views[0], views[1]);
            }
        }

        [TestMethod]
        public void StackViewContainer_TakesThemAsArrangedSubviews ()
        {
            var stack = new UIStackView();
            ObservableCollection<UIView> views = _Views(2);
            var repeater = new UIElementRepeater { Container = stack, DisplayChildren = views };

            _AssertSubviews(stack.ArrangedSubviews, views[0], views[1]);
        }

        [TestMethod]
        public void DisplayChildrenBeforeContainer_GoInWhenTheContainerArrives ()
        {
            ObservableCollection<UIView> views = _Views(2);
            var repeater = new UIElementRepeater { DisplayChildren = views };
            var container = new UIView();
            repeater.Container = container;
            _AssertSubviews(container.Subviews, views[0], views[1]);

            repeater.Container = null;
            Assert.AreEqual(0, container.Subviews.Length);
            Assert.AreEqual(0, repeater.Subviews.Length, "No container, nothing in the repeater");
        }

        [TestMethod]
        public void ViewLeavingAStackView_IsPlacedByFrameAgain ()
        {
            var stack = new UIStackView();
            ObservableCollection<UIView> views = _Views(1);
            var repeater = new UIElementRepeater { Container = stack, DisplayChildren = views };
            Assert.IsFalse(views[0].TranslatesAutoresizingMaskIntoConstraints, "A stack view lays its views out by constraints");

            repeater.Container = new AutoGrid();
            Assert.IsTrue(views[0].TranslatesAutoresizingMaskIntoConstraints, "The grid places its cells by frame");
        }

        [TestMethod]
        public void NewContainer_TakesTheViewsFromTheOld ()
        {
            var first = new UIView();
            ObservableCollection<UIView> views = _Views(2);
            var repeater = new UIElementRepeater { Container = first, DisplayChildren = views };

            var second = new UIView();
            repeater.Container = second;

            Assert.AreEqual(0, first.Subviews.Length);
            _AssertSubviews(second.Subviews, views[0], views[1]);
            Assert.AreSame(second, repeater.Subviews.Single());
        }

        // ===========[ Helper Methods ]===================================
        private static ObservableCollection<UIView> _Views (int count)
        {
            var views = new ObservableCollection<UIView>();
            for (int index = 0; index < count; ++index)
            {
                views.Add(new UIView());
            }

            return views;
        }

        private static void _AssertSubviews (UIView[] actual, params UIView[] expected)
        {
            Assert.AreEqual(expected.Length, actual.Length, "subview count");
            for (int index = 0; index < expected.Length; ++index)
            {
                Assert.AreSame(expected[index], actual[index], $"subview {index}");
            }
        }
    }
}
