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
    public class FlatTreeListView : NSView
    {
        private const double kExpanderColumnWidth = 18.0;
        private const double kDefaultRowHeight = 24.0;
        private const string kDragType = "dev.ajut.flattree.row";

        // Where the WinUI3 tree's row content starts inside its list: the ListViewItem content margin in
        // AJut_FlatTree_ListViewItemStyle (see DragDepthRowOffset)
        private const double kWinUI3TreeRowContentInset = 6.0;

        private readonly ObservableFlatTreeStore<FlatTreeItem> m_store = new ObservableFlatTreeStore<FlatTreeItem>();
        private readonly TreeTableView m_table;
        private readonly NSScrollView m_scrollView;
        private readonly TableDataSource m_dataSource;
        private readonly TableDelegate m_delegate;
        private readonly List<FlatTreeItem> m_selectedItems = new List<FlatTreeItem>();
        private IObservableTreeNode m_root;
        private IEnumerable<IObservableTreeNode> m_rootItemsSource;
        private INotifyCollectionChanged m_listenedRootItems;
        private eFlatTreeSelectionMode m_selectionMode = eFlatTreeSelectionMode.Single;
        private eSiblingOrder m_siblingDisplayOrder = eSiblingOrder.Forward;
        private double m_treeDepthIndentSize = 16.0;
        private double m_dragDepthRowOffset = kWinUI3TreeRowContentInset;
        private bool m_isSyncingSelection;
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

            m_scrollView = new NSScrollView
            {
                HasVerticalScroller = true,
                AutohidesScrollers = true,
                DrawsBackground = false,
                DocumentView = m_table,
                TranslatesAutoresizingMaskIntoConstraints = false,
            };
            this.AddSubview(m_scrollView);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_scrollView.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                m_scrollView.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                m_scrollView.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                m_scrollView.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
            });

            ((INotifyCollectionChanged)m_store).CollectionChanged += this.OnStoreCollectionChanged;
            this.ApplySelectionMode();
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                ((INotifyCollectionChanged)m_store).CollectionChanged -= this.OnStoreCollectionChanged;
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
                this.ListenToRootItems(null);
                this.ReplaceStoreRoot(value != null ? FlatTreeItem.CreateRoot(value, m_treeDepthIndentSize, this.StartItemsExpanded, this.SiblingDisplayOrder) : null);
            }
        }

        /// <summary>
        /// Several top-level nodes, each a row at depth 0 under a hidden, always-expanded root. An observable
        /// collection is followed: any change rebuilds the rows.
        /// </summary>
        public IEnumerable<IObservableTreeNode> RootItemsSource
        {
            get => m_rootItemsSource;
            set
            {
                m_root = null;
                m_rootItemsSource = value;
                this.ListenToRootItems(value as INotifyCollectionChanged);
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
        /// Builds the content of a row, right of its indent and expander. It is called each time the table needs a
        /// row's view; a returned view implementing <see cref="IFlatTreeRowContent"/> is torn down when the table
        /// drops it. By default a row shows its source's ToString.
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
                this.ReplaceStoreRoot(null);
                return;
            }

            m_store.IncludeRoot = false;
            this.ReplaceStoreRoot(FlatTreeItem.CreateUberRoot(m_rootItemsSource, m_treeDepthIndentSize, false, this.SiblingDisplayOrder));
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

        // The gap the shared drop math uses for this hover: above the row in its top half (but never above the first
        // row), below it otherwise. Returned as the row the gap is above, the table's "above" drop row.
        private static int GapRowFor (int hoverIndex, double yFraction)
        {
            return (yFraction < 0.5 && hoverIndex > 0) ? hoverIndex : hoverIndex + 1;
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
        // sits in the gap under the cursor (GapRowFor), which over the first row is below it: the drop math never puts
        // anything above the root, so neither does the line.
        private void ShowDropIndicator (int hoverIndex, double yFraction, FlatTreeDropTarget target)
        {
            const double kChevronCenter = kExpanderColumnWidth / 2.0;

            if (!target.IsValid || m_store.Count == 0)
            {
                this.ClearDropIndicator();
                return;
            }

            // The line goes where the drop lands, which is not always the gap under the cursor: dragged left to a
            // shallower depth between two deep rows (E and F under D), the drop lands after the whole open subtree
            // (after G), so the line is drawn under the last row of the sibling it will follow
            int parentIndex = FlatTreeDragDropManager.FindParentStoreIndex(m_store, target);
            int rowBefore = this.SiblingDisplayOrder == eSiblingOrder.Forward ? this.FindRowBeforeInsertion(target, parentIndex) : -1;

            int gapRow = GapRowFor(hoverIndex, yFraction);
            double lineY;
            if (rowBefore >= 0)
            {
                lineY = (double)m_table.RectForRow(rowBefore).GetMaxY();
            }
            else if (gapRow >= m_store.Count)
            {
                lineY = (double)m_table.RectForRow(m_store.Count - 1).GetMaxY();
            }
            else
            {
                lineY = (double)m_table.RectForRow(gapRow).GetMinY() - (double)m_table.IntercellSpacing.Height / 2.0;
            }

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

        // The row a drop will follow: the last visible row of the sibling before the insertion point (that sibling and
        // its open subtree), or the parent's own row when the drop goes in first. -1 when that row is not in the list.
        private int FindRowBeforeInsertion (FlatTreeDropTarget target, int parentIndex)
        {
            if (target.InsertIndex <= 0)
            {
                return parentIndex;
            }

            IReadOnlyList<IObservableTreeNode> siblings = target.TargetParent.Children;
            if (target.InsertIndex > siblings.Count)
            {
                return -1;
            }

            IObservableTreeNode previous = siblings[target.InsertIndex - 1];
            int row = -1;
            for (int i = 0; i < m_store.Count; ++i)
            {
                if (m_store[i].Source == previous)
                {
                    row = i;
                    break;
                }
            }

            if (row < 0)
            {
                return -1;
            }

            int depth = m_store[row].TreeDepth;
            while (row + 1 < m_store.Count && m_store[row + 1].TreeDepth > depth)
            {
                ++row;
            }

            return row;
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
                const double kLineThickness = 3.0;
                const double kConnectorThickness = 1.0;
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
                    connector.MoveTo(new CGPoint(m_anchorX, topY));
                    connector.LineTo(new CGPoint(m_anchorX, bottomY));
                    connector.LineTo(new CGPoint(m_lineX, bottomY));
                    ThemedColors.Get(ThemedColorKeys.FlatTree_DragParentConnector).SetStroke();
                    connector.Stroke();
                }
            }
        }

        // The table, for the arrow keys and the double click
        private sealed class TreeTableView : NSTableView
        {
            private const ushort kLeftArrowKeyCode = 123;
            private const ushort kRightArrowKeyCode = 124;

            private readonly FlatTreeListView m_owner;

            public TreeTableView (FlatTreeListView owner)
            {
                m_owner = owner;
            }

            public override void KeyDown (NSEvent theEvent)
            {
                if ((theEvent.KeyCode == kLeftArrowKeyCode && m_owner.HandleArrowKey(true))
                    || (theEvent.KeyCode == kRightArrowKeyCode && m_owner.HandleArrowKey(false)))
                {
                    return;
                }

                base.KeyDown(theEvent);
            }

            public override void DraggingExited (INSDraggingInfo sender)
            {
                base.DraggingExited(sender);
                m_owner.ClearDropIndicator();
            }

            public override void MouseDown (NSEvent theEvent)
            {
                // The row is hit tested from the event, before the table's own tracking runs. ClickedRow only holds the
                // clicked row while the table is sending its action, and it was already -1 by the time base.MouseDown
                // returned, so a double click never found its row and never toggled it.
                int row = (int)this.GetRow(this.ConvertPointFromView(theEvent.LocationInWindow, null));
                base.MouseDown(theEvent);
                if (theEvent.ClickCount == 2)
                {
                    m_owner.OnTableDoubleClicked(row);
                }
            }
        }

        // The table's delegate: row views, their teardown, and selection
        private sealed class TableDelegate : NSTableViewDelegate
        {
            private readonly FlatTreeListView m_owner;

            public TableDelegate (FlatTreeListView owner)
            {
                m_owner = owner;
            }

            public override NSView GetViewForItem (NSTableView tableView, NSTableColumn tableColumn, nint row)
            {
                FlatTreeItem item = m_owner.m_store[(int)row];
                return new RowCellView(item, m_owner.MakeRowContent(item));
            }

            public override NSTableRowView CoreGetRowView (NSTableView tableView, nint row) => new RowBackgroundView();

            // The table is done with this row's views: tear the content down so its bindings go too
            public override void DidRemoveRowView (NSTableView tableView, NSTableRowView rowView, nint row) => TeardownRowView(rowView, row);

            public override NSIndexSet GetSelectionIndexes (NSTableView tableView, NSIndexSet proposedSelectionIndexes)
            {
                if (m_owner.m_selectionMode == eFlatTreeSelectionMode.None)
                {
                    return new NSIndexSet();
                }

                // Multi toggles the clicked row. NSTableView only knows the Extended way (a click selects
                // one row, Command toggles, Shift extends), so a plain click in Multi, which the table
                // proposes as just that row, is turned into a toggle against what is selected now.
                // Keyboard moves and Command or Shift clicks are left as the table proposes them.
                NSIndexSet proposed = proposedSelectionIndexes;
                NSEvent currentEvent = NSApplication.SharedApplication.CurrentEvent;
                if ((m_owner.m_selectionMode == eFlatTreeSelectionMode.Multi)
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
                    if ((int)index < m_owner.m_store.Count && m_owner.m_store[(int)index].IsSelectable)
                    {
                        allowed.Add(index);
                    }
                }

                return allowed;
            }

            public override void SelectionDidChange (NSNotification notification) => m_owner.OnTableSelectionChanged();
        }

        // The table's data source: the row count, and dragging rows to reorder them
        private sealed class TableDataSource : NSTableViewDataSource
        {
            private readonly FlatTreeListView m_owner;

            public TableDataSource (FlatTreeListView owner)
            {
                m_owner = owner;
            }

            public override nint GetRowCount (NSTableView tableView) => m_owner.m_store.Count;

            public override INSPasteboardWriting GetPasteboardWriterForRow (NSTableView tableView, nint row)
            {
                FlatTreeItem item = m_owner.m_store[(int)row];
                if (!m_owner.CanStartDrag(item))
                {
                    return null;
                }

                // A selected row drags the whole selection, another row drags alone; the table asks once
                // per dragged row, so the list is worked out on the first
                if (row == m_owner.m_table.SelectedRow || !item.IsSelected)
                {
                    var dragged = new List<FlatTreeItem>();
                    if (item.IsSelected)
                    {
                        foreach (FlatTreeItem selected in m_owner.m_selectedItems)
                        {
                            if (m_owner.CanStartDrag(selected))
                            {
                                dragged.Add(selected);
                            }
                        }
                    }
                    else
                    {
                        dragged.Add(item);
                    }

                    m_owner.m_draggedItems = dragged.ToArray();
                }

                var pasteboardItem = new NSPasteboardItem();
                pasteboardItem.SetStringForType(row.ToString(), kDragType);
                return pasteboardItem;
            }

            // The table's proposed row is ignored (see FindHoveredRow); it is only told the gap that was decided, as an
            // "above" drop, so it keeps accepting the drag
            public override NSDragOperation ValidateDrop (NSTableView tableView, INSDraggingInfo info, nint row, NSTableViewDropOperation dropOperation)
            {
                if (m_owner.m_store.Count == 0)
                {
                    return NSDragOperation.None;
                }

                CGPoint location = tableView.ConvertPointFromView(info.DraggingLocation, null);
                m_owner.FindHoveredRow(location, out int hoverIndex, out double yFraction);
                tableView.SetDropRowDropOperation(GapRowFor(hoverIndex, yFraction), NSTableViewDropOperation.Above);
                FlatTreeDropTarget target = m_owner.ComputeDropTarget(hoverIndex, yFraction, (double)location.X);
                m_owner.ShowDropIndicator(hoverIndex, yFraction, target);
                m_owner.m_shownDropTarget = target;
                return target.IsValid ? NSDragOperation.Move : NSDragOperation.None;
            }

            // The drop lands exactly where the indicator last said it would. It is never worked out again here: on the
            // Mac a drop to the end of a tree showed "last child of the root" and landed inside the last open subtree,
            // so what was shown and what was done had come apart, and the shown target is the one the user aimed with.
            public override bool AcceptDrop (NSTableView tableView, INSDraggingInfo info, nint row, NSTableViewDropOperation dropOperation)
            {
                FlatTreeDropTarget target = m_owner.m_shownDropTarget;
                m_owner.ClearDropIndicator();
                if (target == null || !target.IsValid || m_owner.m_draggedItems == null)
                {
                    return false;
                }

                m_owner.CompleteDrop(target);
                m_owner.m_draggedItems = null;
                return true;
            }

            public override void DraggingSessionEnded (NSTableView tableView, NSDraggingSession draggingSession, CGPoint endedAtScreenPoint, NSDragOperation operation)
            {
                m_owner.ClearDropIndicator();
                m_owner.m_draggedItems = null;
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
        // solid triangle that all but vanishes on the dark palette.
        private sealed class ExpanderGlyph : NSView
        {
            private const double kGlyphHalfLength = 3.5;
            private const double kLineWidth = 1.5;

            private bool m_isExpanded;
            private bool m_isHighlighted;

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

            public override void MouseDown (NSEvent theEvent)
            {
                this.Activated?.Invoke(this, EventArgs.Empty);
            }

            public override void ViewDidChangeEffectiveAppearance ()
            {
                base.ViewDidChangeEffectiveAppearance();
                this.NeedsDisplay = true;
            }

            public override void DrawRect (CGRect dirtyRect)
            {
                double centerX = (double)this.Bounds.Width / 2.0;
                double centerY = (double)this.Bounds.Height / 2.0;
                double half = kGlyphHalfLength;

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

                ThemedColors.Get(m_isHighlighted ? ThemedColorKeys.FlatTree_DragTargetHighlight : ThemedColorKeys.FlatTree_ExpanderGlyph).SetStroke();
                path.Stroke();
            }
        }

        // Draws the row's hover and selection: a full-row fill, and for a selected row a strip down
        // its leading edge plus an outline, in the key window's colors or the unfocused ones
        private sealed class RowBackgroundView : NSTableRowView
        {
            private const double kSelectionStripWidth = 4.0;

            private NSTrackingArea m_trackingArea;
            private bool m_isPointerOver;

            // A selected row in a focused table reports Emphasized here by default, and AppKit hands that to
            // every cell, so stock labels and buttons switch to their "on a strong selection" colors (white
            // text, white glyphs). That suits AppKit's own blue selection, but this row draws the themed
            // selection fill, which is pale in light mode, and the row's content went white on it. The row
            // keeps the normal style so content keeps its own colors on the themed fill.
            public override NSBackgroundStyle InteriorBackgroundStyle => NSBackgroundStyle.Normal;

            public override void DrawSelection (CGRect dirtyRect)
            {
                CGRect bounds = this.Bounds;
                ThemedColors.Get(ThemedColorKeys.FlatTree_SelectionBackground).SetFill();
                NSBezierPath.FillRect(bounds);

                string accentKey = this.Emphasized ? ThemedColorKeys.FlatTree_Selection : ThemedColorKeys.FlatTree_SelectionUnfocused;
                ThemedColors.Get(accentKey).SetFill();
                NSBezierPath.FillRect(new CGRect(bounds.X, bounds.Y, (nfloat)kSelectionStripWidth, bounds.Height));
                ThemedColors.Get(accentKey).SetStroke();
                NSBezierPath.StrokeRect(bounds.Inset((nfloat)0.5, (nfloat)0.5));
            }

            public override void DrawBackground (CGRect dirtyRect)
            {
                base.DrawBackground(dirtyRect);
                if (m_isPointerOver && !this.Selected)
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
