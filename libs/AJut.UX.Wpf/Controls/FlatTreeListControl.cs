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
    public class FlatTreeListControl : Control
    {
        private ListBox PART_ListBoxDisplay;
        private bool m_blockingForSelectionChangeReentrancy;

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

        // ============================[Methods]================================
        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            if (this.PART_ListBoxDisplay != null)
            {
                this.PART_ListBoxDisplay.SelectionChanged -= this.ListBox_OnSelectionChanged;
                this.PART_ListBoxDisplay.MouseDoubleClick -= this.ListBox_OnMouseDoubleClick;
            }

            this.PART_ListBoxDisplay = (ListBox)this.GetTemplateChild(nameof(PART_ListBoxDisplay));
            if (this.PART_ListBoxDisplay != null)
            {
                this.PART_ListBoxDisplay.SelectionChanged += this.ListBox_OnSelectionChanged;
                this.PART_ListBoxDisplay.MouseDoubleClick += this.ListBox_OnMouseDoubleClick;
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
                    return true;
                if (current is ListBoxItem)
                    return false;
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
