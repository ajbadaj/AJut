namespace AJut.UX.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.Storage;
    using AJut.Tree;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    // ===========[ FlatTreeDragDropTests ]=========================================
    // Unit tests for FlatTreeDragDropManager - the shared drag/drop reorder logic.
    // Tests cover:
    //   - Drop target computation for various gap positions and cursor depths
    //   - Validation (no self-drop, no ancestor-drop, root protection)
    //   - Reorder execution with single and multi-select index adjustment
    //   - Edge cases (end of list, single child, empty tree)

    [TestClass]
    public class FlatTreeDragDropTests
    {
        // ===========[ Test Tree Structure ]=======================================
        //
        // Root (depth 0)
        //   Child A (depth 1)     - index 0 under Root
        //     GC A1 (depth 2)     - index 0 under Child A
        //     GC A2 (depth 2)     - index 1 under Child A
        //   Child B (depth 1)     - index 1 under Root
        //     GC B1 (depth 2)     - index 0 under Child B
        //   Child C (depth 1)     - index 2 under Root

        private const double kIndentSize = 16.0;

        private static TestTree BuildTestTree ()
        {
            var root = new TestNode("Root");
            root.CanHaveChildren = true;

            var childA = root.AddChild("Child A");
            childA.CanHaveChildren = true;
            var gcA1 = childA.AddChild("GC A1");
            var gcA2 = childA.AddChild("GC A2");

            var childB = root.AddChild("Child B");
            childB.CanHaveChildren = true;
            var gcB1 = childB.AddChild("GC B1");

            var childC = root.AddChild("Child C");

            var flatRoot = FlatTreeItem.CreateRoot(root, kIndentSize, startExpanded: true);
            var store = new ObservableFlatTreeStore<FlatTreeItem>();
            store.IncludeRoot = false;
            store.RootNode = flatRoot;

            return new TestTree
            {
                Root = root,
                ChildA = childA,
                GcA1 = gcA1,
                GcA2 = gcA2,
                ChildB = childB,
                GcB1 = gcB1,
                ChildC = childC,
                FlatRoot = flatRoot,
                Store = store,
            };
        }

        // ===========[ ComputeDropTargetForGap Tests ]=============================

        [TestMethod]
        public void DropTarget_BetweenGcA2AndChildB_AtDepth2_InsertsSiblingOfGcA2 ()
        {
            var t = BuildTestTree();
            var above = FindFlatItem(t.Store, t.GcA2);
            var below = FindFlatItem(t.Store, t.ChildB);

            // cursorX = depth 2 * 16 = 32
            var target = FlatTreeDragDropManager.ComputeDropTargetForGap(above, below, 32.0, kIndentSize);

            Assert.IsTrue(target.IsValid);
            Assert.AreEqual(t.ChildA, target.TargetParent);
            Assert.AreEqual(2, target.InsertIndex); // after GC A2 (index 1) -> insert at 2
            Assert.AreEqual(2, target.TargetDepth);
        }

        [TestMethod]
        public void DropTarget_BetweenGcA2AndChildB_AtDepth1_InsertsSiblingOfChildA ()
        {
            var t = BuildTestTree();
            var above = FindFlatItem(t.Store, t.GcA2);
            var below = FindFlatItem(t.Store, t.ChildB);

            // cursorX = depth 1 * 16 = 16
            var target = FlatTreeDragDropManager.ComputeDropTargetForGap(above, below, 16.0, kIndentSize);

            Assert.IsTrue(target.IsValid);
            Assert.AreEqual(t.Root, target.TargetParent);
            Assert.AreEqual(1, target.InsertIndex); // after Child A (index 0) -> insert at 1
            Assert.AreEqual(1, target.TargetDepth);
        }

        [TestMethod]
        public void DropTarget_BetweenChildAAndGcA1_InsertsAsFirstChildOfA ()
        {
            var t = BuildTestTree();
            var above = FindFlatItem(t.Store, t.ChildA);
            var below = FindFlatItem(t.Store, t.GcA1);

            // cursorX = depth 2 * 16 = 32
            var target = FlatTreeDragDropManager.ComputeDropTargetForGap(above, below, 32.0, kIndentSize);

            Assert.IsTrue(target.IsValid);
            Assert.AreEqual(t.ChildA, target.TargetParent);
            Assert.AreEqual(0, target.InsertIndex);
            Assert.AreEqual(2, target.TargetDepth);
        }

        [TestMethod]
        public void DropTarget_BetweenGcA1AndGcA2_InsertsBetweenSiblings ()
        {
            var t = BuildTestTree();
            var above = FindFlatItem(t.Store, t.GcA1);
            var below = FindFlatItem(t.Store, t.GcA2);

            var target = FlatTreeDragDropManager.ComputeDropTargetForGap(above, below, 32.0, kIndentSize);

            Assert.IsTrue(target.IsValid);
            Assert.AreEqual(t.ChildA, target.TargetParent);
            Assert.AreEqual(1, target.InsertIndex); // after GC A1 (index 0) -> insert at 1
            Assert.AreEqual(2, target.TargetDepth);
        }

        [TestMethod]
        public void DropTarget_AfterChildC_AtDepth1_InsertsAsLastSiblingOfRoot ()
        {
            var t = BuildTestTree();
            var above = FindFlatItem(t.Store, t.ChildC);

            var target = FlatTreeDragDropManager.ComputeDropTargetForGap(above, null, 16.0, kIndentSize);

            Assert.IsTrue(target.IsValid);
            Assert.AreEqual(t.Root, target.TargetParent);
            Assert.AreEqual(3, target.InsertIndex); // after Child C (index 2) -> insert at 3
            Assert.AreEqual(1, target.TargetDepth);
        }

        [TestMethod]
        public void DropTarget_AfterGcB1_AtDepth2_InsertsAsSiblingOfGcB1 ()
        {
            var t = BuildTestTree();
            var above = FindFlatItem(t.Store, t.GcB1);
            var below = FindFlatItem(t.Store, t.ChildC);

            var target = FlatTreeDragDropManager.ComputeDropTargetForGap(above, below, 32.0, kIndentSize);

            Assert.IsTrue(target.IsValid);
            Assert.AreEqual(t.ChildB, target.TargetParent);
            Assert.AreEqual(1, target.InsertIndex);
            Assert.AreEqual(2, target.TargetDepth);
        }

        [TestMethod]
        public void DropTarget_AfterGcB1_AtDepth1_InsertsAsSiblingOfChildB ()
        {
            var t = BuildTestTree();
            var above = FindFlatItem(t.Store, t.GcB1);
            var below = FindFlatItem(t.Store, t.ChildC);

            var target = FlatTreeDragDropManager.ComputeDropTargetForGap(above, below, 16.0, kIndentSize);

            Assert.IsTrue(target.IsValid);
            Assert.AreEqual(t.Root, target.TargetParent);
            Assert.AreEqual(2, target.InsertIndex); // after Child B (index 1) -> insert at 2
            Assert.AreEqual(1, target.TargetDepth);
        }

        [TestMethod]
        public void DropTarget_CursorXClampsToMaxDepth ()
        {
            var t = BuildTestTree();
            var above = FindFlatItem(t.Store, t.GcA1);
            var below = FindFlatItem(t.Store, t.GcA2);

            // cursorX way to the right (depth 5 equivalent) - should clamp to maxDepth (2)
            var target = FlatTreeDragDropManager.ComputeDropTargetForGap(above, below, 80.0, kIndentSize);

            Assert.IsTrue(target.IsValid);
            Assert.AreEqual(2, target.TargetDepth);
        }

        [TestMethod]
        public void DropTarget_CursorXClampsToMinDepth ()
        {
            var t = BuildTestTree();
            var above = FindFlatItem(t.Store, t.GcA2);
            var below = FindFlatItem(t.Store, t.ChildB);

            // cursorX at 0 (depth 0) - should clamp to minDepth (1)
            var target = FlatTreeDragDropManager.ComputeDropTargetForGap(above, below, 0.0, kIndentSize);

            Assert.IsTrue(target.IsValid);
            Assert.AreEqual(1, target.TargetDepth);
        }

        // ===========[ Validation Tests ]==========================================

        [TestMethod]
        public void Validate_RootItem_Fails ()
        {
            var t = BuildTestTree();
            var flatRoot = t.FlatRoot;
            // The root itself is depth -1 (false root for IncludeRoot=false)
            // But the visible root children (ChildA etc) are at depth 1 - which ARE draggable

            // Create a scenario where we try to drag the actual root
            var rootFlatItem = t.FlatRoot;
            var target = new FlatTreeDropTarget(t.ChildA, 0, 2);
            bool valid = FlatTreeDragDropManager.ValidateDropTarget(new[] { rootFlatItem }, target);
            Assert.IsFalse(valid);
        }

        [TestMethod]
        public void Validate_DropOntoSelf_Fails ()
        {
            var t = BuildTestTree();
            var flatChildA = FindFlatItem(t.Store, t.ChildA);
            var target = new FlatTreeDropTarget(t.ChildA, 0, 2);
            bool valid = FlatTreeDragDropManager.ValidateDropTarget(new[] { flatChildA }, target);
            Assert.IsFalse(valid);
        }

        [TestMethod]
        public void Validate_DropOntoDescendant_Fails ()
        {
            var t = BuildTestTree();
            var flatChildA = FindFlatItem(t.Store, t.ChildA);
            // Try to drop Child A onto GC A1 (a descendant)
            var target = new FlatTreeDropTarget(t.GcA1, 0, 3);
            bool valid = FlatTreeDragDropManager.ValidateDropTarget(new[] { flatChildA }, target);
            Assert.IsFalse(valid);
        }

        [TestMethod]
        public void Validate_ValidDrop_Succeeds ()
        {
            var t = BuildTestTree();
            var flatGcB1 = FindFlatItem(t.Store, t.GcB1);
            // Drop GC B1 onto Child A - valid (not a descendant)
            var target = new FlatTreeDropTarget(t.ChildA, 2, 2);
            bool valid = FlatTreeDragDropManager.ValidateDropTarget(new[] { flatGcB1 }, target);
            Assert.IsTrue(valid);
        }

        [TestMethod]
        public void Validate_CustomPredicate_CanReject ()
        {
            var t = BuildTestTree();
            var flatGcB1 = FindFlatItem(t.Store, t.GcB1);
            var target = new FlatTreeDropTarget(t.ChildA, 2, 2);

            // Custom predicate rejects everything
            bool valid = FlatTreeDragDropManager.ValidateDropTarget(
                new[] { flatGcB1 },
                target,
                (source, parent) => false
            );
            Assert.IsFalse(valid);
        }

        [TestMethod]
        public void Validate_CustomPredicate_CanAllow ()
        {
            var t = BuildTestTree();
            var flatGcB1 = FindFlatItem(t.Store, t.GcB1);
            var target = new FlatTreeDropTarget(t.ChildA, 2, 2);

            bool valid = FlatTreeDragDropManager.ValidateDropTarget(
                new[] { flatGcB1 },
                target,
                (source, parent) => true
            );
            Assert.IsTrue(valid);
        }

        // ===========[ ExecuteReorder Tests ]======================================

        [TestMethod]
        public void Reorder_SingleItem_MovesToNewParent ()
        {
            var t = BuildTestTree();

            // Move GC B1 to be a child of Child A (after GC A2)
            var target = new FlatTreeDropTarget(t.ChildA, 2, 2);
            FlatTreeDragDropManager.ExecuteReorder(new[] { t.GcB1 }, target);

            Assert.AreEqual(3, t.ChildA.Children.Count); // A1, A2, B1
            Assert.AreEqual(t.GcB1, t.ChildA.Children[2]);
            Assert.AreEqual(0, t.ChildB.Children.Count);
        }

        [TestMethod]
        public void Reorder_SingleItem_MoveWithinSameParent_Forward ()
        {
            var t = BuildTestTree();

            // Move GC A1 to after GC A2 (index 2 in Child A, but after removing A1 it becomes index 1)
            var target = new FlatTreeDropTarget(t.ChildA, 2, 2);
            FlatTreeDragDropManager.ExecuteReorder(new[] { t.GcA1 }, target);

            Assert.AreEqual(2, t.ChildA.Children.Count);
            Assert.AreEqual(t.GcA2, t.ChildA.Children[0]);
            Assert.AreEqual(t.GcA1, t.ChildA.Children[1]);
        }

        [TestMethod]
        public void Reorder_SingleItem_MoveWithinSameParent_Backward ()
        {
            var t = BuildTestTree();

            // Move GC A2 to before GC A1 (index 0 in Child A)
            var target = new FlatTreeDropTarget(t.ChildA, 0, 2);
            FlatTreeDragDropManager.ExecuteReorder(new[] { t.GcA2 }, target);

            Assert.AreEqual(2, t.ChildA.Children.Count);
            Assert.AreEqual(t.GcA2, t.ChildA.Children[0]);
            Assert.AreEqual(t.GcA1, t.ChildA.Children[1]);
        }

        [TestMethod]
        public void Reorder_MultiSelect_SameParent_MoveUp ()
        {
            var t = BuildTestTree();

            // Move Child B and Child C to before Child A (index 0 in Root)
            var target = new FlatTreeDropTarget(t.Root, 0, 1);
            FlatTreeDragDropManager.ExecuteReorder(new IObservableTreeNode[] { t.ChildB, t.ChildC }, target);

            Assert.AreEqual(3, t.Root.Children.Count);
            Assert.AreEqual(t.ChildB, t.Root.Children[0]);
            Assert.AreEqual(t.ChildC, t.Root.Children[1]);
            Assert.AreEqual(t.ChildA, t.Root.Children[2]);
        }

        [TestMethod]
        public void Reorder_MultiSelect_DifferentParents ()
        {
            var t = BuildTestTree();

            // Move GC A1 and GC B1 to be children of Child C
            t.ChildC.CanHaveChildren = true;
            var target = new FlatTreeDropTarget(t.ChildC, 0, 2);
            FlatTreeDragDropManager.ExecuteReorder(new IObservableTreeNode[] { t.GcA1, t.GcB1 }, target);

            Assert.AreEqual(1, t.ChildA.Children.Count); // only GC A2 left
            Assert.AreEqual(0, t.ChildB.Children.Count);
            Assert.AreEqual(2, t.ChildC.Children.Count);
            Assert.AreEqual(t.GcA1, t.ChildC.Children[0]);
            Assert.AreEqual(t.GcB1, t.ChildC.Children[1]);
        }

        [TestMethod]
        public void Reorder_IndexAdjustment_WhenRemovingAboveInsertPoint ()
        {
            var t = BuildTestTree();

            // Move Child A (index 0) to after Child C (index 2 -> insert at 3).
            // After removing Child A, Child B is at 0 and Child C is at 1,
            // so the adjusted insert index should be 2 (3 - 1 = 2).
            var target = new FlatTreeDropTarget(t.Root, 3, 1);
            FlatTreeDragDropManager.ExecuteReorder(new[] { (IObservableTreeNode)t.ChildA }, target);

            Assert.AreEqual(3, t.Root.Children.Count);
            Assert.AreEqual(t.ChildB, t.Root.Children[0]);
            Assert.AreEqual(t.ChildC, t.Root.Children[1]);
            Assert.AreEqual(t.ChildA, t.Root.Children[2]);
        }

        // ===========[ FindParentStoreIndex Tests ]================================

        [TestMethod]
        public void FindParentStoreIndex_ReturnsCorrectIndex ()
        {
            var t = BuildTestTree();
            var target = new FlatTreeDropTarget(t.ChildA, 0, 2);
            int index = FlatTreeDragDropManager.FindParentStoreIndex(t.Store, target);

            // Child A should be at store index 0 (first visible item, root excluded)
            Assert.AreEqual(0, index);
        }

        [TestMethod]
        public void FindParentStoreIndex_RootParent_ReturnsNegativeOne ()
        {
            var t = BuildTestTree();
            // Root is not in the store (IncludeRoot=false)
            var target = new FlatTreeDropTarget(t.Root, 0, 1);
            int index = FlatTreeDragDropManager.FindParentStoreIndex(t.Store, target);

            Assert.AreEqual(-1, index);
        }

        // ===========[ Integration: ComputeDropTarget with store ]=================

        [TestMethod]
        public void ComputeDropTarget_ViaCursorPosition_BottomHalfOfRow ()
        {
            var t = BuildTestTree();
            // Cursor in bottom half of GC A2 (store index 2), X at depth 1
            // Expected: gap is between GC A2 and Child B, depth 1 -> sibling of Child A
            int gcA2Index = t.Store.IndexOf(FindFlatItem(t.Store, t.GcA2));
            var draggedItems = new[] { FindFlatItem(t.Store, t.GcB1) };

            var target = FlatTreeDragDropManager.ComputeDropTarget(
                t.Store, draggedItems, gcA2Index, 0.75, 16.0, kIndentSize
            );

            Assert.IsTrue(target.IsValid);
            Assert.AreEqual(t.Root, target.TargetParent);
            Assert.AreEqual(1, target.InsertIndex);
        }

        [TestMethod]
        public void ComputeDropTarget_ViaCursorPosition_TopHalfOfRow ()
        {
            var t = BuildTestTree();
            // Cursor in top half of Child B (store index 3), X at depth 1
            // Expected: gap is between GC A2 and Child B, depth 1 -> sibling of Child A
            int childBIndex = t.Store.IndexOf(FindFlatItem(t.Store, t.ChildB));
            var draggedItems = new[] { FindFlatItem(t.Store, t.GcB1) };

            var target = FlatTreeDragDropManager.ComputeDropTarget(
                t.Store, draggedItems, childBIndex, 0.25, 16.0, kIndentSize
            );

            Assert.IsTrue(target.IsValid);
            Assert.AreEqual(t.Root, target.TargetParent);
            Assert.AreEqual(1, target.InsertIndex);
        }

        // ===========[ Helpers ]===================================================

        private static FlatTreeItem FindFlatItem (ObservableFlatTreeStore<FlatTreeItem> store, IObservableTreeNode source)
        {
            // Also check the root (which may not be in the store if IncludeRoot=false)
            if (store.RootNode?.Source == source)
            {
                return store.RootNode;
            }

            foreach (FlatTreeItem item in store)
            {
                if (item.Source == source)
                {
                    return item;
                }
            }

            // Search all items including hidden ones
            foreach (FlatTreeItem item in TreeTraversal<FlatTreeItem>.All(store.RootNode))
            {
                if (item.Source == source)
                {
                    return item;
                }
            }

            return null;
        }

        // ===========[ Test Support Types ]========================================

        private class TestTree
        {
            public TestNode Root;
            public TestNode ChildA;
            public TestNode GcA1;
            public TestNode GcA2;
            public TestNode ChildB;
            public TestNode GcB1;
            public TestNode ChildC;
            public FlatTreeItem FlatRoot;
            public ObservableFlatTreeStore<FlatTreeItem> Store;
        }

        private class TestNode : ObservableTreeNode<TestNode>
        {
            public TestNode (string name)
            {
                this.Name = name;
            }

            public string Name { get; }

            public TestNode AddChild (string name)
            {
                var child = new TestNode(name);
                this.InsertChild(this.Children.Count, child);
                return child;
            }

            public override string ToString () => this.Name;
        }
    }
}
