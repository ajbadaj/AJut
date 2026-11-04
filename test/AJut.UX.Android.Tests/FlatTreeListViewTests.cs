namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AJut.Storage;
    using AJut.UX;
    using AJut.UX.Controls;
    using AJut.UX.Theming;
    using Android.Graphics;
    using Android.OS;
    using Android.Views;
    using Android.Widget;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class FlatTreeListViewTests
    {
        private const int kTreeHeightDp = 400;

        // A tree shorter than its rows, so some are off the list
        private const int kShortTreeHeightDp = 200;

        // How far down a row a drag ends to drop below it
        private const float kBottomQuarter = 0.75f;

        // Waited past the long press delay, so the press has turned into the long press for certain
        private static readonly TimeSpan kPastTheLongPress = TimeSpan.FromMilliseconds(250);

        [TestMethod]
        public void Tree_InAWindow_ShowsOneRowPerTopLevelNode ()
        {
            FlatTreeListView tree = MakeTree(TestNode.Make("A"), TestNode.Make("B"), TestNode.Make("C"));
            using (TestViews.AttachSized(tree, kTreeHeightDp))
            {
                Assert.AreEqual(3, ListOf(tree).Adapter!.Count);
            }
        }

        [TestMethod]
        public void Tree_ExpandingARow_ShowsItsChildrenRightUnderIt ()
        {
            TestNode a = TestNode.Make("A", "A1", "A2");
            FlatTreeListView tree = MakeTree(a, TestNode.Make("B"));
            using (TestViews.AttachSized(tree, kTreeHeightDp))
            {
                tree.FindItemForSource(a)!.IsExpanded = true;

                Assert.AreEqual(4, ListOf(tree).Adapter!.Count);
                CollectionAssert.AreEqual(new[] { "A", "A1", "A2", "B" }, tree.Items.Select(i => i.Source.ToString()).ToArray());
            }
        }

        [TestMethod]
        public void Tree_CollapsingARow_TakesItsChildrenOut ()
        {
            TestNode a = TestNode.Make("A", "A1", "A2");
            FlatTreeListView tree = MakeTree(a, TestNode.Make("B"));
            using (TestViews.AttachSized(tree, kTreeHeightDp))
            {
                FlatTreeItem item = tree.FindItemForSource(a)!;
                item.IsExpanded = true;
                item.IsExpanded = false;

                Assert.AreEqual(2, ListOf(tree).Adapter!.Count);
            }
        }

        [TestMethod]
        public void Tree_OutOfAWindow_ShowsNoRows_ThenCatchesUpWhenBack ()
        {
            TestNode a = TestNode.Make("A", "A1", "A2");
            FlatTreeListView tree = MakeTree(a, TestNode.Make("B"));
            IDisposable attached = TestViews.AttachSized(tree, kTreeHeightDp);
            attached.Dispose();
            Assert.AreEqual(0, ListOf(tree).Adapter!.Count, "No rows out of a window");

            // Expanded while out of the window, which the list never heard about
            tree.FindItemForSource(a)!.IsExpanded = true;
            using (TestViews.AttachSized(tree, kTreeHeightDp))
            {
                Assert.AreEqual(4, ListOf(tree).Adapter!.Count);
            }
        }

        [TestMethod]
        public async Task Tree_LeavingTheWindow_TearsDownEveryShownRowsContent ()
        {
            var made = new List<CountedContent>();
            FlatTreeListView tree = MakeTree(TestNode.Make("A"), TestNode.Make("B"), TestNode.Make("C"));
            tree.RowContentFactory = _MakeContent;

            IDisposable attached = TestViews.AttachSized(tree, kTreeHeightDp);
            await TestViews.LayOutAsync(tree);
            Assert.IsTrue(made.Count >= 3, $"Every row was built, {made.Count} were");

            attached.Dispose();
            Assert.AreEqual(0, made.Count(c => !c.IsTornDown), "Every row's content let go when the tree left the window");

            View _MakeContent (FlatTreeItem item)
            {
                var content = new CountedContent();
                made.Add(content);
                return content;
            }
        }

        [TestMethod]
        public async Task Tree_ARowChangingElsewhere_LeavesTheShownRowsAsTheyWere ()
        {
            var made = new Dictionary<string, CountedContent>();
            TestNode b = TestNode.Make("B", "B1");
            FlatTreeListView tree = MakeTree(TestNode.Make("A"), b);
            tree.RowContentFactory = _MakeContent;
            using (TestViews.AttachSized(tree, kTreeHeightDp))
            {
                await TestViews.LayOutAsync(tree);
                CountedContent aBefore = made["A"];

                tree.FindItemForSource(b)!.IsExpanded = true;
                await TestViews.LayOutAsync(tree);

                Assert.AreSame(aBefore, made["A"], "Row A was not built again for B expanding");
                Assert.IsFalse(aBefore.IsTornDown);
                Assert.IsTrue(TestViews.Descendants<CountedContent>(tree).Contains(aBefore), "Row A's view is still the one shown");
            }

            View _MakeContent (FlatTreeItem item)
            {
                var content = new CountedContent();
                made[item.Source.ToString()!] = content;
                return content;
            }
        }

        [TestMethod]
        public async Task Tree_ARowScrolledOffTheList_LetsGoOfItsContent ()
        {
            const int kRowCount = 60;
            var made = new Dictionary<string, CountedContent>();
            FlatTreeListView tree = MakeTree(Enumerable.Range(0, kRowCount).Select(i => TestNode.Make($"Row {i}")).ToArray());
            tree.RowContentFactory = _MakeContent;
            using (TestViews.AttachSized(tree, kShortTreeHeightDp))
            {
                await TestViews.LayOutAsync(tree);
                CountedContent first = made["Row 0"];

                ListOf(tree).SetSelection(kRowCount - 1);
                await TestViews.LayOutAsync(tree);

                Assert.IsTrue(first.IsTornDown, "The first row is off the list, so its content is let go");
                Assert.IsTrue(made.Count < kRowCount, $"Only the rows on screen were ever built, {made.Count} of {kRowCount}");
            }

            View _MakeContent (FlatTreeItem item)
            {
                var content = new CountedContent();
                made[item.Source.ToString()!] = content;
                return content;
            }
        }

        [TestMethod]
        public void Tree_SingleSelection_KeepsOnlyTheFirstItemGiven ()
        {
            FlatTreeListView tree = MakeTree(TestNode.Make("A"), TestNode.Make("B"));
            using (TestViews.AttachSized(tree, kTreeHeightDp))
            {
                tree.SetSelection(new[] { tree.Items[0], tree.Items[1] });

                Assert.AreEqual(1, tree.SelectedItems.Count);
                Assert.AreSame(tree.Items[0], tree.SelectedItem);
                Assert.IsTrue(tree.Items[0].IsSelected);
                Assert.IsFalse(tree.Items[1].IsSelected);
            }
        }

        [TestMethod]
        public void Tree_MultiSelection_KeepsEveryItemGiven ()
        {
            FlatTreeListView tree = MakeTree(TestNode.Make("A"), TestNode.Make("B"));
            tree.SelectionMode = eFlatTreeSelectionMode.Multi;
            using (TestViews.AttachSized(tree, kTreeHeightDp))
            {
                tree.SetSelection(new[] { tree.Items[0], tree.Items[1] });
                Assert.AreEqual(2, tree.SelectedItems.Count);
            }
        }

        [TestMethod]
        public async Task Tree_TappingARow_SelectsIt ()
        {
            FlatTreeListView tree = MakeTree(TestNode.Make("A"), TestNode.Make("B"));
            using (TestViews.AttachSized(tree, kTreeHeightDp))
            {
                await TestViews.LayOutAsync(tree);
                ListOf(tree).GetChildAt(1)!.PerformClick();

                Assert.AreSame(tree.Items[1], tree.SelectedItem);
            }
        }

        [TestMethod]
        public async Task Tree_TappingAChevron_ExpandsItsRow ()
        {
            TestNode a = TestNode.Make("A", "A1");
            FlatTreeListView tree = MakeTree(a);
            using (TestViews.AttachSized(tree, kTreeHeightDp))
            {
                await TestViews.LayOutAsync(tree);
                ChevronOf(ListOf(tree).GetChildAt(0)!).PerformClick();

                Assert.IsTrue(tree.FindItemForSource(a)!.IsExpanded);
                Assert.AreEqual(2, ListOf(tree).Adapter!.Count);
            }
        }

        [TestMethod]
        public void Tree_CollapsingTheParentOfASelectedRow_DeselectsIt ()
        {
            TestNode a = TestNode.Make("A", "A1");
            FlatTreeListView tree = MakeTree(a);
            using (TestViews.AttachSized(tree, kTreeHeightDp))
            {
                FlatTreeItem parent = tree.FindItemForSource(a)!;
                parent.IsExpanded = true;
                FlatTreeItem child = tree.Items[1];
                tree.SelectedItem = child;

                SelectionChange<FlatTreeItem>? change = null;
                tree.SelectionChanged += _OnSelectionChanged;
                parent.IsExpanded = false;
                tree.SelectionChanged -= _OnSelectionChanged;

                Assert.IsNull(tree.SelectedItem);
                Assert.IsFalse(child.IsSelected);
                Assert.IsNotNull(change, "The deselection is reported");
                Assert.IsTrue(change.Removed.Contains(child));

                void _OnSelectionChanged (object? sender, SelectionChange<FlatTreeItem> e) => change = e;
            }
        }

        [TestMethod]
        public void Tree_TrySelectItemForSource_ExpandsTheWayToIt ()
        {
            TestNode a = TestNode.Make("A", "A1");
            TestNode a1 = (TestNode)a.Children[0];
            FlatTreeListView tree = MakeTree(a);
            using (TestViews.AttachSized(tree, kTreeHeightDp))
            {
                Assert.IsTrue(tree.TrySelectItemForSource(a1, scrollIntoView: false));
                Assert.AreEqual(2, ListOf(tree).Adapter!.Count);
                Assert.AreSame(a1, tree.SelectedItem!.Source);
            }
        }

        [TestMethod]
        public async Task Tree_LongPressThenDrag_MovesTheRow ()
        {
            TestNode a = TestNode.Make("A", "A1", "A2");
            FlatTreeListView tree = MakeTree(a, TestNode.Make("B"));
            tree.IsDragDropReorderEnabled = true;
            tree.FindItemForSource(a)!.IsExpanded = true;
            using (TestViews.AttachSized(tree, kTreeHeightDp))
            {
                await TestViews.LayOutAsync(tree);
                ListView list = ListOf(tree);
                View a1Row = list.GetChildAt(1)!;
                View a2Row = list.GetChildAt(2)!;
                float x = a1Row.Width / 2f;
                long down = SystemClock.UptimeMillis();

                // Held past the long press delay, then moved straight down over the bottom of A2: no sideways move keeps
                // A1's depth, so it lands under A, after A2
                Dispatch(list, down, MotionEventActions.Down, x, a1Row.Top + (a1Row.Height / 2f));
                await Task.Delay(MobileLook.FlatTreeList.DragLongPressDelay + kPastTheLongPress);
                float dropY = a2Row.Top + (a2Row.Height * kBottomQuarter);
                Dispatch(list, down, MotionEventActions.Move, x, dropY);
                Dispatch(list, down, MotionEventActions.Up, x, dropY);

                CollectionAssert.AreEqual(new[] { "A2", "A1" }, a.Children.OfType<TestNode>().Select(c => c.Name).ToArray());
            }
        }

        [TestMethod]
        public async Task Tree_LongPressLiftedWithoutMoving_PutsTheRowBack ()
        {
            TestNode a = TestNode.Make("A", "A1", "A2");
            FlatTreeListView tree = MakeTree(a, TestNode.Make("B"));
            tree.IsDragDropReorderEnabled = true;
            tree.FindItemForSource(a)!.IsExpanded = true;
            using (TestViews.AttachSized(tree, kTreeHeightDp))
            {
                await TestViews.LayOutAsync(tree);
                ListView list = ListOf(tree);
                View a1Row = list.GetChildAt(1)!;
                float x = a1Row.Width / 2f;
                float y = a1Row.Top + (a1Row.Height / 2f);
                long down = SystemClock.UptimeMillis();

                Dispatch(list, down, MotionEventActions.Down, x, y);
                await Task.Delay(MobileLook.FlatTreeList.DragLongPressDelay + kPastTheLongPress);
                Dispatch(list, down, MotionEventActions.Up, x, y);

                CollectionAssert.AreEqual(new[] { "A1", "A2" }, a.Children.OfType<TestNode>().Select(c => c.Name).ToArray(), "Nothing moved");
                list.GetChildAt(3)!.PerformClick();
                Assert.AreSame(tree.Items[3], tree.SelectedItem, "The drag is over, so a tap selects again");
            }
        }

        [TestMethod]
        public async Task Tree_LongPressOnAButtonInARow_LeavesTheHoldToTheButton ()
        {
            TestNode a = TestNode.Make("A", "A1", "A2");
            FlatTreeListView tree = MakeTree(a, TestNode.Make("B"));
            tree.IsDragDropReorderEnabled = true;
            tree.RowContentFactory = _MakeButton;
            tree.FindItemForSource(a)!.IsExpanded = true;
            using (TestViews.AttachSized(tree, kTreeHeightDp))
            {
                await TestViews.LayOutAsync(tree);
                ListView list = ListOf(tree);
                View a1Button = TestViews.Descendants<Button>(list.GetChildAt(1)!).First();
                View a2Row = list.GetChildAt(2)!;
                PointF press = TestViews.CenterIn(a1Button, list);
                long down = SystemClock.UptimeMillis();

                Dispatch(list, down, MotionEventActions.Down, press.X, press.Y);
                await Task.Delay(MobileLook.FlatTreeList.DragLongPressDelay + kPastTheLongPress);
                float dropY = a2Row.Top + (a2Row.Height * kBottomQuarter);
                Dispatch(list, down, MotionEventActions.Move, press.X, dropY);
                Dispatch(list, down, MotionEventActions.Up, press.X, dropY);

                CollectionAssert.AreEqual(new[] { "A1", "A2" }, a.Children.OfType<TestNode>().Select(c => c.Name).ToArray(), "The row was never lifted");
            }

            View _MakeButton (FlatTreeItem item) => new Button(TestHost.RequiredContext) { Text = item.Source.ToString() };
        }

        [TestMethod]
        public async Task Tree_Disabled_TakesNoTouchesAndDisablesEveryChevron ()
        {
            FlatTreeListView tree = MakeTree(TestNode.Make("A", "A1"), TestNode.Make("B", "B1"));
            tree.Enabled = false;
            using (TestViews.AttachSized(tree, kTreeHeightDp))
            {
                await TestViews.LayOutAsync(tree);
                ListView list = ListOf(tree);
                Assert.IsFalse(list.Enabled, "The list neither scrolls nor takes a tap");

                View[] parts = TestViews.Descendants<View>(list).Where(v => v.Clickable).ToArray();
                Assert.IsTrue(parts.Length >= 4, "Each row, and each expandable row's chevron");
                Assert.IsTrue(parts.All(p => !p.Enabled), "Every part of every row is disabled");

                list.GetChildAt(0)!.PerformClick();
                Assert.IsNull(tree.SelectedItem, "A tap selects nothing");
            }
        }

        [TestMethod]
        public void Tree_DisabledInALayout_DisablesItsList ()
        {
            View root = LayoutInflater.From(TestHost.RequiredContext)!.Inflate(Resource.Layout.test_enabled_false, null)!;
            var tree = root.FindViewById<FlatTreeListView>(Resource.Id.test_disabled_tree)!;

            Assert.IsFalse(tree.Enabled);
            Assert.IsFalse(ListOf(tree).Enabled, "The list the tree holds follows it");
        }

        [TestMethod]
        public async Task Tree_EnabledAgain_TakesTouchesAgain ()
        {
            FlatTreeListView tree = MakeTree(TestNode.Make("A", "A1"));
            tree.Enabled = false;
            using (TestViews.AttachSized(tree, kTreeHeightDp))
            {
                tree.Enabled = true;
                await TestViews.LayOutAsync(tree);
                ListView list = ListOf(tree);
                Assert.IsTrue(list.Enabled);
                View[] parts = TestViews.Descendants<View>(list).Where(v => v.Clickable).ToArray();
                Assert.IsTrue(parts.Length >= 2, "The row and its chevron");
                Assert.IsTrue(parts.All(p => p.Enabled), "Every row is built again, enabled");
            }
        }

        // ===========[ Helper Methods ]===================================
        private static FlatTreeListView MakeTree (params TestNode[] rootItems)
        {
            return new FlatTreeListView(TestHost.RequiredContext) { RootItemsSource = rootItems };
        }

        private static ListView ListOf (View view) => TestViews.Descendants<ListView>(view).First();

        // A row's chevron is its first clickable view after the row itself
        private static View ChevronOf (View row) => TestViews.Descendants<View>(row).Skip(1).First(v => v.Clickable);

        private static void Dispatch (View view, long downTime, MotionEventActions action, float x, float y)
        {
            MotionEvent e = MotionEvent.Obtain(downTime, SystemClock.UptimeMillis(), action, x, y, MetaKeyStates.None)!;
            view.DispatchTouchEvent(e);
            e.Recycle();
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed class CountedContent : View, IFlatTreeRowContent
        {
            public CountedContent () : base(TestHost.RequiredContext)
            {
                this.SetMinimumHeight(TestViews.ToPixels(10));
            }

            public bool IsTornDown { get; private set; }

            public void Teardown () => this.IsTornDown = true;
        }

        // A named tree node; one made with children can be expanded
        private sealed class TestNode : ObservableTreeNode<TestNode>
        {
            public TestNode (string name)
            {
                this.Name = name;
            }

            public string Name { get; }

            public static TestNode Make (string name, params string[] childNames)
            {
                var node = new TestNode(name);
                if (childNames.Length > 0)
                {
                    node.CanHaveChildren = true;
                }

                foreach (string childName in childNames)
                {
                    node.InsertChild(node.Children.Count, new TestNode(childName));
                }

                return node;
            }

            public override string ToString () => this.Name;
        }
    }
}
