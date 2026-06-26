namespace AJut.UX.Controls
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using AJut;
    using AJut.Tree;
    using AJut.UX.PropertyInteraction;
    using AJut.Storage;
    using DPUtils = DPUtils<PropertyGrid>;

    public class PropertyGrid : Control, IDisposable, IPropertyGrid
    {
        private readonly PropertyGridManager m_manager;
        private FlatTreeListControl PART_TreeList;

        // Last source object reported through SelectedSourceObjectChanged. Held so we only raise that
        // event when the resolved element actually changes - not when selection moves between two rows
        // inside the same element's subtree.
        private object m_lastSelectedSourceObject;

        // ===========[ Commands ]=================================================
        /// <summary>
        /// Resets the target PropertyEditTarget (passed as CommandParameter) to its default value.
        /// Sourced from the label ContextMenu in the control template.
        /// </summary>
        public static readonly RoutedUICommand SetPropertyToDefaultCommand = new RoutedUICommand(
            "Set to Default",
            nameof(SetPropertyToDefaultCommand),
            typeof(PropertyGrid)
        );

        static PropertyGrid ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGrid), new FrameworkPropertyMetadata(typeof(PropertyGrid)));
        }

        public PropertyGrid ()
        {
            m_manager = new PropertyGridManager(this);
            this.CommandBindings.Add(new CommandBinding(
                SetPropertyToDefaultCommand,
                this.OnSetPropertyToDefaultExecuted,
                this.OnSetPropertyToDefaultCanExecute
            ));

            // Drop the source -> grid PropertyChanged subscription whenever the grid leaves the
            // visual tree, and restore it when it comes back. Without this a long-lived source
            // object pins the whole grid after a consumer simply drops it (without Dispose or
            // nulling the source) - see OnSourceItemPropertyChanged subscription below.
            this.Loaded += this.OnLoaded;
            this.Unloaded += this.OnUnloaded;
        }

        public void Dispose ()
        {
            if (this.PART_TreeList != null)
            {
                this.PART_TreeList.SelectionChanged -= this.OnTreeListSelectionChanged;
                this.PART_TreeList.DragDropReorderRequested -= this.OnDragDropReorderRequested;
            }

            // Drop the per-target subscriptions and each target's elevated INPC hooks while the
            // tree is still walkable. m_manager.Dispose() only clears collections; without this a
            // long-lived source object keeps the disposed grid (and its target tree) alive.
            this._TeardownTargetSubscriptions();
            m_manager.Dispose();
            this.ItemsSource = null;
            this.SingleItemSource = null;
        }

        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            if (this.PART_TreeList != null)
            {
                this.PART_TreeList.SelectionChanged -= this.OnTreeListSelectionChanged;
                this.PART_TreeList.DragDropReorderRequested -= this.OnDragDropReorderRequested;
            }

            this.PART_TreeList = this.GetTemplateChild(nameof(PART_TreeList)) as FlatTreeListControl;
            if (this.PART_TreeList != null)
            {
                this.PART_TreeList.IsDragDropReorderEnabled = true;
                this.PART_TreeList.CanDragItem = _CanDragPropertyItem;
                this.PART_TreeList.CanDropItem = _CanDropPropertyItem;
                this.PART_TreeList.DragDropReorderRequested += this.OnDragDropReorderRequested;

                this.PART_TreeList.SelectionChanged += this.OnTreeListSelectionChanged;

                this.PART_TreeList.DragGhostTemplate = this.DragGhostTemplate;
            }
        }

        public static readonly DependencyProperty ItemsSourceProperty = DPUtils.Register(_ => _.ItemsSource, (d, e) => d.OnItemsSourceChanged(e));
        public IEnumerable ItemsSource
        {
            get => (IEnumerable)this.GetValue(ItemsSourceProperty);
            set => this.SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty SingleItemSourceProperty = DPUtils.Register(_ => _.SingleItemSource, (d, e) => d.OnSingleItemSourceChanged(e));
        public object SingleItemSource
        {
            get => (object)this.GetValue(SingleItemSourceProperty);
            set => this.SetValue(SingleItemSourceProperty, value);
        }

        /// <summary>
        /// The top-level PropertyEditTarget items (children of the hidden $root node).
        /// Bind FlatTreeListControl.RootItemsSource to this so it creates an always-expanded
        /// uber root, making all top-level properties immediately visible while keeping
        /// sub-object children collapsed until the user expands them.
        /// Updated whenever RebuildEditTargets runs.
        /// </summary>
        private static readonly DependencyPropertyKey PropertyTreeItemsPropertyKey = DPUtils.RegisterReadOnly(_ => _.PropertyTreeItems);
        public static readonly DependencyProperty PropertyTreeItemsProperty = PropertyTreeItemsPropertyKey.DependencyProperty;
        public IReadOnlyList<IObservableTreeNode> PropertyTreeItems
        {
            get => (IReadOnlyList<IObservableTreeNode>)this.GetValue(PropertyTreeItemsProperty);
            private set => this.SetValue(PropertyTreeItemsPropertyKey, value);
        }

        public static readonly DependencyProperty ItemTemplateSelectorProperty = DPUtils.Register(_ => _.ItemTemplateSelector);
        public PropertyGridTemplateSelector ItemTemplateSelector
        {
            get => (PropertyGridTemplateSelector)this.GetValue(ItemTemplateSelectorProperty);
            set => this.SetValue(ItemTemplateSelectorProperty, value);
        }

        public static readonly DependencyProperty TextLabelStyleProperty = DPUtils.Register(_ => _.TextLabelStyle);
        public Style TextLabelStyle
        {
            get => (Style)this.GetValue(TextLabelStyleProperty);
            set => this.SetValue(TextLabelStyleProperty, value);
        }

        public static readonly DependencyProperty MaxRecursionDepthProperty = DPUtils.Register(_ => _.MaxRecursionDepth, 5, (d, e) => d.m_manager.MaxRecursionDepth = e.NewValue);
        public int MaxRecursionDepth
        {
            get => (int)this.GetValue(MaxRecursionDepthProperty);
            set => this.SetValue(MaxRecursionDepthProperty, value);
        }

        public static readonly DependencyProperty TreeDepthIndentSizeProperty = DPUtils.Register(_ => _.TreeDepthIndentSize, 8.0);
        public double TreeDepthIndentSize
        {
            get => (double)this.GetValue(TreeDepthIndentSizeProperty);
            set => this.SetValue(TreeDepthIndentSizeProperty, value);
        }

        public static readonly DependencyProperty RowSpacingProperty = DPUtils.Register(_ => _.RowSpacing, 2.0);
        public double RowSpacing
        {
            get => (double)this.GetValue(RowSpacingProperty);
            set => this.SetValue(RowSpacingProperty, value);
        }

        public static readonly DependencyProperty FixedRowHeightProperty = DPUtils.Register(_ => _.FixedRowHeight, double.NaN);
        public double FixedRowHeight
        {
            get => (double)this.GetValue(FixedRowHeightProperty);
            set => this.SetValue(FixedRowHeightProperty, value);
        }

        public static readonly DependencyProperty LabelColumnWidthProperty = DPUtils.Register(_ => _.LabelColumnWidth, double.NaN);
        public double LabelColumnWidth
        {
            get => (double)this.GetValue(LabelColumnWidthProperty);
            set => this.SetValue(LabelColumnWidthProperty, value);
        }

        /// <summary>Decides where per-property tooltips render (property name, value, both, or none).</summary>
        public static readonly DependencyProperty PropertyToolTipPlacementProperty = DPUtils.Register(_ => _.PropertyToolTipPlacement, ePropertyToolTipPlacement.PropertyNameOnly);
        public ePropertyToolTipPlacement PropertyToolTipPlacement
        {
            get => (ePropertyToolTipPlacement)this.GetValue(PropertyToolTipPlacementProperty);
            set => this.SetValue(PropertyToolTipPlacementProperty, value);
        }

        /// <summary>
        /// Format string for the element count display in list editors.
        /// {0} is replaced with the element count. Default: "Elements ({0})".
        /// </summary>
        public static readonly DependencyProperty ListElementCountFormatProperty = DPUtils.Register(_ => _.ListElementCountFormat, "Elements ({0})");
        public string ListElementCountFormat
        {
            get => (string)this.GetValue(ListElementCountFormatProperty);
            set => this.SetValue(ListElementCountFormatProperty, value);
        }

        /// <summary>
        /// DataTemplate for the property label when the value equals the default.
        /// DataContext is the PropertyEditTarget. Defaults to a plain TextBlock showing DisplayName.
        /// </summary>
        public static readonly DependencyProperty DefaultValueLabelDataTemplateProperty = DPUtils.Register(_ => _.DefaultValueLabelDataTemplate);
        public DataTemplate DefaultValueLabelDataTemplate
        {
            get => (DataTemplate)this.GetValue(DefaultValueLabelDataTemplateProperty);
            set => this.SetValue(DefaultValueLabelDataTemplateProperty, value);
        }

        /// <summary>
        /// DataTemplate for the property label when the value differs from the default.
        /// DataContext is the PropertyEditTarget. Defaults to a bold TextBlock showing DisplayName.
        /// </summary>
        public static readonly DependencyProperty ModifiedValueLabelDataTemplateProperty = DPUtils.Register(_ => _.ModifiedValueLabelDataTemplate);
        public DataTemplate ModifiedValueLabelDataTemplate
        {
            get => (DataTemplate)this.GetValue(ModifiedValueLabelDataTemplateProperty);
            set => this.SetValue(ModifiedValueLabelDataTemplateProperty, value);
        }

        /// <summary>
        /// Style applied to the subtitle TextBlock in each property row. TargetType must be TextBlock.
        /// Defaults to smaller, italic, 0.85 opacity text.
        /// </summary>
        public static readonly DependencyProperty LabelSubtitleStyleProperty = DPUtils.Register(_ => _.LabelSubtitleStyle);
        public Style LabelSubtitleStyle
        {
            get => (Style)this.GetValue(LabelSubtitleStyleProperty);
            set => this.SetValue(LabelSubtitleStyleProperty, value);
        }

        public static readonly DependencyProperty DragGhostTemplateProperty = DPUtils.Register(_ => _.DragGhostTemplate, (d, e) => d.OnDragGhostTemplateChanged(e));
        public DataTemplate DragGhostTemplate
        {
            get => (DataTemplate)this.GetValue(DragGhostTemplateProperty);
            set => this.SetValue(DragGhostTemplateProperty, value);
        }
        private void OnDragGhostTemplateChanged (DependencyPropertyChangedEventArgs<DataTemplate> e)
        {
            if (this.PART_TreeList != null)
            {
                this.PART_TreeList.DragGhostTemplate = e.NewValue;
            }
        }

        // ===========[ Lifecycle - source subscription ]==========================
        // The grid mirrors external edits by subscribing to its source object(s) PropertyChanged.
        // That subscription is a source -> grid strong reference, so a long-lived source pins the
        // grid once it leaves the tree (a consumer that drops the grid without Dispose or nulling
        // the source). Toggle the subscription with tree membership: drop it on Unloaded so a
        // dropped grid collects, restore + refresh it on Loaded so a re-shown grid stays live.
        private void OnLoaded (object sender, RoutedEventArgs e)
        {
            this.UpdateSourceSubscription(subscribe: true);
            this.RefreshAllTargets();
        }

        private void OnUnloaded (object sender, RoutedEventArgs e)
        {
            this.UpdateSourceSubscription(subscribe: false);
        }

        private void UpdateSourceSubscription (bool subscribe)
        {
            // Always detach first so re-entry / a sub already established when the source DP was set
            // before load cannot double-subscribe.
            if (this.SingleItemSource is INotifyPropertyChanged single)
            {
                single.PropertyChanged -= this.OnSourceItemPropertyChanged;
                if (subscribe)
                {
                    single.PropertyChanged += this.OnSourceItemPropertyChanged;
                }
            }

            if (this.ItemsSource != null)
            {
                // The ItemsSource collection's CollectionChanged is also a source -> grid link, so toggle
                // it alongside the per-item PropertyChanged subs.
                if (this.ItemsSource is INotifyCollectionChanged collectionChange)
                {
                    collectionChange.CollectionChanged -= this.NotifyCollectionChangedItemsSource_OnCollectionChanged;
                    if (subscribe)
                    {
                        collectionChange.CollectionChanged += this.NotifyCollectionChangedItemsSource_OnCollectionChanged;
                    }
                }

                foreach (INotifyPropertyChanged pc in this.ItemsSource.OfType<INotifyPropertyChanged>())
                {
                    pc.PropertyChanged -= this.OnSourceItemPropertyChanged;
                    if (subscribe)
                    {
                        pc.PropertyChanged += this.OnSourceItemPropertyChanged;
                    }
                }
            }
        }

        // Re-read every target's value in case the source changed while the grid was detached.
        // Snapshots first - RecacheEditValue on a list target may rebuild children mid-enumeration.
        private void RefreshAllTargets ()
        {
            foreach (var target in m_manager.Items.OfType<PropertyEditTarget>().ToArray())
            {
                target.RecacheEditValue();
            }

            m_manager.UpdateConditionalVisibility();
        }

        private void OnSingleItemSourceChanged (DependencyPropertyChangedEventArgs<object> e)
        {
            if (e.OldValue is INotifyPropertyChanged oldPropChanged)
            {
                oldPropChanged.PropertyChanged -= this.OnSourceItemPropertyChanged;
            }

            if (e.HasNewValue)
            {
                this.ItemsSource = null;
                this.RebuildEditTargets();

                if (e.NewValue is INotifyPropertyChanged newPropChanged)
                {
                    newPropChanged.PropertyChanged += this.OnSourceItemPropertyChanged;
                }
            }
            else
            {
                this._TeardownTargetSubscriptions();
                m_manager.Dispose();
                this.PropertyTreeItems = null;
            }
        }

        private void OnItemsSourceChanged (DependencyPropertyChangedEventArgs<IEnumerable> e)
        {
            if (e.OldValue is INotifyCollectionChanged oldCollectionChange)
            {
                oldCollectionChange.CollectionChanged -= this.NotifyCollectionChangedItemsSource_OnCollectionChanged;

                foreach (INotifyPropertyChanged pc in e.OldValue.OfType<INotifyPropertyChanged>())
                {
                    pc.PropertyChanged -= this.OnSourceItemPropertyChanged;
                }
            }

            if (e.HasNewValue)
            {
                this.SingleItemSource = null;
                this.RebuildEditTargets();

                if (e.NewValue is INotifyCollectionChanged newCollectionChange)
                {
                    newCollectionChange.CollectionChanged -= this.NotifyCollectionChangedItemsSource_OnCollectionChanged;
                    newCollectionChange.CollectionChanged += this.NotifyCollectionChangedItemsSource_OnCollectionChanged;
                }

                foreach (INotifyPropertyChanged pc in e.NewValue.OfType<INotifyPropertyChanged>())
                {
                    pc.PropertyChanged += this.OnSourceItemPropertyChanged;
                }
            }
            else
            {
                this._TeardownTargetSubscriptions();
                m_manager.Dispose();
                this.PropertyTreeItems = null;
            }
        }

        private void OnSourceItemPropertyChanged (object sender, PropertyChangedEventArgs e)
        {
            // Snapshot: RecacheEditValue on list targets may rebuild children,
            // which modifies the Items collection during enumeration.
            var items = m_manager.Items.Where(i => i.ShouldEvaluateFor(e.PropertyName)).ToArray();
            foreach (var item in items)
            {
                item.RecacheEditValue();
            }

            // External INPC changes may affect ShowIf/HideIf conditions
            m_manager.UpdateConditionalVisibility();
        }

        private void NotifyCollectionChangedItemsSource_OnCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (INotifyPropertyChanged pc in e.OldItems?.OfType<INotifyPropertyChanged>() ?? Enumerable.Empty<INotifyPropertyChanged>())
            {
                pc.PropertyChanged -= this.OnSourceItemPropertyChanged;
            }

            foreach (INotifyPropertyChanged pc in e.NewItems?.OfType<INotifyPropertyChanged>() ?? Enumerable.Empty<INotifyPropertyChanged>())
            {
                pc.PropertyChanged += this.OnSourceItemPropertyChanged;
            }

            this.RebuildEditTargets();
        }

        /// <summary>
        /// Fires whenever any property in the displayed tree is edited via the property grid.
        /// Subscribe to detect any change and refresh displays that mirror the source object.
        /// </summary>
        public event EventHandler PropertyTreeChanged;

        /// <summary>
        /// Fires whenever the selected row changes - by user click or programmatic selection. Read
        /// <see cref="SelectedTarget"/> / <see cref="SelectedSourceObject"/> in the handler.
        /// </summary>
        public event EventHandler SelectedTargetChanged;

        /// <summary>
        /// Fires when the source object behind the selected row changes. Moving the selection between two
        /// rows inside the same list element's subtree resolves to the same element, so this does NOT fire
        /// in that case - only when the resolved <see cref="SelectedSourceObject"/> actually changes.
        /// </summary>
        public event EventHandler SelectedSourceObjectChanged;

        // ===========[ Selection ]================================================
        /// <summary>
        /// The PropertyEditTarget behind the currently selected row, or null when nothing is selected.
        /// Setting it selects that row (expanding any collapsed ancestors) and scrolls it into view; set
        /// to null to clear the selection. Returns null until the template has been applied.
        /// </summary>
        public PropertyEditTarget SelectedTarget
        {
            get => this.PART_TreeList?.SelectedItem as PropertyEditTarget;
            set
            {
                if (this.PART_TreeList == null)
                {
                    return;
                }

                if (value == null)
                {
                    this.PART_TreeList.SelectedItems.Clear();
                    return;
                }

                this.PART_TreeList.TrySelectItemForSource(value, scrollIntoView: true);
            }
        }

        /// <summary>
        /// The source object behind the selected row, resolved by walking up from the selected row to the
        /// nearest list-element row and returning its element instance. Null when nothing is selected or
        /// the selection sits outside any list element. Selecting any row inside an element's subtree
        /// (the element row itself or any of its sub-property rows) resolves to that same element.
        /// </summary>
        public object SelectedSourceObject => _ResolveSourceObject(this.SelectedTarget);

        /// <summary>
        /// Selects the row representing <paramref name="sourceObject"/> (matched by reference), expanding
        /// any collapsed ancestors and scrolling it into view. Returns false when no row represents the
        /// instance. Only list-element rows carry a source object, so this targets list elements.
        /// </summary>
        public bool TrySelectSourceObject (object sourceObject)
        {
            if (sourceObject == null || this.PART_TreeList == null)
            {
                return false;
            }

            PropertyEditTarget match = this.FindTargetForSourceObject(sourceObject);
            if (match == null)
            {
                return false;
            }

            return this.PART_TreeList.TrySelectItemForSource(match, scrollIntoView: true);
        }

        private void RebuildEditTargets ()
        {
            // Unsubscribe from old tree (and hidden conditional targets) before rebuild.
            this._TeardownTargetSubscriptions();

            m_manager.RebuildEditTargets();
            this.PropertyTreeItems = m_manager.RootNode != null
                ? ((IObservableTreeNode)m_manager.RootNode).Children
                : null;

            // Subscribe to all new targets for PropertyTreeChanged notification,
            // including conditionally-hidden targets so they fire events when shown.
            if (m_manager.RootNode != null)
            {
                foreach (var target in _WalkAllTargets(m_manager.RootNode))
                {
                    target.PropertyChanged += this.OnAnyTargetPropertyChanged;
                }

                foreach (var target in m_manager.HiddenConditionalTargets)
                {
                    target.PropertyChanged += this.OnAnyTargetPropertyChanged;
                }
            }
        }

        private void OnAnyTargetPropertyChanged (object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == PropertyEditTarget.SourceCommittedPropertyName)
            {
                this.PropertyTreeChanged?.Invoke(this, EventArgs.Empty);

                // List operations (add/remove/reorder) rebuild children via RebuildListChildren,
                // creating new PropertyEditTarget objects that aren't subscribed yet. Re-subscribe
                // all descendants of the list parent so future edits fire PropertyTreeChanged.
                if (sender is PropertyEditTarget { IsListEditor: true } listTarget)
                {
                    foreach (var child in _WalkAllTargets(listTarget))
                    {
                        child.PropertyChanged -= this.OnAnyTargetPropertyChanged;
                        child.PropertyChanged += this.OnAnyTargetPropertyChanged;
                    }
                }

                // Button actions may change source properties without INPC - recache all targets
                if (sender is PropertyEditTarget { Editor: "Button" } && m_manager.RootNode != null)
                {
                    foreach (var target in _WalkAllTargets(m_manager.RootNode))
                    {
                        if (target.Editor != "Button")
                        {
                            target.RecacheEditValue();
                        }
                    }
                }

                // A property edit may change a ShowIf/HideIf condition - toggle
                // affected targets in/out without a full rebuild.
                m_manager.UpdateConditionalVisibility();
            }
        }

        // Drops every per-target PropertyChanged subscription and tears down each target's elevated
        // sub-object INPC hooks. Walks the live tree plus the hidden-conditional targets (which are
        // subscribed even while out of the tree). Called before every rebuild and on dispose / source
        // clear so a torn-down grid does not leave subscriptions pinning the target/source graph.
        private void _TeardownTargetSubscriptions ()
        {
            if (m_manager.RootNode == null)
            {
                return;
            }

            foreach (var target in _WalkAllTargets(m_manager.RootNode))
            {
                target.PropertyChanged -= this.OnAnyTargetPropertyChanged;
                target.Teardown();
            }

            foreach (var target in m_manager.HiddenConditionalTargets)
            {
                target.PropertyChanged -= this.OnAnyTargetPropertyChanged;
                target.Teardown();
            }
        }

        private static IEnumerable<PropertyEditTarget> _WalkAllTargets (IObservableTreeNode node)
        {
            foreach (var child in node.Children)
            {
                if (child is PropertyEditTarget target)
                {
                    yield return target;
                    if (target.ElevatedChildTarget != null)
                    {
                        yield return target.ElevatedChildTarget;
                    }

                    foreach (var descendant in _WalkAllTargets(target))
                    {
                        yield return descendant;
                    }
                }
            }
        }

        // ===========[ Selection handling ]=======================================
        private void OnTreeListSelectionChanged (object sender, EventArgs<SelectionChange<IObservableTreeNode>> e)
        {
            this.SelectedTargetChanged?.Invoke(this, EventArgs.Empty);

            object newSource = this.SelectedSourceObject;
            if (!ReferenceEquals(newSource, m_lastSelectedSourceObject))
            {
                m_lastSelectedSourceObject = newSource;
                this.SelectedSourceObjectChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private PropertyEditTarget FindTargetForSourceObject (object sourceObject)
        {
            if (m_manager.RootNode == null)
            {
                return null;
            }

            foreach (PropertyEditTarget target in _WalkAllTargets(m_manager.RootNode))
            {
                if (ReferenceEquals(target.SourceObject, sourceObject))
                {
                    return target;
                }
            }

            return null;
        }

        // Walk up (including the row itself) to the nearest row that stands in for a source object - i.e.
        // the nearest list-element ancestor - and return its element instance.
        private static object _ResolveSourceObject (PropertyEditTarget target)
        {
            for (PropertyEditTarget cursor = target; cursor != null; cursor = cursor.Parent as PropertyEditTarget)
            {
                object source = cursor.SourceObject;
                if (source != null)
                {
                    return source;
                }
            }

            return null;
        }

        private void OnSetPropertyToDefaultExecuted (object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is PropertyEditTarget target)
            {
                target.ResetToDefault();
            }
        }

        private void OnSetPropertyToDefaultCanExecute (object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = e.Parameter is PropertyEditTarget target && target.HasDefaultValue && !target.IsExpandable;
        }

        // ===========[ Drag/Drop for list element reordering ]====================

        private static bool _CanDragPropertyItem (IObservableTreeNode node)
        {
            return node is PropertyEditTarget target
                && target.IsListElement
                && (target.EditContext as PropertyGridListElementContext)?.ParentListContext?.CanReorder == true;
        }

        private static bool _CanDropPropertyItem (IObservableTreeNode draggedNode, IObservableTreeNode targetParent)
        {
            if (draggedNode is PropertyEditTarget draggedTarget && draggedTarget.IsListElement
                && targetParent is PropertyEditTarget parentTarget && parentTarget.IsListEditor)
            {
                var draggedListCtx = (draggedTarget.EditContext as PropertyGridListElementContext)?.ParentListContext;
                var parentListCtx = parentTarget.EditContext as PropertyGridListContext;
                return draggedListCtx != null && draggedListCtx == parentListCtx;
            }

            return false;
        }

        private void OnDragDropReorderRequested (object sender, FlatTreeReorderEventArgs e)
        {
            if (e.Items.Length == 1 && e.Items[0] is PropertyEditTarget draggedTarget && draggedTarget.IsListElement)
            {
                var elementCtx = draggedTarget.EditContext as PropertyGridListElementContext;
                if (elementCtx?.ParentListContext != null)
                {
                    e.Cancel = true;
                    int fromIndex = elementCtx.Index;
                    int toIndex = e.InsertIndex > fromIndex ? e.InsertIndex - 1 : e.InsertIndex;
                    if (fromIndex != toIndex)
                    {
                        elementCtx.ParentListContext.MoveElement(fromIndex, toIndex);
                    }
                }
            }
        }
    }
}
