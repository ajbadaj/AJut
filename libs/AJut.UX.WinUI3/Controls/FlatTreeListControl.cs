namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Linq;
    using AJut.Storage;
    using AJut.Tree;
    using AJut.UX;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media;
    using DPUtils = AJut.UX.DPUtils<FlatTreeListControl>;

    // ===========[ FlatTreeListControl ]=======================================
    // A tree control backed by an ObservableFlatTreeStore<FlatTreeItem>. Source
    // nodes must implement IObservableTreeNode.
    //
    // Bind Root (single root) or RootItemsSource (multi-root) to populate.
    // The store auto-manages the flat list via ChildInserted/ChildRemoved events
    // from FlatTreeItem, which wraps each IObservableTreeNode source.
    //
    // Each ListView row is a FlatTreeItemRow - a thin wrapper Control with a
    // ContentTemplate DP. FlatTreeListControl pushes its ItemTemplate down to
    // each realized FlatTreeItemRow via ContainerContentChanging (necessary
    // because WinUI3 DataTemplates have no ancestor binding).
    //
    // Template parts:
    //   PART_ListView  - the inner ListView that does the actual rendering

    [TemplatePart(Name = nameof(PART_ListView), Type = typeof(ListView))]
    public class FlatTreeListControl : Control
    {
        // ===========[ Instance fields ]==========================================
        private ListView PART_ListView;
        private readonly ObservableFlatTreeStore<FlatTreeItem> m_store;
        private readonly ObservableCollection<FlatTreeItem> m_selectedItems = new ObservableCollection<FlatTreeItem>();
        private bool m_blockingReentrancy;

        // ===========[ Construction ]=============================================
        public FlatTreeListControl ()
        {
            this.DefaultStyleKey = typeof(FlatTreeListControl);
            m_store = new ObservableFlatTreeStore<FlatTreeItem>();
        }

        // ===========[ Events ]===================================================
        public event EventHandler<SelectionChange<FlatTreeItem>> SelectionChanged;
        public event EventHandler<FlatTreeItem> ItemDoubleClicked;

        /// <summary>
        /// Fires after FlatTreeListControl's own ContainerContentChanging handling (ItemTemplate push).
        /// Consumers (e.g. PropertyGrid) can subscribe to push additional state into each realized row.
        /// </summary>
        public event Windows.Foundation.TypedEventHandler<ListViewBase, ContainerContentChangingEventArgs> ContainerContentChanging;

        // ===========[ Dependency Properties ]=====================================
        public static readonly DependencyProperty RootProperty = DPUtils.Register(_ => _.Root, (d, e) => d.OnRootChanged(e.NewValue));
        public IObservableTreeNode Root
        {
            get => (IObservableTreeNode)this.GetValue(RootProperty);
            set => this.SetValue(RootProperty, value);
        }
        private void OnRootChanged (IObservableTreeNode newRoot)
        {
            m_store.RootNode?.DisposeTree();
            m_store.IncludeRoot = this.IncludeRoot;
            m_store.RootNode = newRoot == null
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
            m_store.RootNode?.DisposeTree();
            m_store.IncludeRoot = false;
            m_store.RootNode = FlatTreeItem.CreateUberRoot(
                newValue ?? Enumerable.Empty<IObservableTreeNode>(), this.TreeDepthIndentSize);

            if (oldValue is INotifyCollectionChanged oldObservable)
            {
                oldObservable.CollectionChanged -= this.RootItems_OnCollectionChanged;
            }
            if (newValue is INotifyCollectionChanged newObservable)
            {
                newObservable.CollectionChanged += this.RootItems_OnCollectionChanged;
            }
        }

        public static readonly DependencyProperty IncludeRootProperty = DPUtils.Register(_ => _.IncludeRoot, true, (d, e) => d.m_store.IncludeRoot = e.NewValue);
        public bool IncludeRoot
        {
            get => (bool)this.GetValue(IncludeRootProperty);
            set => this.SetValue(IncludeRootProperty, value);
        }

        public static readonly DependencyProperty TreeDepthIndentSizeProperty = DPUtils.Register(_ => _.TreeDepthIndentSize, 16.0, (d, e) => d.OnTreeDepthIndentSizeChanged());
        public double TreeDepthIndentSize
        {
            get => (double)this.GetValue(TreeDepthIndentSizeProperty);
            set => this.SetValue(TreeDepthIndentSizeProperty, value);
        }
        private void OnTreeDepthIndentSizeChanged ()
        {
            if (m_store.RootNode == null)
            {
                return;
            }

            double size = this.TreeDepthIndentSize;
            foreach (FlatTreeItem item in TreeTraversal<FlatTreeItem>.All(m_store.RootNode))
            {
                item.TreeDepthIndentSize = size;
            }
        }

        public static readonly DependencyProperty RowSpacingProperty = DPUtils.Register(_ => _.RowSpacing, 2.0, (d, e) => d.OnRowSpacingChanged());
        public double RowSpacing
        {
            get => (double)this.GetValue(RowSpacingProperty);
            set => this.SetValue(RowSpacingProperty, value);
        }
        private void OnRowSpacingChanged ()
        {
            if (this.PART_ListView == null)
            {
                return;
            }

            double spacing = this.RowSpacing;
            for (int i = 0; i < this.PART_ListView.Items.Count; ++i)
            {
                if (this.PART_ListView.ContainerFromIndex(i) is ListViewItem container)
                {
                    container.Margin = new Thickness(0, 0, 0, spacing);
                }
            }
        }

        public static readonly DependencyProperty FixedRowHeightProperty = DPUtils.Register(_ => _.FixedRowHeight, double.NaN, (d, e) => d.OnFixedRowHeightChanged());
        public double FixedRowHeight
        {
            get => (double)this.GetValue(FixedRowHeightProperty);
            set => this.SetValue(FixedRowHeightProperty, value);
        }
        private void OnFixedRowHeightChanged ()
        {
            if (this.PART_ListView == null)
            {
                return;
            }

            double height = this.FixedRowHeight;
            for (int i = 0; i < this.PART_ListView.Items.Count; ++i)
            {
                if (this.PART_ListView.ContainerFromIndex(i) is ListViewItem container)
                {
                    container.Height = double.IsNaN(height) ? double.NaN : height;
                }
            }
        }

        public static readonly DependencyProperty ListViewItemContainerStyleProperty = DPUtils.Register(_ => _.ListViewItemContainerStyle, (d, e) => d.OnListViewItemContainerStyleChanged(e.NewValue));
        public Style ListViewItemContainerStyle
        {
            get => (Style)this.GetValue(ListViewItemContainerStyleProperty);
            set => this.SetValue(ListViewItemContainerStyleProperty, value);
        }
        private void OnListViewItemContainerStyleChanged (Style newStyle)
        {
            if (this.PART_ListView != null)
            {
                this.PART_ListView.ItemContainerStyle = newStyle;
            }
        }

        public static readonly DependencyProperty StartItemsExpandedProperty = DPUtils.Register(_ => _.StartItemsExpanded);
        public bool StartItemsExpanded
        {
            get => (bool)this.GetValue(StartItemsExpandedProperty);
            set => this.SetValue(StartItemsExpandedProperty, value);
        }

        public static readonly DependencyProperty ItemTemplateProperty = DPUtils.Register(_ => _.ItemTemplate, (d, e) => d.OnItemTemplateChanged());
        public DataTemplate ItemTemplate
        {
            get => (DataTemplate)this.GetValue(ItemTemplateProperty);
            set => this.SetValue(ItemTemplateProperty, value);
        }

        public static readonly DependencyProperty SelectionModeProperty = DPUtils.Register(_ => _.SelectionMode, eFlatTreeSelectionMode.Single, (d, e) => d.ApplySelectionMode());
        public eFlatTreeSelectionMode SelectionMode
        {
            get => (eFlatTreeSelectionMode)this.GetValue(SelectionModeProperty);
            set => this.SetValue(SelectionModeProperty, value);
        }

        public static readonly DependencyProperty SelectedItemProperty = DPUtils.Register(_ => _.SelectedItem, (d, e) => d.OnSelectedItemChanged(e.NewValue));
        public FlatTreeItem SelectedItem
        {
            get => (FlatTreeItem)this.GetValue(SelectedItemProperty);
            set => this.SetValue(SelectedItemProperty, value);
        }

        public ObservableCollection<FlatTreeItem> SelectedItems => m_selectedItems;

        public static readonly DependencyProperty ShouldToggleExpansionOnDoubleClickProperty = DPUtils.Register(_ => _.ShouldToggleExpansionOnDoubleClick, true);
        public bool ShouldToggleExpansionOnDoubleClick
        {
            get => (bool)this.GetValue(ShouldToggleExpansionOnDoubleClickProperty);
            set => this.SetValue(ShouldToggleExpansionOnDoubleClickProperty, value);
        }

        // ExpanderGlyphForeground: foreground brush for each row's expand/collapse glyph.
        // Default is set in the default style (TextFillColorSecondaryBrush). Propagated to
        // each FlatTreeItemRow via ContainerContentChanging so callers can customize the
        // glyph color without requiring the AJut theme to be loaded.
        public static readonly DependencyProperty ExpanderGlyphForegroundProperty = DPUtils.Register(
            _ => _.ExpanderGlyphForeground,
            (d, e) => d.OnExpanderGlyphForegroundChanged());
        public Brush ExpanderGlyphForeground
        {
            get => (Brush)this.GetValue(ExpanderGlyphForegroundProperty);
            set => this.SetValue(ExpanderGlyphForegroundProperty, value);
        }

        // ===========[ Other Properties ]====================================

        public ObservableFlatTreeStore<FlatTreeItem> Items => m_store;

        // ===========[ Template application ]====================================
        protected override void OnApplyTemplate ()
        {
            base.OnApplyTemplate();

            if (this.PART_ListView != null)
            {
                this.PART_ListView.SelectionChanged -= this.ListView_OnSelectionChanged;
                this.PART_ListView.DoubleTapped -= this.ListView_OnDoubleTapped;
                this.PART_ListView.KeyUp -= this.ListView_OnKeyUp;
                this.PART_ListView.ContainerContentChanging -= this.ListView_OnContainerContentChanging;
            }

            this.PART_ListView = (ListView)this.GetTemplateChild(nameof(PART_ListView));
            if (this.PART_ListView == null)
            {
                return;
            }

            this.PART_ListView.SelectionChanged += this.ListView_OnSelectionChanged;
            this.PART_ListView.DoubleTapped += this.ListView_OnDoubleTapped;
            this.PART_ListView.KeyUp += this.ListView_OnKeyUp;
            this.PART_ListView.ContainerContentChanging += this.ListView_OnContainerContentChanging;

            if (this.ListViewItemContainerStyle != null)
            {
                this.PART_ListView.ItemContainerStyle = this.ListViewItemContainerStyle;
            }

            this.ApplySelectionMode();
            this.PART_ListView.ItemsSource = m_store;
        }

        // ===========[ Public interface ]=========================================
        public void ScrollIntoView (FlatTreeItem item)
        {
            this.PART_ListView?.ScrollIntoView(item);
        }

        public void ExpandAll ()
        {
            if (m_store.RootNode == null)
            {
                return;
            }

            foreach (FlatTreeItem item in TreeTraversal<FlatTreeItem>.All(m_store.RootNode).ToList())
            {
                if (item.IsExpandable && !item.IsExpanded)
                {
                    item.IsExpanded = true;
                }
            }
        }

        public void CollapseAll ()
        {
            if (m_store.RootNode == null)
            {
                return;
            }

            // Collapse bottom-up so children are hidden before parents.
            foreach (FlatTreeItem item in TreeTraversal<FlatTreeItem>.All(m_store.RootNode).Reverse().ToList())
            {
                if (item.IsExpandable && item.IsExpanded)
                {
                    item.IsExpanded = false;
                }
            }
        }

        // ===========[ ListView event handlers ]==================================
        private void ListView_OnSelectionChanged (object sender, SelectionChangedEventArgs e)
        {
            if (m_blockingReentrancy)
            {
                return;
            }

            m_blockingReentrancy = true;
            try
            {
                FlatTreeItem[] added = e.AddedItems.OfType<FlatTreeItem>().ToArray();
                FlatTreeItem[] removed = e.RemovedItems.OfType<FlatTreeItem>().ToArray();

                foreach (FlatTreeItem item in removed)
                {
                    item.IsSelected = false;
                    this.SelectedItems.Remove(item);
                }

                foreach (FlatTreeItem item in added)
                {
                    item.IsSelected = true;
                    if (!this.SelectedItems.Contains(item))
                    {
                        this.SelectedItems.Add(item);
                    }
                }

                this.SelectedItem = this.PART_ListView.SelectedItem as FlatTreeItem;
                this.FireSelectionChanged(added, removed);
            }
            finally
            {
                m_blockingReentrancy = false;
            }
        }

        private void ListView_OnDoubleTapped (object sender, DoubleTappedRoutedEventArgs e)
        {
            // Determine the actual tapped FlatTreeItem by walking up from OriginalSource
            // to find the ListViewItem container. Using SelectedItem instead would cause
            // double-tapping any interactive editor (TextBox, Button, etc.) inside a row
            // to toggle whichever item happens to be selected, not the actually-tapped row.
            FlatTreeItem tappedItem = null;
            if (e.OriginalSource is DependencyObject source)
            {
                DependencyObject current = source;
                while (current != null)
                {
                    if (current is ListViewItem lvi && this.PART_ListView.ItemFromContainer(lvi) is FlatTreeItem flatItem)
                    {
                        tappedItem = flatItem;
                        break;
                    }

                    current = VisualTreeHelper.GetParent(current);
                }
            }

            if (tappedItem != null)
            {
                if (this.ShouldToggleExpansionOnDoubleClick && tappedItem.IsExpandable)
                {
                    tappedItem.IsExpanded = !tappedItem.IsExpanded;
                }

                this.ItemDoubleClicked?.Invoke(this, tappedItem);
            }
        }

        private void ListView_OnKeyUp (object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Left)
            {
                foreach (FlatTreeItem item in this.SelectedItems.ToList())
                {
                    item.IsExpanded = false;
                }
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Right)
            {
                foreach (FlatTreeItem item in this.SelectedItems.ToList())
                {
                    item.IsExpanded = true;
                }
                e.Handled = true;
            }
        }

        private void ListView_OnContainerContentChanging (ListViewBase sender, ContainerContentChangingEventArgs e)
        {
            if (e.ItemContainer?.ContentTemplateRoot is FlatTreeItemRow row)
            {
                // WinUI3 ContentPresenter does not update the content tree's DataContext when Content
                // changes but ContentTemplate stays the same object. Null-reset forces a real template
                // change (null -> template) so the presenter re-inflates with the new item's DataContext.
                // Note: with StackPanel ItemsPanel (no virtualization) this path is never hit on first
                // load - FlatTreeItemRow.OnLoaded handles that case instead.
                row.ContentTemplate = null;
                row.ContentTemplate = this.ItemTemplate;
                row.ExpanderGlyphForeground = this.ExpanderGlyphForeground;
            }

            if (e.ItemContainer != null)
            {
                e.ItemContainer.Margin = new Thickness(0, 0, 0, this.RowSpacing);
                if (!double.IsNaN(this.FixedRowHeight))
                {
                    e.ItemContainer.Height = this.FixedRowHeight;
                }
            }

            // Fire passthrough so consumers (e.g. PropertyGrid) can push additional state.
            this.ContainerContentChanging?.Invoke(sender, e);
        }

        // ===========[ Property change handlers ]=================================
        private void OnItemTemplateChanged ()
        {
            if (this.PART_ListView == null)
            {
                return;
            }

            for (int i = 0; i < this.PART_ListView.Items.Count; ++i)
            {
                if (this.PART_ListView.ContainerFromIndex(i) is ListViewItem container
                    && container.ContentTemplateRoot is FlatTreeItemRow row)
                {
                    row.ContentTemplate = this.ItemTemplate;
                }
            }
        }

        private void OnExpanderGlyphForegroundChanged ()
        {
            if (this.PART_ListView == null)
            {
                return;
            }

            for (int i = 0; i < this.PART_ListView.Items.Count; ++i)
            {
                if (this.PART_ListView.ContainerFromIndex(i) is ListViewItem container
                    && container.ContentTemplateRoot is FlatTreeItemRow row)
                {
                    row.ExpanderGlyphForeground = this.ExpanderGlyphForeground;
                }
            }
        }

        private void OnSelectedItemChanged (FlatTreeItem newItem)
        {
            if (m_blockingReentrancy || this.PART_ListView == null)
            {
                return;
            }

            m_blockingReentrancy = true;
            try
            {
                // In Extended/Multiple modes, SelectedItem = x adds rather than replaces.
                // Clear first so programmatic single-item select replaces existing selection.
                if (this.SelectionMode != eFlatTreeSelectionMode.Single)
                {
                    this.PART_ListView.SelectedItems.Clear();
                }

                this.PART_ListView.SelectedItem = newItem;
            }
            finally
            {
                m_blockingReentrancy = false;
            }
        }

        private void RootItems_OnCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            // ObservableFlatTreeStore does not observe (subscribe to) the uber root when IncludeRoot=false,
            // so InsertChild/RemoveChild on the uber root fires events no one hears. Rebuild from the
            // current full state of RootItemsSource on every change instead.
            m_store.RootNode?.DisposeTree();
            m_store.RootNode = FlatTreeItem.CreateUberRoot(
                this.RootItemsSource ?? Enumerable.Empty<IObservableTreeNode>(), this.TreeDepthIndentSize);
        }

        // ===========[ Apply helpers ]============================================
        private void ApplySelectionMode ()
        {
            if (this.PART_ListView == null)
            {
                return;
            }

            this.PART_ListView.SelectionMode = this.SelectionMode switch
            {
                eFlatTreeSelectionMode.None     => ListViewSelectionMode.None,
                eFlatTreeSelectionMode.Multi    => ListViewSelectionMode.Multiple,
                eFlatTreeSelectionMode.Extended => ListViewSelectionMode.Extended,
                _                               => ListViewSelectionMode.Single,
            };
        }

        private void FireSelectionChanged (FlatTreeItem[] added, FlatTreeItem[] removed)
        {
            bool isClear = this.SelectedItems.Count == 0;
            this.SelectionChanged?.Invoke(this, new SelectionChange<FlatTreeItem>(added, removed, isClear));
        }
    }

    // ===========[ eFlatTreeSelectionMode ]=====================================
    public enum eFlatTreeSelectionMode
    {
        None,
        Single,
        Multi,
        Extended,   // click = replace selection; Ctrl+click = add to selection
    }
}
