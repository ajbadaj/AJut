namespace AJut.Application.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Linq;

#if WINDOWS_UWP
    using Windows.UI;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media;
#else
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Controls;
#endif

    using AJut;
    using AJut.Storage;
    using AJut.Tree;
    using DPUtils = DPUtils<FlatTreeListControl>;

    // ============================================================================================================================
    // TODO: Still working on selection, did a major refactor to IObservableTreeNode. Initially I was going to try and make an
    //        interactable tree node version, but having expansion and selection in the tree node made it so that you couldn't show
    //        two FlatTreeListControls with the same nodes and different selection/expansion state, which is when I realized it had
    //        to be stored as local state to the list control. That's when I developed this current FlatTreeListControl.Item strategy
    //        which works, but may still be incomplete with selection synchronization, though attempts have been started in the
    //        constructor and Item selection changed handler.
    // ============================================================================================================================

    /// <summary>
    /// A tree control that is actually visualized by a tree thanks to the underlying magic of <see cref="ObservableFlatTreeStore"/>. This
    /// makes the <see cref="FlatTreeListControl"/> inherintly easy to virtualize, thus making it a very fast tree control. The only restriction
    /// is that nodes (and therefore the tree) must used in this control must implement <see cref="IObservableTreeNode"/>. To setup, bind to or
    /// set <see cref="Root"/> or <see cref="RootItemsSource"/> with your top level node(s).
    /// </summary>
    [TemplatePart(Name=nameof(PART_ListBoxDisplay), Type=typeof(ListBox))]
    public class FlatTreeListControl : Control
    {
        private ListBox PART_ListBoxDisplay;
        private bool m_blockingForSelectionChangeReentrancy = false;

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
            this.Items.RootNode = new Item(this, null, newSourceRootValue);
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
                if (!e.NewItems.IsNullOrEmpty())
                {
                    uberRoot.InsertAndConvert(this, e.NewStartingIndex, e.NewItems.OfType<IObservableTreeNode>());
                }
                if (!e.OldItems.IsNullOrEmpty())
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

        public static readonly DependencyProperty SelectedItemProperty = DPUtils.Register(_ => _.SelectedItem);
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

        // ========================[Construction]==============================
        static FlatTreeListControl ()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FlatTreeListControl), new FrameworkPropertyMetadata(typeof(FlatTreeListControl)));
        }

        public FlatTreeListControl ()
        {
            this.Items = new ObservableFlatTreeStore<Item>();
            this.SelectedItems = new ObservableCollection<IObservableTreeNode>();
            this.SelectionBrush = CoerceUtils.CoerceBrushFrom("#2196f3");
            this.SelectionInactiveBrush = CoerceUtils.CoerceBrushFrom("#94B7D1");

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
                    if (!_e.OldItems.IsNullOrEmpty())
                    {
                        foreach (Item item in _e.OldItems.OfType<IObservableTreeNode>().Select(this.StorageItemForNode).Where(i => i != null))
                        {
                            item.IsSelected = false;
                        }
                    }

                    if (!_e.NewItems.IsNullOrEmpty())
                    {
                        foreach (Item item in _e.NewItems.OfType<IObservableTreeNode>().Select(this.StorageItemForNode).Where(i => i != null))
                        {
                            item.IsSelected = true;
                        }
                    }

                    this.SelectedItem = (this.PART_ListBoxDisplay.SelectedItem as Item)?.Source;
                }
                finally
                {
                    m_blockingForSelectionChangeReentrancy = false;
                }
            }
        }

        // ============================[Events]================================
        public event EventHandler<EventArgs<Item>> ItemAdded;
        public event EventHandler<EventArgs<Item>> ItemRemoved;
        public event EventHandler<SelectionChangedEventArgs> SelectionChanged;

        // ============================[Methods]================================
        public override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            if (this.PART_ListBoxDisplay != null)
            {
                this.PART_ListBoxDisplay.SelectionChanged -= _OnSelectionChanged;
            }

            this.PART_ListBoxDisplay = (ListBox)this.GetTemplateChild(nameof(PART_ListBoxDisplay));
            this.PART_ListBoxDisplay.SelectionChanged += _OnSelectionChanged;

            void _OnSelectionChanged (object sender, SelectionChangedEventArgs _e)
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
                    if (_e.AddedItems != null)
                    {
                        this.SelectedItems.AddEach(_e.AddedItems.OfType<Item>().Select(_ => _.Source));
                    }

                    if (_e.RemovedItems != null)
                    {
                        this.SelectedItems.RemoveEach(_e.RemovedItems.OfType<Item>().Select(_ => _.Source));
                    }

                    this.SelectedItem = (this.PART_ListBoxDisplay.SelectedItem as Item)?.Source;

                    this.SelectionChanged?.Invoke(this, _e);
                }
                finally
                {
                    m_blockingForSelectionChangeReentrancy = false;
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

        private void OnItemCreated (Item item)
        {
            item.IsSelectedChanged += this.Item_IsSelectedChanged;
            this.ItemAdded?.Invoke(this, new EventArgs<Item>(item));
        }

        private void OnItemRemoved (Item item)
        {
            item.IsSelectedChanged -= this.Item_IsSelectedChanged;
            this.SelectedItems.Remove(item.Source);
            if (this.SelectedItem == item.Source)
            {
                this.SelectedItem = null;
            }
            this.ItemRemoved?.Invoke(this, new EventArgs<Item>(item));
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
                        this.PART_ListBoxDisplay.SelectedItem = null;
                    }
                    else
                    {
                        this.PART_ListBoxDisplay.SelectedItems.Remove(item);
                    }
                }

                this.SelectedItem = (this.PART_ListBoxDisplay.SelectedItem as Item)?.Source;
                this.SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(ListBox.SelectedEvent, removed, added));
            }
            finally
            {
                m_blockingForSelectionChangeReentrancy = false;
            }
        }

        // =====================================================================
        // ============================[Classes]================================
        // =====================================================================

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
                void _SourceChildInserted (object _sender, ChildInsertedEventArgs _e)
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
                void _SourceParentChanged (object _sender, ParentChangedEventArgs _e)
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
                                child.ForwardAddedNotification();
                            }

                            m_hiddenChildren.Clear();
                        }
                        // When you collapse, store the hidden children
                        else
                        {
                            m_hiddenChildren.AddRange(base.Children);
                            foreach (var child in m_hiddenChildren.ToList())
                            {
                                base.RemoveChild(child);
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

            private void ForwardAddedNotification ()
            {
                for (int index = 0; index < this.Children.Count; ++index)
                {
                    this.RaiseChildInsertedEvent(index, this.Children[index]);
                }
            }
        }
    }
}