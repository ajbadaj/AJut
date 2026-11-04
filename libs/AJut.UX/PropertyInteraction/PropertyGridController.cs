namespace AJut.UX.PropertyInteraction
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using AJut.Storage;

    /// <summary>
    /// What a toolkit's PropertyGrid view gives the <see cref="PropertyGridController"/>: its sources (through
    /// <see cref="IPropertyGrid"/>), and somewhere to show the top-level property rows.
    /// </summary>
    public interface IPropertyGridHost : IPropertyGrid
    {
        /// <summary>Shows these top-level targets (the hidden root's children) in the grid's tree view; null clears it</summary>
        void ShowRootItems (IReadOnlyList<IObservableTreeNode>? rootItems);
    }

    // ===========[ PropertyGridController ]=====================================
    // Everything a PropertyGrid does that never touches a view, shared by every toolkit's grid:
    //  - Rebuilding the edit targets when a source is set, and clearing them when it is unset.
    //  - Mirroring external edits: the source's PropertyChanged recaches the affected targets.
    //  - Following every target's SourceCommitted: raising PropertyTreeChanged, re-subscribing a
    //    list's rebuilt children, recaching everything after a Button runs (a button can change
    //    the source without raising PropertyChanged), and re-running ShowIf/HideIf.
    //  - Tearing all of that down: every per-target subscription and each target's own hooks, on
    //    every rebuild, on a source being unset, and on Dispose.
    //  - Dropping the source to grid subscription while the view is out of the window, so a long
    //    lived source never pins a grid that was dropped without being cleaned up, and catching up
    //    when it comes back: a rebuild if the ItemsSource's items changed while it was out, otherwise
    //    a full refresh that keeps the tree.
    //  - Selection by source object, and reordering list elements by drag.
    //
    // The toolkit's view owns the tree view, the rows and the editors. It calls the On* methods
    // from its own source setters and window attach/detach, and shows what ShowRootItems hands it.
    [RequiresUnreferencedCode(PropertyGridTrimming.kMessage)]
    [RequiresDynamicCode(PropertyGridTrimming.kListMessage)]
    public sealed class PropertyGridController : IDisposable
    {
        private readonly IPropertyGridHost m_host;
        private readonly PropertyGridManager m_manager;
        private IReadOnlyList<IObservableTreeNode>? m_rootItems;

        // Exactly what this controller is subscribed to from the ItemsSource: its collection, and each
        // item. Unsubscribing walks these rather than the current ItemsSource, because by then the
        // source may have changed (a replaced plain list, a Reset that gives no removed items) and no
        // longer says what was subscribed.
        private INotifyCollectionChanged? m_followedCollection;
        private readonly List<INotifyPropertyChanged> m_followedItems = new List<INotifyPropertyChanged>();

        // The single source this controller is subscribed to, for the same reason
        private INotifyPropertyChanged? m_followedSingleSource;

        // The ItemsSource items the shown tree was built from, in order, or null when the tree was not
        // built from an ItemsSource. While detached nothing follows the collection, so on reattach this
        // is how the controller tells whether the source gained, lost or reordered items while it was away.
        private object[]? m_builtFromItems;

        // Last source object reported as changed by OnSelectedTargetChanged, so a selection moving
        // between two rows inside the same list element's subtree does not count as a change
        private object? m_lastSelectedSourceObject;

        // ===========[ Setup/Construction/Teardown ]===========================
        public PropertyGridController (IPropertyGridHost host)
        {
            m_host = host;
            m_manager = new PropertyGridManager(host);
        }

        /// <summary>
        /// Lets go of the sources (the single source, the ItemsSource collection and its items), then drops every
        /// per-target subscription and each target's own hooks while the tree is still walkable, then clears the
        /// manager. The manager's own Dispose only clears collections; without the teardown first, those
        /// subscriptions (and the whole source graph they reach) outlive the grid.
        /// </summary>
        public void Dispose ()
        {
            this.UnfollowSingleSource();
            this.UnfollowItemsSource();
            this.TeardownTargetSubscriptions();
            m_manager.Dispose();
            m_rootItems = null;
            m_builtFromItems = null;
            m_lastSelectedSourceObject = null;
        }

        // ===========[ Events ]================================================
        /// <summary>Raised whenever any property in the tree is edited through the grid</summary>
        public event EventHandler? PropertyTreeChanged;

        private void OnSourceItemPropertyChanged (object? sender, PropertyChangedEventArgs e)
        {
            // Snapshot: RecacheEditValue on a list target may rebuild children, which changes Items mid enumeration
            PropertyEditTarget[] targets = m_manager.Items.OfType<PropertyEditTarget>().ToArray();
            foreach (PropertyEditTarget target in targets)
            {
                if (target.ShouldEvaluateFor(e.PropertyName!))
                {
                    target.RecacheEditValue();
                }
            }

            // An external change may flip a ShowIf/HideIf condition
            m_manager.UpdateConditionalVisibility();
        }

        private void OnItemsSourceCollectionChanged (object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Re-follow the whole collection rather than applying OldItems and NewItems: a Reset gives
            // no OldItems, so only the followed list knows what to let go of. The items come from the
            // grid's ItemsSource, not the sender, since a wrapper forwarding an inner collection's events
            // can raise them with any sender.
            this.UnfollowItems();
            if (m_host.ItemsSource != null)
            {
                this.FollowItems(m_host.ItemsSource);
            }

            this.RebuildEditTargets();
        }

        private void OnAnyTargetPropertyChanged (object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != PropertyEditTarget.SourceCommittedPropertyName)
            {
                return;
            }

            this.PropertyTreeChanged?.Invoke(this, EventArgs.Empty);

            // A list add, remove or reorder rebuilds the list's children as new targets, which are not
            // subscribed yet; re-subscribe everything under the list so their edits are followed too
            if (sender is PropertyEditTarget { IsListEditor: true } listTarget)
            {
                foreach (PropertyEditTarget child in WalkAllTargets(listTarget))
                {
                    child.PropertyChanged -= this.OnAnyTargetPropertyChanged;
                    child.PropertyChanged += this.OnAnyTargetPropertyChanged;
                }
            }

            // A button action may change the source without raising PropertyChanged, so re-read everything
            if (sender is PropertyEditTarget { Editor: PropertyGridRowRules.kButtonEditorKey } && m_manager.RootNode != null)
            {
                foreach (PropertyEditTarget target in WalkAllTargets(m_manager.RootNode))
                {
                    if (target.Editor != PropertyGridRowRules.kButtonEditorKey)
                    {
                        target.RecacheEditValue();
                    }
                }
            }

            // An edit may flip a ShowIf/HideIf condition; toggle the affected targets without a rebuild
            m_manager.UpdateConditionalVisibility();
        }

        // ===========[ Properties ]============================================
        /// <summary>The manager that builds the edit targets</summary>
        public PropertyGridManager Manager => m_manager;

        /// <summary>The top-level targets (the hidden root's children), or null when there is no source</summary>
        public IReadOnlyList<IObservableTreeNode>? RootItems => m_rootItems;

        /// <summary>
        /// True when the whole grid is read-only: no row's value can be edited, no button row runs, no list element is
        /// added, removed or dragged to reorder, and nothing resets to its default. The toolkit's view sets this from its
        /// own IsReadOnly and pushes the state to its rows (see <see cref="PropertyGridRowRules"/>); the controller
        /// refuses list drags itself, through <see cref="CanDragRow"/>.
        /// </summary>
        public bool IsReadOnly { get; set; }

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Call when the grid's single source changes. A grid whose ItemsSource should be cleared by this does
        /// that itself, before calling.
        /// </summary>
        public void OnSingleItemSourceChanged (object? newValue)
        {
            // Let go of the single source being followed, rather than trusting the host to say what it was
            this.UnfollowSingleSource();

            if (newValue == null)
            {
                this.ClearEditTargets();
                return;
            }

            this.RebuildEditTargets();
            this.FollowSingleSource(newValue);
        }

        /// <summary>Call when the grid's ItemsSource changes</summary>
        public void OnItemsSourceChanged (IEnumerable? newValue)
        {
            // Let go of whatever the old source had us following, observable or not
            this.UnfollowItemsSource();

            if (newValue == null)
            {
                this.ClearEditTargets();
                return;
            }

            this.RebuildEditTargets();
            this.FollowItemsSource(newValue);
        }

        /// <summary>
        /// Call when the grid's view comes (back) into a window: re-subscribes to the sources, then catches up on
        /// what changed while the view was out. If the ItemsSource gained, lost or reordered items the tree is
        /// rebuilt; otherwise every value is re-read and the tree is kept, so expanded rows stay expanded.
        /// </summary>
        public void OnAttached ()
        {
            this.UpdateSourceSubscription(subscribe: true);
            if (this.ItemsSourceDiffersFromBuiltTree())
            {
                this.RebuildEditTargets();
            }
            else
            {
                this.RefreshAllTargets();
            }
        }

        /// <summary>
        /// Call when the grid's view leaves its window: drops the source to grid subscription, so a long lived
        /// source does not keep a grid that was dropped without Dispose or a cleared source
        /// </summary>
        public void OnDetached ()
        {
            this.UpdateSourceSubscription(subscribe: false);
        }

        /// <summary>Re-reads every target's value from its source, then re-runs ShowIf/HideIf</summary>
        public void RefreshAllTargets ()
        {
            if (m_manager.RootNode == null)
            {
                return;
            }

            // Snapshot first: RecacheEditValue on a list target may rebuild children mid enumeration
            foreach (PropertyEditTarget target in m_manager.Items.OfType<PropertyEditTarget>().ToArray())
            {
                target.RecacheEditValue();
            }

            m_manager.UpdateConditionalVisibility();
        }

        /// <summary>
        /// Call when the grid's selected row changes. Returns true when the source object behind the selection
        /// (see <see cref="ResolveSourceObject"/>) changed too, which is when a grid raises its
        /// SelectedSourceObjectChanged.
        /// </summary>
        public bool OnSelectedTargetChanged (PropertyEditTarget? selected)
        {
            object? newSource = ResolveSourceObject(selected);
            if (ReferenceEquals(newSource, m_lastSelectedSourceObject))
            {
                return false;
            }

            m_lastSelectedSourceObject = newSource;
            return true;
        }

        /// <summary>Finds the target whose list element instance is <paramref name="sourceObject"/> (by reference), or null</summary>
        public PropertyEditTarget? FindTargetForSourceObject (object sourceObject)
        {
            if (m_manager.RootNode == null)
            {
                return null;
            }

            foreach (PropertyEditTarget target in WalkAllTargets(m_manager.RootNode))
            {
                if (ReferenceEquals(target.SourceObject, sourceObject))
                {
                    return target;
                }
            }

            return null;
        }

        /// <summary>
        /// The source object a row stands for: walks up from the row (itself included) to the nearest list element
        /// row and returns its element instance. Any row inside an element's subtree resolves to that element; a
        /// row outside every list element resolves to null.
        /// </summary>
        public static object? ResolveSourceObject (PropertyEditTarget? target)
        {
            for (PropertyEditTarget? cursor = target; cursor != null; cursor = cursor.Parent as PropertyEditTarget)
            {
                object? source = cursor.SourceObject;
                if (source != null)
                {
                    return source;
                }
            }

            return null;
        }

        /// <summary>Can this row be dragged to reorder it in this grid: a list element whose list allows reordering, while the grid isn't read-only</summary>
        public bool CanDragRow (IObservableTreeNode node) => !this.IsReadOnly && CanDragItem(node);

        /// <summary>Can this row be dragged to reorder it: a list element whose list allows reordering</summary>
        public static bool CanDragItem (IObservableTreeNode node)
        {
            return node is PropertyEditTarget target
                && target.IsListElement
                && (target.EditContext as PropertyGridListElementContext)?.ParentListContext?.CanReorder == true;
        }

        /// <summary>Can a dragged row land under this parent: only back into its own list</summary>
        public static bool CanDropItem (IObservableTreeNode draggedNode, IObservableTreeNode targetParent)
        {
            if (draggedNode is PropertyEditTarget draggedTarget && draggedTarget.IsListElement
                && targetParent is PropertyEditTarget parentTarget && parentTarget.IsListEditor)
            {
                PropertyGridListContext? draggedListContext = (draggedTarget.EditContext as PropertyGridListElementContext)?.ParentListContext;
                PropertyGridListContext? parentListContext = parentTarget.EditContext as PropertyGridListContext;
                return draggedListContext != null && draggedListContext == parentListContext;
            }

            return false;
        }

        /// <summary>
        /// Handles a tree view's reorder request for a list element by moving the element in its list (which
        /// rebuilds the rows), and cancels the tree view's own move. Returns false, leaving the request alone,
        /// when it is not one list element.
        /// </summary>
        public static bool TryHandleReorder (FlatTreeReorderEventArgs e)
        {
            if (e.Items.Length != 1 || e.Items[0] is not PropertyEditTarget draggedTarget || !draggedTarget.IsListElement)
            {
                return false;
            }

            if (draggedTarget.EditContext is not PropertyGridListElementContext elementContext || elementContext.ParentListContext == null)
            {
                return false;
            }

            e.Cancel = true;

            // The insert index counts the dragged element's own slot; once it is lifted out, every later
            // index shifts down by one
            int fromIndex = elementContext.Index;
            int toIndex = e.InsertIndex > fromIndex ? e.InsertIndex - 1 : e.InsertIndex;
            if (fromIndex != toIndex)
            {
                elementContext.ParentListContext.MoveElement(fromIndex, toIndex);
            }

            return true;
        }

        // ===========[ Helper Methods ]========================================
        private void UpdateSourceSubscription (bool subscribe)
        {
            // Always detach first, so a subscription made when the source was set before the view came into
            // a window cannot double up
            this.UnfollowSingleSource();
            if (subscribe && m_host.SingleItemSource != null)
            {
                this.FollowSingleSource(m_host.SingleItemSource);
            }

            // The ItemsSource collection's CollectionChanged is a source -> grid link too, so it goes and
            // comes back with the item subscriptions
            this.UnfollowItemsSource();
            if (subscribe && m_host.ItemsSource != null)
            {
                this.FollowItemsSource(m_host.ItemsSource);
            }
        }

        private void FollowSingleSource (object singleSource)
        {
            if (singleSource is INotifyPropertyChanged propertyChanged)
            {
                propertyChanged.PropertyChanged -= this.OnSourceItemPropertyChanged;
                propertyChanged.PropertyChanged += this.OnSourceItemPropertyChanged;
                m_followedSingleSource = propertyChanged;
            }
        }

        private void UnfollowSingleSource ()
        {
            if (m_followedSingleSource != null)
            {
                m_followedSingleSource.PropertyChanged -= this.OnSourceItemPropertyChanged;
                m_followedSingleSource = null;
            }
        }

        private void FollowItemsSource (IEnumerable itemsSource)
        {
            if (itemsSource is INotifyCollectionChanged collection)
            {
                collection.CollectionChanged -= this.OnItemsSourceCollectionChanged;
                collection.CollectionChanged += this.OnItemsSourceCollectionChanged;
                m_followedCollection = collection;
            }

            this.FollowItems(itemsSource);
        }

        private void UnfollowItemsSource ()
        {
            if (m_followedCollection != null)
            {
                m_followedCollection.CollectionChanged -= this.OnItemsSourceCollectionChanged;
                m_followedCollection = null;
            }

            this.UnfollowItems();
        }

        private void FollowItems (IEnumerable itemsSource)
        {
            foreach (INotifyPropertyChanged item in itemsSource.OfType<INotifyPropertyChanged>())
            {
                item.PropertyChanged += this.OnSourceItemPropertyChanged;
                m_followedItems.Add(item);
            }
        }

        private void UnfollowItems ()
        {
            foreach (INotifyPropertyChanged item in m_followedItems)
            {
                item.PropertyChanged -= this.OnSourceItemPropertyChanged;
            }

            m_followedItems.Clear();
        }

        private void RebuildEditTargets ()
        {
            // Unsubscribe from the old tree (and the hidden conditional targets) first
            this.TeardownTargetSubscriptions();

            m_manager.RebuildEditTargets();

            // The manager builds from the single source when there is one, so only an ItemsSource-built tree
            // can go stale while detached
            m_builtFromItems = m_host.SingleItemSource == null ? m_host.ItemsSource?.Cast<object>().ToArray() : null;

            // The root's children, not the root: the view wraps each top-level target in a tree item
            // under an always-expanded root, so every top-level row shows while sub-object rows start
            // collapsed
            m_rootItems = m_manager.RootNode != null ? ((IObservableTreeNode)m_manager.RootNode).Children : null;
            m_host.ShowRootItems(m_rootItems);

            // Follow every target, including the conditionally hidden ones, so they report edits once shown
            if (m_manager.RootNode != null)
            {
                foreach (PropertyEditTarget target in WalkAllTargets(m_manager.RootNode))
                {
                    target.PropertyChanged += this.OnAnyTargetPropertyChanged;
                }

                foreach (PropertyEditTarget target in m_manager.HiddenConditionalTargets)
                {
                    target.PropertyChanged += this.OnAnyTargetPropertyChanged;
                }
            }
        }

        private void ClearEditTargets ()
        {
            this.TeardownTargetSubscriptions();
            m_manager.Dispose();
            m_rootItems = null;
            m_builtFromItems = null;
            m_host.ShowRootItems(null);
        }

        // Whether the ItemsSource's items, by reference and in order, are no longer what the shown tree was built from
        private bool ItemsSourceDiffersFromBuiltTree ()
        {
            if (m_builtFromItems == null || m_host.SingleItemSource != null || m_host.ItemsSource == null)
            {
                return false;
            }

            int index = 0;
            foreach (object item in m_host.ItemsSource)
            {
                if (index >= m_builtFromItems.Length || !ReferenceEquals(item, m_builtFromItems[index]))
                {
                    return true;
                }

                ++index;
            }

            return index != m_builtFromItems.Length;
        }

        // Drops every per-target subscription and each target's own hooks, over the live tree plus the
        // hidden conditional targets (which stay subscribed while out of the tree)
        private void TeardownTargetSubscriptions ()
        {
            if (m_manager.RootNode == null)
            {
                return;
            }

            foreach (PropertyEditTarget target in WalkAllTargets(m_manager.RootNode))
            {
                target.PropertyChanged -= this.OnAnyTargetPropertyChanged;
                target.Teardown();
            }

            foreach (PropertyEditTarget target in m_manager.HiddenConditionalTargets)
            {
                target.PropertyChanged -= this.OnAnyTargetPropertyChanged;
                target.Teardown();
            }
        }

        // Every target in the tree under node, including each target's elevated child
        private static IEnumerable<PropertyEditTarget> WalkAllTargets (IObservableTreeNode node)
        {
            foreach (IObservableTreeNode child in node.Children)
            {
                if (child is PropertyEditTarget target)
                {
                    yield return target;
                    if (target.ElevatedChildTarget != null)
                    {
                        yield return target.ElevatedChildTarget;
                    }

                    foreach (PropertyEditTarget descendant in WalkAllTargets(target))
                    {
                        yield return descendant;
                    }
                }
            }
        }
    }
}
