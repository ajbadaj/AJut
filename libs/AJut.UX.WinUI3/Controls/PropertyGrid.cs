namespace AJut.UX.Controls
{
    using AJut.Storage;
    using AJut.UX.PropertyInteraction;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Linq;
    using DPUtils = AJut.UX.DPUtils<PropertyGrid>;

    // ===========[ PropertyGrid ]===============================================
    // Reflection-based property editor. Given a C# object (or view model),
    // discovers its public properties and renders each with a type-appropriate
    // editor via ItemTemplateSelector.
    //
    // Consumers register editor templates on PropertyGridTemplateSelector keyed by
    // PropertyEditTarget.Editor (the property type name or [PGEditor] override).
    //
    // Complex reference-type properties whose value is non-null are shown as
    // expandable tree nodes whose children are the sub-object's properties.
    // Uses FlatTreeListControl internally for tree expansion/indentation.
    //
    // Template parts:
    //   PART_TreeList  - the inner FlatTreeListControl that renders property rows

    [TemplatePart(Name = nameof(PART_TreeList), Type = typeof(FlatTreeListControl))]
    public class PropertyGrid : Control, IPropertyGrid, IDisposable
    {
        // ===========[ Instance fields ]==========================================
        private readonly PropertyGridManager m_manager;
        private FlatTreeListControl PART_TreeList;

        // ===========[ Construction ]=============================================
        public PropertyGrid ()
        {
            this.DefaultStyleKey = typeof(PropertyGrid);
            m_manager = new PropertyGridManager(this);
        }

        public void Dispose ()
        {
            m_manager.Dispose();
            this.SingleItemSource = null;
            this.ItemsSource = null;
        }

        public PropertyGridManager Manager => m_manager;

        // ===========[ IPropertyGrid ]===========================================
        IEnumerable IPropertyGrid.ItemsSource => this.ItemsSource;
        object IPropertyGrid.SingleItemSource => this.SingleItemSource;

        // ===========[ Dependency Properties ]====================================
        public static readonly DependencyProperty ItemsSourceProperty = DPUtils.Register(_ => _.ItemsSource, (d, e) => d.OnItemsSourceChanged(e.OldValue, e.NewValue));
        public IEnumerable ItemsSource
        {
            get => (IEnumerable)this.GetValue(ItemsSourceProperty);
            set => this.SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty SingleItemSourceProperty = DPUtils.Register(_ => _.SingleItemSource, (d, e) => d.OnSingleItemSourceChanged(e.OldValue, e.NewValue));
        public object SingleItemSource
        {
            get => this.GetValue(SingleItemSourceProperty);
            set => this.SetValue(SingleItemSourceProperty, value);
        }

        public static readonly DependencyProperty ItemTemplateSelectorProperty = DPUtils.Register(_ => _.ItemTemplateSelector);
        public PropertyGridTemplateSelector ItemTemplateSelector
        {
            get => (PropertyGridTemplateSelector)this.GetValue(ItemTemplateSelectorProperty);
            set => this.SetValue(ItemTemplateSelectorProperty, value);
        }

        public static readonly DependencyProperty RowTemplateProperty = DPUtils.Register(_ => _.RowTemplate, (d, e) => d.ApplyRowTemplate());
        public DataTemplate RowTemplate
        {
            get => (DataTemplate)this.GetValue(RowTemplateProperty);
            set => this.SetValue(RowTemplateProperty, value);
        }

        public static readonly DependencyProperty IsReadOnlyProperty = DPUtils.Register(_ => _.IsReadOnly, false);
        public bool IsReadOnly
        {
            get => (bool)this.GetValue(IsReadOnlyProperty);
            set => this.SetValue(IsReadOnlyProperty, value);
        }

        public static readonly DependencyProperty MaxRecursionDepthProperty = DPUtils.Register(_ => _.MaxRecursionDepth, 5, (d, e) => d.m_manager.MaxRecursionDepth = (int)e.NewValue);
        public int MaxRecursionDepth
        {
            get => (int)this.GetValue(MaxRecursionDepthProperty);
            set => this.SetValue(MaxRecursionDepthProperty, value);
        }

        public static readonly DependencyProperty TreeDepthIndentSizeProperty = DPUtils.Register(_ => _.TreeDepthIndentSize, 16.0, (d, e) => d.OnTreeDepthIndentSizeChanged(e.NewValue));
        public double TreeDepthIndentSize
        {
            get => (double)this.GetValue(TreeDepthIndentSizeProperty);
            set => this.SetValue(TreeDepthIndentSizeProperty, value);
        }
        private void OnTreeDepthIndentSizeChanged (double newValue)
        {
            if (this.PART_TreeList != null)
            {
                this.PART_TreeList.TreeDepthIndentSize = newValue;
            }
        }

        public static readonly DependencyProperty RowSpacingProperty = DPUtils.Register(_ => _.RowSpacing, 2.0, (d, e) => d.OnRowSpacingChanged(e.NewValue));
        public double RowSpacing
        {
            get => (double)this.GetValue(RowSpacingProperty);
            set => this.SetValue(RowSpacingProperty, value);
        }
        private void OnRowSpacingChanged (double newValue)
        {
            if (this.PART_TreeList != null)
            {
                this.PART_TreeList.RowSpacing = newValue;
            }
        }

        public static readonly DependencyProperty FixedRowHeightProperty = DPUtils.Register(_ => _.FixedRowHeight, double.NaN, (d, e) => d.OnFixedRowHeightChanged(e.NewValue));
        public double FixedRowHeight
        {
            get => (double)this.GetValue(FixedRowHeightProperty);
            set => this.SetValue(FixedRowHeightProperty, value);
        }
        private void OnFixedRowHeightChanged (double newValue)
        {
            if (this.PART_TreeList != null)
            {
                this.PART_TreeList.FixedRowHeight = newValue;
            }
        }

        public static readonly DependencyProperty LabelColumnWidthProperty = DPUtils.Register(_ => _.LabelColumnWidth, double.NaN);
        public double LabelColumnWidth
        {
            get => (double)this.GetValue(LabelColumnWidthProperty);
            set => this.SetValue(LabelColumnWidthProperty, value);
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

        public static readonly DependencyProperty InsertionLineXOffsetProperty = DPUtils.Register(_ => _.InsertionLineXOffset, 0.0);
        public double InsertionLineXOffset
        {
            get => (double)this.GetValue(InsertionLineXOffsetProperty);
            set => this.SetValue(InsertionLineXOffsetProperty, value);
        }

        // Padding applied to each PropertyGridItemRow (insets label + editor from the row edges).
        // Read by PropertyGridItemRow.OnLoaded and applied as Padding so the template's
        // {TemplateBinding Padding} drives the inner content grid's Margin.
        public static readonly DependencyProperty ElementPaddingProperty = DPUtils.Register(_ => _.ElementPadding, new Thickness(3, 2, 3, 2));
        public Thickness ElementPadding
        {
            get => (Thickness)this.GetValue(ElementPaddingProperty);
            set => this.SetValue(ElementPaddingProperty, value);
        }

        public static readonly DependencyProperty DragGhostTemplateProperty = DPUtils.Register(_ => _.DragGhostTemplate, (d, e) => d.OnDragGhostTemplateChanged(e.NewValue));
        public DataTemplate DragGhostTemplate
        {
            get => (DataTemplate)this.GetValue(DragGhostTemplateProperty);
            set => this.SetValue(DragGhostTemplateProperty, value);
        }

        // Container style for each ListView row inside the inner FlatTreeListControl.
        // Defaults (via Style Setter) to PropertyGrid_ListViewItemStyle - a minimal no-chrome
        // template that suppresses full-row selection highlight so PropertyGridItemRow's own
        // label-only selection indicator is the only visual. Override to customise container
        // padding or add hover/press visuals without touching the PropertyGrid template.
        public static readonly DependencyProperty ListViewItemContainerStyleProperty = DPUtils.Register(_ => _.ListViewItemContainerStyle, (d, e) => d.OnListViewItemContainerStyleChanged(e.NewValue));
        public Style ListViewItemContainerStyle
        {
            get => (Style)this.GetValue(ListViewItemContainerStyleProperty);
            set => this.SetValue(ListViewItemContainerStyleProperty, value);
        }
        private void OnListViewItemContainerStyleChanged (Style newValue)
        {
            if (this.PART_TreeList != null)
            {
                this.PART_TreeList.ListViewItemContainerStyle = newValue;
            }
        }
        private void OnDragGhostTemplateChanged (DataTemplate newValue)
        {
            if (this.PART_TreeList != null)
            {
                this.PART_TreeList.DragGhostTemplate = newValue;
            }
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
        /// The top-level PropertyEditTarget items (children of the hidden $root node).
        /// Updated whenever RebuildEditTargets runs. Pushed to PART_TreeList.RootItemsSource
        /// in code rather than via TemplateBinding (see PropertyGrid.xaml for why).
        /// </summary>
        public static readonly DependencyProperty PropertyTreeItemsProperty = DPUtils.Register(_ => _.PropertyTreeItems);
        public IReadOnlyList<IObservableTreeNode> PropertyTreeItems
        {
            get => (IReadOnlyList<IObservableTreeNode>)this.GetValue(PropertyTreeItemsProperty);
            private set => this.SetValue(PropertyTreeItemsProperty, value);
        }

        // ===========[ Events ]===================================================
        /// <summary>
        /// Fires whenever any property in the displayed tree is edited via the property grid.
        /// Subscribe to detect any change and refresh displays that mirror the source object.
        /// </summary>
        public event EventHandler PropertyTreeChanged;

        // ===========[ Template application ]====================================
        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            if (this.PART_TreeList != null)
            {
                this.PART_TreeList.DragDropReorderRequested -= this.OnDragDropReorderRequested;
            }

            this.PART_TreeList = this.GetTemplateChild(nameof(PART_TreeList)) as FlatTreeListControl;
            if (this.PART_TreeList != null)
            {
                // WinUI3 TemplateBinding does not read the current source DP value at
                // binding-establishment time - it only reacts to post-establishment changes.
                // RebuildEditTargets() commonly runs before the template is applied (e.g. when
                // the PropertyGrid lives in a tab that isn't yet selected), so push manually.
                this.PART_TreeList.RootItemsSource = this.PropertyTreeItems;
                this.PART_TreeList.TreeDepthIndentSize = this.TreeDepthIndentSize;
                this.PART_TreeList.RowSpacing = this.RowSpacing;
                this.PART_TreeList.FixedRowHeight = this.FixedRowHeight;
                this.PART_TreeList.ListViewItemContainerStyle = this.ListViewItemContainerStyle;

                // Enable drag/drop reordering for list elements
                this.PART_TreeList.IsDragDropReorderEnabled = true;
                this.PART_TreeList.CanDragItem = _CanDragPropertyItem;
                this.PART_TreeList.CanDropItem = _CanDropPropertyItem;
                this.PART_TreeList.DragDropReorderRequested += this.OnDragDropReorderRequested;

                // Suppress list add/remove/reorder animations in the PropertyGrid
                this.PART_TreeList.SuppressItemTransitions = true;

                // Forward drag ghost template (push manually - WinUI3 TemplateBinding doesn't backfill)
                this.PART_TreeList.DragGhostTemplate = this.DragGhostTemplate;
            }

            this.ApplyRowTemplate();
        }

        // ===========[ Property change handlers ]=================================
        private void OnSingleItemSourceChanged (object oldValue, object newValue)
        {
            if (oldValue is INotifyPropertyChanged oldPropChanged)
            {
                oldPropChanged.PropertyChanged -= this.OnSourceItemPropertyChanged;
            }

            if (newValue != null)
            {
                this.ItemsSource = null;
                this.RebuildEditTargets();

                if (newValue is INotifyPropertyChanged newPropChanged)
                {
                    newPropChanged.PropertyChanged += this.OnSourceItemPropertyChanged;
                }
            }
            else
            {
                m_manager.Dispose();
                this.PropertyTreeItems = null;
            }
        }

        private void OnItemsSourceChanged (IEnumerable oldValue, IEnumerable newValue)
        {
            if (oldValue is INotifyCollectionChanged oldCollectionChange)
            {
                oldCollectionChange.CollectionChanged -= this.ItemsSource_OnCollectionChanged;
                foreach (INotifyPropertyChanged pc in oldValue.OfType<INotifyPropertyChanged>())
                {
                    pc.PropertyChanged -= this.OnSourceItemPropertyChanged;
                }
            }

            if (newValue != null)
            {
                this.RebuildEditTargets();

                if (newValue is INotifyCollectionChanged newCollectionChange)
                {
                    newCollectionChange.CollectionChanged += this.ItemsSource_OnCollectionChanged;
                }

                foreach (INotifyPropertyChanged pc in newValue.OfType<INotifyPropertyChanged>())
                {
                    pc.PropertyChanged += this.OnSourceItemPropertyChanged;
                }
            }
            else
            {
                m_manager.Dispose();
                this.PropertyTreeItems = null;
            }
        }

        private void OnSourceItemPropertyChanged (object sender, PropertyChangedEventArgs e)
        {
            foreach (var target in m_manager.Items.OfType<PropertyEditTarget>())
            {
                if (target.ShouldEvaluateFor(e.PropertyName))
                {
                    target.RecacheEditValue();
                }
            }

            // External INPC changes may affect ShowIf/HideIf conditions
            m_manager.UpdateConditionalVisibility();
        }

        private void ItemsSource_OnCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            this.RebuildEditTargets();
        }

        private void RebuildEditTargets ()
        {
            // Unsubscribe from old tree (and hidden conditional targets) before rebuild.
            if (m_manager.RootNode != null)
            {
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

            m_manager.RebuildEditTargets();

            // Pass the root's children (not the root itself) as RootItemsSource so that
            // FlatTreeListControl.CreateUberRoot wraps each top-level property in a FlatTreeItem
            // with the uber root always expanded - making all top-level rows visible immediately
            // while sub-object rows start collapsed until the user expands them.
            this.PropertyTreeItems = m_manager.RootNode != null
                ? ((IObservableTreeNode)m_manager.RootNode).Children
                : null;

            // Also push directly in case the template is already applied (TemplateBinding in
            // WinUI3 does not backfill pre-establishment values, so OnApplyTemplate handles
            // the opposite ordering; this handles source changes after the template is applied).
            if (this.PART_TreeList != null)
            {
                this.PART_TreeList.RootItemsSource = this.PropertyTreeItems;
            }

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

        private void ApplyRowTemplate ()
        {
            if (this.PART_TreeList != null)
            {
                this.PART_TreeList.ItemTemplate = this.RowTemplate;
            }
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
