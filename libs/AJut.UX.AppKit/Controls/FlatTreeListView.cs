namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using AJut.Storage;
    using AJut.Tree;
    using AJut.UX.Theming;
    using AppKit;
    using CoreGraphics;
    using Foundation;
    using ObjCRuntime;

    /// <summary>How many rows a <see cref="FlatTreeListView"/> lets the user select</summary>
    public enum eFlatTreeSelectionMode
    {
        /// <summary>Rows cannot be selected</summary>
        None,

        /// <summary>One row at a time</summary>
        Single,

        /// <summary>Any number of rows, each click toggling one</summary>
        Multi,

        /// <summary>Any number of rows the Mac way: a click selects one, Command-click toggles, Shift-click selects a range</summary>
        Extended,
    }

    // ===========[ FlatTreeListView ]===========================================
    // A tree shown as a flat list of rows: the AppKit view over AJut.UX's FlatTreeItem (expand
    // and collapse, indent, visible versus hidden children) and AJut.Core's ObservableFlatTreeStore
    // (the visible rows in order). Rows are an NSTableView's, one column, no header.
    //
    // NSOutlineView is the obvious AppKit choice and is not used: it keeps its own expansion state,
    // which would fight FlatTreeItem's (and through it IExpandableNode's), and it wants an NSObject
    // per item. The flat store already is the outline's flattened form, so a plain table over it
    // does exactly what the WinUI3 and WPF FlatTreeListControls do, with the same models.
    //
    // The store reports an expand or collapse one row at a time, synchronously, each event's index
    // correct against the rows right after the previous event (a collapse removes at the same index
    // over and over). So each event is applied to the table the moment it arrives, as a single row
    // insert or remove. Collecting them and applying the set afterward would be wrong.
    //
    // Row views are built fresh for each row the table asks for, never recycled. Recycling is
    // where the WinUI3 grid got stale editors from, and a PropertyGrid row holds live editors; a
    // row's content is torn down when the table drops its view.
    //
    // An AppKit view and its managed peer keep each other alive while anything native the view
    // retains holds the view: the native side roots the peer, and the peer roots the native side.
    // A shown row's view is retained by the table, and reaches the store through its tree item and
    // whatever its content binds to, and the store and the root items reach this view through their
    // events. So out of a window the tree shows no rows at all, its row views are removed and torn
    // down, and it stops following the store and the root items; back in one it catches up and
    // builds its rows again, with the selection and scroll position as they were. The table, its
    // data source, and its delegate hold the tree weakly.
    public class FlatTreeListView : NSView
    {
        private const double kExpanderColumnWidth = 18.0;
        private const double kDefaultRowHeight = 24.0;
        private const double kListBorderThickness = 1.0;
        private const string kDragType = "dev.ajut.flattree.row";

        // Where the WinUI3 tree's row content starts inside its list: the ListViewItem content margin in
        // AJut_FlatTree_ListViewItemStyle (see DragDepthRowOffset)
        private const double kWinUI3TreeRowContentInset = 6.0;

        private readonly ObservableFlatTreeStore<FlatTreeItem> m_store = new ObservableFlatTreeStore<FlatTreeItem>();
        private readonly ThemedView m_listFrame;
        private readonly NSLayoutConstraint[] m_scrollViewInsets;
        private readonly TreeTableView m_table;
        private readonly NSScrollView m_scrollView;
        private readonly TableDataSource m_dataSource;
        private readonly TableDelegate m_delegate;
        private readonly List<FlatTreeItem> m_selectedItems = new List<FlatTreeItem>();
        private IObservableTreeNode m_root;
        private IEnumerable<IObservableTreeNode> m_rootItemsSource;
        private IObservableTreeNode[] m_builtFromRootItems;
        private INotifyCollectionChanged m_listenedRootItems;
        private bool m_isFollowingStore;
        private CGPoint m_scrollOffsetWhenDetached;
        private eFlatTreeSelectionMode m_selectionMode = eFlatTreeSelectionMode.Single;
        private eSiblingOrder m_siblingDisplayOrder = eSiblingOrder.Forward;
        private double m_treeDepthIndentSize = 16.0;
        private double m_dragDepthRowOffset = kWinUI3TreeRowContentInset;
        private bool m_isSyncingSelection;
        private bool m_drawsRowSelection = true;
        private FlatTreeItem[] m_draggedItems;
        private readonly DropIndicatorView m_dropIndicator = new DropIndicatorView();
        private FlatTreeDropTarget m_shownDropTarget;
        private RowCellView m_highlightedParentCell;

        // ===========[ Setup/Construction/Teardown ]===========================
        public FlatTreeListView ()
        {
            m_table = new TreeTableView(this)
            {
                HeaderView = null,
                UsesAlternatingRowBackgroundColors = false,
                BackgroundColor = NSColor.Clear,
                IntercellSpacing = new CGSize(0, 2),

                // Rows size to their content by default, as FixedRowHeight documents (NaN). RowHeight is then only the
                // estimate the table starts from. A fixed 24 clips any row taller than one line, like a PropertyGrid
                // row with a subtitle under its name.
                UsesAutomaticRowHeights = true,
                RowHeight = (nfloat)kDefaultRowHeight,
                AllowsEmptySelection = true,
            };
            m_table.AddColumn(new NSTableColumn("main") { ResizingMask = NSTableColumnResizing.Autoresizing });
            m_table.ColumnAutoresizingStyle = NSTableViewColumnAutoresizingStyle.FirstColumnOnly;
            m_table.RegisterForDraggedTypes(new[] { kDragType });

            // The table's own insertion line marks the gap it proposes, which is not always where the drop lands (over
            // the root it showed a line above a row nothing can go above), and it cannot show depth. The drop is drawn
            // from the computed target instead, as the WinUI3 tree does.
            m_table.DraggingDestinationFeedbackStyle = NSTableViewDraggingDestinationFeedbackStyle.None;
            m_dropIndicator.AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable;
            m_table.AddSubview(m_dropIndicator);
            m_table.SetDraggingSourceOperationMask(NSDragOperation.Move, true);

            m_dataSource = new TableDataSource(this);
            m_delegate = new TableDelegate(this);
            m_table.DataSource = m_dataSource;
            m_table.Delegate = m_delegate;

            // The list's fill and outline, behind the rows, as WinUI3's themed FlatTreeListControl has them. The rows sit
            //  inside the outline (see ListBorderColorKey), which a layer would otherwise draw over.
            m_listFrame = new ThemedView
            {
                BackgroundColorKey = ThemedColorKeys.FlatTree_ListBackground,
                BorderColorKey = ThemedColorKeys.FlatTree_ListBorder,
                BorderThickness = kListBorderThickness,
                TranslatesAutoresizingMaskIntoConstraints = false,
            };
            this.AddSubview(m_listFrame);

            m_scrollView = new NSScrollView
            {
                HasVerticalScroller = true,
                AutohidesScrollers = true,
                DrawsBackground = false,
                DocumentView = m_table,
                TranslatesAutoresizingMaskIntoConstraints = false,
            };
            this.AddSubview(m_scrollView);

            // Top, bottom, leading, trailing: the order ApplyScrollViewInset sets them in
            m_scrollViewInsets = new[]
            {
                m_scrollView.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                m_scrollView.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                m_scrollView.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                m_scrollView.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
            };
            this.ApplyScrollViewInset(kListBorderThickness);
            NSLayoutConstraint.ActivateConstraints(m_scrollViewInsets);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_listFrame.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                m_listFrame.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                m_listFrame.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                m_listFrame.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
            });

            this.ApplySelectionMode();
        }

        /// <summary>
        /// Used by the runtime when AppKit needs a managed peer for a native view whose peer is gone (one disposed while
        /// still in a window, say). It only answers AppKit's calls: the native view keeps the table it has. None of its
        /// managed state survives a dispose, so a call that needs that state can still fail: dispose it only once AppKit
        /// is done with it.
        /// </summary>
        protected FlatTreeListView (NativeHandle handle) : base(handle)
        {
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                this.FollowStore(false);
                this.ListenToRootItems(null);

                // The table only reports the row views it drops while it lives, so the rows still
                // showing are torn down here, or their content's bindings outlive the list
                m_table.EnumerateAvailableRowViews(TeardownRowView);
                m_store.RootNode?.DisposeTree();
                m_table.DataSource = null;
                m_table.Delegate = null;
            }

            base.Dispose(disposing);
        }

        // ===========[ Events ]================================================
        /// <summary>Raised when the selected rows change, by the user or from code</summary>
        public event EventHandler<SelectionChange<FlatTreeItem>> SelectionChanged;

        /// <summary>Raised when a row is double clicked, after any expand or collapse it caused</summary>
        public event EventHandler<FlatTreeItem> ItemDoubleClicked;

        /// <summary>
        /// Raised when the user drops dragged rows somewhere valid. Set Cancel to do the move yourself (a
        /// PropertyGrid moves list elements in their list instead); otherwise the nodes are moved in their tree.
        /// </summary>
        public event EventHandler<FlatTreeReorderEventArgs> DragDropReorderRequested;

        private void OnStoreCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            // One row per event, applied right away; see the class comment for why
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add when e.NewItems?.Count == 1 && e.NewStartingIndex >= 0:
                    m_table.InsertRows(NSIndexSet.FromIndex(e.NewStartingIndex), NSTableViewAnimation.None);
                    break;

                case NotifyCollectionChangedAction.Remove when e.OldItems?.Count == 1 && e.OldStartingIndex >= 0:
                    this.ForgetSelected((FlatTreeItem)e.OldItems[0]);
                    m_table.RemoveRows(NSIndexSet.FromIndex(e.OldStartingIndex), NSTableViewAnimation.None);
                    break;

                default:
                    m_table.ReloadData();
                    this.RestoreTableSelection();
                    break;
            }
        }

        private void OnRootItemsCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            // The store does not watch a root it is not showing, so a change to the top-level items
            // rebuilds the tree, as the WinUI3 control does
            this.RebuildFromRootItems();
        }

        public override void ViewDidMoveToWindow ()
        {
            base.ViewDidMoveToWindow();
            if (this.Window != null)
            {
                this.OnAttached();
            }
            else
            {
                this.OnDetached();
            }
        }

        // ===========[ Properties ]============================================
        public override bool IsFlipped => true;

        /// <summary>One root node; its descendants are the rows (and the root itself, with <see cref="IncludeRoot"/>)</summary>
        public IObservableTreeNode Root
        {
            get => m_root;
            set
            {
                m_root = value;
                m_rootItemsSource = null;
                m_builtFromRootItems = null;
                this.ListenToRootItems(null);
                this.ReplaceStoreRoot(value != null ? FlatTreeItem.CreateRoot(value, m_treeDepthIndentSize, this.StartItemsExpanded, this.SiblingDisplayOrder) : null);
            }
        }

        /// <summary>
        /// Several top-level nodes, each a row at depth 0 under a hidden, always-expanded root. An observable
        /// collection is followed while the tree is in a window: any change rebuilds the rows.
        /// </summary>
        public IEnumerable<IObservableTreeNode> RootItemsSource
        {
            get => m_rootItemsSource;
            set
            {
                m_root = null;
                m_rootItemsSource = value;
                this.ListenToRootItems(this.Window != null ? value as INotifyCollectionChanged : null);
                this.RebuildFromRootItems();
            }
        }

        /// <summary>Whether the <see cref="Root"/> node itself is a row</summary>
        public bool IncludeRoot
        {
            get => m_store.IncludeRoot;
            set => m_store.IncludeRoot = value;
        }

        /// <summary>The rows, in order</summary>
        public ObservableFlatTreeStore<FlatTreeItem> Items => m_store;

        /// <summary>Points of indent per tree depth</summary>
        public double TreeDepthIndentSize
        {
            get => m_treeDepthIndentSize;
            set
            {
                m_treeDepthIndentSize = value;
                if (m_store.RootNode != null)
                {
                    foreach (FlatTreeItem item in EnumerateAll(m_store.RootNode))
                    {
                        item.TreeDepthIndentSize = value;
                    }
                }

                m_table.ReloadData();
            }
        }

        /// <summary>Space between rows, in points</summary>
        public double RowSpacing
        {
            get => (double)m_table.IntercellSpacing.Height;
            set => m_table.IntercellSpacing = new CGSize(0, (nfloat)value);
        }

        /// <summary>A fixed height for every row, or NaN (the default) to size each row to its content</summary>
        public double FixedRowHeight
        {
            get => m_table.UsesAutomaticRowHeights ? double.NaN : (double)m_table.RowHeight;
            set
            {
                m_table.UsesAutomaticRowHeights = double.IsNaN(value);
                if (!double.IsNaN(value))
                {
                    m_table.RowHeight = (nfloat)value;
                }
            }
        }

        /// <summary>Whether a <see cref="Root"/> starts with its rows expanded</summary>
        public bool StartItemsExpanded { get; set; }

        /// <summary>The order siblings are shown in; changing it rebuilds the rows</summary>
        public eSiblingOrder SiblingDisplayOrder
        {
            get => m_siblingDisplayOrder;
            set
            {
                if (m_siblingDisplayOrder == value)
                {
                    return;
                }

                m_siblingDisplayOrder = value;
                this.RebuildFromCurrentRoot();
            }
        }

        public eFlatTreeSelectionMode SelectionMode
        {
            get => m_selectionMode;
            set
            {
                m_selectionMode = value;
                this.ApplySelectionMode();
                this.SetSelection(m_selectedItems.ToArray());
            }
        }

        /// <summary>The selected row (the first, when several are selected), or null</summary>
        public FlatTreeItem SelectedItem
        {
            get => m_selectedItems.Count > 0 ? m_selectedItems[0] : null;
            set => this.SetSelection(value != null ? new[] { value } : Array.Empty<FlatTreeItem>());
        }

        /// <summary>Every selected row</summary>
        public IReadOnlyList<FlatTreeItem> SelectedItems => m_selectedItems;

        /// <summary>Whether a double click on an expandable row expands or collapses it; on by default</summary>
        public bool ShouldToggleExpansionOnDoubleClick { get; set; } = true;

        /// <summary>
        /// The <see cref="ThemedColors"/> key the list is filled with, or null for no fill. By default the AJut list
        /// background with the theme loaded, and clear without one.
        /// </summary>
        public string ListBackgroundColorKey
        {
            get => m_listFrame.BackgroundColorKey;
            set => m_listFrame.BackgroundColorKey = value;
        }

        /// <summary>
        /// The <see cref="ThemedColors"/> key of the list's 1pt outline, or null for none, which also lets the rows reach
        /// the list's edges. By default the AJut list border with the theme loaded, and clear without one.
        /// </summary>
        public string ListBorderColorKey
        {
            get => m_listFrame.BorderColorKey;
            set
            {
                m_listFrame.BorderColorKey = value;
                this.ApplyScrollViewInset((value != null) ? kListBorderThickness : 0.0);
            }
        }

        /// <summary>
        /// Whether a selected row draws the tree's selection (a fill, a leading strip and an outline); on by default. A
        /// control that marks selection its own way turns it off, the way the PropertyGrid marks only a row's label, and
        /// the pointer-over look then draws on a selected row the same as on any other.
        /// </summary>
        public bool DrawsRowSelection
        {
            get => m_drawsRowSelection;
            set
            {
                m_drawsRowSelection = value;
                m_table.EnumerateAvailableRowViews(MarkRowViewForRedisplay);
            }
        }

        /// <summary>The text the drag ghost shows for one dragged row; its source's ToString when not set. Several rows show "{n} items".</summary>
        public Func<FlatTreeItem, string> DragGhostTextFactory { get; set; }

        /// <summary>
        /// Builds the content of a row, right of its indent and expander. It is called each time the table needs a
        /// row's view; a returned view implementing <see cref="IFlatTreeRowContent"/> is torn down when the table
        /// drops it. By default a row shows its source's ToString. The view takes the rest of the row's width and is
        /// centered in its height at its own height, so a container needs a height of its own (its content pinned to
        /// its top and bottom, or a height constraint): one with none is given none.
        /// </summary>
        public Func<FlatTreeItem, NSView> RowContentFactory { get; set; }

        /// <summary>Lets the user drag rows to reorder or reparent them</summary>
        public bool IsDragDropReorderEnabled { get; set; }

        /// <summary>Whether a node may be dragged; all may when not set</summary>
        public Func<IObservableTreeNode, bool> CanDragItem { get; set; }

        /// <summary>Whether a dragged node may land under a parent; any may when not set</summary>
        public Func<IObservableTreeNode, IObservableTreeNode, bool> CanDropItem { get; set; }

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Replaces the selection. Single mode keeps only the first item, None selects nothing.</summary>
        public void SetSelection (IEnumerable<FlatTreeItem> items)
        {
            var wanted = new List<FlatTreeItem>();
            if (items != null && m_selectionMode != eFlatTreeSelectionMode.None)
            {
                foreach (FlatTreeItem item in items)
                {
                    if (item != null && item.IsSelectable && !wanted.Contains(item))
                    {
                        wanted.Add(item);
                        if (m_selectionMode == eFlatTreeSelectionMode.Single)
                        {
                            break;
                        }
                    }
                }
            }

            this.ApplySelection(wanted);
            this.RestoreTableSelection();
        }

        /// <summary>Finds the row for a source node, collapsed or not, or null</summary>
        public FlatTreeItem FindItemForSource (object source)
        {
            if (m_store.RootNode == null || source == null)
            {
                return null;
            }

            foreach (FlatTreeItem item in EnumerateAll(m_store.RootNode))
            {
                if (ReferenceEquals(item.Source, source))
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>Selects the row for a source node, expanding its collapsed ancestors first. False if there is no such row.</summary>
        public bool TrySelectItemForSource (object source, bool scrollIntoView = true)
        {
            FlatTreeItem item = this.FindItemForSource(source);
            if (item == null)
            {
                return false;
            }

            // Expand top down, so each expansion inserts its rows under an already visible parent
            var ancestors = new Stack<FlatTreeItem>();
            for (FlatTreeItem parent = item.Parent; parent != null; parent = parent.Parent)
            {
                ancestors.Push(parent);
            }

            while (ancestors.Count > 0)
            {
                FlatTreeItem ancestor = ancestors.Pop();
                if (!ancestor.IsExpanded && !ancestor.IsFalseRoot)
                {
                    ancestor.IsExpanded = true;
                }
            }

            this.SetSelection(new[] { item });
            if (scrollIntoView)
            {
                this.ScrollIntoView(item);
            }

            return true;
        }

        /// <summary>
        /// Builds every shown row's content again through <see cref="RowContentFactory"/>, keeping expansion and
        /// selection; for when something the content is built from has changed. The old content is torn down.
        /// </summary>
        public void ReloadRowContent ()
        {
            m_table.ReloadData();
            this.RestoreTableSelection();
        }

        public void ScrollIntoView (FlatTreeItem item)
        {
            int index = m_store.IndexOf(item);
            if (index >= 0)
            {
                m_table.ScrollRowToVisible(index);
            }
        }

        /// <summary>Expands every row, top down</summary>
        public void ExpandAll ()
        {
            if (m_store.RootNode == null)
            {
                return;
            }

            foreach (FlatTreeItem item in new List<FlatTreeItem>(EnumerateAll(m_store.RootNode)))
            {
                if (item.IsExpandable)
                {
                    item.IsExpanded = true;
                }
            }
        }

        /// <summary>Collapses every row, bottom up</summary>
        public void CollapseAll ()
        {
            if (m_store.RootNode == null)
            {
                return;
            }

            var all = new List<FlatTreeItem>(EnumerateAll(m_store.RootNode));
            for (int i = all.Count - 1; i >= 0; --i)
            {
                if (all[i].IsExpandable && !all[i].IsFalseRoot)
                {
                    all[i].IsExpanded = false;
                }
            }
        }

        // ===========[ Helper Methods ]========================================
        // Every item under root, root first, collapsed ones included: Children is only the visible
        // children, AllChildren is all of them
        private static IEnumerable<FlatTreeItem> EnumerateAll (FlatTreeItem root)
        {
            yield return root;
            foreach (FlatTreeItem child in root.AllChildren)
            {
                foreach (FlatTreeItem descendant in EnumerateAll(child))
                {
                    yield return descendant;
                }
            }
        }

        private void ReplaceStoreRoot (FlatTreeItem newRoot)
        {
            // The old tree has to let go of its source subscriptions, or they duplicate children later
            this.ApplySelection(new List<FlatTreeItem>());
            m_store.RootNode?.DisposeTree();
            m_store.RootNode = newRoot;
        }

        // A setting the items are built with changed, so they are built again from whichever source is set
        private void RebuildFromCurrentRoot ()
        {
            if (m_root != null)
            {
                this.ReplaceStoreRoot(FlatTreeItem.CreateRoot(m_root, m_treeDepthIndentSize, this.StartItemsExpanded, m_siblingDisplayOrder));
            }
            else
            {
                this.RebuildFromRootItems();
            }
        }

        private void RebuildFromRootItems ()
        {
            if (m_rootItemsSource == null)
            {
                m_builtFromRootItems = null;
                this.ReplaceStoreRoot(null);
                return;
            }

            m_builtFromRootItems = new List<IObservableTreeNode>(m_rootItemsSource).ToArray();
            m_store.IncludeRoot = false;
            this.ReplaceStoreRoot(FlatTreeItem.CreateUberRoot(m_builtFromRootItems, m_treeDepthIndentSize, false, this.SiblingDisplayOrder));
        }

        // Whether the top-level items, by reference and in order, are no longer what the tree was built from
        private static bool RootItemsDiffer (IEnumerable<IObservableTreeNode> current, IObservableTreeNode[] builtFrom)
        {
            if (builtFrom == null)
            {
                return true;
            }

            int index = 0;
            foreach (IObservableTreeNode node in current)
            {
                if ((index >= builtFrom.Length) || !ReferenceEquals(node, builtFrom[index]))
                {
                    return true;
                }

                ++index;
            }

            return index != builtFrom.Length;
        }

        private void OnAttached ()
        {
            this.FollowStore(true);
            this.ListenToRootItems(m_rootItemsSource as INotifyCollectionChanged);

            // While out of a window nothing followed the top-level items, so the tree is rebuilt if they changed then;
            // otherwise the table catches up on any row changes it missed
            if ((m_rootItemsSource != null) && RootItemsDiffer(m_rootItemsSource, m_builtFromRootItems))
            {
                this.RebuildFromRootItems();
            }

            this.PruneSelection();
            m_table.ReloadData();
            this.RestoreTableSelection();
            m_scrollView.ContentView.ScrollToPoint(m_scrollOffsetWhenDetached);
            m_scrollView.ReflectScrolledClipView(m_scrollView.ContentView);
        }

        // No rows out of a window (see the class comment). The rows the table was showing are removed outright, which has
        // it let go of their views at once, rather than left to a reload that may wait for a layout pass that never comes
        // out of a window; any view it still has is torn down here.
        private void OnDetached ()
        {
            this.ClearDropIndicator();
            m_draggedItems = null;
            m_scrollOffsetWhenDetached = m_scrollView.ContentView.Bounds.Location;
            int shownRows = (int)m_table.RowCount;
            this.FollowStore(false);
            this.ListenToRootItems(null);
            if (shownRows > 0)
            {
                m_table.RemoveRows(NSIndexSet.FromNSRange(new NSRange(0, shownRows)), NSTableViewAnimation.None);
            }

            m_table.EnumerateAvailableRowViews(TeardownRowView);
        }

        private void FollowStore (bool follow)
        {
            if (follow == m_isFollowingStore)
            {
                return;
            }

            m_isFollowingStore = follow;
            if (follow)
            {
                ((INotifyCollectionChanged)m_store).CollectionChanged += this.OnStoreCollectionChanged;
            }
            else
            {
                ((INotifyCollectionChanged)m_store).CollectionChanged -= this.OnStoreCollectionChanged;
            }
        }

        // Rows that left the store while it was not followed stop being selected, as a collapse does
        private void PruneSelection ()
        {
            var wanted = new List<FlatTreeItem>();
            foreach (FlatTreeItem item in m_selectedItems)
            {
                if (m_store.Contains(item))
                {
                    wanted.Add(item);
                }
            }

            this.ApplySelection(wanted);
        }

        private void ListenToRootItems (INotifyCollectionChanged rootItems)
        {
            if (m_listenedRootItems != null)
            {
                m_listenedRootItems.CollectionChanged -= this.OnRootItemsCollectionChanged;
            }

            m_listenedRootItems = rootItems;
            if (m_listenedRootItems != null)
            {
                m_listenedRootItems.CollectionChanged -= this.OnRootItemsCollectionChanged;
                m_listenedRootItems.CollectionChanged += this.OnRootItemsCollectionChanged;
            }
        }

        private void ApplySelectionMode ()
        {
            m_table.AllowsMultipleSelection = m_selectionMode == eFlatTreeSelectionMode.Multi || m_selectionMode == eFlatTreeSelectionMode.Extended;
        }

        // Moves the selection to exactly wanted: IsSelected on each item, the list, and one event
        private void ApplySelection (List<FlatTreeItem> wanted)
        {
            var removed = new List<FlatTreeItem>();
            foreach (FlatTreeItem item in m_selectedItems)
            {
                if (!wanted.Contains(item))
                {
                    removed.Add(item);
                }
            }

            var added = new List<FlatTreeItem>();
            foreach (FlatTreeItem item in wanted)
            {
                if (!m_selectedItems.Contains(item))
                {
                    added.Add(item);
                }
            }

            if (added.Count == 0 && removed.Count == 0)
            {
                return;
            }

            foreach (FlatTreeItem item in removed)
            {
                item.IsSelected = false;
            }

            foreach (FlatTreeItem item in added)
            {
                item.IsSelected = true;
            }

            m_selectedItems.Clear();
            m_selectedItems.AddRange(wanted);
            this.SelectionChanged?.Invoke(this, new SelectionChange<FlatTreeItem>(added.ToArray(), removed.ToArray(), m_selectedItems.Count == 0));
        }

        // A selected row that collapses out of view stops being selected, as on the other stacks
        private void ForgetSelected (FlatTreeItem item)
        {
            if (m_selectedItems.Contains(item))
            {
                var wanted = new List<FlatTreeItem>(m_selectedItems);
                wanted.Remove(item);
                this.ApplySelection(wanted);
            }
        }

        // Pushes the selection list into the table's own selection, without it echoing back
        private void RestoreTableSelection ()
        {
            var indexes = new NSMutableIndexSet();
            foreach (FlatTreeItem item in m_selectedItems)
            {
                int index = m_store.IndexOf(item);
                if (index >= 0)
                {
                    indexes.Add((nuint)index);
                }
            }

            m_isSyncingSelection = true;
            try
            {
                m_table.SelectRows(indexes, false);
            }
            finally
            {
                m_isSyncingSelection = false;
            }
        }

        private void OnTableSelectionChanged ()
        {
            if (m_isSyncingSelection)
            {
                return;
            }

            var wanted = new List<FlatTreeItem>();
            foreach (nuint index in m_table.SelectedRows.ToArray())
            {
                if ((int)index < m_store.Count)
                {
                    wanted.Add(m_store[(int)index]);
                }
            }

            this.ApplySelection(wanted);
        }

        private void OnTableDoubleClicked (int row)
        {
            if (row < 0 || row >= m_store.Count)
            {
                return;
            }

            FlatTreeItem item = m_store[row];
            if (this.ShouldToggleExpansionOnDoubleClick && item.IsExpandable)
            {
                item.IsExpanded = !item.IsExpanded;
            }

            this.ItemDoubleClicked?.Invoke(this, item);
        }

        // Left collapses an expanded row or moves to its parent; Right expands a collapsed row or
        // moves to its first child. Same rules as the WinUI3 control.
        private bool HandleArrowKey (bool isLeft)
        {
            if (m_selectedItems.Count == 0)
            {
                return false;
            }

            FlatTreeItem item = m_selectedItems[0];
            if (isLeft)
            {
                if (item.IsExpanded)
                {
                    item.IsExpanded = false;
                }
                else if (item.Parent != null && m_store.Contains(item.Parent))
                {
                    this.SetSelection(new[] { item.Parent });
                    this.ScrollIntoView(item.Parent);
                }

                return true;
            }

            if (item.IsExpandable && !item.IsExpanded)
            {
                item.IsExpanded = true;
            }
            else if (item.IsExpanded && item.Children.Count > 0)
            {
                this.SetSelection(new[] { item.Children[0] });
                this.ScrollIntoView(item.Children[0]);
            }

            return true;
        }

        // Shaped as NSTableViewRowHandler, so Dispose can hand it to EnumerateAvailableRowViews. A row
        // cell tears down once, so a row reported both ways is harmless.
        private static void TeardownRowView (NSTableRowView rowView, nint row)
        {
            foreach (NSView view in rowView.Subviews)
            {
                (view as RowCellView)?.Teardown();
            }
        }

        // Shaped as NSTableViewRowHandler too, for redrawing the rows on show after something they draw from changed
        private static void MarkRowViewForRedisplay (NSTableRowView rowView, nint row)
        {
            rowView.NeedsDisplay = true;
        }

        // The rows sit inside the list's outline when it has one, and reach its edges when it has none
        private void ApplyScrollViewInset (double inset)
        {
            m_scrollViewInsets[0].Constant = (nfloat)inset;
            m_scrollViewInsets[1].Constant = (nfloat)(-inset);
            m_scrollViewInsets[2].Constant = (nfloat)inset;
            m_scrollViewInsets[3].Constant = (nfloat)(-inset);
        }

        // What the drag ghost says: one row's text, by DragGhostTextFactory or its source's ToString, or how many rows go
        private string MakeDragGhostText (FlatTreeItem[] draggedItems)
        {
            if (draggedItems.Length != 1)
            {
                return $"{draggedItems.Length} items";
            }

            FlatTreeItem item = draggedItems[0];
            return this.DragGhostTextFactory?.Invoke(item) ?? item.Source?.ToString() ?? "Item";
        }

        private NSView MakeRowContent (FlatTreeItem item)
        {
            if (this.RowContentFactory != null)
            {
                return this.RowContentFactory(item);
            }

            NSTextField label = NSTextField.CreateLabel(item.Source?.ToString() ?? String.Empty);
            label.LineBreakMode = NSLineBreakMode.TruncatingTail;
            return label;
        }

        // ===========[ Drag and drop ]=========================================
        private bool CanStartDrag (FlatTreeItem item)
        {
            // Roots never drag: a row at depth 0 has nowhere above it to go
            return this.IsDragDropReorderEnabled
                && item.TreeDepth > 0
                && !item.IsFalseRoot
                && (this.CanDragItem == null || this.CanDragItem(item.Source));
        }

        /// <summary>
        /// Where the shared drop math takes row content to start, in points, when it turns the cursor's x into a depth.
        /// The WinUI3 tree is the reference for how depth targeting feels, and it measures the cursor from its list's left
        /// edge, where row content starts 6 in (its item style's content margin), or at 0 in the PropertyGrid (no margin).
        /// This tree measures from its own row content and adds this, so the depth bands sit in the same place against
        /// the rows. 6 by default, as the WinUI3 tree; the PropertyGrid sets 0.
        /// </summary>
        internal double DragDepthRowOffset
        {
            get => m_dragDepthRowOffset;
            set => m_dragDepthRowOffset = value;
        }

        // The gap is decided the way the WinUI3 tree decides it, from the row under the cursor and which half of it the
        // cursor is in, never from the gap the table proposes. The table proposes a drop "on" a row while the cursor is
        // over its middle, and reading that as the gap above the row sent bottom-half drops one gap too high. Past the
        // last row is that row's bottom edge, as on WinUI3.
        private void FindHoveredRow (CGPoint location, out int hoverIndex, out double yFraction)
        {
            hoverIndex = (int)m_table.GetRow(location);
            if (hoverIndex < 0 || hoverIndex >= m_store.Count)
            {
                hoverIndex = m_store.Count - 1;
                yFraction = 1.0;
                return;
            }

            // The table is flipped, so a row's MinY is its top edge
            CGRect rowRect = m_table.RectForRow(hoverIndex);
            yFraction = rowRect.Height > 0 ? Math.Clamp((double)(location.Y - rowRect.GetMinY()) / (double)rowRect.Height, 0.0, 1.0) : 0.5;
        }

        private FlatTreeDropTarget ComputeDropTarget (int hoverIndex, double yFraction, double cursorX)
        {
            if (m_draggedItems == null || m_store.Count == 0)
            {
                return FlatTreeDropTarget.Invalid;
            }

            // The table's x is not where rows start (the table style can inset rows), so the cursor is measured from
            // the row content's own left edge, then placed the way WinUI3 places it (see DragDepthRowOffset)
            double depthX = cursorX - this.GetRowContentX(hoverIndex) + m_dragDepthRowOffset;
            FlatTreeDropTarget target = FlatTreeDragDropManager.ComputeDropTarget(m_store, m_draggedItems, hoverIndex, yFraction, depthX, m_treeDepthIndentSize, this.SiblingDisplayOrder);
            return FlatTreeDragDropManager.ValidateDropTarget(m_draggedItems, target, this.CanDropItem) ? target : FlatTreeDropTarget.Invalid;
        }

        private void CompleteDrop (FlatTreeDropTarget target)
        {
            var sources = new IObservableTreeNode[m_draggedItems.Length];
            for (int i = 0; i < sources.Length; ++i)
            {
                sources[i] = m_draggedItems[i].Source;
            }

            var args = new FlatTreeReorderEventArgs(sources, target.TargetParent, target.InsertIndex);
            this.DragDropReorderRequested?.Invoke(this, args);
            if (!args.Cancel)
            {
                FlatTreeDragDropManager.ExecuteReorder(sources, target);
            }
        }

        // The insertion line starts at the content column of the target depth, and a dashed L runs to it from the new
        // parent's chevron, which is highlighted, the same pieces and rules as the WinUI3 tree's drag overlay. The line
        // goes where the drop lands (ComputeInsertionLine), which over the first row is below it: the drop math never
        // puts anything above the root, so neither does the line.
        private void ShowDropIndicator (int hoverIndex, double yFraction, FlatTreeDropTarget target)
        {
            const double kChevronCenter = kExpanderColumnWidth / 2.0;

            FlatTreeInsertionLine line = FlatTreeDragDropManager.ComputeInsertionLine(m_store, target, hoverIndex, yFraction, this.SiblingDisplayOrder);
            if (line == null)
            {
                this.ClearDropIndicator();
                return;
            }

            int parentIndex = FlatTreeDragDropManager.FindParentStoreIndex(m_store, target);
            double lineY = line.IsBelowRow
                ? (double)m_table.RectForRow(line.Row).GetMaxY()
                : (double)m_table.RectForRow(line.Row).GetMinY() - (double)m_table.IntercellSpacing.Height / 2.0;

            double rowContentX = this.GetRowContentX(hoverIndex);
            double lineX = rowContentX + target.TargetDepth * m_treeDepthIndentSize + kExpanderColumnWidth;
            m_dropIndicator.Frame = m_table.Bounds;
            m_dropIndicator.SetLine(lineX, lineY);

            this.ClearParentHighlight();
            if (parentIndex >= 0)
            {
                // The connector starts at the parent's chevron itself, measured from the drawn glyph, so it meets the
                // tip; the estimate (row middle, chevron column center) is only for a parent row that is not on screen
                m_highlightedParentCell = m_table.GetView(0, parentIndex, false) as RowCellView;
                CGPoint anchor;
                if (m_highlightedParentCell != null)
                {
                    anchor = m_highlightedParentCell.GetChevronAnchorIn(m_table);
                    m_highlightedParentCell.SetDropHighlight(true);
                }
                else
                {
                    anchor = new CGPoint(rowContentX + (target.TargetDepth - 1) * m_treeDepthIndentSize + kChevronCenter, m_table.RectForRow(parentIndex).GetMidY());
                }

                m_dropIndicator.SetConnector((double)anchor.X, (double)anchor.Y);
            }
            else
            {
                m_dropIndicator.ClearConnector();
            }
        }

        // Where a row's content (its indent, expander and view) starts, in table coordinates. The row's own cell view
        // is measured when the table has it on screen; otherwise the table's frame for that cell, which follows the
        // same style insets. Every row starts at the same x, so any row answers for all of them.
        private double GetRowContentX (int row)
        {
            if (m_table.GetView(0, row, false) is NSView cell)
            {
                return (double)m_table.ConvertPointFromView(CGPoint.Empty, cell).X;
            }

            return (double)m_table.GetCellFrame(0, row).X;
        }

        private void ClearDropIndicator ()
        {
            m_shownDropTarget = null;
            m_dropIndicator.Clear();
            this.ClearParentHighlight();
        }

        private void ClearParentHighlight ()
        {
            m_highlightedParentCell?.SetDropHighlight(false);
            m_highlightedParentCell = null;
        }

        // ===========[ Subclasses/structs ]====================================
        // Draws the drop: the insertion line and the dashed connector from the parent's chevron. It sits over the
        // table's rows and never takes a click.
        private sealed class DropIndicatorView : NSView
        {
            private static readonly nfloat[] kConnectorDash = new nfloat[] { 4, 3 };

            private bool m_hasLine;
            private double m_lineX;
            private double m_lineY;
            private bool m_hasConnector;
            private double m_anchorX;
            private double m_anchorY;

            public DropIndicatorView ()
            {
            }

            // For a peer the runtime makes over an existing native indicator
            private DropIndicatorView (NativeHandle handle) : base(handle)
            {
            }

            public override bool IsFlipped => true;

            public override NSView HitTest (CGPoint aPoint) => null;

            public void SetLine (double x, double y)
            {
                m_hasLine = true;
                m_lineX = x;
                m_lineY = y;
                this.NeedsDisplay = true;
            }

            public void SetConnector (double anchorX, double anchorY)
            {
                m_hasConnector = true;
                m_anchorX = anchorX;
                m_anchorY = anchorY;
                this.NeedsDisplay = true;
            }

            public void ClearConnector ()
            {
                m_hasConnector = false;
                this.NeedsDisplay = true;
            }

            public void Clear ()
            {
                m_hasLine = false;
                m_hasConnector = false;
                this.NeedsDisplay = true;
            }

            public override void DrawRect (CGRect dirtyRect)
            {
                // The connector is WinUI3's: 2 thick, and drawn at 0.8 on top of its key's own 0.5
                const double kLineThickness = 3.0;
                const double kConnectorThickness = 2.0;
                const double kConnectorOpacity = 0.8;
                const double kLineEndInset = 4.0;

                if (!m_hasLine)
                {
                    return;
                }

                double lineWidth = Math.Max(0.0, (double)this.Bounds.Width - m_lineX - kLineEndInset);
                ThemedColors.Get(ThemedColorKeys.FlatTree_DragInsertionLine).SetFill();
                NSBezierPath.FillRect(new CGRect(m_lineX, m_lineY - kLineThickness / 2.0, lineWidth, kLineThickness));

                if (m_hasConnector)
                {
                    double topY = Math.Min(m_anchorY, m_lineY);
                    double bottomY = Math.Max(m_anchorY, m_lineY);
                    var connector = new NSBezierPath { LineWidth = (nfloat)kConnectorThickness };
                    connector.SetLineDash(kConnectorDash, 0);
                    connector.LineJoinStyle = NSLineJoinStyle.Round;
                    connector.MoveTo(new CGPoint(m_anchorX, topY));
                    connector.LineTo(new CGPoint(m_anchorX, bottomY));
                    connector.LineTo(new CGPoint(m_lineX, bottomY));
                    ThemedColors.Get(ThemedColorKeys.FlatTree_DragParentConnector).SetStroke();

                    // The context's alpha multiplies what is drawn, as an element's opacity does on WinUI3
                    NSGraphicsContext context = NSGraphicsContext.CurrentContext;
                    context.SaveGraphicsState();
                    context.CGContext.SetAlpha((nfloat)kConnectorOpacity);
                    connector.Stroke();
                    context.RestoreGraphicsState();
                }
            }
        }

        // The table, for the arrow keys and the double click. The tree retains it natively, so it holds the tree weakly.
        private sealed class TreeTableView : NSTableView
        {
            private const ushort kLeftArrowKeyCode = 123;
            private const ushort kRightArrowKeyCode = 124;

            private readonly WeakReference<FlatTreeListView> m_owner;

            public TreeTableView (FlatTreeListView owner)
            {
                m_owner = new WeakReference<FlatTreeListView>(owner);
            }

            // For a peer the runtime makes over an existing native table, which has no owner to tell
            private TreeTableView (NativeHandle handle) : base(handle)
            {
                m_owner = new WeakReference<FlatTreeListView>(null);
            }

            public override void KeyDown (NSEvent theEvent)
            {
                if (m_owner.TryGetTarget(out FlatTreeListView owner)
                    && (((theEvent.KeyCode == kLeftArrowKeyCode) && owner.HandleArrowKey(true))
                        || ((theEvent.KeyCode == kRightArrowKeyCode) && owner.HandleArrowKey(false))))
                {
                    return;
                }

                base.KeyDown(theEvent);
            }

            public override void DraggingExited (INSDraggingInfo sender)
            {
                base.DraggingExited(sender);
                if (m_owner.TryGetTarget(out FlatTreeListView owner))
                {
                    owner.ClearDropIndicator();
                }
            }

            public override void MouseDown (NSEvent theEvent)
            {
                // The row is hit tested from the event, before the table's own tracking runs. ClickedRow only holds the
                // clicked row while the table is sending its action, and it was already -1 by the time base.MouseDown
                // returned, so a double click never found its row and never toggled it.
                int row = (int)this.GetRow(this.ConvertPointFromView(theEvent.LocationInWindow, null));
                base.MouseDown(theEvent);
                if ((theEvent.ClickCount == 2) && m_owner.TryGetTarget(out FlatTreeListView owner))
                {
                    owner.OnTableDoubleClicked(row);
                }
            }
        }

        // The table's delegate: row views, their teardown, and selection. The table's peer keeps its delegate alive, so it
        // holds the tree weakly.
        private sealed class TableDelegate : NSTableViewDelegate
        {
            private readonly WeakReference<FlatTreeListView> m_owner;

            public TableDelegate (FlatTreeListView owner)
            {
                m_owner = new WeakReference<FlatTreeListView>(owner);
            }

            // For a peer the runtime makes over an existing native delegate, which has no owner to tell
            private TableDelegate (NativeHandle handle) : base(handle)
            {
                m_owner = new WeakReference<FlatTreeListView>(null);
            }

            public override NSView GetViewForItem (NSTableView tableView, NSTableColumn tableColumn, nint row)
            {
                if (!m_owner.TryGetTarget(out FlatTreeListView owner) || ((int)row >= owner.m_store.Count))
                {
                    return new NSView();
                }

                FlatTreeItem item = owner.m_store[(int)row];
                return new RowCellView(item, owner.MakeRowContent(item));
            }

            public override NSTableRowView CoreGetRowView (NSTableView tableView, nint row) => new RowBackgroundView(m_owner);

            // The table is done with this row's views: tear the content down so its bindings go too
            public override void DidRemoveRowView (NSTableView tableView, NSTableRowView rowView, nint row) => TeardownRowView(rowView, row);

            public override NSIndexSet GetSelectionIndexes (NSTableView tableView, NSIndexSet proposedSelectionIndexes)
            {
                if (!m_owner.TryGetTarget(out FlatTreeListView owner) || (owner.m_selectionMode == eFlatTreeSelectionMode.None))
                {
                    return new NSIndexSet();
                }

                // Multi toggles the clicked row. NSTableView only knows the Extended way (a click selects
                // one row, Command toggles, Shift extends), so a plain click in Multi, which the table
                // proposes as just that row, is turned into a toggle against what is selected now.
                // Keyboard moves and Command or Shift clicks are left as the table proposes them.
                NSIndexSet proposed = proposedSelectionIndexes;
                NSEvent currentEvent = NSApplication.SharedApplication.CurrentEvent;
                if ((owner.m_selectionMode == eFlatTreeSelectionMode.Multi)
                    && (currentEvent?.Type == NSEventType.LeftMouseDown)
                    && ((currentEvent.ModifierFlags & (NSEventModifierMask.CommandKeyMask | NSEventModifierMask.ShiftKeyMask)) == 0)
                    && (proposedSelectionIndexes.Count == 1))
                {
                    var toggled = new NSMutableIndexSet(tableView.SelectedRows);
                    nuint clicked = proposedSelectionIndexes.FirstIndex;
                    if (toggled.Contains(clicked))
                    {
                        toggled.Remove(clicked);
                    }
                    else
                    {
                        toggled.Add(clicked);
                    }

                    proposed = toggled;
                }

                // Rows that cannot be selected are left out of the user's selection
                var allowed = new NSMutableIndexSet();
                foreach (nuint index in proposed.ToArray())
                {
                    if ((int)index < owner.m_store.Count && owner.m_store[(int)index].IsSelectable)
                    {
                        allowed.Add(index);
                    }
                }

                return allowed;
            }

            public override void SelectionDidChange (NSNotification notification)
            {
                if (m_owner.TryGetTarget(out FlatTreeListView owner))
                {
                    owner.OnTableSelectionChanged();
                }
            }
        }

        // The table's data source: the row count, and dragging rows to reorder them. The table's peer keeps its data source
        // alive, so it holds the tree weakly.
        private sealed class TableDataSource : NSTableViewDataSource
        {
            private readonly WeakReference<FlatTreeListView> m_owner;

            public TableDataSource (FlatTreeListView owner)
            {
                m_owner = new WeakReference<FlatTreeListView>(owner);
            }

            // For a peer the runtime makes over an existing native data source, which has no owner to tell
            private TableDataSource (NativeHandle handle) : base(handle)
            {
                m_owner = new WeakReference<FlatTreeListView>(null);
            }

            // No rows while the tree isn't following its store, which is while it is out of a window
            public override nint GetRowCount (NSTableView tableView)
            {
                return (m_owner.TryGetTarget(out FlatTreeListView owner) && owner.m_isFollowingStore) ? owner.m_store.Count : 0;
            }

            public override INSPasteboardWriting GetPasteboardWriterForRow (NSTableView tableView, nint row)
            {
                if (!m_owner.TryGetTarget(out FlatTreeListView owner))
                {
                    return null;
                }

                FlatTreeItem item = owner.m_store[(int)row];
                if (!owner.CanStartDrag(item))
                {
                    return null;
                }

                // A selected row drags the whole selection, another row drags alone; the table asks once
                // per dragged row, so the list is worked out on the first
                if (row == owner.m_table.SelectedRow || !item.IsSelected)
                {
                    var dragged = new List<FlatTreeItem>();
                    if (item.IsSelected)
                    {
                        foreach (FlatTreeItem selected in owner.m_selectedItems)
                        {
                            if (owner.CanStartDrag(selected))
                            {
                                dragged.Add(selected);
                            }
                        }
                    }
                    else
                    {
                        dragged.Add(item);
                    }

                    owner.m_draggedItems = dragged.ToArray();
                }

                var pasteboardItem = new NSPasteboardItem();
                pasteboardItem.SetStringForType(row.ToString(), kDragType);
                return pasteboardItem;
            }

            // The drag shows one ghost label beside the pointer in place of the table's own images of the dragged rows:
            // WPF's themed ghost (WinUI3's is in literal colors), saying the dragged row's text or how many rows go. The
            // table gives the session one dragging item per row, made from the writers above, so enumerating them as
            // NSPasteboardItem reaches every one; the first takes the ghost and the rest show nothing.
            // https://developer.apple.com/documentation/appkit/nstableviewdatasource/tableview(_:draggingsession:willbeginat:forrowindexes:)
            // https://developer.apple.com/documentation/appkit/nsdraggingsession/enumeratedraggingitems(options:for:classes:searchoptions:using:)
            public override void DraggingSessionWillBegin (NSTableView tableView, NSDraggingSession draggingSession, CGPoint willBeginAtScreenPoint, NSIndexSet rowIndexes)
            {
                const double kPointerGap = 12.0;

                // The classes are Objective-C class objects, which the managed side only reaches as an NSObject through
                //  the runtime. Without one the table's own row images are left showing.
                if (!m_owner.TryGetTarget(out FlatTreeListView owner))
                {
                    return;
                }

                FlatTreeItem[] dragged = owner.m_draggedItems;
                NSWindow window = tableView.Window;
                NSObject pasteboardItemClass = ObjCRuntime.Runtime.GetNSObject(ObjCRuntime.Class.GetHandle(typeof(NSPasteboardItem)));
                if ((dragged == null) || (dragged.Length == 0) || (window == null) || (pasteboardItemClass == null))
                {
                    return;
                }

                NSImage ghost = DragGhostPainter.CreateImage(owner.MakeDragGhostText(dragged), tableView.EffectiveAppearance);
                CGPoint pointer = tableView.ConvertPointFromView(window.ConvertPointFromScreen(willBeginAtScreenPoint), null);
                var ghostFrame = new CGRect(pointer.X + (nfloat)kPointerGap, pointer.Y - (ghost.Size.Height / 2), ghost.Size.Width, ghost.Size.Height);

                // The rows after the first show nothing. The binding's SetDraggingFrame throws on a null image (it takes
                //  the native handle without a null check), so they take an empty one, which draws nothing.
                var blank = new NSImage(new CGSize(1, 1));

                draggingSession.EnumerateDraggingItems(
                    NSDraggingItemEnumerationOptions.ClearNonenumeratedImages,
                    tableView,
                    NSArray.FromNSObjects(pasteboardItemClass),
                    new NSDictionary(),
                    _ShowGhostOnFirst
                );

                void _ShowGhostOnFirst (NSDraggingItem draggingItem, nint index, ref bool stop)
                {
                    draggingItem.SetDraggingFrame(ghostFrame, (index == 0) ? ghost : blank);
                }
            }

            // The table's proposed row is ignored (see FindHoveredRow); it is only told the gap that was decided, as an
            // "above" drop, so it keeps accepting the drag
            public override NSDragOperation ValidateDrop (NSTableView tableView, INSDraggingInfo info, nint row, NSTableViewDropOperation dropOperation)
            {
                if (!m_owner.TryGetTarget(out FlatTreeListView owner) || (owner.m_store.Count == 0))
                {
                    return NSDragOperation.None;
                }

                CGPoint location = tableView.ConvertPointFromView(info.DraggingLocation, null);
                owner.FindHoveredRow(location, out int hoverIndex, out double yFraction);
                tableView.SetDropRowDropOperation(FlatTreeDragDropManager.ComputeGapRow(hoverIndex, yFraction), NSTableViewDropOperation.Above);
                FlatTreeDropTarget target = owner.ComputeDropTarget(hoverIndex, yFraction, (double)location.X);
                owner.ShowDropIndicator(hoverIndex, yFraction, target);
                owner.m_shownDropTarget = target;
                return target.IsValid ? NSDragOperation.Move : NSDragOperation.None;
            }

            // The drop lands exactly where the indicator last said it would. It is never worked out again here: on the
            // Mac a drop to the end of a tree showed "last child of the root" and landed inside the last open subtree,
            // so what was shown and what was done had come apart, and the shown target is the one the user aimed with.
            public override bool AcceptDrop (NSTableView tableView, INSDraggingInfo info, nint row, NSTableViewDropOperation dropOperation)
            {
                if (!m_owner.TryGetTarget(out FlatTreeListView owner))
                {
                    return false;
                }

                FlatTreeDropTarget target = owner.m_shownDropTarget;
                owner.ClearDropIndicator();
                if (target == null || !target.IsValid || owner.m_draggedItems == null)
                {
                    return false;
                }

                owner.CompleteDrop(target);
                owner.m_draggedItems = null;
                return true;
            }

            public override void DraggingSessionEnded (NSTableView tableView, NSDraggingSession draggingSession, CGPoint endedAtScreenPoint, NSDragOperation operation)
            {
                if (m_owner.TryGetTarget(out FlatTreeListView owner))
                {
                    owner.ClearDropIndicator();
                    owner.m_draggedItems = null;
                }
            }
        }

        // One row's content: indent, expander, then the row's own content view
        private sealed class RowCellView : NSView
        {
            private readonly FlatTreeItem m_item;
            private readonly NSView m_content;
            private readonly ExpanderGlyph m_expander;
            private bool m_isTornDown;

            public RowCellView (FlatTreeItem item, NSView content)
            {
                m_item = item;
                m_content = content;

                m_expander = new ExpanderGlyph();
                m_expander.Activated += this.OnExpanderActivated;

                var row = new FillStack(NSUserInterfaceLayoutOrientation.Horizontal)
                {
                    Padding = new NSEdgeInsets(0, (nfloat)item.IndentWidth, 0, 2),
                };
                m_expander.TranslatesAutoresizingMaskIntoConstraints = false;
                m_expander.WidthAnchor.ConstraintEqualTo((nfloat)kExpanderColumnWidth).Active = true;
                m_expander.HeightAnchor.ConstraintEqualTo((nfloat)kExpanderColumnWidth).Active = true;
                row.Add(m_expander, alignment: eFillStackCrossAlignment.Center);
                row.Add(content, grows: true, alignment: eFillStackCrossAlignment.Center);
                row.TranslatesAutoresizingMaskIntoConstraints = false;
                this.AddSubview(row);
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    row.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                    row.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                    row.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                    row.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
                });

                m_item.PropertyChanged += this.OnItemPropertyChanged;
                this.RefreshExpander();
            }

            // For a peer the runtime makes over an existing native cell, which has no item to follow
            private RowCellView (NativeHandle handle) : base(handle)
            {
                m_isTornDown = true;
            }

            public override bool IsFlipped => true;

            public void Teardown ()
            {
                if (m_isTornDown)
                {
                    return;
                }

                m_isTornDown = true;
                m_item.PropertyChanged -= this.OnItemPropertyChanged;
                m_expander.Activated -= this.OnExpanderActivated;
                (m_content as IFlatTreeRowContent)?.Teardown();
            }

            /// <summary>The chevron's connector anchor (see <see cref="ExpanderGlyph.ConnectorAnchor"/>) in <paramref name="view"/>'s coordinates</summary>
            public CGPoint GetChevronAnchorIn (NSView view) => view.ConvertPointFromView(m_expander.ConnectorAnchor, m_expander);

            /// <summary>Draws this row's chevron in the drag highlight while a drop would land under this row</summary>
            public void SetDropHighlight (bool isHighlighted) => m_expander.IsHighlighted = isHighlighted;

            private void OnExpanderActivated (object sender, EventArgs e)
            {
                m_item.IsExpanded = !m_item.IsExpanded;
            }

            private void OnItemPropertyChanged (object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(FlatTreeItem.IsExpanded) || e.PropertyName == nameof(FlatTreeItem.IsExpandable))
                {
                    this.RefreshExpander();
                }
            }

            private void RefreshExpander ()
            {
                m_expander.Hidden = !m_item.IsExpandable;
                m_expander.IsExpanded = m_item.IsExpanded;
            }
        }

        // The expand/collapse chevron: a thin stroked glyph in the symbol color, the role the WinUI3 tree's FontIcon
        // chevron plays. A GeometryButton fills and traces in button face colors, so a chevron drawn with one is a
        // solid triangle that all but vanishes on the dark palette. Under the pointer, and while pressed, a rounded box
        // behind the chevron fills and the chevron lightens, as the flagships' expanders do.
        private sealed class ExpanderGlyph : NSView
        {
            private const double kGlyphHalfLength = 3.5;
            private const double kLineWidth = 1.5;

            private bool m_isExpanded;
            private bool m_isHighlighted;
            private bool m_isPointerOver;
            private bool m_isPressed;

            public ExpanderGlyph ()
            {
                // One area for the chevron's life, kept in step with its visible rect by AppKit, as ManagedButton's is
                this.AddTrackingArea(new NSTrackingArea(
                    CGRect.Empty,
                    NSTrackingAreaOptions.MouseEnteredAndExited | NSTrackingAreaOptions.ActiveInActiveApp | NSTrackingAreaOptions.InVisibleRect,
                    this,
                    null
                ));
            }

            // For a peer the runtime makes over an existing native chevron
            private ExpanderGlyph (NativeHandle handle) : base(handle)
            {
            }

            public event EventHandler<EventArgs> Activated;

            public bool IsHighlighted
            {
                get => m_isHighlighted;
                set
                {
                    if (m_isHighlighted != value)
                    {
                        m_isHighlighted = value;
                        this.NeedsDisplay = true;
                    }
                }
            }

            public bool IsExpanded
            {
                get => m_isExpanded;
                set
                {
                    if (m_isExpanded != value)
                    {
                        m_isExpanded = value;
                        this.NeedsDisplay = true;
                    }
                }
            }

            public override bool IsFlipped => true;

            /// <summary>
            /// Where a drop connector meets this chevron, in its own coordinates: the tip of the down pointing (expanded)
            /// chevron, or the bottom of the right pointing one
            /// </summary>
            public CGPoint ConnectorAnchor
            {
                get
                {
                    double centerX = (double)this.Bounds.Width / 2.0;
                    double centerY = (double)this.Bounds.Height / 2.0;
                    double belowCenter = m_isExpanded ? kGlyphHalfLength / 2.0 : kGlyphHalfLength;
                    return new CGPoint(centerX, centerY + belowCenter + kLineWidth / 2.0);
                }
            }

            public override bool AcceptsFirstMouse (NSEvent theEvent) => true;

            // A plain view is not an accessibility element, so without these VoiceOver could not open or close a row
            public override bool AccessibilityElement => true;
            public override string AccessibilityRole => NSAccessibilityRoles.DisclosureTriangleRole;
            public override string AccessibilityLabel => m_isExpanded ? "Collapse" : "Expand";

            public override bool AccessibilityPerformPress ()
            {
                this.Activated?.Invoke(this, EventArgs.Empty);
                return true;
            }

            // The row opens or closes on the press, as it always has; the pressed fill shows until the release
            public override void MouseDown (NSEvent theEvent)
            {
                this.SetPressed(true);
                this.Activated?.Invoke(this, EventArgs.Empty);
            }

            public override void MouseUp (NSEvent theEvent)
            {
                this.SetPressed(false);
            }

            public override void MouseEntered (NSEvent theEvent)
            {
                m_isPointerOver = true;
                this.NeedsDisplay = true;
            }

            public override void MouseExited (NSEvent theEvent)
            {
                m_isPointerOver = false;
                this.NeedsDisplay = true;
            }

            public override void ViewDidChangeEffectiveAppearance ()
            {
                base.ViewDidChangeEffectiveAppearance();
                this.NeedsDisplay = true;
            }

            public override void DrawRect (CGRect dirtyRect)
            {
                // WinUI3's toggle is its 9 point glyph padded 3 all around, with its corners rounded 2
                const double kBoxSize = 15.0;
                const double kBoxCornerRadius = 2.0;

                double centerX = (double)this.Bounds.Width / 2.0;
                double centerY = (double)this.Bounds.Height / 2.0;
                double half = kGlyphHalfLength;

                // 1. The box behind the chevron, only under the pointer or pressed
                string boxKey = m_isPressed ? ThemedColorKeys.FlatTree_ExpanderPressedBackground
                    : m_isPointerOver ? ThemedColorKeys.FlatTree_ExpanderPointerOverBackground
                    : null;
                if (boxKey != null)
                {
                    var box = new CGRect(centerX - (kBoxSize / 2.0), centerY - (kBoxSize / 2.0), kBoxSize, kBoxSize);
                    ThemedColors.Get(boxKey).SetFill();
                    NSBezierPath.FromRoundedRect(box, (nfloat)kBoxCornerRadius, (nfloat)kBoxCornerRadius).Fill();
                }

                // 2. The chevron
                var path = new NSBezierPath
                {
                    LineWidth = (nfloat)kLineWidth,
                    LineCapStyle = NSLineCapStyle.Round,
                    LineJoinStyle = NSLineJoinStyle.Round,
                };

                // Collapsed points right, expanded points down (the view is flipped, so +y is down)
                if (m_isExpanded)
                {
                    path.MoveTo(new CGPoint(centerX - half, centerY - half / 2.0));
                    path.LineTo(new CGPoint(centerX, centerY + half / 2.0));
                    path.LineTo(new CGPoint(centerX + half, centerY - half / 2.0));
                }
                else
                {
                    path.MoveTo(new CGPoint(centerX - half / 2.0, centerY - half));
                    path.LineTo(new CGPoint(centerX + half / 2.0, centerY));
                    path.LineTo(new CGPoint(centerX - half / 2.0, centerY + half));
                }

                string strokeKey = m_isHighlighted ? ThemedColorKeys.FlatTree_DragTargetHighlight
                    : (m_isPressed || m_isPointerOver) ? ThemedColorKeys.FlatTree_ExpanderGlyphPointerOver
                    : ThemedColorKeys.FlatTree_ExpanderGlyph;
                ThemedColors.Get(strokeKey).SetStroke();
                path.Stroke();
            }

            private void SetPressed (bool isPressed)
            {
                if (m_isPressed != isPressed)
                {
                    m_isPressed = isPressed;
                    this.NeedsDisplay = true;
                }
            }
        }

        // The image the drag ghost shows: the text on a rounded fill with an outline, the whole label at 0.65, as WPF's
        // ghost is drawn. An NSImage made with a drawing handler is drawn whenever AppKit wants it, so the painter keeps
        // only the text and the appearance to draw in, and resolves the ghost's dynamic colors under that appearance.
        private sealed class DragGhostPainter
        {
            private const double kFontSize = 11.0;
            private const double kPaddingX = 8.0;
            private const double kPaddingY = 4.0;
            private const double kBorderThickness = 1.0;
            private const double kCornerRadius = 4.0;
            private const double kOpacity = 0.65;

            private readonly string m_text;
            private readonly NSAppearance m_appearance;
            private readonly CGSize m_size;
            private CGRect m_drawRect;

            private DragGhostPainter (string text, NSAppearance appearance)
            {
                m_text = text;
                m_appearance = appearance;
                CGSize textSize = this.MakeText().Size;
                m_size = new CGSize(
                    (nfloat)(Math.Ceiling((double)textSize.Width) + (2.0 * (kPaddingX + kBorderThickness))),
                    (nfloat)(Math.Ceiling((double)textSize.Height) + (2.0 * (kPaddingY + kBorderThickness)))
                );
            }

            public static NSImage CreateImage (string text, NSAppearance appearance)
            {
                var painter = new DragGhostPainter(text, appearance);
                return NSImage.ImageWithSize(painter.m_size, true, painter.Draw);
            }

            private bool Draw (CGRect drawRect)
            {
                m_drawRect = drawRect;
                if (m_appearance != null)
                {
                    m_appearance.PerformAsCurrentDrawingAppearance(this.DrawUnderAppearance);
                }
                else
                {
                    this.DrawUnderAppearance();
                }

                return true;
            }

            // A transparency layer fades the label as one, the way an element's opacity does, rather than the text and
            // the fill each on their own
            private void DrawUnderAppearance ()
            {
                CoreGraphics.CGContext context = NSGraphicsContext.CurrentContext.CGContext;
                context.SetAlpha((nfloat)kOpacity);
                context.BeginTransparencyLayer();

                nfloat halfBorder = (nfloat)(kBorderThickness / 2.0);
                NSBezierPath surface = NSBezierPath.FromRoundedRect(m_drawRect.Inset(halfBorder, halfBorder), (nfloat)kCornerRadius, (nfloat)kCornerRadius);
                ThemedColors.Get(ThemedColorKeys.FlatTree_DragGhostBackground).SetFill();
                surface.Fill();
                surface.LineWidth = (nfloat)kBorderThickness;
                ThemedColors.Get(ThemedColorKeys.FlatTree_DragGhostBorder).SetStroke();
                surface.Stroke();

                this.MakeText().DrawAtPoint(new CGPoint(m_drawRect.X + (nfloat)(kPaddingX + kBorderThickness), m_drawRect.Y + (nfloat)(kPaddingY + kBorderThickness)));
                context.EndTransparencyLayer();
            }

            private NSAttributedString MakeText ()
            {
                var attributes = new NSStringAttributes
                {
                    Font = NSFont.SystemFontOfSize((nfloat)kFontSize),
                    ForegroundColor = ThemedColors.Get(ThemedColorKeys.FlatTree_DragGhostForeground),
                };

                return new NSAttributedString(m_text ?? String.Empty, attributes);
            }
        }

        // Draws the row's hover and selection, as the flagships' rows draw them: a selected row gets a full-row fill, a
        // strip down its leading edge and an outline, in the focused colors or the unfocused ones. The row under the
        // pointer gets the hover fill and outline, and a selected one under the pointer keeps its fill with its strip and
        // outline in the hover color. With the list's DrawsRowSelection off, selection draws nothing here and the hover
        // draws the same on every row.
        //
        // The table retains its row views natively, so a row view holds the tree weakly; a strong hold would keep a tree
        // dropped without Dispose alive through its own rows (see the class comment).
        private sealed class RowBackgroundView : NSTableRowView
        {
            private const double kSelectionStripWidth = 4.0;

            private readonly WeakReference<FlatTreeListView> m_owner;
            private NSTrackingArea m_trackingArea;
            private bool m_isPointerOver;

            public RowBackgroundView (WeakReference<FlatTreeListView> owner)
            {
                m_owner = owner;
            }

            // For a peer the runtime makes over an existing native row, which has no owner to tell
            private RowBackgroundView (NativeHandle handle) : base(handle)
            {
                m_owner = new WeakReference<FlatTreeListView>(null);
            }

            // The list's DrawsRowSelection, or the default (on) once the tree is gone
            private bool DrawsRowSelection => !m_owner.TryGetTarget(out FlatTreeListView owner) || owner.DrawsRowSelection;

            // A selected row in a focused table reports Emphasized here by default, and AppKit hands that to
            // every cell, so stock labels and buttons switch to their "on a strong selection" colors (white
            // text, white glyphs). That suits AppKit's own blue selection, but this row draws the themed
            // selection fill, which is pale in light mode, and the row's content went white on it. The row
            // keeps the normal style so content keeps its own colors on the themed fill.
            public override NSBackgroundStyle InteriorBackgroundStyle => NSBackgroundStyle.Normal;

            public override void DrawSelection (CGRect dirtyRect)
            {
                if (!this.DrawsRowSelection)
                {
                    return;
                }

                CGRect bounds = this.Bounds;
                ThemedColors.Get(ThemedColorKeys.FlatTree_SelectionBackground).SetFill();
                NSBezierPath.FillRect(bounds);

                // Selected and under the pointer, the strip and outline take the hover color and the fill stays
                string accentKey = m_isPointerOver ? ThemedColorKeys.FlatTree_SelectionPointerOver
                    : this.Emphasized ? ThemedColorKeys.FlatTree_Selection
                    : ThemedColorKeys.FlatTree_SelectionUnfocused;
                ThemedColors.Get(accentKey).SetFill();
                NSBezierPath.FillRect(new CGRect(bounds.X, bounds.Y, (nfloat)kSelectionStripWidth, bounds.Height));
                ThemedColors.Get(accentKey).SetStroke();
                NSBezierPath.StrokeRect(bounds.Inset((nfloat)0.5, (nfloat)0.5));
            }

            public override void DrawBackground (CGRect dirtyRect)
            {
                base.DrawBackground(dirtyRect);
                if (m_isPointerOver && !(this.Selected && this.DrawsRowSelection))
                {
                    ThemedColors.Get(ThemedColorKeys.FlatTree_HoverBackground).SetFill();
                    NSBezierPath.FillRect(this.Bounds);
                    ThemedColors.Get(ThemedColorKeys.FlatTree_HoverBorder).SetStroke();
                    NSBezierPath.StrokeRect(this.Bounds.Inset((nfloat)0.5, (nfloat)0.5));
                }
            }

            public override void UpdateTrackingAreas ()
            {
                base.UpdateTrackingAreas();
                if (m_trackingArea != null)
                {
                    this.RemoveTrackingArea(m_trackingArea);
                }

                m_trackingArea = new NSTrackingArea(
                    this.Bounds,
                    NSTrackingAreaOptions.MouseEnteredAndExited | NSTrackingAreaOptions.ActiveInActiveApp | NSTrackingAreaOptions.InVisibleRect,
                    this,
                    null
                );
                this.AddTrackingArea(m_trackingArea);
            }

            public override void MouseEntered (NSEvent theEvent)
            {
                m_isPointerOver = true;
                this.NeedsDisplay = true;
            }

            public override void MouseExited (NSEvent theEvent)
            {
                m_isPointerOver = false;
                this.NeedsDisplay = true;
            }
        }
    }

    /// <summary>A row content view that has something to let go of when its row leaves the table (bindings, subscriptions)</summary>
    public interface IFlatTreeRowContent
    {
        void Teardown ();
    }
}
