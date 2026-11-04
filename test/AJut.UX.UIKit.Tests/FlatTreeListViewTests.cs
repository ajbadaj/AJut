namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using AJut.Storage;
    using AJut.UX;
    using AJut.UX.Controls;
    using CoreGraphics;
    using Foundation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using ObjCRuntime;
    using UIKit;

    [TestClass]
    public class FlatTreeListViewTests
    {
        [TestMethod]
        public void Tree_InAWindow_ShowsOneRowPerTopLevelNode ()
        {
            var tree = new FlatTreeListView { RootItemsSource = new[] { TestNode.Make("A"), TestNode.Make("B"), TestNode.Make("C") } };
            using (TestHost.Attach(tree))
            {
                Assert.AreEqual(3, (int)TableOf(tree).NumberOfRowsInSection(0));
            }
        }

        [TestMethod]
        public void Tree_ExpandingARow_ShowsItsChildrenRightUnderIt ()
        {
            TestNode a = TestNode.Make("A", "A1", "A2");
            var tree = new FlatTreeListView { RootItemsSource = new[] { a, TestNode.Make("B") } };
            using (TestHost.Attach(tree))
            {
                tree.FindItemForSource(a).IsExpanded = true;

                Assert.AreEqual(4, (int)TableOf(tree).NumberOfRowsInSection(0));
                CollectionAssert.AreEqual(new[] { "A", "A1", "A2", "B" }, tree.Items.Select(i => i.Source.ToString()).ToArray());
            }
        }

        [TestMethod]
        public void Tree_CollapsingARow_TakesItsChildrenOut ()
        {
            TestNode a = TestNode.Make("A", "A1", "A2");
            var tree = new FlatTreeListView { RootItemsSource = new[] { a, TestNode.Make("B") } };
            using (TestHost.Attach(tree))
            {
                FlatTreeItem item = tree.FindItemForSource(a);
                item.IsExpanded = true;
                item.IsExpanded = false;

                Assert.AreEqual(2, (int)TableOf(tree).NumberOfRowsInSection(0));
            }
        }

        [TestMethod]
        public void Tree_OutOfAWindow_ShowsNoRows_ThenCatchesUpWhenBack ()
        {
            TestNode a = TestNode.Make("A", "A1", "A2");
            var tree = new FlatTreeListView { RootItemsSource = new[] { a, TestNode.Make("B") } };
            IDisposable attached = TestHost.Attach(tree);
            attached.Dispose();
            Assert.AreEqual(0, (int)TableOf(tree).NumberOfRowsInSection(0), "No rows out of a window");

            // Expanded while out of the window, which the table never heard about
            tree.FindItemForSource(a).IsExpanded = true;
            using (TestHost.Attach(tree))
            {
                Assert.AreEqual(4, (int)TableOf(tree).NumberOfRowsInSection(0));
            }
        }

        [TestMethod]
        public async Task Tree_LeavingTheWindow_TearsDownEveryShownRowsContent ()
        {
            var made = new List<CountedContent>();
            var tree = new FlatTreeListView
            {
                RootItemsSource = new[] { TestNode.Make("A"), TestNode.Make("B"), TestNode.Make("C") },
                RowContentFactory = item =>
                {
                    var content = new CountedContent();
                    made.Add(content);
                    return content;
                },
            };

            IDisposable attached = TestHost.Attach(tree);
            await TestHost.LayOutAsync(tree);
            Assert.IsTrue(made.Count >= 3, $"Every row was built, {made.Count} were");

            attached.Dispose();
            Assert.AreEqual(0, made.Count(c => !c.IsTornDown), "Every row's content let go when the tree left the window");
        }

        [TestMethod]
        public void Tree_SingleSelection_KeepsOnlyTheFirstItemGiven ()
        {
            var tree = new FlatTreeListView { RootItemsSource = new[] { TestNode.Make("A"), TestNode.Make("B") } };
            using (TestHost.Attach(tree))
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
            var tree = new FlatTreeListView { SelectionMode = eFlatTreeSelectionMode.Multi, RootItemsSource = new[] { TestNode.Make("A"), TestNode.Make("B") } };
            using (TestHost.Attach(tree))
            {
                tree.SetSelection(new[] { tree.Items[0], tree.Items[1] });
                Assert.AreEqual(2, tree.SelectedItems.Count);
            }
        }

        [TestMethod]
        public void Tree_CollapsingTheParentOfASelectedRow_DeselectsIt ()
        {
            TestNode a = TestNode.Make("A", "A1");
            var tree = new FlatTreeListView { RootItemsSource = new[] { a } };
            using (TestHost.Attach(tree))
            {
                FlatTreeItem parent = tree.FindItemForSource(a);
                parent.IsExpanded = true;
                FlatTreeItem child = tree.Items[1];
                tree.SelectedItem = child;

                SelectionChange<FlatTreeItem> change = null;
                tree.SelectionChanged += _OnSelectionChanged;
                parent.IsExpanded = false;
                tree.SelectionChanged -= _OnSelectionChanged;

                Assert.IsNull(tree.SelectedItem);
                Assert.IsFalse(child.IsSelected);
                Assert.IsNotNull(change, "The deselection is reported");
                Assert.IsTrue(change.Removed.Contains(child));

                void _OnSelectionChanged (object sender, SelectionChange<FlatTreeItem> e) => change = e;
            }
        }

        [TestMethod]
        public void Tree_TrySelectItemForSource_ExpandsTheWayToIt ()
        {
            TestNode a = TestNode.Make("A", "A1");
            TestNode a1 = (TestNode)a.Children[0];
            var tree = new FlatTreeListView { RootItemsSource = new[] { a } };
            using (TestHost.Attach(tree))
            {
                Assert.IsTrue(tree.TrySelectItemForSource(a1, scrollIntoView: false));
                Assert.AreEqual(2, (int)TableOf(tree).NumberOfRowsInSection(0));
                Assert.AreSame(a1, tree.SelectedItem.Source);
            }
        }

        [TestMethod]
        public async Task Tree_Disabled_TakesNoTouchesAndDisablesEveryChevron ()
        {
            var tree = new FlatTreeListView { RootItemsSource = new[] { TestNode.Make("A", "A1"), TestNode.Make("B", "B1") }, Enabled = false };
            using (TestHost.Attach(tree))
            {
                await TestHost.LayOutAsync(tree);
                UITableView table = TableOf(tree);
                Assert.IsFalse(table.UserInteractionEnabled, "The rows take no touches");

                UIControl[] parts = TestHost.Descendants<UIControl>(table).ToArray();
                Assert.IsTrue(parts.Length >= 2, "Each expandable row has its chevron");
                Assert.IsTrue(parts.All(p => !p.Enabled), "Every part of every row is disabled");
            }
        }

        [TestMethod]
        public async Task Tree_EnabledAgain_TakesTouchesAgain ()
        {
            var tree = new FlatTreeListView { RootItemsSource = new[] { TestNode.Make("A", "A1") }, Enabled = false };
            using (TestHost.Attach(tree))
            {
                tree.Enabled = true;
                await TestHost.LayOutAsync(tree);
                UITableView table = TableOf(tree);
                Assert.IsTrue(table.UserInteractionEnabled);
                UIControl[] parts = TestHost.Descendants<UIControl>(table).ToArray();
                Assert.IsTrue(parts.Length >= 1, "The expandable row has its chevron");
                Assert.IsTrue(parts.All(p => p.Enabled), "Every row is built again, enabled");
            }
        }

        [TestMethod]
        public async Task Tree_RowShownAgainAfterItsDisplayEnded_DrawsTheSelectionItIsGiven ()
        {
            var tree = new FlatTreeListView { RootItemsSource = new[] { TestNode.Make("A"), TestNode.Make("B") } };
            using (TestHost.Attach(tree))
            {
                await TestHost.LayOutAsync(tree);
                UITableView table = TableOf(tree);
                NSIndexPath firstRow = NSIndexPath.FromRowSection(0, 0);
                UITableViewCell cell = table.CellAt(firstRow);
                Assert.IsNotNull(cell, "The first row is shown");

                EndDisplayThenShowAgain(table, cell, firstRow);

                // Nothing lays the row out in between, so only the row following its item can draw this
                tree.SelectedItem = tree.Items[0];
                Assert.IsTrue(
                    cell.AccessibilityTraits.HasFlag(UIAccessibilityTrait.Selected),
                    "A row the table shows again draws the selection it is given"
                );
            }
        }

        [TestMethod]
        public async Task Tree_RowShownAgainAfterItsDisplayEnded_HasContentThatIsNotTornDown ()
        {
            var made = new List<CountedContent>();
            var tree = new FlatTreeListView
            {
                RootItemsSource = new[] { TestNode.Make("A"), TestNode.Make("B") },
                RowContentFactory = item =>
                {
                    var content = new CountedContent();
                    made.Add(content);
                    return content;
                },
            };

            using (TestHost.Attach(tree))
            {
                await TestHost.LayOutAsync(tree);
                UITableView table = TableOf(tree);
                NSIndexPath firstRow = NSIndexPath.FromRowSection(0, 0);
                UITableViewCell cell = table.CellAt(firstRow);
                Assert.IsNotNull(cell, "The first row is shown");

                EndDisplayThenShowAgain(table, cell, firstRow);

                // A PropertyGrid row's content is its editors: torn down, they no longer reach their property
                Assert.IsTrue(
                    made.Any(c => !c.IsTornDown && c.IsDescendantOfView(cell)),
                    "A row the table shows again has content that is not torn down"
                );
            }
        }

        // ===========[ Helper Methods ]===================================
        private static UITableView TableOf (UIView view) => TestHost.Descendants<UITableView>(view).First();

        // UIKit can end a cell's display and later show that same cell again without asking the source for it. This
        // plays that through the table's source the way UIKit calls it: the end of display, then willDisplay, which
        // UIKit only sends to a source that answers it.
        private static void EndDisplayThenShowAgain (UITableView table, UITableViewCell cell, NSIndexPath indexPath)
        {
            var source = (UITableViewSource)table.Source;
            source.CellDisplayingEnded(table, cell, indexPath);
            if (source.RespondsToSelector(new Selector("tableView:willDisplayCell:forRowAtIndexPath:")))
            {
                source.WillDisplay(table, cell, indexPath);
            }
        }

        // ===========[ Subclasses/structs ]===================================
        private sealed class CountedContent : UIView, IFlatTreeRowContent
        {
            public CountedContent () : base(new CGRect(0, 0, 10, 10))
            {
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
