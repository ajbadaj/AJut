namespace AJut.UX.PropertyInteraction
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.Storage;
    using AJut.Text.AJson;
    using AJut.Tree;

    public interface IPropertyGrid
    {
        public IEnumerable ItemsSource { get; }
        public object SingleItemSource { get; }
    }

    public class PropertyGridManager : IDisposable
    {
        // ===========[ Instance fields ]==========================================
        private readonly IPropertyGrid m_propertyGrid;

        // Keyed by full property path (e.g. "Address.City") within the current source type.
        // Preserved across RebuildEditTargets calls so expanded nodes stay expanded.
        private readonly Dictionary<string, bool> m_expandedStates = new();

        // ===========[ Construction ]==========================================
        public PropertyGridManager (IPropertyGrid propertyGrid)
        {
            m_propertyGrid = propertyGrid;
        }

        // ===========[ Properties ]==========================================

        /// <summary>
        /// The hidden root node whose children are the top-level property edit targets.
        /// Bind a FlatTreeListControl to this with IncludeRoot=false to get a proper
        /// expandable tree view of all properties (including sub-object properties).
        /// </summary>
        public PropertyEditTarget RootNode { get; private set; }

        /// <summary>
        /// Legacy flat-store access. The store's RootNode is the same as <see cref="RootNode"/>;
        /// IncludeRoot is false so the store contains only top-level items.
        /// </summary>
        public ObservableFlatTreeStore<PropertyEditTarget> Items { get; } = new ObservableFlatTreeStore<PropertyEditTarget>();

        // ===========[ Public Interface Methods ]==========================================

        public void Dispose ()
        {
            this.Items.Clear();
            this.RootNode = null;
        }

        public void RebuildEditTargets ()
        {
            // 1. Snapshot current expansion state before clearing the tree.
            _SnapshotExpandedStates();

            this.Items.Clear();
            this.RootNode = null;

            if (m_propertyGrid.ItemsSource == null && m_propertyGrid.SingleItemSource == null)
            {
                return;
            }

            IEnumerable sourceItems;
            if (m_propertyGrid.SingleItemSource != null)
            {
                sourceItems = Enumerable.Repeat(m_propertyGrid.SingleItemSource, 1);
            }
            else
            {
                sourceItems = m_propertyGrid.ItemsSource;
            }

            if (sourceItems == null)
            {
                return;
            }

            var root = new PropertyEditTarget("$_root_", () => null, null);
            var editTargets = new Dictionary<int, PropertyEditTarget>();

            foreach (object item in sourceItems)
            {
                IEnumerable<PropertyEditTarget> targets;
                if (item is IPropertyEditManager propManager)
                {
                    targets = propManager.GenerateEditTargets();
                }
                else
                {
                    targets = PropertyEditTarget.GenerateForPropertiesOf(item);
                }

                foreach (PropertyEditTarget target in targets)
                {
                    _Add(target);
                }
            }

            // ------ Group post-processing: collect targets with matching GroupId
            var orderedTargets = new List<PropertyEditTarget>(editTargets.Values);
            var groupHeaders = new Dictionary<string, PropertyEditTarget>();
            var groupedIndices = new HashSet<int>();

            for (int i = 0; i < orderedTargets.Count; ++i)
            {
                string groupId = orderedTargets[i].GroupId;
                if (string.IsNullOrEmpty(groupId))
                {
                    continue;
                }

                if (!groupHeaders.TryGetValue(groupId, out PropertyEditTarget groupHeader))
                {
                    // Create synthetic group header at the position of the first member
                    groupHeader = new PropertyEditTarget($"$group_{groupId}", () => null, null)
                    {
                        DisplayName = groupId,
                        IsExpandable = true,
                    };
                    groupHeaders[groupId] = groupHeader;

                    // Replace the first member's slot with the group header
                    orderedTargets[i].Setup();
                    groupHeader.InsertChild(groupHeader.Children.Count, orderedTargets[i]);
                    orderedTargets[i] = groupHeader;
                }
                else
                {
                    // Subsequent members: attach as children and mark index for removal
                    orderedTargets[i].Setup();
                    groupHeader.InsertChild(groupHeader.Children.Count, orderedTargets[i]);
                    groupedIndices.Add(i);
                }
            }

            // Add all non-removed targets to root
            for (int i = 0; i < orderedTargets.Count; ++i)
            {
                if (groupedIndices.Contains(i))
                {
                    continue;
                }

                PropertyEditTarget target = orderedTargets[i];
                target.Setup();
                root.InsertChild(root.Children.Count, target);
            }

            // 2. Restore previously-snapshotted expansion states into the new targets.
            _RestoreExpandedStates(root);

            this.RootNode = root;
            this.Items.IncludeRoot = false;
            this.Items.RootNode = root;

            void _Add (PropertyEditTarget _target)
            {
                int _id = _target.GetHashCode();
                if (editTargets.TryGetValue(_id, out PropertyEditTarget _found))
                {
                    _found.TakeOn(_target);
                    return;
                }

                editTargets.Add(_id, _target);
            }
        }

        /// <summary>
        /// Serializes the current expansion state to a string so it can be saved across
        /// application sessions. Pass the returned string to <see cref="RestoreExpandedState"/>
        /// on the next launch before the first <see cref="RebuildEditTargets"/> call.
        /// </summary>
        public string SaveExpandedState ()
        {
            // Merge live tree state into the snapshot dictionary first.
            _SnapshotExpandedStates();

            var expandedPaths = m_expandedStates
                .Where(kvp => kvp.Value)
                .Select(kvp => kvp.Key)
                .ToList();

            return JsonHelper.BuildJsonForObject(expandedPaths)?.Data?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Restores expansion state from a string previously produced by <see cref="SaveExpandedState"/>.
        /// Call this before <see cref="RebuildEditTargets"/> to have the restored state applied
        /// when the tree is first built.
        /// </summary>
        public void RestoreExpandedState (string savedState)
        {
            if (string.IsNullOrEmpty(savedState))
            {
                return;
            }

            List<string> paths = JsonHelper.BuildObjectForJson<List<string>>(JsonHelper.ParseText(savedState));
            if (paths == null)
            {
                return;
            }

            m_expandedStates.Clear();
            foreach (string path in paths)
            {
                m_expandedStates[path] = true;
            }
        }

        // ===========[ Private helpers ]==========================================

        private void _SnapshotExpandedStates ()
        {
            if (this.RootNode == null)
            {
                return;
            }

            foreach (PropertyEditTarget target in TreeTraversal<IObservableTreeNode>.All(this.RootNode).OfType<PropertyEditTarget>())
            {
                string path = _BuildPropertyPath(target);
                if (!string.IsNullOrEmpty(path))
                {
                    m_expandedStates[path] = target.IsExpanded;
                }
            }
        }

        private void _RestoreExpandedStates (PropertyEditTarget root)
        {
            if (m_expandedStates.Count == 0)
            {
                return;
            }

            foreach (PropertyEditTarget target in TreeTraversal<IObservableTreeNode>.All(root).OfType<PropertyEditTarget>())
            {
                string path = _BuildPropertyPath(target);
                if (path != null && m_expandedStates.TryGetValue(path, out bool wasExpanded))
                {
                    target.IsExpanded = wasExpanded;
                }
            }
        }

        private static string _BuildPropertyPath (PropertyEditTarget target)
        {
            // Walk up the parent chain to construct a full dotted path, excluding the hidden root.
            var parts = new List<string>();
            PropertyEditTarget current = target;
            while (current != null && current.PropertyPathTarget != "$_root_")
            {
                parts.Insert(0, current.PropertyPathTarget);
                current = current.Parent as PropertyEditTarget;
            }

            return parts.Count > 0 ? string.Join(".", parts) : null;
        }
    }
}
