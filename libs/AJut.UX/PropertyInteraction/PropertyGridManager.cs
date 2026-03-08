namespace AJut.UX.PropertyInteraction
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using AJut.Storage;
    using AJut.Text.AJson;
    using AJut.Tree;
    using AJut.TypeManagement;

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

        // Visibility condition tracking for ShowIf/HideIf toggle-in-place
        private readonly List<VisibilityCondition> m_visibilityConditions = new();
        private readonly Dictionary<PropertyEditTarget, List<PropertyEditTarget>> m_naturalChildOrder = new();
        private bool m_hasConditionalVisibility;

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

        /// <summary>
        /// Returns all conditionally-hidden targets so callers can subscribe to their
        /// events even while they are not in the live tree.
        /// </summary>
        public IEnumerable<PropertyEditTarget> HiddenConditionalTargets
            => m_visibilityConditions.Where(c => !c.LastResult).Select(c => c.Target);

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
            m_visibilityConditions.Clear();
            m_naturalChildOrder.Clear();
            m_hasConditionalVisibility = false;

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

            // Materialize source items so we can iterate twice (targets + conditions)
            var sourceList = new List<object>();
            foreach (object item in sourceItems)
            {
                sourceList.Add(item);

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

                // Generate button targets from [PGButton] methods
                foreach (PropertyEditTarget buttonTarget in PropertyEditTarget.GenerateButtonsForMethodsOf(item))
                {
                    _Add(buttonTarget);
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

            // 3. Build visibility conditions linked to targets and remove hidden ones.
            _BuildAndApplyVisibilityConditions(sourceList, root);

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
        /// Re-evaluates all ShowIf/HideIf visibility conditions and toggles affected
        /// targets in/out of the tree without a full rebuild. Returns true if any
        /// condition changed.
        /// </summary>
        public bool UpdateConditionalVisibility ()
        {
            if (!m_hasConditionalVisibility)
            {
                return false;
            }

            bool anyChanged = false;

            // 1. Process removals (newly hidden)
            foreach (var cond in m_visibilityConditions)
            {
                bool visible = cond.IsVisible();
                if (visible == cond.LastResult)
                {
                    continue;
                }

                anyChanged = true;
                cond.LastResult = visible;

                if (!visible)
                {
                    cond.Parent.RemoveChild(cond.Target);
                }
            }

            // 2. Process additions (newly visible), sorted by natural index
            //    so earlier targets are inserted first for correct positioning.
            foreach (var cond in m_visibilityConditions.OrderBy(c => c.NaturalIndex))
            {
                if (!cond.LastResult || cond.Parent.Children.Any(c => c == cond.Target))
                {
                    continue;
                }

                // Compute insertion index from the natural child order
                int insertAt = _ComputeInsertionIndex(cond);
                cond.Parent.InsertChild(insertAt, cond.Target);
            }

            return anyChanged;
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

        private void _BuildAndApplyVisibilityConditions (List<object> sourceItems, PropertyEditTarget root)
        {
            // 1. Build lookup from PropertyPathTarget -> target in the built tree
            var targetLookup = new Dictionary<string, PropertyEditTarget>();
            foreach (PropertyEditTarget topChild in root.Children.OfType<PropertyEditTarget>())
            {
                targetLookup[topChild.PropertyPathTarget] = topChild;

                // Children of group headers
                if (topChild.PropertyPathTarget.StartsWith("$group_"))
                {
                    foreach (PropertyEditTarget groupChild in topChild.Children.OfType<PropertyEditTarget>())
                    {
                        targetLookup[groupChild.PropertyPathTarget] = groupChild;
                    }
                }
            }

            // 2. For each source item, scan members for ShowIf/HideIf and link to targets
            foreach (object item in sourceItems)
            {
                Type type = item.GetType();

                foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    PGShowIfAttribute showIf = TypeMetadataExtensionRegistrar.GetAttribute<PGShowIfAttribute>(prop);
                    PGHideIfAttribute hideIf = TypeMetadataExtensionRegistrar.GetAttribute<PGHideIfAttribute>(prop);
                    if (showIf == null && hideIf == null)
                    {
                        continue;
                    }

                    if (!targetLookup.TryGetValue(prop.Name, out PropertyEditTarget target))
                    {
                        continue;
                    }

                    _AddLinkedCondition(item, target, showIf, hideIf);
                }

                foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    if (method.GetCustomAttribute<PGButtonAttribute>() == null)
                    {
                        continue;
                    }

                    PGShowIfAttribute showIf = method.GetCustomAttribute<PGShowIfAttribute>();
                    PGHideIfAttribute hideIf = method.GetCustomAttribute<PGHideIfAttribute>();
                    if (showIf == null && hideIf == null)
                    {
                        continue;
                    }

                    if (!targetLookup.TryGetValue(method.Name, out PropertyEditTarget target))
                    {
                        continue;
                    }

                    _AddLinkedCondition(item, target, showIf, hideIf);
                }
            }

            // 3. Snapshot the natural child order for parents that have conditional children
            foreach (var cond in m_visibilityConditions)
            {
                if (!m_naturalChildOrder.ContainsKey(cond.Parent))
                {
                    m_naturalChildOrder[cond.Parent] = cond.Parent.Children.OfType<PropertyEditTarget>().ToList();
                }
            }

            // 4. Evaluate initial visibility and remove hidden targets
            foreach (var cond in m_visibilityConditions)
            {
                cond.LastResult = cond.IsVisible();
                if (!cond.LastResult)
                {
                    cond.Parent.RemoveChild(cond.Target);
                }
            }
        }

        private void _AddLinkedCondition (object sourceItem, PropertyEditTarget target, PGShowIfAttribute showIf, PGHideIfAttribute hideIf)
        {
            m_hasConditionalVisibility = true;

            PropertyEditTarget parent = target.Parent as PropertyEditTarget;
            int naturalIndex = 0;
            if (parent != null)
            {
                for (int i = 0; i < parent.Children.Count; ++i)
                {
                    if (parent.Children[i] == target)
                    {
                        naturalIndex = i;
                        break;
                    }
                }
            }

            Func<bool> evaluator;
            if (showIf != null && hideIf != null)
            {
                var s = showIf;
                var h = hideIf;
                evaluator = () =>
                    (PropertyEditTarget.EvaluateBoolMember(sourceItem, s.TargetMember) == s.ShowWhen)
                    && (PropertyEditTarget.EvaluateBoolMember(sourceItem, h.TargetMember) != h.HideWhen);
            }
            else if (showIf != null)
            {
                var s = showIf;
                evaluator = () => PropertyEditTarget.EvaluateBoolMember(sourceItem, s.TargetMember) == s.ShowWhen;
            }
            else
            {
                var h = hideIf;
                evaluator = () => PropertyEditTarget.EvaluateBoolMember(sourceItem, h.TargetMember) != h.HideWhen;
            }

            m_visibilityConditions.Add(new VisibilityCondition
            {
                Target = target,
                Parent = parent,
                NaturalIndex = naturalIndex,
                IsVisible = evaluator,
            });
        }

        private int _ComputeInsertionIndex (VisibilityCondition cond)
        {
            if (!m_naturalChildOrder.TryGetValue(cond.Parent, out List<PropertyEditTarget> order))
            {
                return cond.Parent.Children.Count;
            }

            // Count how many targets that come before this one in the natural
            // order are currently present in the parent's children.
            int insertAt = 0;
            foreach (PropertyEditTarget child in order)
            {
                if (child == cond.Target)
                {
                    break;
                }

                if (cond.Parent.Children.Any(c => c == child))
                {
                    ++insertAt;
                }
            }

            return insertAt;
        }

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

        // ===========[ Subclasses ]==========================================

        private class VisibilityCondition
        {
            public PropertyEditTarget Target;
            public PropertyEditTarget Parent;
            public int NaturalIndex;
            public Func<bool> IsVisible;
            public bool LastResult;
        }
    }
}
