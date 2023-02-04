namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Linq;
    using System.Reflection;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using AJut;
    using AJut.Storage;
    using AJut.Tree;
    using DPUtils = DPUtils<FlatTreeListControl>;

    /// <summary>
    /// A tree control that is actually visualized by a tree thanks to the underlying magic of <see cref="ObservableFlatTreeStore"/>. This
    /// makes the <see cref="FlatTreeListControl"/> inherintly easy to virtualize, thus making it a very fast tree control. The only restriction
    /// is that nodes (and therefore the tree) must used in this control must implement <see cref="IObservableTreeNode"/>. To setup, bind to or
    /// set <see cref="Root"/> or <see cref="RootItemsSource"/> with your top level node(s).
    /// </summary>
    [TemplatePart(Name = nameof(PART_ListBoxDisplay), Type = typeof(ListBox))]
    public class FlatTreeListControl : Control
    {
        private ListBox PART_ListBoxDisplay;
        private bool m_blockingForSelectionChangeReentrancy = false;

        // ========================[Construction]==============================
        static FlatTreeListControl ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FlatTreeListControl), new FrameworkPropertyMetadata(typeof(FlatTreeListControl)));
        }

        public FlatTreeListControl ()
        {
            this.Items = new ItemsStorageContainer(this);
            this.SelectedItems = new ObservableCollection<IObservableTreeNode>();

            this.SelectedItems.CollectionChanged += _OnSelectedItemsCollectionChanged;

            void _OnSelectedItemsCollectionChanged (object _sender, NotifyCollectionChangedEventArgs _e)
            {
                // ===================================================================
                // = What happens when selection changes via external insert\removal = 
                // = of the SelectedItems collection                                 =
                // ===================================================================

                if (m_blockingForSelectionChangeReentrancy || this.PART_ListBoxDisplay == null)
                {
                    return;
                }

                m_blockingForSelectionChangeReentrancy = true;
                try
                {
                    IObservableTreeNode[] removed = null;
                    if (!_e.OldItems.IsNullOrEmpty())
                    {
                        removed = _e.OldItems.OfType<IObservableTreeNode>().Select(this.StorageItemForNode).Where(i => i != null).ToArray();
                        foreach (Item item in removed)
                        {
                            item.IsSelected = false;
                            this.PART_ListBoxDisplay.SelectedItems.Remove(item);
                        }
                    }

                    IObservableTreeNode[] added = null;
                    if (!_e.NewItems.IsNullOrEmpty())
                    {
                        added = _e.NewItems.OfType<IObservableTreeNode>().Select(this.StorageItemForNode).Where(i => i != null).ToArray();
                        foreach (Item item in added)
                        {
                            item.IsSelected = true;
                            this.PART_ListBoxDisplay.SelectedItems.Add(item);
                        }
                    }

                    // It's a clear
                    if (added == null && removed == null)
                    {
                        foreach (Item item in this.Items)
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
        }

        // ============================[Events]================================
        public event EventHandler<EventArgs<Item>> StorageItemAdded;
        public event EventHandler<EventArgs<Item>> StorageItemRemoved;
        public event EventHandler<EventArgs<SelectionChange<IObservableTreeNode>>> SelectionChanged;

        // =================[Core Dependency Properties]===================
        public static readonly DependencyProperty RootProperty = DPUtils.Register(_ => _.Root, (d, e) => d.OnRootChanged(e.NewValue));
        public IObservableTreeNode Root
        {
            get => (IObservableTreeNode)this.GetValue(RootProperty);
            set => this.SetValue(RootProperty, value);
        }
        private void OnRootChanged (IObservableTreeNode newSourceRootValue)
        {
            this.Items.IncludeRoot = this.IncludeRoot;

            if (newSourceRootValue == null)
            {
                this.Items.RootNode = null;
            }
            else
            {
                this.Items.RootNode = new Item(this, null, newSourceRootValue);
            }
        }

        public static readonly DependencyProperty RootItemsSourceProperty = DPUtils.Register(_ => _.RootItemsSource, (d, e) => d.OnRootItemsSourceChanged(e.OldValue, e.NewValue));
        public IEnumerable<IObservableTreeNode> RootItemsSource
        {
            get => (IEnumerable<IObservableTreeNode>)this.GetValue(RootItemsSourceProperty);
            set => this.SetValue(RootItemsSourceProperty, value);
        }
        private void OnRootItemsSourceChanged (IEnumerable<IObservableTreeNode> oldValue, IEnumerable<IObservableTreeNode> newValue)
        {
            this.Items.RootNode = Item.CreateUberRoot(this, newValue);
            this.IncludeRoot = false;

            if (oldValue is INotifyCollectionChanged oldObservable)
            {
                oldObservable.CollectionChanged -= _RootItemsOnCollectionChanged;
            }
            if (newValue is INotifyCollectionChanged newObservable)
            {
                newObservable.CollectionChanged += _RootItemsOnCollectionChanged;
            }

            void _RootItemsOnCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
            {
                // By the sheer fact of being here, this must be an "uber root"
                var uberRoot = (Item)this.Items.RootNode;
                if (e.Action == NotifyCollectionChangedAction.Reset)
                {
                    uberRoot.Clear();
                }
                else if (!e.NewItems.IsNullOrEmpty())
                {
                    uberRoot.InsertAndConvert(this, e.NewStartingIndex, e.NewItems.OfType<IObservableTreeNode>());
                }
                else if (!e.OldItems.IsNullOrEmpty())
                {
                    uberRoot.RemoveAllSourceElements(e.OldItems.OfType<IObservableTreeNode>());
                }
            }
        }

        private static readonly DependencyPropertyKey IncludeRootPropertyKey = DPUtils.RegisterReadOnly(_ => _.IncludeRoot, true, (d, e) => d.Items.IncludeRoot = e.HasNewValue ? e.NewValue : true);
        public static readonly DependencyProperty IncludeRootProperty = IncludeRootPropertyKey.DependencyProperty;
        public bool IncludeRoot
        {
            get => (bool)this.GetValue(IncludeRootProperty);
            protected set => this.SetValue(IncludeRootPropertyKey, value);
        }

        public static readonly DependencyProperty SelectedItemProperty = DPUtils.Register(_ => _.SelectedItem, (d,e)=>d.OnSelectedItemChanged(e));
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
        public static readonly DependencyProperty TabbingSizeProperty = DPUtils.Register(_ => _.TabbingSize, 8.0);
        public double TabbingSize
        {
            get => (double)this.GetValue(TabbingSizeProperty);
            set => this.SetValue(TabbingSizeProperty, value);
        }

        private static readonly DependencyPropertyKey ItemsPropertyKey = DPUtils.RegisterReadOnly(_ => _.Items);
        public static readonly DependencyProperty ItemsProperty = ItemsPropertyKey.DependencyProperty;
        public ObservableFlatTreeStore<Item> Items
        {
            get => (ObservableFlatTreeStore<Item>)this.GetValue(ItemsProperty);
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
        public Brush SelectionBrush
        {
            get => (Brush)this.GetValue(SelectionBrushProperty);
            set => this.SetValue(SelectionBrushProperty, value);
        }

        public static readonly DependencyProperty SelectionInactiveBrushProperty = DPUtils.Register(_ => _.SelectionInactiveBrush);
        public Brush SelectionInactiveBrush
        {
            get => (Brush)this.GetValue(SelectionInactiveBrushProperty);
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
        public Brush GlyphBrush
        {
            get => (Brush)this.GetValue(GlyphBrushProperty);
            set => this.SetValue(GlyphBrushProperty, value);
        }

        public static readonly DependencyProperty GlyphHighlightBrushProperty = DPUtils.Register(_ => _.GlyphHighlightBrush);
        public Brush GlyphHighlightBrush
        {
            get => (Brush)this.GetValue(GlyphHighlightBrushProperty);
            set => this.SetValue(GlyphHighlightBrushProperty, value);
        }

        public static readonly DependencyProperty GlyphBackgroundHighlightBrushProperty = DPUtils.Register(_ => _.GlyphBackgroundHighlightBrush);
        public Brush GlyphBackgroundHighlightBrush
        {
            get => (Brush)this.GetValue(GlyphBackgroundHighlightBrushProperty);
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
                this.PART_ListBoxDisplay.SelectionChanged -= _OnSelectionChanged;
                this.PART_ListBoxDisplay.MouseDoubleClick -= _OnMouseDoubleClick;
            }

            this.PART_ListBoxDisplay = (ListBox)this.GetTemplateChild(nameof(PART_ListBoxDisplay));
            this.PART_ListBoxDisplay.SelectionChanged += _OnSelectionChanged;
            this.PART_ListBoxDisplay.MouseDoubleClick += _OnMouseDoubleClick;

            void _OnSelectionChanged (object _sender, SelectionChangedEventArgs _e)
            {
                // =====================================================
                // = What happens when the ListBox's selection changes =
                // = *remember* the ListBox selection deals with Items =
                // =====================================================

                if (m_blockingForSelectionChangeReentrancy)
                {
                    return;
                }

                m_blockingForSelectionChangeReentrancy = true;
                try
                {
                    IObservableTreeNode[] added = null;
                    if (_e.AddedItems != null)
                    {
                        var allStoreItems = _e.AddedItems.OfType<Item>().ToList();
                        added = allStoreItems.Select(_ => _.Source).ToArray();
                        this.SelectedItems.AddEach(added);

                        foreach (Item item in allStoreItems)
                        {
                            item.IsSelected = true;
                        }
                    }

                    IObservableTreeNode[] removed = null;
                    if (!_e.RemovedItems.IsNullOrEmpty())
                    {
                        var allStoreItems = _e.RemovedItems.OfType<Item>().ToList();
                        removed = allStoreItems.Select(_ => _.Source).ToArray();
                        this.SelectedItems.RemoveEach(removed);

                        foreach (Item item in allStoreItems)
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
            void _OnMouseDoubleClick (object _sender, MouseButtonEventArgs _e)
            {
                if (this.ShouldToggleExpandableItemExpansionOnDoubleClick && this.PART_ListBoxDisplay.SelectedItem is Item item && item.IsExpandable)
                {
                    item.IsExpanded = !item.IsExpanded;
                }
            }
        }

        public IEnumerable<Item> AllItems ()
        {
            return TreeTraversal<Item>.All(this.Items.RootNode);
        }

        public Item StorageItemForNode (IObservableTreeNode sourceNode)
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

        private void OnItemCreated (Item item)
        {
            item.IsSelectedChanged -= this.Item_IsSelectedChanged;
            item.IsSelectedChanged += this.Item_IsSelectedChanged;
            this.ApplySelectionOfReaddedItem(item);
            this.StorageItemAdded?.Invoke(this, new(item));
        }

        private void OnItemRemoved (Item item)
        {
            item.IsSelectedChanged -= this.Item_IsSelectedChanged;
            this.SelectedItems.Remove(item.Source);
            if (this.SelectedItem == item.Source)
            {
                this.SelectedItem = null;
            }
            this.StorageItemRemoved?.Invoke(this, new(item));
        }

        private void OnSelectedItemChanged (DependencyPropertyChangedEventArgs<IObservableTreeNode> e)
        {
            if (m_blockingForSelectionChangeReentrancy || this.PART_ListBoxDisplay == null)
            {
                return;
            }

            if (e.NewValue != null)
            {
                var item = this.PART_ListBoxDisplay.Items.OfType<Item>().FirstOrDefault(i => i.Source == e.NewValue);
                this.PART_ListBoxDisplay.SelectedItem = item;
            }
        }

        private void Item_IsSelectedChanged (object sender, EventArgs e)
        {
            // ==================================================
            // = What happens when an item is directly selected =
            // ==================================================

            if (m_blockingForSelectionChangeReentrancy || this.PART_ListBoxDisplay == null)
            {
                return;
            }

            m_blockingForSelectionChangeReentrancy = true;
            try
            {
                var added = new List<IObservableTreeNode>();
                var removed = new List<IObservableTreeNode>();

                var item = (Item)sender;
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
                        _DeselctAllBut(item.Source);
                        this.SelectedItems.ResetTo(new[] { item.Source });
                    }
                    else
                    {
                        item.IsSelected = true;
                        this.SelectedItems.Add(item.Source);
                    }
                }
                else
                {
                    removed.Add(item.Source);
                    if (this.SelectionMode == SelectionMode.Single && this.PART_ListBoxDisplay.SelectedItem == item)
                    {
                        _DeselctAllBut(null);
                        this.PART_ListBoxDisplay.SelectedItem = null;
                    }
                    else
                    {
                        item.IsSelected = false;
                        this.PART_ListBoxDisplay.SelectedItems.Remove(item);
                    }
                }

                this.ApplySelectionChanges(added, removed);
            }
            finally
            {
                m_blockingForSelectionChangeReentrancy = false;
            }

            void _DeselctAllBut (IObservableTreeNode _item)
            {
                foreach (var formerItemSelection in this.SelectedItems.Select(this.StorageItemForNode).Where(i => i != _item))
                {
                    formerItemSelection.IsSelected = false;
                }
            }
        }

        private void ApplySelectionChanges (IEnumerable<IObservableTreeNode> added, IEnumerable<IObservableTreeNode> removed)
        {
            this.SelectedItem = (this.PART_ListBoxDisplay.SelectedItem as Item)?.Source;
            if (this.SelectedItems.Count == 0)
            {
                this.SelectionChanged?.Invoke(this,
                    new EventArgs<SelectionChange<IObservableTreeNode>>(
                        new SelectionChange<IObservableTreeNode>(null, null, true)
                    )
                );
            }
            else
            {
                this.SelectionChanged?.Invoke(this,
                    new EventArgs<SelectionChange<IObservableTreeNode>>(
                        new SelectionChange<IObservableTreeNode>(added?.ToArray(), removed?.ToArray())
                    )
                );
            }
        }

        protected override void OnKeyUp (KeyEventArgs e)
        {
            if (e.Key == Key.Left)
            {
                foreach (var selected in this.SelectedItems.Select(this.StorageItemForNode))
                {
                    selected.IsExpanded = false;
                }

                e.Handled = true;
                return;
            }
            else if (e.Key == Key.Right)
            {
                foreach (var selected in this.SelectedItems.Select(this.StorageItemForNode))
                {
                    selected.IsExpanded = true;
                }

                e.Handled = true;
                return;

            }

            base.OnKeyUp(e);
        }

        private IDisposable TemporarilyBlockSelectionChanges ()
        {
            m_blockingForSelectionChangeReentrancy = true;
            return new DisposeActionTrigger(() => m_blockingForSelectionChangeReentrancy = false);
        }

        private void ApplySelectionOfReaddedItem (Item item)
        {
            if (item.IsSelected)
            {
                if (this.SelectedItems.Contains(item.Source))
                {
                    // This was selected and collapsed
                    if (!this.PART_ListBoxDisplay.SelectedItems.Contains(item))
                    {
                        this.PART_ListBoxDisplay.SelectedItems.Add(item);
                    }
                }
                else
                {
                    // This was selected and not already tracked
                    this.SelectedItems.Add(item.Source);
                }
            }
        }

        // ============================[Classes]================================

        /// <summary>
        /// The storage container of the provided <see cref="IObservableTreeNode"/> instances managed by the list. This
        /// container tracks TreeDepth (used to create tabbing spaces in display), selection, and expansion state. In
        /// addition it tracks hidden vs displayed items depending on if it's expanded or not.
        /// </summary>
        public class Item : ObservableTreeNode<Item>
        {
            private static IReadOnlyList<Item> kCollapsedChildSet = new Item[0];
            private readonly FlatTreeListControl m_owner;
            private readonly List<Item> m_hiddenChildren = new List<Item>();

            // ========================[Construction]==============================

            static Item ()
            {
                TreeTraversal<Item>.SetupDefaults(i => i.AllChildren, i => i.Parent);
            }

            public static Item CreateUberRoot (FlatTreeListControl owner, IEnumerable<IObservableTreeNode> multiRoot)
            {
                return new Item(owner, multiRoot);
            }

            private Item (FlatTreeListControl owner, IEnumerable<IObservableTreeNode> multiRoot)
            {
                m_owner = owner;
                this.IsFalseRoot = true;
                this.Source = null;
                this.IsExpandable = true;
                this.IsExpanded = true;
                this.InsertAndConvert(owner, 0, multiRoot);
            }

            public Item (FlatTreeListControl owner, Item parent, IObservableTreeNode source)
            {
                m_owner = owner;
                this.Source = source;
                base.Parent = parent;
                this.Source.CanHaveChildrenChanged += _SourceCanHaveChildrenChanged;
                this.IsExpandable = this.Source.CanHaveChildren;
                this.IsExpanded = owner.StartItemsExpanded;

                // Handle source events
                this.Source.ChildInserted += _SourceChildInserted;
                this.Source.ChildRemoved += _SourceChildRemoved;
                this.Source.CanHaveChildrenChanged += _SourceCanHaveChildrenChanged;
                this.Source.ParentChanged += _SourceParentChanged;

                m_owner.OnItemCreated(this);

                this.InsertAndConvert(owner, 0, source.Children);

                void _SourceCanHaveChildrenChanged (object _sender, EventArgs<bool> _e) => this.IsExpandable = _e.Value;
                void _SourceChildInserted (object _sender, TreeNodeInsertedEventArgs _e)
                {
                    var child = new Item(owner, this, _e.Node);
                    if (this.IsExpanded)
                    {
                        this.InsertChild(_e.InsertIndex, child);
                    }
                    else
                    {
                        m_hiddenChildren.Insert(_e.InsertIndex, child);
                    }
                }
                void _SourceChildRemoved (object _sender, EventArgs<IObservableTreeNode> _e)
                {
                    Item toRemove = this.Children.FirstOrDefault(i => i.Source == _e.Value);
                    if (toRemove != null && this.RemoveChild(toRemove))
                    {
                        this.RaiseChildRemovedEvent(toRemove);
                    }
                }
                void _SourceParentChanged (object _sender, TreeNodeParentChangedEventArgs _e)
                {
                    base.Parent = m_owner.Items.FirstOrDefault(i => i.Source == _e);
                }
            }

            // ============================[Events]================================

            public event EventHandler<EventArgs> IsSelectedChanged;

            // ==========================[Properties]==============================

            public int TreeDepth => this.Parent == null ? (m_owner.IncludeRoot ? 0 : -1) : this.Parent.TreeDepth + 1;

            public IObservableTreeNode Source { get; }
            public bool IsFalseRoot { get; } = false;

            private bool m_isExpanded = true;
            public bool IsExpanded
            {
                get => m_isExpanded;
                set
                {
                    if (this.SetAndRaiseIfChanged(ref m_isExpanded, value))
                    {
                        // When you expand, add all the hidden children back
                        if (value)
                        {
                            foreach (Item child in m_hiddenChildren)
                            {
                                base.InsertChild(base.Children.Count, child);
                            }

                            m_hiddenChildren.Clear();
                        }
                        // When you collapse, store the hidden children
                        else
                        {
                            m_hiddenChildren.AddRange(base.Children);
                            using (m_owner.TemporarilyBlockSelectionChanges())
                            {
                                foreach (var child in m_hiddenChildren.ToList())
                                {
                                    base.RemoveChild(child);
                                }
                            }
                        }
                    }
                }
            }

            private bool m_isExpandable;
            public bool IsExpandable
            {
                get => m_isExpandable;
                set
                {
                    if (this.SetAndRaiseIfChanged(ref m_isExpandable, value))
                    {
                        this.IsExpanded = false;
                    }
                }
            }

            private bool m_isSelected;
            public bool IsSelected
            {
                get => m_isSelected;
                set
                {
                    if (this.IsSelectable || !value)
                    {
                        this.SetAndRaiseIfChanged(ref m_isSelected, value);
                        this.IsSelectedChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }

            private bool m_isSelectable = true;
            public bool IsSelectable
            {
                get => m_isSelectable;
                set
                {
                    if (this.SetAndRaiseIfChanged(ref m_isSelectable, value))
                    {
                        this.IsSelected = false;
                    }
                }
            }

            public override Item Parent
            {
                get => base.Parent;
                set
                {
                    if (base.Parent != value)
                    {
                        this.Source.Parent = value.Source;
                        this.RaisePropertyChanged(nameof(TreeDepth));
                    }
                }
            }

            public override IReadOnlyList<Item> Children => this.IsExpanded ? base.Children : kCollapsedChildSet;
            public override bool CanHaveChildren => this.Source.CanHaveChildren;

            public IEnumerable<Item> AllChildren => this.IsExpanded ? base.Children : m_hiddenChildren;

            // ============================[Methods]================================

            public override void InsertChild (int index, Item child)
            {
                if (this.IsExpanded)
                {
                    base.InsertChild(index, child);
                }
                else
                {
                    child.Parent = this;
                    m_hiddenChildren.Insert(index, child);
                }
            }

            public override bool RemoveChild (Item child)
            {
                bool wasRemoved = false;
                if (this.IsExpanded)
                {
                    wasRemoved = base.RemoveChild(child);
                }
                else
                {
                    wasRemoved = m_hiddenChildren.Remove(child);
                }

                if (wasRemoved)
                {
                    m_owner.OnItemRemoved(child);
                    return true;
                }

                return false;
            }

            public void InsertAndConvert (FlatTreeListControl owner, int startIndex, IEnumerable<IObservableTreeNode> sourceChildren)
            {
                var sourceChildrenList = sourceChildren.ToList();
                for (int index = 0; index < sourceChildrenList.Count; ++index)
                {
                    this.InsertChild(startIndex + index, new Item(owner, this, sourceChildrenList[index]));
                }
            }

            public void RemoveAllSourceElements (IEnumerable<IObservableTreeNode> sourceChildren)
            {
                var sourceChildrenList = sourceChildren.ToList();
                for (int index = 0; index < sourceChildrenList.Count; ++index)
                {
                    Item found = this.Children.FirstOrDefault(i => i.Source == sourceChildrenList[index]);
                    if (found != null && this.RemoveChild(found))
                    {
                        this.RaiseChildRemovedEvent(found);
                    }
                }
            }
        }

        private class ItemsStorageContainer : ObservableFlatTreeStore<Item>
        {
            private FlatTreeListControl m_owner;
            public ItemsStorageContainer (FlatTreeListControl owner)
            {
                m_owner = owner;
            }

            protected override void OnObserve (Item item)
            {
                base.OnObserve(item);
                m_owner.ApplySelectionOfReaddedItem(item);
            }
        }
    }
}