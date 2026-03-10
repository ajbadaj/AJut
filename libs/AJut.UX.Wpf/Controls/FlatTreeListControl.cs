namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Shapes;
    using AJut;
    using AJut.Storage;
    using AJut.Tree;
    using AJut.UX;
    using DPUtils = DPUtils<FlatTreeListControl>;

    /// <summary>
    /// A tree control visualized as a flat virtualized list. Nodes must implement
    /// <see cref="IObservableTreeNode"/>. Bind <see cref="Root"/> (single root) or
    /// <see cref="RootItemsSource"/> (multiple roots) to populate.
    /// </summary>
    [TemplatePart(Name = nameof(PART_ListBoxDisplay), Type = typeof(ListBox))]
    [TemplatePart(Name = nameof(PART_DragOverlay), Type = typeof(Canvas))]
    [TemplatePart(Name = nameof(PART_InsertionLine), Type = typeof(Rectangle))]
    [TemplatePart(Name = nameof(PART_ParentConnectorLine), Type = typeof(Polyline))]
    [TemplatePart(Name = nameof(PART_DragGhost), Type = typeof(Border))]
    [TemplatePart(Name = nameof(PART_DragGhostContent), Type = typeof(ContentPresenter))]
    public class FlatTreeListControl : Control
    {
        // ===========[ Constants ]================================================
        private const double kDragThresholdPx = 6.0;
        private const double kInsertionLineHeight = 3.0;
        private const double kParentConnectorWidth = 2.0;

        private ListBox PART_ListBoxDisplay;
        private Canvas PART_DragOverlay;
        private Rectangle PART_InsertionLine;
        private Polyline PART_ParentConnectorLine;
        private Border PART_DragGhost;
        private ContentPresenter PART_DragGhostContent;
        private bool m_blockingForSelectionChangeReentrancy;

        // Drag state
        private bool m_isDragPending;
        private bool m_isDragging;
        private Point m_dragStartPoint;
        private FlatTreeItem[] m_dragItems;
        private double m_rowXInOverlay = double.NaN;
        private FlatTreeDropTarget m_currentDropTarget;
        private ToggleButton m_highlightedExpander;
        private Brush m_highlightedExpanderOriginalBrush;

        // ========================[Construction]==============================
        static FlatTreeListControl ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FlatTreeListControl), new FrameworkPropertyMetadata(typeof(FlatTreeListControl)));
        }

        public FlatTreeListControl ()
        {
            this.Items = new ItemsStorageContainer(this);
            this.SelectedItems = new ObservableCollection<IObservableTreeNode>();
            this.SelectedItems.CollectionChanged += this.SelectedItems_OnCollectionChanged;

            // Sync RowSpacingThickness with the default RowSpacing value since the
            // DP change handler only fires on changes, not on initialization.
            this.RowSpacingThickness = new Thickness(0, 0, 0, this.RowSpacing);
        }

        // ============================[Events]================================
        public event EventHandler<EventArgs<FlatTreeItem>> StorageItemAdded;
        public event EventHandler<EventArgs<FlatTreeItem>> StorageItemRemoved;
        public event EventHandler<EventArgs<SelectionChange<IObservableTreeNode>>> SelectionChanged;

        /// Fires before a drag/drop reorder is executed. Set Cancel=true to prevent
        /// the default reorder (e.g. to wrap in undo/redo instead).
        public event EventHandler<FlatTreeReorderEventArgs> DragDropReorderRequested;

        /// Fires when an external item is dropped onto the tree (extensibility hook).
        public event EventHandler<FlatTreeExternalDropEventArgs> ExternalItemDropped;

        // =================[Core Dependency Properties]===================
        public static readonly DependencyProperty RootProperty = DPUtils.Register(_ => _.Root, (d, e) => d.OnRootChanged(e.NewValue));
        public IObservableTreeNode Root
        {
            get => (IObservableTreeNode)this.GetValue(RootProperty);
            set => this.SetValue(RootProperty, value);
        }
        private void OnRootChanged (IObservableTreeNode newRoot)
        {
            this.Items.IncludeRoot = this.IncludeRoot;
            this.Items.RootNode = newRoot == null
                ? null
                : FlatTreeItem.CreateRoot(newRoot, this.TreeDepthIndentSize, this.StartItemsExpanded);
        }

        public static readonly DependencyProperty RootItemsSourceProperty = DPUtils.Register(_ => _.RootItemsSource, (d, e) => d.OnRootItemsSourceChanged(e.OldValue, e.NewValue));
        public IEnumerable<IObservableTreeNode> RootItemsSource
        {
            get => (IEnumerable<IObservableTreeNode>)this.GetValue(RootItemsSourceProperty);
            set => this.SetValue(RootItemsSourceProperty, value);
        }
        private void OnRootItemsSourceChanged (IEnumerable<IObservableTreeNode> oldValue, IEnumerable<IObservableTreeNode> newValue)
        {
            // IncludeRoot must be false before setting RootNode so the uber root is never
            // subscribed (observed) by the store - it will be rebuilt from scratch on each
            // collection change rather than incrementally updated via events on the root.
            this.IncludeRoot = false;
            this.Items.RootNode = FlatTreeItem.CreateUberRoot(newValue ?? Enumerable.Empty<IObservableTreeNode>(), this.TreeDepthIndentSize);

            if (oldValue is INotifyCollectionChanged oldObservable)
            {
                oldObservable.CollectionChanged -= this.RootItems_OnCollectionChanged;
            }
            if (newValue is INotifyCollectionChanged newObservable)
            {
                newObservable.CollectionChanged += this.RootItems_OnCollectionChanged;
            }
        }

        private static readonly DependencyPropertyKey IncludeRootPropertyKey = DPUtils.RegisterReadOnly(_ => _.IncludeRoot, true, (d, e) => d.Items.IncludeRoot = e.HasNewValue ? e.NewValue : true);
        public static readonly DependencyProperty IncludeRootProperty = IncludeRootPropertyKey.DependencyProperty;
        public bool IncludeRoot
        {
            get => (bool)this.GetValue(IncludeRootProperty);
            protected set => this.SetValue(IncludeRootPropertyKey, value);
        }

        public static readonly DependencyProperty SelectedItemProperty = DPUtils.Register(_ => _.SelectedItem, (d, e) => d.OnSelectedItemChanged(e));
        public IObservableTreeNode SelectedItem
        {
            get => (IObservableTreeNode)this.GetValue(SelectedItemProperty);
            set => this.SetValue(SelectedItemProperty, value);
        }

        private static readonly DependencyPropertyKey SelectedItemsPropertyKey = DPUtils.RegisterReadOnly(_ => _.SelectedItems);
        public static readonly DependencyProperty SelectedItemsProperty = SelectedItemsPropertyKey.DependencyProperty;
        public ObservableCollection<IObservableTreeNode> SelectedItems
        {
            get => (ObservableCollection<IObservableTreeNode>)this.GetValue(SelectedItemsProperty);
            protected set => this.SetValue(SelectedItemsPropertyKey, value);
        }

        public static readonly DependencyProperty SelectionModeProperty = DPUtils.Register(_ => _.SelectionMode, SelectionMode.Extended);
        public SelectionMode SelectionMode
        {
            get => (SelectionMode)this.GetValue(SelectionModeProperty);
            set => this.SetValue(SelectionModeProperty, value);
        }

        public static readonly DependencyProperty StartItemsExpandedProperty = DPUtils.Register(_ => _.StartItemsExpanded);
        public bool StartItemsExpanded
        {
            get => (bool)this.GetValue(StartItemsExpandedProperty);
            set => this.SetValue(StartItemsExpandedProperty, value);
        }

        // ===================[Display Dependency Properties]===================
        public static readonly DependencyProperty TreeDepthIndentSizeProperty = DPUtils.Register(_ => _.TreeDepthIndentSize, 8.0, (d, e) => d.OnTreeDepthIndentSizeChanged());
        public double TreeDepthIndentSize
        {
            get => (double)this.GetValue(TreeDepthIndentSizeProperty);
            set => this.SetValue(TreeDepthIndentSizeProperty, value);
        }
        private void OnTreeDepthIndentSizeChanged ()
        {
            if (this.Items?.RootNode == null)
            {
                return;
            }

            double size = this.TreeDepthIndentSize;
            foreach (FlatTreeItem item in TreeTraversal<FlatTreeItem>.All(this.Items.RootNode))
            {
                item.TreeDepthIndentSize = size;
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
            this.RowSpacingThickness = new Thickness(0, 0, 0, newValue);
        }

        private static readonly DependencyPropertyKey RowSpacingThicknessPropertyKey = DPUtils.RegisterReadOnly(_ => _.RowSpacingThickness);
        public static readonly DependencyProperty RowSpacingThicknessProperty = RowSpacingThicknessPropertyKey.DependencyProperty;
        public Thickness RowSpacingThickness
        {
            get => (Thickness)this.GetValue(RowSpacingThicknessProperty);
            private set => this.SetValue(RowSpacingThicknessPropertyKey, value);
        }

        public static readonly DependencyProperty FixedRowHeightProperty = DPUtils.Register(_ => _.FixedRowHeight, double.NaN);
        public double FixedRowHeight
        {
            get => (double)this.GetValue(FixedRowHeightProperty);
            set => this.SetValue(FixedRowHeightProperty, value);
        }

        private static readonly DependencyPropertyKey ItemsPropertyKey = DPUtils.RegisterReadOnly(_ => _.Items);
        public static readonly DependencyProperty ItemsProperty = ItemsPropertyKey.DependencyProperty;
        public ObservableFlatTreeStore<FlatTreeItem> Items
        {
            get => (ObservableFlatTreeStore<FlatTreeItem>)this.GetValue(ItemsProperty);
            protected set => this.SetValue(ItemsPropertyKey, value);
        }

        public static readonly DependencyProperty ItemTemplateProperty = DPUtils.Register(_ => _.ItemTemplate);
        public DataTemplate ItemTemplate
        {
            get => (DataTemplate)this.GetValue(ItemTemplateProperty);
            set => this.SetValue(ItemTemplateProperty, value);
        }

        public static readonly DependencyProperty ItemTemplateSelectorProperty = DPUtils.Register(_ => _.ItemTemplateSelector);
        public DataTemplateSelector ItemTemplateSelector
        {
            get => (DataTemplateSelector)this.GetValue(ItemTemplateSelectorProperty);
            set => this.SetValue(ItemTemplateSelectorProperty, value);
        }

        public static readonly DependencyProperty SelectionBrushProperty = DPUtils.Register(_ => _.SelectionBrush);
        public System.Windows.Media.Brush SelectionBrush
        {
            get => (System.Windows.Media.Brush)this.GetValue(SelectionBrushProperty);
            set => this.SetValue(SelectionBrushProperty, value);
        }

        public static readonly DependencyProperty SelectionInactiveBrushProperty = DPUtils.Register(_ => _.SelectionInactiveBrush);
        public System.Windows.Media.Brush SelectionInactiveBrush
        {
            get => (System.Windows.Media.Brush)this.GetValue(SelectionInactiveBrushProperty);
            set => this.SetValue(SelectionInactiveBrushProperty, value);
        }

        public static readonly DependencyProperty CollapsedElementGlyphProperty = DPUtils.Register(_ => _.CollapsedElementGlyph);
        public object CollapsedElementGlyph
        {
            get => this.GetValue(CollapsedElementGlyphProperty);
            set => this.SetValue(CollapsedElementGlyphProperty, value);
        }

        public static readonly DependencyProperty ExpandedElementGlyphProperty = DPUtils.Register(_ => _.ExpandedElementGlyph);
        public object ExpandedElementGlyph
        {
            get => this.GetValue(ExpandedElementGlyphProperty);
            set => this.SetValue(ExpandedElementGlyphProperty, value);
        }

        public static readonly DependencyProperty ExpandCollapseGlyphSizeProperty = DPUtils.Register(_ => _.ExpandCollapseGlyphSize);
        public double ExpandCollapseGlyphSize
        {
            get => (double)this.GetValue(ExpandCollapseGlyphSizeProperty);
            set => this.SetValue(ExpandCollapseGlyphSizeProperty, value);
        }

        public static readonly DependencyProperty GlyphBrushProperty = DPUtils.Register(_ => _.GlyphBrush);
        public System.Windows.Media.Brush GlyphBrush
        {
            get => (System.Windows.Media.Brush)this.GetValue(GlyphBrushProperty);
            set => this.SetValue(GlyphBrushProperty, value);
        }

        public static readonly DependencyProperty GlyphHighlightBrushProperty = DPUtils.Register(_ => _.GlyphHighlightBrush);
        public System.Windows.Media.Brush GlyphHighlightBrush
        {
            get => (System.Windows.Media.Brush)this.GetValue(GlyphHighlightBrushProperty);
            set => this.SetValue(GlyphHighlightBrushProperty, value);
        }

        public static readonly DependencyProperty GlyphBackgroundHighlightBrushProperty = DPUtils.Register(_ => _.GlyphBackgroundHighlightBrush);
        public System.Windows.Media.Brush GlyphBackgroundHighlightBrush
        {
            get => (System.Windows.Media.Brush)this.GetValue(GlyphBackgroundHighlightBrushProperty);
            set => this.SetValue(GlyphBackgroundHighlightBrushProperty, value);
        }

        public static readonly DependencyProperty GlyphPaddingProperty = DPUtils.Register(_ => _.GlyphPadding);
        public Thickness GlyphPadding
        {
            get => (Thickness)this.GetValue(GlyphPaddingProperty);
            set => this.SetValue(GlyphPaddingProperty, value);
        }

        public static readonly DependencyProperty ListBoxItemContainerStyleProperty = DPUtils.Register(_ => _.ListBoxItemContainerStyle);
        public Style ListBoxItemContainerStyle
        {
            get => (Style)this.GetValue(ListBoxItemContainerStyleProperty);
            set => this.SetValue(ListBoxItemContainerStyleProperty, value);
        }

        public static readonly DependencyProperty ShouldToggleExpandableItemExpansionOnDoubleClickProperty = DPUtils.Register(_ => _.ShouldToggleExpandableItemExpansionOnDoubleClick, true);
        public bool ShouldToggleExpandableItemExpansionOnDoubleClick
        {
            get => (bool)this.GetValue(ShouldToggleExpandableItemExpansionOnDoubleClickProperty);
            set => this.SetValue(ShouldToggleExpandableItemExpansionOnDoubleClickProperty, value);
        }

        // ---- Drag/Drop Reorder DPs ----

        public static readonly DependencyProperty IsDragDropReorderEnabledProperty = DPUtils.Register(_ => _.IsDragDropReorderEnabled);
        public bool IsDragDropReorderEnabled
        {
            get => (bool)this.GetValue(IsDragDropReorderEnabledProperty);
            set => this.SetValue(IsDragDropReorderEnabledProperty, value);
        }

        public static readonly DependencyProperty CanDropItemProperty = DPUtils.Register(_ => _.CanDropItem);
        public Func<IObservableTreeNode, IObservableTreeNode, bool> CanDropItem
        {
            get => (Func<IObservableTreeNode, IObservableTreeNode, bool>)this.GetValue(CanDropItemProperty);
            set => this.SetValue(CanDropItemProperty, value);
        }

        public static readonly DependencyProperty CanDragItemProperty = DPUtils.Register(_ => _.CanDragItem);
        public Func<IObservableTreeNode, bool> CanDragItem
        {
            get => (Func<IObservableTreeNode, bool>)this.GetValue(CanDragItemProperty);
            set => this.SetValue(CanDragItemProperty, value);
        }

        public static readonly DependencyProperty InsertionLineBrushProperty = DPUtils.Register(_ => _.InsertionLineBrush);
        public Brush InsertionLineBrush
        {
            get => (Brush)this.GetValue(InsertionLineBrushProperty);
            set => this.SetValue(InsertionLineBrushProperty, value);
        }

        public static readonly DependencyProperty ParentConnectorBrushProperty = DPUtils.Register(_ => _.ParentConnectorBrush);
        public Brush ParentConnectorBrush
        {
            get => (Brush)this.GetValue(ParentConnectorBrushProperty);
            set => this.SetValue(ParentConnectorBrushProperty, value);
        }

        public static readonly DependencyProperty DragTargetHighlightBrushProperty = DPUtils.Register(_ => _.DragTargetHighlightBrush);
        public Brush DragTargetHighlightBrush
        {
            get => (Brush)this.GetValue(DragTargetHighlightBrushProperty);
            set => this.SetValue(DragTargetHighlightBrushProperty, value);
        }

        /// <summary>
        /// DataTemplate for the drag ghost element shown during drag-drop.
        /// DataContext is the source item (IObservableTreeNode). When null,
        /// falls back to Source.ToString(). For multi-item drag shows "N items".
        /// </summary>
        public static readonly DependencyProperty DragGhostTemplateProperty = DPUtils.Register(_ => _.DragGhostTemplate);
        public DataTemplate DragGhostTemplate
        {
            get => (DataTemplate)this.GetValue(DragGhostTemplateProperty);
            set => this.SetValue(DragGhostTemplateProperty, value);
        }

        /// <summary>
        /// Extra horizontal offset (px) added to the drag insertion line X position.
        /// The base formula aligns to the default ListBoxItem layout. Override in consumers
        /// (e.g. PropertyGrid) if a different item container style shifts row content.
        /// </summary>
        public static readonly DependencyProperty InsertionLineXOffsetProperty = DPUtils.Register(_ => _.InsertionLineXOffset, 0.0);
        public double InsertionLineXOffset
        {
            get => (double)this.GetValue(InsertionLineXOffsetProperty);
            set => this.SetValue(InsertionLineXOffsetProperty, value);
        }

        // ============================[Methods]================================
        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            if (this.PART_ListBoxDisplay != null)
            {
                this.PART_ListBoxDisplay.SelectionChanged -= this.ListBox_OnSelectionChanged;
                this.PART_ListBoxDisplay.MouseDoubleClick -= this.ListBox_OnMouseDoubleClick;
                this.PART_ListBoxDisplay.PreviewMouseLeftButtonDown -= this.ListBox_OnPreviewMouseLeftButtonDown;
                this.PART_ListBoxDisplay.PreviewMouseMove -= this.ListBox_OnPreviewMouseMove;
                this.PART_ListBoxDisplay.PreviewMouseLeftButtonUp -= this.ListBox_OnPreviewMouseLeftButtonUp;
            }

            this.PART_ListBoxDisplay = (ListBox)this.GetTemplateChild(nameof(PART_ListBoxDisplay));
            this.PART_DragOverlay = this.GetTemplateChild(nameof(PART_DragOverlay)) as Canvas;
            this.PART_InsertionLine = this.GetTemplateChild(nameof(PART_InsertionLine)) as Rectangle;
            this.PART_ParentConnectorLine = this.GetTemplateChild(nameof(PART_ParentConnectorLine)) as Polyline;
            this.PART_DragGhost = this.GetTemplateChild(nameof(PART_DragGhost)) as Border;
            this.PART_DragGhostContent = this.GetTemplateChild(nameof(PART_DragGhostContent)) as ContentPresenter;

            if (this.PART_ListBoxDisplay != null)
            {
                this.PART_ListBoxDisplay.SelectionChanged += this.ListBox_OnSelectionChanged;
                this.PART_ListBoxDisplay.MouseDoubleClick += this.ListBox_OnMouseDoubleClick;
                this.PART_ListBoxDisplay.PreviewMouseLeftButtonDown += this.ListBox_OnPreviewMouseLeftButtonDown;
                this.PART_ListBoxDisplay.PreviewMouseMove += this.ListBox_OnPreviewMouseMove;
                this.PART_ListBoxDisplay.PreviewMouseLeftButtonUp += this.ListBox_OnPreviewMouseLeftButtonUp;
            }
        }

        public IEnumerable<FlatTreeItem> AllItems ()
        {
            return this.Items?.RootNode == null
                ? Enumerable.Empty<FlatTreeItem>()
                : TreeTraversal<FlatTreeItem>.All(this.Items.RootNode);
        }

        public FlatTreeItem StorageItemForNode (IObservableTreeNode sourceNode)
        {
            return this.AllItems().FirstOrDefault(i => i.Source == sourceNode);
        }

        protected override void OnGotFocus (RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            this.PART_ListBoxDisplay?.Focus();
        }

        public void FocusInside ()
        {
            this.PART_ListBoxDisplay?.Focus();
        }

        protected override void OnKeyUp (KeyEventArgs e)
        {
            if (e.Key == Key.Left)
            {
                foreach (FlatTreeItem selected in this.SelectedItems.Select(this.StorageItemForNode).Where(i => i != null))
                {
                    selected.IsExpanded = false;
                }
                e.Handled = true;
                return;
            }
            else if (e.Key == Key.Right)
            {
                foreach (FlatTreeItem selected in this.SelectedItems.Select(this.StorageItemForNode).Where(i => i != null))
                {
                    selected.IsExpanded = true;
                }
                e.Handled = true;
                return;
            }

            base.OnKeyUp(e);
        }

        // ============================[Private helpers]================================
        private void OnItemRemoved (FlatTreeItem item)
        {
            this.SelectedItems.Remove(item.Source);
            if (this.SelectedItem == item.Source)
            {
                this.SelectedItem = null;
            }
            this.StorageItemRemoved?.Invoke(this, new EventArgs<FlatTreeItem>(item));
        }

        private void OnSelectedItemChanged (DependencyPropertyChangedEventArgs<IObservableTreeNode> e)
        {
            if (m_blockingForSelectionChangeReentrancy || this.PART_ListBoxDisplay == null)
            {
                return;
            }

            if (e.NewValue != null)
            {
                FlatTreeItem item = this.PART_ListBoxDisplay.Items.OfType<FlatTreeItem>().FirstOrDefault(i => i.Source == e.NewValue);
                this.PART_ListBoxDisplay.SelectedItem = item;
            }
        }

        private void ApplySelectionChanges (IEnumerable<IObservableTreeNode> added, IEnumerable<IObservableTreeNode> removed)
        {
            this.SelectedItem = (this.PART_ListBoxDisplay.SelectedItem as FlatTreeItem)?.Source;
            bool isClear = this.SelectedItems.Count == 0;
            this.SelectionChanged?.Invoke(this, new EventArgs<SelectionChange<IObservableTreeNode>>(
                new SelectionChange<IObservableTreeNode>(added?.ToArray(), removed?.ToArray(), isClear)));
        }

        private void ApplySelectionOfReaddedItem (FlatTreeItem item)
        {
            if (!item.IsSelected)
            {
                return;
            }

            if (this.SelectedItems.Contains(item.Source))
            {
                // Was selected and collapsed - restore visual selection
                if (this.PART_ListBoxDisplay != null && !this.PART_ListBoxDisplay.SelectedItems.Contains(item))
                {
                    this.PART_ListBoxDisplay.SelectedItems.Add(item);
                }
            }
            else
            {
                // Selected but not yet tracked
                this.SelectedItems.Add(item.Source);
            }
        }

        private void DeselctAllBut (IObservableTreeNode keep)
        {
            foreach (FlatTreeItem item in this.SelectedItems.Select(this.StorageItemForNode).Where(i => i != null && i.Source != keep))
            {
                item.IsSelected = false;
            }
        }

        private IDisposable TemporarilyBlockSelectionChanges ()
        {
            m_blockingForSelectionChangeReentrancy = true;
            return new DisposeActionTrigger(() => m_blockingForSelectionChangeReentrancy = false);
        }

        // ============================[Event handlers]================================
        private void Item_IsSelectedChanged (object sender, EventArgs e)
        {
            if (m_blockingForSelectionChangeReentrancy || this.PART_ListBoxDisplay == null)
            {
                return;
            }

            m_blockingForSelectionChangeReentrancy = true;
            try
            {
                var added = new List<IObservableTreeNode>();
                var removed = new List<IObservableTreeNode>();
                var item = (FlatTreeItem)sender;

                if (item.IsSelected)
                {
                    added.Add(item.Source);
                    if (this.SelectionMode == SelectionMode.Single && this.SelectedItem != null)
                    {
                        removed.Add(this.SelectedItem);
                    }

                    this.PART_ListBoxDisplay.SelectedItem = item;
                    if (this.SelectionMode == SelectionMode.Single)
                    {
                        this.DeselctAllBut(item.Source);
                        this.SelectedItems.ResetTo(new[] { item.Source });
                    }
                    else
                    {
                        this.SelectedItems.Add(item.Source);
                    }
                }
                else
                {
                    removed.Add(item.Source);
                    if (this.SelectionMode == SelectionMode.Single && this.PART_ListBoxDisplay.SelectedItem == item)
                    {
                        this.DeselctAllBut(null);
                        this.PART_ListBoxDisplay.SelectedItem = null;
                    }
                    else
                    {
                        this.PART_ListBoxDisplay.SelectedItems.Remove(item);
                    }

                    this.SelectedItems.Remove(item.Source);
                }

                this.ApplySelectionChanges(added, removed);
            }
            finally
            {
                m_blockingForSelectionChangeReentrancy = false;
            }
        }

        private void ListBox_OnSelectionChanged (object sender, SelectionChangedEventArgs e)
        {
            if (m_blockingForSelectionChangeReentrancy)
            {
                return;
            }

            m_blockingForSelectionChangeReentrancy = true;
            try
            {
                IObservableTreeNode[] added = null;
                if (e.AddedItems != null)
                {
                    var storeItems = e.AddedItems.OfType<FlatTreeItem>().ToList();
                    added = storeItems.Select(i => i.Source).ToArray();
                    this.SelectedItems.AddEach(added);
                    foreach (FlatTreeItem item in storeItems)
                    {
                        item.IsSelected = true;
                    }
                }

                IObservableTreeNode[] removed = null;
                if (!e.RemovedItems.IsNullOrEmpty())
                {
                    var storeItems = e.RemovedItems.OfType<FlatTreeItem>().ToList();
                    removed = storeItems.Select(i => i.Source).ToArray();
                    this.SelectedItems.RemoveEach(removed);
                    foreach (FlatTreeItem item in storeItems)
                    {
                        item.IsSelected = false;
                    }
                }

                this.ApplySelectionChanges(added, removed);
            }
            finally
            {
                m_blockingForSelectionChangeReentrancy = false;
            }
        }

        private void ListBox_OnMouseDoubleClick (object sender, MouseButtonEventArgs e)
        {
            if (this.ShouldToggleExpandableItemExpansionOnDoubleClick
                && !IsDoubleClickFromInteractiveControl(e.OriginalSource as DependencyObject)
                && this.PART_ListBoxDisplay.SelectedItem is FlatTreeItem item
                && item.IsExpandable)
            {
                item.IsExpanded = !item.IsExpanded;
            }
        }

        // Returns true if the click source is inside a ButtonBase or TextBoxBase before
        // reaching a ListBoxItem - used to prevent double-click expand/collapse from firing
        // when the user rapidly clicks inside an editor (NumericEditor buttons, TextBox, etc.).
        private static bool IsDoubleClickFromInteractiveControl (DependencyObject source)
        {
            var current = source;
            while (current != null)
            {
                if (current is ButtonBase || current is TextBoxBase)
                {
                    return true;
                }
                if (current is ListBoxItem)
                {
                    return false;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        private void SelectedItems_OnCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            if (m_blockingForSelectionChangeReentrancy || this.PART_ListBoxDisplay == null)
            {
                return;
            }

            m_blockingForSelectionChangeReentrancy = true;
            try
            {
                IObservableTreeNode[] removed = null;
                if (!e.OldItems.IsNullOrEmpty())
                {
                    removed = e.OldItems.OfType<IObservableTreeNode>().ToArray();
                    foreach (FlatTreeItem item in removed.Select(this.StorageItemForNode).Where(i => i != null))
                    {
                        item.IsSelected = false;
                        this.PART_ListBoxDisplay.SelectedItems.Remove(item);
                    }
                }

                IObservableTreeNode[] added = null;
                if (!e.NewItems.IsNullOrEmpty())
                {
                    added = e.NewItems.OfType<IObservableTreeNode>().ToArray();
                    foreach (FlatTreeItem item in added.Select(this.StorageItemForNode).Where(i => i != null))
                    {
                        item.IsSelected = true;
                        this.PART_ListBoxDisplay.SelectedItems.Add(item);
                    }
                }

                // Clear
                if (added == null && removed == null)
                {
                    foreach (FlatTreeItem item in this.Items)
                    {
                        item.IsSelected = false;
                    }
                    this.PART_ListBoxDisplay.SelectedItem = null;
                }

                this.ApplySelectionChanges(added, removed);
            }
            finally
            {
                m_blockingForSelectionChangeReentrancy = false;
            }
        }

        private void RootItems_OnCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            // ObservableFlatTreeStore does not observe (subscribe to) the uber root when IncludeRoot=false,
            // so InsertChild/RemoveChild on the uber root fires events no one hears. Rebuild from the
            // current full state of RootItemsSource on every change instead.
            this.Items.RootNode = FlatTreeItem.CreateUberRoot(
                this.RootItemsSource ?? Enumerable.Empty<IObservableTreeNode>(), this.TreeDepthIndentSize);
        }

        // ============================[Drag/Drop Mouse Handlers]================================

        private void ListBox_OnPreviewMouseLeftButtonDown (object sender, MouseButtonEventArgs e)
        {
            if (!this.IsDragDropReorderEnabled || m_isDragging)
            {
                return;
            }

            m_isDragPending = true;
            m_dragStartPoint = e.GetPosition(this.PART_ListBoxDisplay);
        }

        private void ListBox_OnPreviewMouseMove (object sender, MouseEventArgs e)
        {
            if (!this.IsDragDropReorderEnabled || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            Point currentPoint = e.GetPosition(this.PART_ListBoxDisplay);

            if (m_isDragPending && !m_isDragging)
            {
                double dx = currentPoint.X - m_dragStartPoint.X;
                double dy = currentPoint.Y - m_dragStartPoint.Y;
                if (Math.Sqrt(dx * dx + dy * dy) >= kDragThresholdPx)
                {
                    this.BeginDragDropItemMove();
                }
                else
                {
                    return;
                }
            }

            if (!m_isDragging)
            {
                return;
            }

            this.UpdateDragDropItemMove(currentPoint);
            e.Handled = true;
        }

        private void ListBox_OnPreviewMouseLeftButtonUp (object sender, MouseButtonEventArgs e)
        {
            if (m_isDragging)
            {
                this.CompleteDrag_Wpf();
                e.Handled = true;
            }

            m_isDragPending = false;
        }

        private void BeginDragDropItemMove ()
        {
            // Reset measured row position (will be lazily computed in UpdateDragDropItemMove)
            m_rowXInOverlay = double.NaN;

            // Find the item directly under the press point
            FlatTreeItem pressedItem = this.FindFlatTreeItemAtPoint_Wpf(m_dragStartPoint);

            if (pressedItem != null && pressedItem.IsSelected && this.SelectedItems.Count > 0)
            {
                // Pressed item is part of the current selection - drag all selected items
                // (supports multi-select drag where you select several, then drag one)
                m_dragItems = this.SelectedItems
                    .Select(this.StorageItemForNode)
                    .Where(i => i != null && i.TreeDepth > 0 && !i.IsFalseRoot)
                    .ToArray();
            }
            else if (pressedItem != null && pressedItem.TreeDepth > 0 && !pressedItem.IsFalseRoot)
            {
                // Pressed item is NOT selected - drag just this single item.
                // Allows click-hold-drag without requiring prior selection.
                m_dragItems = new[] { pressedItem };
            }

            if (m_dragItems == null || m_dragItems.Length == 0)
            {
                m_isDragPending = false;
                return;
            }

            // Apply CanDragItem filter
            if (this.CanDragItem != null)
            {
                m_dragItems = m_dragItems.Where(i => this.CanDragItem(i.Source)).ToArray();
                if (m_dragItems.Length == 0)
                {
                    m_isDragPending = false;
                    return;
                }
            }

            m_isDragging = true;
            m_isDragPending = false;

            if (this.PART_DragOverlay != null)
            {
                this.PART_DragOverlay.Visibility = Visibility.Visible;
                // Enable hit testing with transparent background to block hover highlights
                // on the ListBox rows underneath during drag
                this.PART_DragOverlay.IsHitTestVisible = true;
                this.PART_DragOverlay.Background = Brushes.Transparent;
            }

            // Setup ghost content
            if (this.PART_DragGhostContent != null)
            {
                if (m_dragItems.Length == 1 && this.DragGhostTemplate != null)
                {
                    // Use the template with the source item as DataContext
                    this.PART_DragGhostContent.ContentTemplate = this.DragGhostTemplate;
                    this.PART_DragGhostContent.Content = m_dragItems[0].Source;
                }
                else
                {
                    // Fallback: plain text label
                    string label = m_dragItems.Length == 1
                        ? (m_dragItems[0].Source?.ToString() ?? "Item")
                        : $"{m_dragItems.Length} items";
                    this.PART_DragGhostContent.ContentTemplate = null;
                    this.PART_DragGhostContent.Content = new TextBlock { Text = label, Foreground = Brushes.White, FontSize = 11 };
                }

                this.PART_DragGhost.Visibility = Visibility.Visible;
            }

            this.PART_ListBoxDisplay?.CaptureMouse();
        }

        private void UpdateDragDropItemMove (Point cursorInListBox)
        {
            if (m_dragItems == null || this.PART_DragOverlay == null || this.PART_ListBoxDisplay == null)
            {
                return;
            }

            // Position ghost (offset below cursor so it doesn't block the insertion line)
            if (this.PART_DragGhost != null)
            {
                Point cursorInOverlay = this.PART_ListBoxDisplay.TranslatePoint(cursorInListBox, this.PART_DragOverlay);
                Canvas.SetLeft(this.PART_DragGhost, cursorInOverlay.X + 12);
                Canvas.SetTop(this.PART_DragGhost, cursorInOverlay.Y + 17);
            }

            // Find hover row
            int hoverIndex = -1;
            double cursorYFraction = 0.5;
            double rowTopInOverlay = 0;
            double rowHeight = 0;

            for (int i = 0; i < this.PART_ListBoxDisplay.Items.Count; ++i)
            {
                if (this.PART_ListBoxDisplay.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem container)
                {
                    Point containerOrigin = container.TranslatePoint(new Point(0, 0), this.PART_ListBoxDisplay);
                    double containerBottom = containerOrigin.Y + container.ActualHeight;

                    if (cursorInListBox.Y >= containerOrigin.Y && cursorInListBox.Y < containerBottom)
                    {
                        hoverIndex = i;
                        cursorYFraction = (cursorInListBox.Y - containerOrigin.Y) / container.ActualHeight;
                        rowHeight = container.ActualHeight;
                        rowTopInOverlay = container.TranslatePoint(new Point(0, 0), this.PART_DragOverlay).Y;
                        break;
                    }
                }
            }

            if (hoverIndex < 0 && this.Items.Count > 0)
            {
                hoverIndex = this.Items.Count - 1;
                cursorYFraction = 1.0;
                if (this.PART_ListBoxDisplay.ItemContainerGenerator.ContainerFromIndex(hoverIndex) is ListBoxItem lastContainer)
                {
                    rowHeight = lastContainer.ActualHeight;
                    rowTopInOverlay = lastContainer.TranslatePoint(new Point(0, 0), this.PART_DragOverlay).Y;
                }
            }

            if (hoverIndex < 0)
            {
                this.HideDragIndicators_Wpf();
                return;
            }

            double indentSize = this.TreeDepthIndentSize;
            var dropTarget = FlatTreeDragDropManager.ComputeDropTarget(
                this.Items, m_dragItems, hoverIndex, cursorYFraction, cursorInListBox.X, indentSize
            );

            if (dropTarget == null
                || !FlatTreeDragDropManager.ValidateDropTarget(m_dragItems, dropTarget, this.CanDropItem))
            {
                this.HideDragIndicators_Wpf();
                m_currentDropTarget = null;
                return;
            }

            m_currentDropTarget = dropTarget;

            // Lazily measure the row content X position in overlay coords. This captures
            // the ListBoxItem container's Border padding and any ListBox internal chrome,
            // so all line positions are calculated correctly regardless of container style.
            if (double.IsNaN(m_rowXInOverlay))
            {
                if (this.PART_ListBoxDisplay.ItemContainerGenerator.ContainerFromIndex(hoverIndex) is ListBoxItem measureContainer)
                {
                    Grid rowGrid = FindVisualDescendant<Grid>(measureContainer);
                    if (rowGrid != null)
                    {
                        m_rowXInOverlay = rowGrid.TranslatePoint(new Point(0, 0), this.PART_DragOverlay).X;
                    }
                }

                if (double.IsNaN(m_rowXInOverlay))
                {
                    m_rowXInOverlay = 2.0;
                }
            }

            // Compute shared line Y and insertion line X (content column start, after 14px expander)
            double lineY = cursorYFraction < 0.5 ? rowTopInOverlay : rowTopInOverlay + rowHeight;
            const double kExpanderColumnWidth = 14.0;
            double lineX = m_rowXInOverlay + dropTarget.TargetDepth * indentSize + kExpanderColumnWidth + this.InsertionLineXOffset;

            // Position insertion line at the content column start for the target depth
            if (this.PART_InsertionLine != null)
            {
                Canvas.SetLeft(this.PART_InsertionLine, lineX);
                Canvas.SetTop(this.PART_InsertionLine, lineY - kInsertionLineHeight / 2.0);
                this.PART_InsertionLine.Width = Math.Max(0, this.PART_DragOverlay.ActualWidth - lineX - 4.0);
                this.PART_InsertionLine.Height = kInsertionLineHeight;
                this.PART_InsertionLine.Visibility = Visibility.Visible;

                if (this.InsertionLineBrush != null)
                {
                    this.PART_InsertionLine.Fill = this.InsertionLineBrush;
                }
            }

            // Position parent connector L-shape and highlight parent's chevron
            this.ClearParentHighlight_Wpf();

            int parentStoreIndex = FlatTreeDragDropManager.FindParentStoreIndex(this.Items, dropTarget);
            if (parentStoreIndex >= 0
                && this.PART_ListBoxDisplay.ItemContainerGenerator.ContainerFromIndex(parentStoreIndex) is ListBoxItem parentContainer)
            {
                // Highlight the parent's expander chevron
                ToggleButton expander = FindVisualDescendant<ToggleButton>(parentContainer);
                if (expander != null)
                {
                    m_highlightedExpanderOriginalBrush = expander.Foreground;
                    m_highlightedExpander = expander;
                    Brush highlightBrush = this.DragTargetHighlightBrush ?? this.InsertionLineBrush;
                    if (highlightBrush != null)
                    {
                        expander.Foreground = highlightBrush;
                    }
                }

                // Draw L-shape connector from parent's chevron to insertion line
                if (this.PART_ParentConnectorLine != null)
                {
                    Point parentOrigin = parentContainer.TranslatePoint(new Point(0, 0), this.PART_DragOverlay);
                    double parentCenterY = parentOrigin.Y + parentContainer.ActualHeight / 2.0;

                    // Chevron center X in overlay space (7 = half of 14px expander column)
                    int parentDepth = dropTarget.TargetDepth - 1;
                    double chevronX = m_rowXInOverlay + parentDepth * indentSize + 7.0;

                    double topY = Math.Min(parentCenterY, lineY) + 5.0;
                    double bottomY = Math.Max(parentCenterY, lineY);

                    this.PART_ParentConnectorLine.Points = new PointCollection
                    {
                        new Point(chevronX, topY),
                        new Point(chevronX, bottomY),
                        new Point(lineX, bottomY),
                    };
                    this.PART_ParentConnectorLine.Visibility = Visibility.Visible;

                    if (this.ParentConnectorBrush != null)
                    {
                        this.PART_ParentConnectorLine.Stroke = this.ParentConnectorBrush;
                    }
                }
            }
            else
            {
                if (this.PART_ParentConnectorLine != null)
                {
                    this.PART_ParentConnectorLine.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void CompleteDrag_Wpf ()
        {
            if (m_dragItems != null && m_currentDropTarget != null && m_currentDropTarget.IsValid)
            {
                IObservableTreeNode[] sourceNodes = m_dragItems.Select(i => i.Source).ToArray();
                var args = new FlatTreeReorderEventArgs(sourceNodes, m_currentDropTarget.TargetParent, m_currentDropTarget.InsertIndex);
                this.DragDropReorderRequested?.Invoke(this, args);

                if (!args.Cancel)
                {
                    FlatTreeDragDropManager.ExecuteReorder(sourceNodes, m_currentDropTarget);
                }
            }

            this.CancelDrag_Wpf();
        }

        private void CancelDrag_Wpf ()
        {
            m_isDragging = false;
            m_isDragPending = false;
            m_dragItems = null;
            m_currentDropTarget = null;
            this.HideDragIndicators_Wpf();

            if (this.PART_DragOverlay != null)
            {
                this.PART_DragOverlay.Visibility = Visibility.Collapsed;
                this.PART_DragOverlay.IsHitTestVisible = false;
                this.PART_DragOverlay.Background = null;
            }

            if (this.PART_DragGhost != null)
            {
                this.PART_DragGhost.Visibility = Visibility.Collapsed;
            }

            this.PART_ListBoxDisplay?.ReleaseMouseCapture();
        }

        private void HideDragIndicators_Wpf ()
        {
            if (this.PART_InsertionLine != null)
            {
                this.PART_InsertionLine.Visibility = Visibility.Collapsed;
            }

            if (this.PART_ParentConnectorLine != null)
            {
                this.PART_ParentConnectorLine.Visibility = Visibility.Collapsed;
            }

            this.ClearParentHighlight_Wpf();
        }

        private void ClearParentHighlight_Wpf ()
        {
            if (m_highlightedExpander != null)
            {
                m_highlightedExpander.Foreground = m_highlightedExpanderOriginalBrush;
                m_highlightedExpander = null;
                m_highlightedExpanderOriginalBrush = null;
            }
        }

        private static T FindVisualDescendant<T> (DependencyObject parent) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; ++i)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                {
                    return match;
                }

                T descendant = FindVisualDescendant<T>(child);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }

        private FlatTreeItem FindFlatTreeItemAtPoint_Wpf (Point pointInListBox)
        {
            if (this.PART_ListBoxDisplay == null)
            {
                return null;
            }

            for (int i = 0; i < this.PART_ListBoxDisplay.Items.Count; ++i)
            {
                if (this.PART_ListBoxDisplay.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem container)
                {
                    Point origin = container.TranslatePoint(new Point(0, 0), this.PART_ListBoxDisplay);
                    if (pointInListBox.Y >= origin.Y && pointInListBox.Y < origin.Y + container.ActualHeight)
                    {
                        return this.PART_ListBoxDisplay.Items[i] as FlatTreeItem;
                    }
                }
            }

            return null;
        }

        // ============================[Inner types]================================

        private class ItemsStorageContainer : ObservableFlatTreeStore<FlatTreeItem>
        {
            private readonly FlatTreeListControl m_owner;

            public ItemsStorageContainer (FlatTreeListControl owner)
            {
                m_owner = owner;
            }

            protected override void OnObserve (FlatTreeItem item)
            {
                base.OnObserve(item);
                item.IsSelectedChanged += m_owner.Item_IsSelectedChanged;
                m_owner.ApplySelectionOfReaddedItem(item);
                m_owner.StorageItemAdded?.Invoke(m_owner, new EventArgs<FlatTreeItem>(item));
            }

            protected override void OnDisregard (FlatTreeItem item)
            {
                item.IsSelectedChanged -= m_owner.Item_IsSelectedChanged;
                m_owner.OnItemRemoved(item);
                base.OnDisregard(item);
            }
        }
    }
}
