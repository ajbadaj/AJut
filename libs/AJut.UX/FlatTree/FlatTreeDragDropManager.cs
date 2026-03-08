namespace AJut.UX
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.Storage;
    using AJut.Tree;

    // ===========[ FlatTreeDropTarget ]============================================
    // Represents a computed drop location in the tree: which parent to insert
    // under, at what child index, and at what visual depth (for rendering the
    // insertion indicator). A null TargetParent means the drop is invalid.

    public class FlatTreeDropTarget
    {
        public static readonly FlatTreeDropTarget Invalid = new FlatTreeDropTarget(null, -1, -1);

        public FlatTreeDropTarget (IObservableTreeNode targetParent, int insertIndex, int targetDepth)
        {
            this.TargetParent = targetParent;
            this.InsertIndex = insertIndex;
            this.TargetDepth = targetDepth;
        }

        // ===========[ Properties ]===============================================

        /// The parent node to insert under.
        public IObservableTreeNode TargetParent { get; }

        /// The child index within TargetParent.Children to insert at.
        public int InsertIndex { get; }

        /// The tree depth of the insertion (for rendering the indicator line).
        public int TargetDepth { get; }

        /// True when this represents a valid drop location.
        public bool IsValid => this.TargetParent != null;
    }

    // ===========[ FlatTreeReorderEventArgs ]======================================
    // Fired before a drag/drop reorder is executed. Consumers can set Cancel=true
    // to prevent default execution (e.g. to wrap in undo/redo instead).

    public class FlatTreeReorderEventArgs : EventArgs
    {
        public FlatTreeReorderEventArgs (IObservableTreeNode[] items, IObservableTreeNode targetParent, int insertIndex)
        {
            this.Items = items;
            this.TargetParent = targetParent;
            this.InsertIndex = insertIndex;
        }

        public IObservableTreeNode[] Items { get; }
        public IObservableTreeNode TargetParent { get; }
        public int InsertIndex { get; }

        /// Set to true to prevent the default reorder from executing.
        public bool Cancel { get; set; }
    }

    // ===========[ FlatTreeExternalDropEventArgs ]=================================
    // Fired when an external item is dropped onto the tree. Consumers must handle
    // the actual insertion (this is just a notification with the target location).

    public class FlatTreeExternalDropEventArgs : EventArgs
    {
        public FlatTreeExternalDropEventArgs (object externalData, IObservableTreeNode targetParent, int insertIndex, int targetDepth)
        {
            this.ExternalData = externalData;
            this.TargetParent = targetParent;
            this.InsertIndex = insertIndex;
            this.TargetDepth = targetDepth;
        }

        public object ExternalData { get; }
        public IObservableTreeNode TargetParent { get; }
        public int InsertIndex { get; }
        public int TargetDepth { get; }
        public bool Handled { get; set; }
    }

    // ===========[ FlatTreeDragDropManager ]=======================================
    // Pure computation: given a flat tree store and a cursor position, computes
    // where a drop would land. Also provides reorder execution with multi-select
    // index adjustment. No UI framework dependency.

    public static class FlatTreeDragDropManager
    {
        // ===========[ Drop Target Computation ]===================================

        /// <summary>
        /// Computes the drop target for a drag/drop reorder operation.
        /// </summary>
        /// <param name="store">The flat tree store backing the list.</param>
        /// <param name="draggedItems">The items being dragged (for exclusion).</param>
        /// <param name="hoverStoreIndex">Index in the flat store that the cursor is over.</param>
        /// <param name="cursorYFraction">0.0 = top of row, 1.0 = bottom of row.</param>
        /// <param name="cursorX">Horizontal pixel position of the cursor within the list area.</param>
        /// <param name="indentSize">Pixels per tree depth level (TreeDepthIndentSize).</param>
        public static FlatTreeDropTarget ComputeDropTarget (
            ObservableFlatTreeStore<FlatTreeItem> store,
            FlatTreeItem[] draggedItems,
            int hoverStoreIndex,
            double cursorYFraction,
            double cursorX,
            double indentSize)
        {
            if (store == null || store.Count == 0 || draggedItems == null || draggedItems.Length == 0)
            {
                return FlatTreeDropTarget.Invalid;
            }

            // Clamp hover index to valid range
            hoverStoreIndex = Math.Clamp(hoverStoreIndex, 0, store.Count - 1);

            // 1. Determine the gap: we insert between two rows.
            //    If cursor is in the top half of the row, the gap is ABOVE the row.
            //    If cursor is in the bottom half, the gap is BELOW the row.
            FlatTreeItem above;
            FlatTreeItem below;

            if (cursorYFraction < 0.5 && hoverStoreIndex > 0)
            {
                // Gap is between [hoverStoreIndex - 1] and [hoverStoreIndex]
                above = store[hoverStoreIndex - 1];
                below = store[hoverStoreIndex];
            }
            else
            {
                // Gap is between [hoverStoreIndex] and [hoverStoreIndex + 1]
                above = store[hoverStoreIndex];
                below = (hoverStoreIndex + 1 < store.Count) ? store[hoverStoreIndex + 1] : null;
            }

            // Skip dragged items in the above/below determination
            above = FindNonDraggedItem(store, above, draggedItems, -1);
            below = FindNonDraggedItemForward(store, below, draggedItems);

            if (above == null)
            {
                return FlatTreeDropTarget.Invalid;
            }

            return ComputeDropTargetForGap(above, below, cursorX, indentSize);
        }

        /// <summary>
        /// Computes the drop target for a gap between two specific rows.
        /// Useful for unit testing the core algorithm directly.
        /// </summary>
        public static FlatTreeDropTarget ComputeDropTargetForGap (
            FlatTreeItem above,
            FlatTreeItem below,
            double cursorX,
            double indentSize)
        {
            if (above == null)
            {
                return FlatTreeDropTarget.Invalid;
            }

            // 2. Determine valid depth range for this gap
            int maxDepth = above.IsExpandable && above.IsExpanded
                ? above.TreeDepth + 1
                : above.TreeDepth;

            // For the special case where 'above' can have children but currently has none
            // and is expanded (or could accept children), allow inserting as first child
            if (above.IsExpandable && above.Source.Children.Count == 0)
            {
                maxDepth = above.TreeDepth + 1;
            }

            // Minimum depth is always 1 (children-of-root level). Depth 0 returns
            // Invalid naturally since root.Parent is null. This allows cursor X to
            // target shallower depths even between siblings deep in the tree —
            // ComputeParentAndIndex walks up from 'above' to find the correct ancestor.
            int minDepth = 1;

            // Ensure valid range
            if (minDepth > maxDepth)
            {
                // Shouldn't happen in a well-formed tree, but guard against it
                return FlatTreeDropTarget.Invalid;
            }

            // 3. Map cursor X to a depth level
            // Account for the expander column offset (~18px + indent)
            int depthFromCursor = (int)Math.Floor(cursorX / Math.Max(indentSize, 1.0));
            int targetDepth = Math.Clamp(depthFromCursor, minDepth, maxDepth);

            // 4. Compute the actual parent and insert index for this depth
            return ComputeParentAndIndex(above, targetDepth);
        }

        // ===========[ Validation ]================================================

        /// <summary>
        /// Validates whether the given drop target is legal for the dragged items.
        /// Checks: no drop onto self or descendants, root items not draggable,
        /// and optionally a custom predicate.
        /// </summary>
        public static bool ValidateDropTarget (
            FlatTreeItem[] draggedItems,
            FlatTreeDropTarget target,
            Func<IObservableTreeNode, IObservableTreeNode, bool> canDropPredicate = null)
        {
            if (target == null || !target.IsValid)
            {
                return false;
            }

            foreach (FlatTreeItem item in draggedItems)
            {
                // Root items (depth 0) and false roots are never draggable
                if (item.TreeDepth <= 0 || item.IsFalseRoot)
                {
                    return false;
                }

                // Can't drop onto self
                if (item.Source == target.TargetParent)
                {
                    return false;
                }

                // Can't drop onto a descendant of self
                if (IsDescendantOf(target.TargetParent, item.Source))
                {
                    return false;
                }

                // Custom predicate check
                if (canDropPredicate != null && !canDropPredicate(item.Source, target.TargetParent))
                {
                    return false;
                }
            }

            return true;
        }

        // ===========[ Reorder Execution ]=========================================

        /// <summary>
        /// Executes a reorder operation: removes sources from their old parents
        /// and inserts them at the target location. Handles multi-select index
        /// adjustment correctly.
        /// </summary>
        public static void ExecuteReorder (IObservableTreeNode[] sourceNodes, FlatTreeDropTarget target)
        {
            if (target == null || !target.IsValid || sourceNodes == null || sourceNodes.Length == 0)
            {
                return;
            }

            IObservableTreeNode targetParent = target.TargetParent;
            int insertIndex = target.InsertIndex;

            // Count how many of the source nodes are children of the target parent
            // at indices BEFORE the insert index - these removals will shift the
            // insert index down by one each.
            int indexAdjustment = 0;
            foreach (IObservableTreeNode source in sourceNodes)
            {
                if (source.Parent == targetParent)
                {
                    int currentIndex = IndexOfChild(targetParent, source);
                    if (currentIndex >= 0 && currentIndex < insertIndex)
                    {
                        ++indexAdjustment;
                    }
                }
            }

            // Remove all source nodes from their current parents
            foreach (IObservableTreeNode source in sourceNodes)
            {
                source.Parent?.RemoveChild(source);
            }

            // Adjust the insert index
            insertIndex -= indexAdjustment;
            insertIndex = Math.Max(0, Math.Min(insertIndex, targetParent.Children.Count));

            // Insert all source nodes at the adjusted index
            for (int i = 0; i < sourceNodes.Length; ++i)
            {
                targetParent.InsertChild(insertIndex + i, sourceNodes[i]);
            }
        }

        // ===========[ Visual Helpers ]============================================

        /// <summary>
        /// Computes the flat-store index of the parent row for the given drop target,
        /// so the UI can draw a connector line from the insertion point up to the parent.
        /// Returns -1 if the parent is not visible in the store.
        /// </summary>
        public static int FindParentStoreIndex (ObservableFlatTreeStore<FlatTreeItem> store, FlatTreeDropTarget target)
        {
            if (target == null || !target.IsValid || store == null)
            {
                return -1;
            }

            for (int i = 0; i < store.Count; ++i)
            {
                if (store[i].Source == target.TargetParent)
                {
                    return i;
                }
            }

            return -1;
        }

        // ===========[ Private Helpers ]==========================================

        private static FlatTreeDropTarget ComputeParentAndIndex (FlatTreeItem above, int targetDepth)
        {
            if (targetDepth == above.TreeDepth + 1)
            {
                // Insert as first child of 'above'
                return new FlatTreeDropTarget(above.Source, 0, targetDepth);
            }

            // Walk up from 'above' to find the item at targetDepth
            FlatTreeItem target = above;
            while (target != null && target.TreeDepth > targetDepth)
            {
                target = target.Parent;
            }

            if (target == null || target.Source?.Parent == null)
            {
                return FlatTreeDropTarget.Invalid;
            }

            // Insert as next sibling of target
            IObservableTreeNode sourceNode = target.Source;
            IObservableTreeNode parentNode = sourceNode.Parent;
            int indexInParent = IndexOfChild(parentNode, sourceNode);
            if (indexInParent < 0)
            {
                return FlatTreeDropTarget.Invalid;
            }

            return new FlatTreeDropTarget(parentNode, indexInParent + 1, targetDepth);
        }

        private static int IndexOfChild (IObservableTreeNode parent, IObservableTreeNode child)
        {
            for (int i = 0; i < parent.Children.Count; ++i)
            {
                if (parent.Children[i] == child)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsDescendantOf (IObservableTreeNode potentialDescendant, IObservableTreeNode potentialAncestor)
        {
            IObservableTreeNode current = potentialDescendant;
            while (current != null)
            {
                if (current == potentialAncestor)
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }

        /// Walk backward from 'start' to find the nearest non-dragged item.
        private static FlatTreeItem FindNonDraggedItem (
            ObservableFlatTreeStore<FlatTreeItem> store,
            FlatTreeItem start,
            FlatTreeItem[] draggedItems,
            int direction)
        {
            if (start == null)
            {
                return null;
            }

            HashSet<FlatTreeItem> dragSet = new HashSet<FlatTreeItem>(draggedItems);
            if (!dragSet.Contains(start))
            {
                return start;
            }

            int startIndex = store.IndexOf(start);
            if (startIndex < 0)
            {
                return null;
            }

            for (int i = startIndex + direction; i >= 0 && i < store.Count; i += direction)
            {
                if (!dragSet.Contains(store[i]))
                {
                    return store[i];
                }
            }

            return null;
        }

        /// Walk forward from 'start' to find the nearest non-dragged item.
        private static FlatTreeItem FindNonDraggedItemForward (
            ObservableFlatTreeStore<FlatTreeItem> store,
            FlatTreeItem start,
            FlatTreeItem[] draggedItems)
        {
            if (start == null)
            {
                return null;
            }

            return FindNonDraggedItem(store, start, draggedItems, 1);
        }
    }
}
