namespace AJut.UX.Tests
{
    using System.Collections.ObjectModel;
    using System.Linq;
    using AJut.UX.Controls;
    using Android.Views;
    using Android.Widget;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class UIElementRepeaterTests
    {
        [TestMethod]
        public void DisplayChildren_GoAheadOfTheContainersOwn ()
        {
            var own = new View(TestHost.RequiredContext);
            var container = new LinearLayout(TestHost.RequiredContext);
            container.AddView(own);
            ObservableCollection<View> views = _Views(2);
            var repeater = new UIElementRepeater(TestHost.RequiredContext) { Container = container, DisplayChildren = views };

            _AssertChildren(container, views[0], views[1], own);
            Assert.AreSame(container, repeater.GetChildAt(0), "The container is the repeater's one child");
        }

        [TestMethod]
        public void Attached_EveryChangeIsFollowed ()
        {
            var container = new LinearLayout(TestHost.RequiredContext);
            ObservableCollection<View> views = _Views(3);
            var repeater = new UIElementRepeater(TestHost.RequiredContext) { Container = container, DisplayChildren = views };
            using (TestHost.Attach(repeater))
            {
                views.Add(new View(TestHost.RequiredContext));
                views.Insert(1, new View(TestHost.RequiredContext));
                views.Move(0, 3);
                views.RemoveAt(2);
                _AssertChildren(container, views.ToArray());

                views.Clear();
                _AssertChildren(container);
            }
        }

        [TestMethod]
        public void Detached_ChangesWaitUntilItIsAttachedAgain ()
        {
            var container = new LinearLayout(TestHost.RequiredContext);
            ObservableCollection<View> views = _Views(1);
            var repeater = new UIElementRepeater(TestHost.RequiredContext) { Container = container, DisplayChildren = views };
            using (TestHost.Attach(repeater))
            {
            }

            // Out of a window it no longer follows, and it lets go of its container, which keeps its views, so a
            //  collection that outlives the screen keeps only the container
            views.Add(new View(TestHost.RequiredContext));
            _AssertChildren(container, views[0]);
            Assert.IsNull(container.Parent, "The repeater lets go of its container");
            Assert.AreEqual(0, repeater.ChildCount);

            using (TestHost.Attach(repeater))
            {
                Assert.AreSame(container, repeater.GetChildAt(0), "Back in a window, the container is back");
                _AssertChildren(container, views[0], views[1]);
            }
        }

        [TestMethod]
        public void NewContainer_TakesTheViewsFromTheOld ()
        {
            var first = new LinearLayout(TestHost.RequiredContext);
            ObservableCollection<View> views = _Views(2);
            var repeater = new UIElementRepeater(TestHost.RequiredContext) { Container = first, DisplayChildren = views };

            var second = new FrameLayout(TestHost.RequiredContext);
            repeater.Container = second;

            _AssertChildren(first);
            _AssertChildren(second, views[0], views[1]);
            Assert.AreSame(second, repeater.GetChildAt(0));
            Assert.AreEqual(1, repeater.ChildCount, "The old container is gone from the repeater");
        }

        [TestMethod]
        public void DisplayChildrenBeforeContainer_GoInWhenTheContainerArrives ()
        {
            ObservableCollection<View> views = _Views(2);
            var repeater = new UIElementRepeater(TestHost.RequiredContext) { DisplayChildren = views };
            var container = new LinearLayout(TestHost.RequiredContext);
            repeater.Container = container;
            _AssertChildren(container, views[0], views[1]);

            repeater.Container = null;
            _AssertChildren(container);
            Assert.AreEqual(0, repeater.ChildCount, "No container, nothing in the repeater");
        }

        [TestMethod]
        public void AViewStillInAnotherContainer_IsMovedIn ()
        {
            // An older screen's repeater, say, whose container still holds it
            var elsewhere = new LinearLayout(TestHost.RequiredContext);
            ObservableCollection<View> views = _Views(1);
            elsewhere.AddView(views[0]);

            var container = new LinearLayout(TestHost.RequiredContext);
            var repeater = new UIElementRepeater(TestHost.RequiredContext) { Container = container, DisplayChildren = views };

            _AssertChildren(container, views[0]);
            _AssertChildren(elsewhere);
        }

        [TestMethod]
        public void Repeater_InflatedWithAViewGroupInside_TakesItAsTheContainer ()
        {
            View root = LayoutInflater.From(TestHost.RequiredContext)!.Inflate(Resource.Layout.ui_element_repeater_test, null)!;
            var repeater = root.FindViewById<UIElementRepeater>(Resource.Id.ui_element_repeater)!;
            var grid = root.FindViewById<AutoGrid>(Resource.Id.ui_element_repeater_grid)!;
            Assert.AreSame(grid, repeater.Container);
            Assert.AreEqual(ViewGroup.LayoutParams.MatchParent, grid.LayoutParameters!.Height, "It fills the repeater, whatever the layout wrote");

            ObservableCollection<View> views = _Views(2);
            repeater.DisplayChildren = views;
            _AssertChildren(grid, views[0], views[1]);
        }

        // ===========[ Helper Methods ]===================================
        private static ObservableCollection<View> _Views (int count)
        {
            var views = new ObservableCollection<View>();
            for (int index = 0; index < count; ++index)
            {
                views.Add(new View(TestHost.RequiredContext));
            }

            return views;
        }

        private static void _AssertChildren (ViewGroup container, params View[] expected)
        {
            Assert.AreEqual(expected.Length, container.ChildCount, "child count");
            for (int index = 0; index < expected.Length; ++index)
            {
                Assert.AreSame(expected[index], container.GetChildAt(index), $"child {index}");
            }
        }
    }
}
