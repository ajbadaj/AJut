namespace AJut.UX.Controls
{
    using AJut.Storage;
    using AJut.Tree;
    using AJut.UX;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Input;
    using Microsoft.UI.Xaml.Media;
    using Microsoft.UI.Xaml.Shapes;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;
    using System.Linq;
    using Windows.Foundation;
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
        private const double kGhostOpacity = 0.65;

        // ===========[ Instance fields ]==========================================
        private ListView PART_ListView;
        private Canvas PART_DragOverlay;
        private Rectangle PART_InsertionLine;
        private Polyline PART_ParentConnectorLine;
        private Border PART_DragGhost;
        private ContentPresenter PART_DragGhostContent;

        private readonly ObservableFlatTreeStore<FlatTreeItem> m_store;
        private readonly ObservableCollection<FlatTreeItem> m_selectedItems = new ObservableCollection<FlatTreeItem>();
        private bool m_blockingReentrancy;

        // Drag state
        private bool m_isDragPending;
        private bool m_isDragging;
        private Point m_dragStartPoint;
        private Pointer m_dragPointer;
        private FlatTreeItem[] m_dragItems;
        private FlatTreeDropTarget m_currentDropTarget;
        private FlatTreeItemRow m_highlightedParentRow;
        private Brush m_highlightedParentOriginalBrush;
        private double m_rowXInOverlay = double.NaN;

        // ===========[ Construction ]=============================================
        public FlatTreeListControl ()
        {
            this.DefaultStyleKey = typeof(FlatTreeListControl);
            m_store = new ObservableFlatTreeStore<FlatTreeItem>();
        }

        // ===========[ Events ]===================================================
        public event EventHandler<SelectionChange<FlatTreeItem>> SelectionChanged;
        public event EventHandler<FlatTreeItem> ItemDoubleClicked;

        /// Fires before a drag/drop reorder is executed. Set Cancel=true to prevent
        /// the default reorder (e.g. to wrap in undo/redo instead).
        public event EventHandler<FlatTreeReorderEventArgs> DragDropReorderRequested;

        /// Fires when an external item is dropped onto the tree (stretch goal hook).
        public event EventHandler<FlatTreeExternalDropEventArgs> ExternalItemDropped;

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
        /// When true, suppresses the default WinUI3 ListView add/delete/reposition
        /// animations by clearing ItemContainerTransitions. Used by PropertyGrid to
        /// prevent distracting transitions during list add/remove/reorder without
        /// affecting other FlatTreeListControl instances.
        /// </summary>
        public static readonly DependencyProperty SuppressItemTransitionsProperty = DPUtils.Register(_ => _.SuppressItemTransitions, false);
        public bool SuppressItemTransitions
        {
            get => (bool)this.GetValue(SuppressItemTransitionsProperty);
            set => this.SetValue(SuppressItemTransitionsProperty, value);
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
        /// The base formula aligns to the default ListViewItem style (Margin="6,0,0,0").
        /// PropertyGrid uses a no-margin ListViewItem style, so it sets this to compensate.
        /// </summary>
        public static readonly DependencyProperty InsertionLineXOffsetProperty = DPUtils.Register(_ => _.InsertionLineXOffset);
        public double InsertionLineXOffset
        {
            get => (double)this.GetValue(InsertionLineXOffsetProperty);
            set => this.SetValue(InsertionLineXOffsetProperty, value);
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
                this.PART_ListView.RemoveHandler(PointerPressedEvent, (PointerEventHandler)this.ListView_OnPointerPressed);
                this.PART_ListView.RemoveHandler(PointerMovedEvent, (PointerEventHandler)this.ListView_OnPointerMoved);
                this.PART_ListView.RemoveHandler(PointerReleasedEvent, (PointerEventHandler)this.ListView_OnPointerReleased);
                this.PART_ListView.PointerCaptureLost -= this.ListView_OnPointerCaptureLost;
            }

            this.PART_ListView = (ListView)this.GetTemplateChild(nameof(PART_ListView));
            this.PART_DragOverlay = this.GetTemplateChild(nameof(PART_DragOverlay)) as Canvas;
            this.PART_InsertionLine = this.GetTemplateChild(nameof(PART_InsertionLine)) as Rectangle;
            this.PART_ParentConnectorLine = this.GetTemplateChild(nameof(PART_ParentConnectorLine)) as Polyline;
            this.PART_DragGhost = this.GetTemplateChild(nameof(PART_DragGhost)) as Border;
            this.PART_DragGhostContent = this.GetTemplateChild(nameof(PART_DragGhostContent)) as ContentPresenter;

            if (this.PART_ListView == null)
            {
                return;
            }

            this.PART_ListView.SelectionChanged += this.ListView_OnSelectionChanged;
            this.PART_ListView.DoubleTapped += this.ListView_OnDoubleTapped;
            this.PART_ListView.KeyUp += this.ListView_OnKeyUp;
            this.PART_ListView.ContainerContentChanging += this.ListView_OnContainerContentChanging;
            // Use AddHandler with handledEventsToo so we receive pointer events even
            // after the ListView marks them Handled for its own selection/scroll logic.
            this.PART_ListView.AddHandler(PointerPressedEvent, (PointerEventHandler)this.ListView_OnPointerPressed, true);
            this.PART_ListView.AddHandler(PointerMovedEvent, (PointerEventHandler)this.ListView_OnPointerMoved, true);
            this.PART_ListView.AddHandler(PointerReleasedEvent, (PointerEventHandler)this.ListView_OnPointerReleased, true);
            this.PART_ListView.PointerCaptureLost += this.ListView_OnPointerCaptureLost;

            if (this.ListViewItemContainerStyle != null)
            {
                this.PART_ListView.ItemContainerStyle = this.ListViewItemContainerStyle;
            }

            if (this.SuppressItemTransitions)
            {
                this.PART_ListView.ItemContainerTransitions = new Microsoft.UI.Xaml.Media.Animation.TransitionCollection();
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

                if (this.SelectionMode == eFlatTreeSelectionMode.Single)
                {
                    // In Single mode, always fully replace selection. After tree restructures
                    // (e.g. reparenting via drag-drop), items can become orphaned in SelectedItems
                    // because the ListView silently drops them without firing SelectionChanged.
                    // A full clear prevents stale multi-selection.
                    foreach (FlatTreeItem item in m_selectedItems)
                    {
                        item.IsSelected = false;
                    }

                    m_selectedItems.Clear();
                }
                else
                {
                    foreach (FlatTreeItem item in removed)
                    {
                        item.IsSelected = false;
                        this.SelectedItems.Remove(item);
                    }
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
            // Don't intercept arrow keys when focus is inside an editor (TextBox, etc.)
            // - those keys belong to the editor for cursor movement or value nudging.
            var focused = FocusManager.GetFocusedElement(this.XamlRoot);
            if (focused is TextBox || focused is PasswordBox || focused is RichEditBox)
            {
                return;
            }

            if (e.Key == Windows.System.VirtualKey.Left)
            {
                foreach (FlatTreeItem item in this.SelectedItems.ToList())
                {
                    if (item.IsExpanded)
                    {
                        // Collapse expanded node
                        item.IsExpanded = false;
                    }
                    else if (item.Parent != null && m_store.Contains(item.Parent))
                    {
                        // Navigate to parent when already collapsed
                        this.SelectedItem = item.Parent;
                        this.PART_ListView.SelectedItem = item.Parent;
                        this.PART_ListView.ScrollIntoView(item.Parent);
                    }
                }
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Right)
            {
                foreach (FlatTreeItem item in this.SelectedItems.ToList())
                {
                    if (item.IsExpandable && !item.IsExpanded)
                    {
                        // Expand collapsed node
                        item.IsExpanded = true;
                    }
                    else if (item.IsExpanded && item.Children.Count > 0)
                    {
                        // Navigate to first child when already expanded
                        var firstChild = (FlatTreeItem)item.Children[0];
                        this.SelectedItem = firstChild;
                        this.PART_ListView.SelectedItem = firstChild;
                        this.PART_ListView.ScrollIntoView(firstChild);
                    }
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

        // ===========[ Drag/Drop Pointer Handlers ]================================

        private void ListView_OnPointerPressed (object sender, PointerRoutedEventArgs e)
        {
            if (!this.IsDragDropReorderEnabled || m_isDragging)
            {
                return;
            }

            // Only start drag from left button
            if (!e.GetCurrentPoint(this.PART_ListView).Properties.IsLeftButtonPressed)
            {
                return;
            }

            // Find the FlatTreeItem under the pointer
            FlatTreeItem pressedItem = this.FindFlatTreeItemAtPoint(e.GetCurrentPoint(this.PART_ListView).Position);
            if (pressedItem == null || pressedItem.TreeDepth <= 0 || pressedItem.IsFalseRoot)
            {
                return;
            }

            m_isDragPending = true;
            m_dragStartPoint = e.GetCurrentPoint(this.PART_ListView).Position;
            m_dragPointer = e.Pointer;
            // Don't capture yet - let the ListView handle selection normally.
            // We'll capture once the drag threshold is exceeded in PointerMoved.
        }

        private void ListView_OnPointerMoved (object sender, PointerRoutedEventArgs e)
        {
            if (!this.IsDragDropReorderEnabled)
            {
                return;
            }

            Point currentPoint = e.GetCurrentPoint(this.PART_ListView).Position;

            if (m_isDragPending && !m_isDragging)
            {
                // Check if we've moved past the drag threshold
                double dx = currentPoint.X - m_dragStartPoint.X;
                double dy = currentPoint.Y - m_dragStartPoint.Y;
                if (Math.Sqrt(dx * dx + dy * dy) >= kDragThresholdPx)
                {
                    this.BeginDrag();
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

            // Update drag visuals
            this.UpdateDragVisuals(currentPoint);
            e.Handled = true;
        }

        private void ListView_OnPointerReleased (object sender, PointerRoutedEventArgs e)
        {
            if (m_isDragging)
            {
                this.CompleteDrag();
                e.Handled = true;
            }

            this.CancelDragPending(e.Pointer);
        }

        private void ListView_OnPointerCaptureLost (object sender, PointerRoutedEventArgs e)
        {
            if (m_isDragging)
            {
                this.CancelDrag();
            }

            m_isDragPending = false;
        }

        // ===========[ Drag/Drop Logic ]==========================================

        private void BeginDrag ()
        {
            // Reset measured row position (will be lazily computed in UpdateDragVisuals)
            m_rowXInOverlay = double.NaN;

            // Find the item directly under the press point
            FlatTreeItem pressedItem = this.FindFlatTreeItemAtPoint(m_dragStartPoint);

            if (pressedItem != null && pressedItem.IsSelected && m_selectedItems.Count > 0)
            {
                // Pressed item is part of the current selection - drag all selected items
                // (supports multi-select drag where you select several, then drag one)
                m_dragItems = m_selectedItems
                    .Where(i => i.TreeDepth > 0 && !i.IsFalseRoot)
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

            // Now capture the pointer so we receive all subsequent move/release events
            if (m_dragPointer != null)
            {
                this.PART_ListView.CapturePointer(m_dragPointer);
            }

            // Show the overlay
            if (this.PART_DragOverlay != null)
            {
                this.PART_DragOverlay.Visibility = Visibility.Visible;
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
                    this.PART_DragGhostContent.Content = new TextBlock { Text = label, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), FontSize = 11 };
                }

                this.PART_DragGhost.Visibility = Visibility.Visible;
            }
        }

        private void UpdateDragVisuals (Point cursorInListView)
        {
            if (m_dragItems == null || this.PART_DragOverlay == null)
            {
                return;
            }

            // Position the ghost at the cursor
            if (this.PART_DragGhost != null)
            {
                // Convert cursor from ListView space to overlay space
                var transform = this.PART_ListView.TransformToVisual(this.PART_DragOverlay);
                Point cursorInOverlay = transform.TransformPoint(cursorInListView);
                Canvas.SetLeft(this.PART_DragGhost, cursorInOverlay.X + 12);
                Canvas.SetTop(this.PART_DragGhost, cursorInOverlay.Y + 17);
            }

            // Determine which row the cursor is over and compute the drop target
            int hoverIndex = -1;
            double cursorYFraction = 0.5;
            double rowTopInOverlay = 0;
            double rowHeight = 0;

            for (int i = 0; i < this.PART_ListView.Items.Count; ++i)
            {
                if (this.PART_ListView.ContainerFromIndex(i) is ListViewItem container)
                {
                    var containerTransform = container.TransformToVisual(this.PART_ListView);
                    Point containerOrigin = containerTransform.TransformPoint(new Point(0, 0));
                    double containerBottom = containerOrigin.Y + container.ActualHeight;

                    if (cursorInListView.Y >= containerOrigin.Y && cursorInListView.Y < containerBottom)
                    {
                        hoverIndex = i;
                        cursorYFraction = (cursorInListView.Y - containerOrigin.Y) / container.ActualHeight;
                        rowHeight = container.ActualHeight;

                        // Convert container position to overlay space
                        var overlayTransform = container.TransformToVisual(this.PART_DragOverlay);
                        rowTopInOverlay = overlayTransform.TransformPoint(new Point(0, 0)).Y;
                        break;
                    }
                }
            }

            // If cursor is below all items, treat as end-of-list
            if (hoverIndex < 0 && m_store.Count > 0)
            {
                hoverIndex = m_store.Count - 1;
                cursorYFraction = 1.0;
                if (this.PART_ListView.ContainerFromIndex(hoverIndex) is ListViewItem lastContainer)
                {
                    rowHeight = lastContainer.ActualHeight;
                    var overlayTransform = lastContainer.TransformToVisual(this.PART_DragOverlay);
                    rowTopInOverlay = overlayTransform.TransformPoint(new Point(0, 0)).Y;
                }
            }

            if (hoverIndex < 0)
            {
                this.HideDragIndicators();
                return;
            }

            // Compute drop target
            double indentSize = this.TreeDepthIndentSize;
            var dropTarget = FlatTreeDragDropManager.ComputeDropTarget(
                m_store, m_dragItems, hoverIndex, cursorYFraction, cursorInListView.X, indentSize
            );

            // Validate
            if (dropTarget == null
                || !FlatTreeDragDropManager.ValidateDropTarget(m_dragItems, dropTarget, this.CanDropItem))
            {
                this.HideDragIndicators();
                m_currentDropTarget = null;
                return;
            }

            m_currentDropTarget = dropTarget;

            // Lazily measure the row content X position in overlay coords. This captures
            // the ListViewItem container's ContentPresenter margin (6px in the default
            // AJut_FlatTree_ListViewItemStyle, 0 in PropertyGrid's no-chrome style) and
            // any ListView internal chrome offset, so all line positions are calculated
            // correctly regardless of container style.
            if (double.IsNaN(m_rowXInOverlay))
            {
                if (this.PART_ListView.ContainerFromIndex(hoverIndex) is ListViewItem measureContainer)
                {
                    FlatTreeItemRow measureRow = FindDescendant<FlatTreeItemRow>(measureContainer);
                    if (measureRow != null)
                    {
                        m_rowXInOverlay = measureRow.TransformToVisual(this.PART_DragOverlay)
                            .TransformPoint(new Point(0, 0)).X;
                    }
                }

                if (double.IsNaN(m_rowXInOverlay))
                {
                    m_rowXInOverlay = 6.0;
                }
            }

            // Compute shared line Y (gap between rows) and insertion line X (content column start).
            // When hovering in the top half of the first row, clamp to the bottom edge so the
            // insertion indicator never appears above the root of the tree.
            double lineY;
            if (hoverIndex == 0 && cursorYFraction < 0.5)
            {
                lineY = rowTopInOverlay + rowHeight;
            }
            else
            {
                lineY = cursorYFraction < 0.5 ? rowTopInOverlay : rowTopInOverlay + rowHeight;
            }
            const double kExpanderColumnWidth = 18.0;
            double lineX = m_rowXInOverlay + dropTarget.TargetDepth * indentSize + kExpanderColumnWidth + this.InsertionLineXOffset;

            // Position the insertion line at the content column start for the target depth
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

            // Position the parent connector L-shape and highlight the parent's chevron
            this.ClearParentHighlight();

            int parentStoreIndex = FlatTreeDragDropManager.FindParentStoreIndex(m_store, dropTarget);
            if (parentStoreIndex >= 0 && this.PART_ListView.ContainerFromIndex(parentStoreIndex) is ListViewItem parentContainer)
            {
                // Highlight the parent's chevron
                FlatTreeItemRow parentRow = FindDescendant<FlatTreeItemRow>(parentContainer);
                if (parentRow != null)
                {
                    m_highlightedParentOriginalBrush = parentRow.ExpanderGlyphForeground;
                    m_highlightedParentRow = parentRow;
                    Brush highlightBrush = this.DragTargetHighlightBrush ?? this.InsertionLineBrush;
                    if (highlightBrush != null)
                    {
                        parentRow.ExpanderGlyphForeground = highlightBrush;
                    }
                }

                // Draw an L-shape: vertical from parent's chevron down, then horizontal to insertion line
                if (this.PART_ParentConnectorLine != null)
                {
                    var parentTransform = parentContainer.TransformToVisual(this.PART_DragOverlay);
                    double parentCenterY = parentTransform.TransformPoint(new Point(0, 0)).Y
                                         + parentContainer.ActualHeight / 2.0;

                    // Chevron center X in overlay space (9 = half of 18px expander column)
                    int parentDepth = dropTarget.TargetDepth - 1;
                    double chevronX = m_rowXInOverlay + parentDepth * indentSize + 9.0;

                    // L-shape: top of vertical, corner, end of horizontal
                    double topY = Math.Min(parentCenterY, lineY) + 5.0;
                    double bottomY = Math.Max(parentCenterY, lineY);

                    var points = new PointCollection
                    {
                        new Point(chevronX, topY),
                        new Point(chevronX, bottomY),
                        new Point(lineX, bottomY),
                    };
                    this.PART_ParentConnectorLine.Points = points;
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

        private void CompleteDrag ()
        {
            if (m_dragItems != null && m_currentDropTarget != null && m_currentDropTarget.IsValid)
            {
                IObservableTreeNode[] sourceNodes = m_dragItems.Select(i => i.Source).ToArray();

                // Fire the event so consumers can cancel or handle it themselves
                var args = new FlatTreeReorderEventArgs(sourceNodes, m_currentDropTarget.TargetParent, m_currentDropTarget.InsertIndex);
                this.DragDropReorderRequested?.Invoke(this, args);

                if (!args.Cancel)
                {
                    FlatTreeDragDropManager.ExecuteReorder(sourceNodes, m_currentDropTarget);
                }
            }

            this.CancelDrag();
        }

        private void CancelDrag ()
        {
            m_isDragging = false;
            m_isDragPending = false;
            m_dragItems = null;
            m_currentDropTarget = null;

            // Release pointer capture
            if (m_dragPointer != null && this.PART_ListView != null)
            {
                this.PART_ListView.ReleasePointerCapture(m_dragPointer);
                m_dragPointer = null;
            }

            this.HideDragIndicators();

            if (this.PART_DragOverlay != null)
            {
                this.PART_DragOverlay.Visibility = Visibility.Collapsed;
            }

            if (this.PART_DragGhost != null)
            {
                this.PART_DragGhost.Visibility = Visibility.Collapsed;
            }
        }

        private void CancelDragPending (Pointer pointer)
        {
            if (m_isDragPending)
            {
                m_isDragPending = false;
            }
        }

        private void HideDragIndicators ()
        {
            if (this.PART_InsertionLine != null)
            {
                this.PART_InsertionLine.Visibility = Visibility.Collapsed;
            }

            if (this.PART_ParentConnectorLine != null)
            {
                this.PART_ParentConnectorLine.Visibility = Visibility.Collapsed;
            }

            this.ClearParentHighlight();
        }

        private void ClearParentHighlight ()
        {
            if (m_highlightedParentRow != null)
            {
                m_highlightedParentRow.ExpanderGlyphForeground = m_highlightedParentOriginalBrush;
                m_highlightedParentRow = null;
                m_highlightedParentOriginalBrush = null;
            }
        }

        private static T FindDescendant<T> (DependencyObject parent) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; ++i)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                {
                    return match;
                }

                T descendant = FindDescendant<T>(child);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }

        private FlatTreeItem FindFlatTreeItemAtPoint (Point pointInListView)
        {
            if (this.PART_ListView == null)
            {
                return null;
            }

            for (int i = 0; i < this.PART_ListView.Items.Count; ++i)
            {
                if (this.PART_ListView.ContainerFromIndex(i) is ListViewItem container)
                {
                    var transform = container.TransformToVisual(this.PART_ListView);
                    Point origin = transform.TransformPoint(new Point(0, 0));
                    if (pointInListView.Y >= origin.Y
                        && pointInListView.Y < origin.Y + container.ActualHeight)
                    {
                        return this.PART_ListView.ItemFromContainer(container) as FlatTreeItem;
                    }
                }
            }

            return null;
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
