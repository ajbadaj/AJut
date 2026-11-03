namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using AJut.Storage;
    using AJut.Tree;
    using AJut.UX.Theming;
    using CoreAnimation;
    using CoreGraphics;
    using Foundation;
    using ObjCRuntime;
    using UIKit;

    /// <summary>How many rows a <see cref="FlatTreeListView"/> lets the user select</summary>
    public enum eFlatTreeSelectionMode
    {
        /// <summary>Rows cannot be selected; a tap still raises ItemTapped</summary>
        None,

        /// <summary>One row at a time: a tap selects the row tapped</summary>
        Single,

        /// <summary>Any number of rows, each tap toggling one</summary>
        Multi,
    }

    // ===========[ FlatTreeListView ]===========================================
    // A tree shown as a flat list of rows: the UIKit view over AJut.UX's FlatTreeItem (expand and
    // collapse, indent, visible versus hidden children) and AJut.Core's ObservableFlatTreeStore (the
    // visible rows in order). Rows are a UITableView's, one section, plain by default or inset
    // grouped (the PropertyGrid's card look).
    //
    // A UITableView with no outline support is used on purpose, as the AppKit tree uses a plain
    // NSTableView: the flat store already is the tree's flattened form, and FlatTreeItem owns
    // expansion (and through it IExpandableNode), so nothing in UIKit keeps a second copy of it.
    //
    // The store reports an expand or collapse one row at a time, synchronously, each event's index
    // correct against the rows right after the previous event. So each event is applied to the table
    // the moment it arrives, as a single row insert or delete.
    //
    // Cells are built fresh for each row the table asks for, never reused, and their content is torn
    // down when the table stops showing them. Reuse is where the WinUI3 grid got stale editors from,
    // and a PropertyGrid row holds live editors.
    //
    // Selection is the tree's own (FlatTreeItem.IsSelected), drawn by each row: a tap selects or
    // toggles a row and the table's own selection is never used, so the two can't disagree.
    //
    // A long press lifts a row to drag it, over AJut.UX's FlatTreeDragDropManager, the same drop math
    // as every other stack. A finger picks the drop depth by how far it moves sideways from where it
    // lifted the row (ComputeTouchDragCursorX), rather than by where it sits across the list as a
    // mouse does.
    //
    // A UIKit view and its managed peer form a cycle the GC can't break when anything native the view
    // retains holds the view: the native side keeps the peer alive, and the peer keeps the native side
    // alive. A shown row's cell is retained by the table, and reaches the store through its tree item
    // and whatever its content binds to. So out of a window the tree shows no rows at all, its cells
    // are dropped and torn down, and it stops following the store and the root items; back in one it
    // catches up and builds its rows again, scrolled where it was. The table's source, the table, and
    // the auto scroll ticker hold the tree weakly, and the drag gesture's handlers are static.
    public class FlatTreeListView : UIView
    {
        private readonly ObservableFlatTreeStore<FlatTreeItem> m_store = new ObservableFlatTreeStore<FlatTreeItem>();
        private readonly HashSet<RowCell> m_liveCells = new HashSet<RowCell>();
        private readonly List<FlatTreeItem> m_selectedItems = new List<FlatTreeItem>();
        private TreeTableView m_table;
        private TableSource m_source;
        private DropIndicatorView m_dropIndicator;
        private UILongPressGestureRecognizer m_dragRecognizer;
        private IObservableTreeNode m_root;
        private IEnumerable<IObservableTreeNode> m_rootItemsSource;
        private IObservableTreeNode[] m_builtFromRootItems;
        private INotifyCollectionChanged m_listenedRootItems;
        private eFlatTreeSelectionMode m_selectionMode = eFlatTreeSelectionMode.Single;
        private eSiblingOrder m_siblingDisplayOrder = eSiblingOrder.Forward;
        private double m_treeDepthIndentSize = MobileLook.FlatTreeList.TreeDepthIndentSize;
        private double m_rowSpacing = MobileLook.FlatTreeList.RowSpacing;
        private double m_fixedRowHeight = double.NaN;
        private bool m_isDragDropReorderEnabled;
        private bool m_isEnabled = true;
        private bool m_isFollowingStore;
        private bool m_isDisposed;
        private RowLook m_look = RowLook.Tree;
        private CGPoint m_scrollOffsetWhenDetached;

        // The drag in progress, if any
        private FlatTreeItem[] m_draggedItems;
        private int m_draggedDepth;
        private nfloat m_dragStartX;
        private CGPoint m_dragFingerInTree;
        private FlatTreeDropTarget m_shownDropTarget;
        private RowCell m_highlightedParentCell;
        private CADisplayLink m_autoScrollLink;

        // ===========[ Setup/Construction/Teardown ]===========================
        public FlatTreeListView () : this(UITableViewStyle.Plain)
        {
        }

        /// <summary>
        /// A tree in a table of the given style: <see cref="UITableViewStyle.Plain"/> rows run edge to edge on the tree's
        /// own background, <see cref="UITableViewStyle.InsetGrouped"/> sets them in a rounded card, as a settings form is.
        /// </summary>
        public FlatTreeListView (UITableViewStyle style) : base(CGRect.Empty)
        {
            this.Setup(style);
        }

        /// <summary>Used by the runtime when UIKit creates the managed peer for an existing native view.</summary>
        protected FlatTreeListView (NativeHandle handle) : base(handle)
        {
            this.Setup(UITableViewStyle.Plain);
        }

        private void Setup (UITableViewStyle style)
        {
            const float kEstimatedRowHeight = MobileLook.FlatTreeList.MinRowHeight;

            m_table = new TreeTableView(this, style)
            {
                TranslatesAutoresizingMaskIntoConstraints = false,
                RowHeight = UITableView.AutomaticDimension,
                EstimatedRowHeight = kEstimatedRowHeight,
                SeparatorStyle = UITableViewCellSeparatorStyle.None,
                AllowsSelection = true,
            };

            if (style == UITableViewStyle.Plain)
            {
                m_table.BackgroundColor = UIColor.Clear;
            }

            m_source = new TableSource(this);
            m_table.Source = m_source;
            this.AddSubview(m_table);
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                m_table.TopAnchor.ConstraintEqualTo(this.TopAnchor),
                m_table.BottomAnchor.ConstraintEqualTo(this.BottomAnchor),
                m_table.LeadingAnchor.ConstraintEqualTo(this.LeadingAnchor),
                m_table.TrailingAnchor.ConstraintEqualTo(this.TrailingAnchor),
            });

            m_dropIndicator = new DropIndicatorView { UserInteractionEnabled = false };
            m_table.AddSubview(m_dropIndicator);

            // The handlers are static and find the tree through the table they are on, so the recognizer (which the table
            // retains natively) holds nothing that leads back to the tree. It only begins on a row that can be dragged,
            // so a long press anywhere else is left to the row's menu (RowMenuFactory).
            m_dragRecognizer = new UILongPressGestureRecognizer(OnDragGesture)
            {
                MinimumPressDuration = MobileLook.FlatTreeList.DragLongPressDelay.TotalSeconds,
                Enabled = false,
                ShouldBegin = ShouldBeginDrag,
            };
            m_table.AddGestureRecognizer(m_dragRecognizer);
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing && !m_isDisposed)
            {
                m_isDisposed = true;
                this.EndDrag();
                this.FollowStore(false);
                this.ListenToRootItems(null);

                // The table only reports the cells it stops showing while it lives, so the cells still
                // live are torn down here, or their content's bindings outlive the tree
                foreach (RowCell cell in new List<RowCell>(m_liveCells))
                {
                    cell.Teardown();
                }

                m_liveCells.Clear();
                m_store.RootNode?.DisposeTree();
                m_table.Source = null;
            }

            base.Dispose(disposing);
        }

        // ===========[ Events ]================================================
        /// <summary>Raised when the selected rows change, by the user or from code</summary>
        public event EventHandler<SelectionChange<FlatTreeItem>> SelectionChanged;

        /// <summary>Raised when a row is tapped, after any selection change the tap made</summary>
        public event EventHandler<FlatTreeItem> ItemTapped;

        /// <summary>
        /// Raised when the user drops dragged rows somewhere valid. Set Cancel to do the move yourself (a PropertyGrid
        /// moves list elements in their list instead); otherwise the nodes are moved in their tree.
        /// </summary>
        public event EventHandler<FlatTreeReorderEventArgs> DragDropReorderRequested;

        public override void MovedToWindow ()
        {
            base.MovedToWindow();
            if (this.Window != null)
            {
                this.OnAttached();
            }
            else
            {
                this.OnDetached();
            }
        }

        private void OnStoreCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            // One row per event, applied right away; see the class comment for why
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add when (e.NewItems?.Count == 1) && (e.NewStartingIndex >= 0):
                    m_table.InsertRows(new[] { NSIndexPath.FromRowSection(e.NewStartingIndex, 0) }, UITableViewRowAnimation.None);
                    break;

                // The row goes before the selection is told: a SelectionChanged handler that reloads the table in between
                // would have it count the rows without this one, and the delete would then be against the wrong total
                case NotifyCollectionChangedAction.Remove when (e.OldItems?.Count == 1) && (e.OldStartingIndex >= 0):
                    m_table.DeleteRows(new[] { NSIndexPath.FromRowSection(e.OldStartingIndex, 0) }, UITableViewRowAnimation.None);
                    this.ForgetSelected((FlatTreeItem)e.OldItems[0]);
                    break;

                default:
                    this.ReloadTable();
                    break;
            }

            if (m_draggedItems != null)
            {
                this.UpdateDrag();
            }
        }

        private void OnRootItemsCollectionChanged (object sender, NotifyCollectionChangedEventArgs e)
        {
            // The store does not watch a root it is not showing, so a change to the top-level items rebuilds the tree,
            // as the other stacks do
            this.RebuildFromRootItems();
        }

        // ===========[ Properties ]============================================
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
                this.ReplaceStoreRoot(value != null ? FlatTreeItem.CreateRoot(value, m_treeDepthIndentSize, this.StartItemsExpanded, m_siblingDisplayOrder) : null);
            }
        }

        /// <summary>
        /// Several top-level nodes, each a row at depth 0 under a hidden, always expanded root. An observable collection is
        /// followed while the tree is in a window: any change rebuilds the rows.
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

                this.ReloadTable();
            }
        }

        /// <summary>Space between rows, in points</summary>
        public double RowSpacing
        {
            get => m_rowSpacing;
            set
            {
                m_rowSpacing = value;
                this.ReloadTable();
            }
        }

        /// <summary>A fixed height for every row, or NaN (the default) to size each row to its content, never under a comfortable touch target</summary>
        public double FixedRowHeight
        {
            get => m_fixedRowHeight;
            set
            {
                m_fixedRowHeight = value;
                m_table.RowHeight = double.IsNaN(value) ? UITableView.AutomaticDimension : (nfloat)value;
                this.ReloadTable();
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

        /// <summary>
        /// How many rows a tap selects. Changing it trims the selection to suit: Single keeps the first selected row, and
        /// None clears it.
        /// </summary>
        public eFlatTreeSelectionMode SelectionMode
        {
            get => m_selectionMode;
            set
            {
                m_selectionMode = value;
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

        /// <summary>
        /// Builds the content of a row, right of its indent and chevron. It is called each time the table needs a row's
        /// view; a returned view implementing <see cref="IFlatTreeRowContent"/> is torn down when the table stops showing
        /// it. By default a row shows its source's ToString. The view takes the rest of the row's width and is centered in
        /// its height at its own height, and a taller view makes the row taller, so a container needs a height of its own
        /// (its content pinned to its top and bottom, or a height constraint): one with none is given none.
        /// </summary>
        public Func<FlatTreeItem, UIView> RowContentFactory { get; set; }

        /// <summary>
        /// Builds the menu a long press on a row brings up, or returns null for none. A row that can be dragged lifts to
        /// drag on a long press instead, so give those rows no menu.
        /// </summary>
        public Func<FlatTreeItem, UIMenu> RowMenuFactory { get; set; }

        /// <summary>Lets the user press and hold a row, then drag it to reorder or reparent it</summary>
        public bool IsDragDropReorderEnabled
        {
            get => m_isDragDropReorderEnabled;
            set
            {
                m_isDragDropReorderEnabled = value;
                m_dragRecognizer.Enabled = value;
            }
        }

        /// <summary>
        /// Whether the tree takes touches. False stops taps, expanding, selecting, dragging, row menus, and scrolling, and
        /// draws each row's chevron (and the tree's own row text) in the disabled color. The tree passes it to its rows
        /// itself: a UIView has no Enabled to hand down, and UserInteractionEnabled stops touches but leaves nothing looking
        /// disabled. Changing it builds the rows again, so a RowContentFactory can read it as it builds a row.
        /// </summary>
        public bool Enabled
        {
            get => m_isEnabled;
            set
            {
                if (m_isEnabled == value)
                {
                    return;
                }

                m_isEnabled = value;
                m_table.UserInteractionEnabled = value;
                this.EndDrag();
                this.ReloadTable();
            }
        }

        /// <summary>Whether a node may be dragged; all may when not set</summary>
        public Func<IObservableTreeNode, bool> CanDragItem { get; set; }

        /// <summary>Whether a dragged node may land under a parent; any may when not set</summary>
        public Func<IObservableTreeNode, IObservableTreeNode, bool> CanDropItem { get; set; }

        /// <summary>The table the rows are in, for the PropertyGrid built on this tree</summary>
        internal UITableView Table => m_table;

        /// <summary>
        /// The keys a row's selection and press are drawn with: the tree's own by default. A PropertyGrid draws its rows
        /// the way WinUI3's grid row does instead. Changing it builds the shown rows again.
        /// </summary>
        internal RowLook Look
        {
            get => m_look;
            set
            {
                m_look = value ?? RowLook.Tree;
                this.ReloadTable();
            }
        }

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Replaces the selection. Single mode keeps only the first item, None selects nothing.</summary>
        public void SetSelection (IEnumerable<FlatTreeItem> items)
        {
            var wanted = new List<FlatTreeItem>();
            if ((items != null) && (m_selectionMode != eFlatTreeSelectionMode.None))
            {
                foreach (FlatTreeItem item in items)
                {
                    if ((item != null) && item.IsSelectable && !wanted.Contains(item))
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
        }

        /// <summary>Finds the row for a source node, collapsed or not, or null</summary>
        public FlatTreeItem FindItemForSource (object source)
        {
            if ((m_store.RootNode == null) || (source == null))
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
            this.ReloadTable();
        }

        /// <summary>Scrolls the rows so <paramref name="item"/>'s row shows; nothing happens for a collapsed row, or while the tree is out of a window</summary>
        public void ScrollIntoView (FlatTreeItem item)
        {
            int index = m_store.IndexOf(item);
            if ((index >= 0) && (this.Window != null))
            {
                m_table.ScrollToRow(NSIndexPath.FromRowSection(index, 0), UITableViewScrollPosition.None, animated: true);
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
            for (int index = all.Count - 1; index >= 0; --index)
            {
                if (all[index].IsExpandable && !all[index].IsFalseRoot)
                {
                    all[index].IsExpanded = false;
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
            this.ReloadTable();
            m_table.SetContentOffset(this.ClampScrollOffset(m_scrollOffsetWhenDetached), animated: false);
        }

        // No rows out of a window (see the class comment): the source reports none once the store isn't followed, the
        // table drops its cells as it lays out again, and any cell it still had is torn down here
        private void OnDetached ()
        {
            this.EndDrag();
            m_scrollOffsetWhenDetached = m_table.ContentOffset;
            this.FollowStore(false);
            this.ListenToRootItems(null);
            m_table.ReloadData();
            m_table.LayoutIfNeeded();
            foreach (RowCell cell in new List<RowCell>(m_liveCells))
            {
                cell.Teardown();
            }

            m_liveCells.Clear();
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

        // A reload is lazy, so the table is laid out at once: an insert or delete that arrived before the table had read
        // its new row count would be counted against the wrong total, which UIKit treats as a fatal inconsistency
        private void ReloadTable ()
        {
            m_table.ReloadData();
            if (this.Window != null)
            {
                m_table.LayoutIfNeeded();
            }
        }

        private void ReplaceStoreRoot (FlatTreeItem newRoot)
        {
            // The old tree has to let go of its source subscriptions, or they duplicate children later
            this.EndDrag();
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
            this.ReplaceStoreRoot(FlatTreeItem.CreateUberRoot(m_builtFromRootItems, m_treeDepthIndentSize, false, m_siblingDisplayOrder));
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

        // Moves the selection to exactly wanted: IsSelected on each item (which each row draws), the list, and one event
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

            if ((added.Count == 0) && (removed.Count == 0))
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

        // Rows that left the store while it was not followed are let go of the same way
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

        private void OnRowTapped (int row)
        {
            if ((row < 0) || (row >= m_store.Count))
            {
                return;
            }

            FlatTreeItem item = m_store[row];
            if (item.IsSelectable)
            {
                if (m_selectionMode == eFlatTreeSelectionMode.Single)
                {
                    this.SetSelection(new[] { item });
                }
                else if (m_selectionMode == eFlatTreeSelectionMode.Multi)
                {
                    var wanted = new List<FlatTreeItem>(m_selectedItems);
                    if (!wanted.Remove(item))
                    {
                        wanted.Add(item);
                    }

                    this.ApplySelection(wanted);
                }
            }

            this.ItemTapped?.Invoke(this, item);
        }

        private UITableViewCell MakeCell (int row)
        {
            FlatTreeItem item = m_store[row];
            var cell = new RowCell(item, this.MakeRowContent(item), m_look, m_rowSpacing, this.IsLifted(item), m_isEnabled);
            if (m_table.Style == UITableViewStyle.Plain)
            {
                cell.BackgroundColor = UIColor.Clear;
            }

            m_liveCells.Add(cell);
            return cell;
        }

        private UIView MakeRowContent (FlatTreeItem item)
        {
            if (this.RowContentFactory != null)
            {
                return this.RowContentFactory(item) ?? new UIView();
            }

            return new UILabel
            {
                Text = item.Source?.ToString() ?? string.Empty,
                LineBreakMode = UILineBreakMode.TailTruncation,
                TextColor = m_isEnabled ? TreeColors.RowText : TreeColors.RowTextDisabled,
            };
        }

        private void OnCellDropped (RowCell cell)
        {
            cell.Teardown();
            m_liveCells.Remove(cell);
            if (ReferenceEquals(cell, m_highlightedParentCell))
            {
                m_highlightedParentCell = null;
            }
        }

        // ===========[ Drag and drop ]=========================================
        private static void OnDragGesture (UILongPressGestureRecognizer recognizer)
        {
            if ((recognizer.View as TreeTableView)?.Owner is FlatTreeListView owner)
            {
                owner.HandleDragGesture(recognizer);
            }
        }

        private static bool ShouldBeginDrag (UIGestureRecognizer recognizer)
        {
            if ((recognizer.View as TreeTableView)?.Owner is not FlatTreeListView owner)
            {
                return false;
            }

            NSIndexPath indexPath = owner.m_table.IndexPathForRowAtPoint(recognizer.LocationInView(owner.m_table));
            return (indexPath != null)
                && ((int)indexPath.Row < owner.m_store.Count)
                && owner.CanStartDrag(owner.m_store[(int)indexPath.Row]);
        }

        private void HandleDragGesture (UILongPressGestureRecognizer recognizer)
        {
            switch (recognizer.State)
            {
                case UIGestureRecognizerState.Began:
                    if (!this.TryBeginDrag(recognizer.LocationInView(m_table)))
                    {
                        // Cancels this press, so the table carries on as if it never began
                        recognizer.Enabled = false;
                        recognizer.Enabled = m_isDragDropReorderEnabled;
                        return;
                    }

                    m_dragStartX = recognizer.LocationInView(this).X;
                    m_dragFingerInTree = recognizer.LocationInView(this);
                    this.UpdateDrag();
                    break;

                case UIGestureRecognizerState.Changed:
                    if (m_draggedItems != null)
                    {
                        m_dragFingerInTree = recognizer.LocationInView(this);
                        this.UpdateDrag();
                    }

                    break;

                case UIGestureRecognizerState.Ended:
                    FlatTreeDropTarget target = m_shownDropTarget;
                    FlatTreeItem[] dragged = m_draggedItems;
                    this.EndDrag();
                    if ((dragged != null) && (target != null) && target.IsValid)
                    {
                        this.CompleteDrop(dragged, target);
                    }

                    break;

                case UIGestureRecognizerState.Cancelled:
                case UIGestureRecognizerState.Failed:
                    this.EndDrag();
                    break;
            }
        }

        private bool CanStartDrag (FlatTreeItem item)
        {
            // Roots never drag: a row at depth 0 has nowhere above it to go
            return m_isDragDropReorderEnabled
                && m_isEnabled
                && (item.TreeDepth > 0)
                && !item.IsFalseRoot
                && ((this.CanDragItem == null) || this.CanDragItem(item.Source));
        }

        // A selected row drags the whole selection (the rows of it that can be dragged), another row drags alone
        private bool TryBeginDrag (CGPoint locationInTable)
        {
            NSIndexPath indexPath = m_table.IndexPathForRowAtPoint(locationInTable);
            if ((indexPath == null) || ((int)indexPath.Row >= m_store.Count))
            {
                return false;
            }

            FlatTreeItem pressed = m_store[(int)indexPath.Row];
            if (!this.CanStartDrag(pressed))
            {
                return false;
            }

            var dragged = new List<FlatTreeItem>();
            if (pressed.IsSelected)
            {
                foreach (FlatTreeItem item in m_store)
                {
                    if (item.IsSelected && this.CanStartDrag(item))
                    {
                        dragged.Add(item);
                    }
                }
            }
            else
            {
                dragged.Add(pressed);
            }

            m_draggedItems = dragged.ToArray();
            m_draggedDepth = pressed.TreeDepth;
            m_table.ScrollEnabled = false;
            this.RefreshLiftedRows();

            // A tap on the finger as the row lifts. From iOS 17.5 a generator is tied to the view it plays for.
            UIImpactFeedbackGenerator lift = OperatingSystem.IsIOSVersionAtLeast(17, 5)
                ? UIImpactFeedbackGenerator.GetFeedbackGenerator(UIImpactFeedbackStyle.Medium, this)
                : new UIImpactFeedbackGenerator(UIImpactFeedbackStyle.Medium);
            lift.ImpactOccurred();

            // The ticker holds the tree weakly; the display link holds the ticker until EndDrag invalidates it
            m_autoScrollLink?.Invalidate();
            var ticker = new AutoScrollTicker(this);
            m_autoScrollLink = CADisplayLink.Create(ticker.Tick);
            ticker.Link = m_autoScrollLink;
            m_autoScrollLink.AddToRunLoop(NSRunLoop.Main, NSRunLoopMode.Common);
            return true;
        }

        private void EndDrag ()
        {
            m_autoScrollLink?.Invalidate();
            m_autoScrollLink = null;
            if (m_draggedItems == null)
            {
                return;
            }

            m_draggedItems = null;
            this.ClearDropIndicator();
            m_table.ScrollEnabled = true;
            this.RefreshLiftedRows();
        }

        private bool IsLifted (FlatTreeItem item) => (m_draggedItems != null) && (Array.IndexOf(m_draggedItems, item) >= 0);

        private void RefreshLiftedRows ()
        {
            foreach (RowCell cell in m_liveCells)
            {
                cell.SetLifted(this.IsLifted(cell.Item));
            }
        }

        // The finger's place against the rows, then the drop that place means, then the indicator for it
        private void UpdateDrag ()
        {
            if ((m_draggedItems == null) || (m_store.Count == 0))
            {
                this.ClearDropIndicator();
                return;
            }

            CGPoint location = m_table.ConvertPointFromView(m_dragFingerInTree, this);
            this.FindHoveredRow(location, out int hoverIndex, out double yFraction);
            double cursorX = FlatTreeDragDropManager.ComputeTouchDragCursorX(
                m_draggedDepth,
                (double)(m_dragFingerInTree.X - m_dragStartX),
                m_treeDepthIndentSize
            );

            FlatTreeDropTarget target = FlatTreeDragDropManager.ComputeDropTarget(
                m_store,
                m_draggedItems,
                hoverIndex,
                yFraction,
                cursorX,
                m_treeDepthIndentSize,
                m_siblingDisplayOrder
            );

            if (!FlatTreeDragDropManager.ValidateDropTarget(m_draggedItems, target, this.CanDropItem))
            {
                target = FlatTreeDropTarget.Invalid;
            }

            m_shownDropTarget = target;
            this.ShowDropIndicator(hoverIndex, yFraction, target);
        }

        // Scrolls the rows while the finger is held near the top or bottom edge, then works the drop out again, since the
        // finger is now over a different row
        private void OnAutoScrollTick ()
        {
            if (m_draggedItems == null)
            {
                return;
            }

            CGPoint inTable = m_table.ConvertPointFromView(m_dragFingerInTree, this);
            double fingerY = (double)(inTable.Y - m_table.ContentOffset.Y);
            double step = FlatTreeDragDropManager.ComputeAutoScrollStep(
                fingerY,
                (double)m_table.Bounds.Height,
                MobileLook.FlatTreeList.DragAutoScrollEdge,
                MobileLook.FlatTreeList.DragAutoScrollMaxStep
            );
            if (step == 0.0)
            {
                return;
            }

            CGPoint scrolled = this.ClampScrollOffset(new CGPoint(m_table.ContentOffset.X, m_table.ContentOffset.Y + (nfloat)step));
            if (scrolled.Y != m_table.ContentOffset.Y)
            {
                m_table.ContentOffset = scrolled;
                this.UpdateDrag();
            }
        }

        // An offset the rows can actually be scrolled to: from the top inset to as far as the content goes
        private CGPoint ClampScrollOffset (CGPoint offset)
        {
            UIEdgeInsets insets = m_table.AdjustedContentInset;
            double minimumY = -(double)insets.Top;
            double maximumY = Math.Max(minimumY, (double)(m_table.ContentSize.Height + insets.Bottom - m_table.Bounds.Height));
            return new CGPoint(offset.X, (nfloat)Math.Clamp((double)offset.Y, minimumY, maximumY));
        }

        // The row under the finger, and how far down it the finger is. Above the first row is the first row's top, and past
        // the last row is the last row's bottom, as on the other stacks.
        private void FindHoveredRow (CGPoint location, out int hoverIndex, out double yFraction)
        {
            NSIndexPath indexPath = m_table.IndexPathForRowAtPoint(location);
            if ((indexPath == null) || ((int)indexPath.Row >= m_store.Count))
            {
                bool isAbove = location.Y < m_table.RectForRowAtIndexPath(NSIndexPath.FromRowSection(0, 0)).GetMinY();
                hoverIndex = isAbove ? 0 : m_store.Count - 1;
                yFraction = isAbove ? 0.0 : 1.0;
                return;
            }

            hoverIndex = (int)indexPath.Row;
            CGRect rowRect = m_table.RectForRowAtIndexPath(indexPath);
            yFraction = rowRect.Height > 0 ? Math.Clamp((double)(location.Y - rowRect.GetMinY()) / (double)rowRect.Height, 0.0, 1.0) : 0.5;
        }

        private void CompleteDrop (FlatTreeItem[] dragged, FlatTreeDropTarget target)
        {
            var sources = new IObservableTreeNode[dragged.Length];
            for (int index = 0; index < sources.Length; ++index)
            {
                sources[index] = dragged[index].Source;
            }

            var args = new FlatTreeReorderEventArgs(sources, target.TargetParent, target.InsertIndex);
            this.DragDropReorderRequested?.Invoke(this, args);
            if (!args.Cancel)
            {
                FlatTreeDragDropManager.ExecuteReorder(sources, target);
            }
        }

        // The insertion line starts at the content column of the target depth, and a dashed L runs to it from the new
        // parent's chevron, which is highlighted: the same pieces and rules as the WinUI3 and AppKit trees' drag overlays
        private void ShowDropIndicator (int hoverIndex, double yFraction, FlatTreeDropTarget target)
        {
            // The line goes where the drop lands, which is not always the gap under the finger (see ComputeInsertionLine)
            FlatTreeInsertionLine line = FlatTreeDragDropManager.ComputeInsertionLine(m_store, target, hoverIndex, yFraction, m_siblingDisplayOrder);
            if (line == null)
            {
                this.ClearDropIndicator();
                return;
            }

            int parentIndex = FlatTreeDragDropManager.FindParentStoreIndex(m_store, target);
            CGRect lineRow = this.RowRect(line.Row);
            nfloat lineY = line.IsBelowRow ? lineRow.GetMaxY() : lineRow.GetMinY();
            nfloat rowContentX = this.GetRowContentX();
            nfloat lineX = rowContentX + (nfloat)(target.TargetDepth * m_treeDepthIndentSize) + MobileLook.FlatTreeList.ExpanderColumnWidth;
            m_dropIndicator.Frame = new CGRect(0, 0, m_table.Bounds.Width, (nfloat)Math.Max((double)m_table.ContentSize.Height, (double)m_table.Bounds.Height));
            m_table.BringSubviewToFront(m_dropIndicator);

            this.ClearParentHighlight();
            CGPoint? anchor = null;
            if (parentIndex >= 0)
            {
                // The connector starts at the parent's chevron itself, measured from the drawn glyph, so it meets the tip;
                // the estimate is only for a parent row that is not on screen
                m_highlightedParentCell = m_table.CellAt(NSIndexPath.FromRowSection(parentIndex, 0)) as RowCell;
                if (m_highlightedParentCell != null)
                {
                    anchor = m_highlightedParentCell.GetChevronAnchorIn(m_table);
                    m_highlightedParentCell.SetDropHighlight(true);
                }
                else
                {
                    nfloat chevronCenter = MobileLook.FlatTreeList.ExpanderColumnWidth / 2f;
                    anchor = new CGPoint(rowContentX + (nfloat)((target.TargetDepth - 1) * m_treeDepthIndentSize) + chevronCenter, this.RowRect(parentIndex).GetMidY());
                }
            }

            m_dropIndicator.Show(lineX, lineY, anchor, this);
        }

        private CGRect RowRect (int row) => m_table.RectForRowAtIndexPath(NSIndexPath.FromRowSection(row, 0));

        // Where a row's content (its indent, chevron, and view) starts, in table coordinates. Every row starts at the same
        // x, so any row on screen answers for all of them; the inset grouped style sets rows in from the table's edge.
        private nfloat GetRowContentX ()
        {
            foreach (RowCell cell in m_liveCells)
            {
                if (cell.Window != null)
                {
                    return cell.GetContentStartIn(m_table);
                }
            }

            return MobileLook.FlatTreeList.ContentLeadingInset;
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
        /// <summary>
        /// The keys a row's selection and press are drawn with. A null SelectionFill draws a selected row with its outline
        /// and strip alone, as WinUI3's PropertyGrid row does.
        /// </summary>
        internal sealed record RowLook (string SelectionFill, string SelectionAccent, string PressedFill, string PressedBorder)
        {
            /// <summary>A plain tree's rows, as WinUI3's FlatTreeListControl draws them</summary>
            public static readonly RowLook Tree = new RowLook(
                MobileLook.FlatTreeList.SelectionFill,
                MobileLook.FlatTreeList.SelectionAccent,
                MobileLook.FlatTreeList.PressedFill,
                MobileLook.FlatTreeList.PressedBorder
            );
        }

        // Every color the tree paints: the AJut theme's for the look key when the theme is loaded, a consumer's unthemed
        // default when one has been set for the key (ThemedColors.SetUnthemedDefault), otherwise the tree's own system
        // color. The accent parts take the view's TintColor, the way UIKit's own controls take their accent.
        internal static class TreeColors
        {
            public static UIColor ExpanderGlyph => Get(MobileLook.FlatTreeList.ExpanderGlyph, UIColor.SecondaryLabel);
            public static UIColor ExpanderGlyphDisabled => Get(MobileLook.FlatTreeList.ExpanderGlyphDisabled, UIColor.TertiaryLabel);
            public static UIColor ExpanderPressedFill => Get(MobileLook.FlatTreeList.ExpanderPressedFill, UIColor.TertiarySystemFill);
            public static UIColor RowText => Get(MobileLook.FlatTreeList.RowText, UIColor.Label);
            public static UIColor RowTextDisabled => Get(MobileLook.FlatTreeList.RowTextDisabled, UIColor.TertiaryLabel);

            public static UIColor SelectionFill (RowLook look) => (look.SelectionFill == null) ? UIColor.Clear : Get(look.SelectionFill, UIColor.TertiarySystemFill);
            public static UIColor SelectionAccent (RowLook look, UIView view) => Get(look.SelectionAccent, view.TintColor);
            public static UIColor PressedFill (RowLook look) => Get(look.PressedFill, UIColor.QuaternarySystemFill);
            public static UIColor PressedBorder (RowLook look) => Get(look.PressedBorder, UIColor.Separator);
            public static UIColor DragInsertionLine (UIView view) => Get(MobileLook.FlatTreeList.DragInsertionLine, view.TintColor);
            public static UIColor DragTargetHighlight (UIView view) => Get(MobileLook.FlatTreeList.DragTargetHighlight, view.TintColor);

            public static UIColor DragParentConnector (UIView view)
            {
                const float kUnthemedConnectorOpacity = 0.5f;
                return Get(MobileLook.FlatTreeList.DragParentConnector, view.TintColor.ColorWithAlpha(kUnthemedConnectorOpacity));
            }

            private static UIColor Get (string key, UIColor fallback) => ThemedColors.Get(key) ?? fallback;
        }

        // The table, holding the tree weakly so the drag gesture's static handler can find it
        private sealed class TreeTableView : UITableView
        {
            private readonly WeakReference<FlatTreeListView> m_owner;

            public TreeTableView (FlatTreeListView owner, UITableViewStyle style) : base(CGRect.Empty, style)
            {
                m_owner = new WeakReference<FlatTreeListView>(owner);
            }

            public FlatTreeListView Owner => m_owner.TryGetTarget(out FlatTreeListView owner) ? owner : null;
        }

        // The table's source: the rows, their cells, taps, and teardown. The table holds its source strongly, so this
        // holds the tree weakly.
        private sealed class TableSource : UITableViewSource
        {
            private readonly WeakReference<FlatTreeListView> m_owner;

            public TableSource (FlatTreeListView owner)
            {
                m_owner = new WeakReference<FlatTreeListView>(owner);
            }

            // No rows while the tree isn't following its store, which is while it is out of a window
            public override nint RowsInSection (UITableView tableview, nint section)
            {
                return (m_owner.TryGetTarget(out FlatTreeListView owner) && owner.m_isFollowingStore) ? owner.m_store.Count : 0;
            }

            public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
            {
                if (m_owner.TryGetTarget(out FlatTreeListView owner) && owner.m_isFollowingStore && ((int)indexPath.Row < owner.m_store.Count))
                {
                    return owner.MakeCell((int)indexPath.Row);
                }

                return new UITableViewCell(UITableViewCellStyle.Default, (string)null);
            }

            public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
            {
                // The tree's selection is its own (see the class comment), so the table's is cleared at once
                tableView.DeselectRow(indexPath, animated: false);
                if (m_owner.TryGetTarget(out FlatTreeListView owner))
                {
                    owner.OnRowTapped((int)indexPath.Row);
                }
            }

            public override void CellDisplayingEnded (UITableView tableView, UITableViewCell cell, NSIndexPath indexPath)
            {
                if ((cell is RowCell rowCell) && m_owner.TryGetTarget(out FlatTreeListView owner))
                {
                    owner.OnCellDropped(rowCell);
                }
            }

            // The row's menu, if it has one and no drag is on. The menu is built when asked for, and the configuration
            // only holds that menu, nothing that leads back to the tree.
            public override UIContextMenuConfiguration GetContextMenuConfiguration (UITableView tableView, NSIndexPath indexPath, CGPoint point)
            {
                if (!m_owner.TryGetTarget(out FlatTreeListView owner)
                    || (owner.RowMenuFactory == null)
                    || !owner.m_isEnabled
                    || (owner.m_draggedItems != null)
                    || ((int)indexPath.Row >= owner.m_store.Count))
                {
                    return null;
                }

                UIMenu menu = owner.RowMenuFactory(owner.m_store[(int)indexPath.Row]);
                return (menu != null) ? UIContextMenuConfiguration.Create(null, null, _ => menu) : null;
            }
        }

        // One row: the selection fill, outline and strip, the pressed tint, the chevron across the indent, then the
        // row's own content. It draws from its tree item's IsSelected and the table's press, and lets go of its item and
        // its content in Teardown.
        private sealed class RowCell : UITableViewCell
        {
            private readonly FlatTreeItem m_item;
            private readonly UIView m_content;
            private readonly RowLook m_look;
            private readonly UIView m_selectionFill = new UIView { TranslatesAutoresizingMaskIntoConstraints = false, UserInteractionEnabled = false };
            private readonly UIView m_selectionStrip = new UIView { TranslatesAutoresizingMaskIntoConstraints = false, UserInteractionEnabled = false };
            private readonly UIView m_pressedTint = new UIView { TranslatesAutoresizingMaskIntoConstraints = false, UserInteractionEnabled = false };
            private readonly ExpanderControl m_expander;
            private bool m_isTornDown;

            public RowCell (FlatTreeItem item, UIView content, RowLook look, double rowSpacing, bool isLifted, bool isEnabled)
                : base(UITableViewCellStyle.Default, (string)null)
            {
                m_item = item;
                m_content = content;
                m_look = look;
                this.SelectionStyle = UITableViewCellSelectionStyle.None;

                nfloat halfSpacing = (nfloat)(rowSpacing / 2.0);
                nfloat leadingWidth = MobileLook.FlatTreeList.ContentLeadingInset + (nfloat)item.IndentWidth + MobileLook.FlatTreeList.ExpanderColumnWidth;
                m_expander = new ExpanderControl(item, leadingWidth) { TranslatesAutoresizingMaskIntoConstraints = false, Enabled = isEnabled };
                m_content.TranslatesAutoresizingMaskIntoConstraints = false;

                UIView host = this.ContentView;
                host.AddSubview(m_selectionFill);
                m_selectionFill.AddSubview(m_selectionStrip);
                host.AddSubview(m_pressedTint);
                host.AddSubview(m_expander);
                host.AddSubview(m_content);

                // A row is at least a comfortable touch target; the content may make it taller. Both give way (999) to a
                // FixedRowHeight shorter than either, rather than break.
                NSLayoutConstraint minimumHeight = m_selectionFill.HeightAnchor.ConstraintGreaterThanOrEqualTo(MobileLook.FlatTreeList.MinRowHeight - (nfloat)rowSpacing);
                minimumHeight.Priority = 999;
                NSLayoutConstraint contentTop = m_content.TopAnchor.ConstraintGreaterThanOrEqualTo(m_selectionFill.TopAnchor);
                contentTop.Priority = 999;
                NSLayoutConstraint contentBottom = m_content.BottomAnchor.ConstraintLessThanOrEqualTo(m_selectionFill.BottomAnchor);
                contentBottom.Priority = 999;
                NSLayoutConstraint.ActivateConstraints(new[]
                {
                    m_selectionFill.TopAnchor.ConstraintEqualTo(host.TopAnchor, halfSpacing),
                    m_selectionFill.BottomAnchor.ConstraintEqualTo(host.BottomAnchor, -halfSpacing),
                    m_selectionFill.LeadingAnchor.ConstraintEqualTo(host.LeadingAnchor),
                    m_selectionFill.TrailingAnchor.ConstraintEqualTo(host.TrailingAnchor),
                    minimumHeight,

                    m_selectionStrip.TopAnchor.ConstraintEqualTo(m_selectionFill.TopAnchor),
                    m_selectionStrip.BottomAnchor.ConstraintEqualTo(m_selectionFill.BottomAnchor),
                    m_selectionStrip.LeadingAnchor.ConstraintEqualTo(m_selectionFill.LeadingAnchor),
                    m_selectionStrip.WidthAnchor.ConstraintEqualTo(MobileLook.FlatTreeList.SelectionStripWidth),

                    m_pressedTint.TopAnchor.ConstraintEqualTo(m_selectionFill.TopAnchor),
                    m_pressedTint.BottomAnchor.ConstraintEqualTo(m_selectionFill.BottomAnchor),
                    m_pressedTint.LeadingAnchor.ConstraintEqualTo(m_selectionFill.LeadingAnchor),
                    m_pressedTint.TrailingAnchor.ConstraintEqualTo(m_selectionFill.TrailingAnchor),

                    m_expander.TopAnchor.ConstraintEqualTo(m_selectionFill.TopAnchor),
                    m_expander.BottomAnchor.ConstraintEqualTo(m_selectionFill.BottomAnchor),
                    m_expander.LeadingAnchor.ConstraintEqualTo(host.LeadingAnchor),
                    m_expander.WidthAnchor.ConstraintEqualTo(leadingWidth),

                    m_content.LeadingAnchor.ConstraintEqualTo(m_expander.TrailingAnchor),
                    m_content.TrailingAnchor.ConstraintEqualTo(host.TrailingAnchor, -MobileLook.FlatTreeList.ContentTrailingInset),
                    m_content.CenterYAnchor.ConstraintEqualTo(m_selectionFill.CenterYAnchor),
                    contentTop,
                    contentBottom,
                });

                m_selectionFill.Layer.BorderWidth = MobileLook.FlatTreeList.OutlineThickness;
                m_pressedTint.Layer.BorderWidth = MobileLook.FlatTreeList.OutlineThickness;
                m_item.PropertyChanged += this.OnItemPropertyChanged;
                this.SetLifted(isLifted);
                this.RefreshExpander();
                this.RefreshLook();
            }

            public FlatTreeItem Item => m_item;

            public override void SetHighlighted (bool highlighted, bool animated)
            {
                base.SetHighlighted(highlighted, animated);
                this.RefreshLook();
            }

            // A layer border is a CGColor, which freezes a dynamic color, so the look is resolved here as well: UIKit
            // calls LayoutSubviews again whenever the appearance (light/dark) changes
            public override void LayoutSubviews ()
            {
                base.LayoutSubviews();
                this.RefreshLook();
            }

            public void Teardown ()
            {
                if (m_isTornDown)
                {
                    return;
                }

                m_isTornDown = true;
                m_item.PropertyChanged -= this.OnItemPropertyChanged;
                m_expander.Teardown();
                (m_content as IFlatTreeRowContent)?.Teardown();
            }

            public void SetLifted (bool isLifted)
            {
                this.ContentView.Alpha = isLifted ? MobileLook.FlatTreeList.DraggedRowOpacity : 1f;
            }

            /// <summary>Where this row's content (indent, chevron, and view) starts, as an x in <paramref name="view"/></summary>
            public nfloat GetContentStartIn (UIView view)
            {
                return view.ConvertPointFromView(new CGPoint(MobileLook.FlatTreeList.ContentLeadingInset, 0), this.ContentView).X;
            }

            /// <summary>The chevron's connector anchor (the tip of its glyph) in <paramref name="view"/>'s coordinates</summary>
            public CGPoint GetChevronAnchorIn (UIView view) => view.ConvertPointFromView(m_expander.ConnectorAnchor, m_expander);

            /// <summary>Draws this row's chevron in the drag highlight while a drop would land under this row</summary>
            public void SetDropHighlight (bool isHighlighted) => m_expander.IsDropHighlighted = isHighlighted;

            private void OnItemPropertyChanged (object sender, PropertyChangedEventArgs e)
            {
                switch (e.PropertyName)
                {
                    case nameof(FlatTreeItem.IsExpanded):
                    case nameof(FlatTreeItem.IsExpandable):
                        this.RefreshExpander();
                        break;

                    case nameof(FlatTreeItem.IsSelected):
                        this.RefreshLook();
                        break;
                }
            }

            private void RefreshExpander ()
            {
                m_expander.Hidden = !m_item.IsExpandable;
                m_expander.IsExpanded = m_item.IsExpanded;
            }

            // Selected: the selection fill, outlined, with the strip. Pressed: the pressed tint over that, outlined only
            // when the row isn't selected, as WinUI3's PressedSelected keeps the selection's outline alone.
            private void RefreshLook ()
            {
                bool isSelected = m_item.IsSelected;
                bool isPressed = this.Highlighted;
                UIColor accent = TreeColors.SelectionAccent(m_look, this);

                m_selectionFill.BackgroundColor = isSelected ? TreeColors.SelectionFill(m_look) : UIColor.Clear;
                m_selectionFill.Layer.BorderColor = (isSelected ? accent : UIColor.Clear).GetResolvedColor(this.TraitCollection).CGColor;
                m_selectionStrip.BackgroundColor = isSelected ? accent : UIColor.Clear;
                m_pressedTint.BackgroundColor = isPressed ? TreeColors.PressedFill(m_look) : UIColor.Clear;
                m_pressedTint.Layer.BorderColor = ((isPressed && !isSelected) ? TreeColors.PressedBorder(m_look) : UIColor.Clear).GetResolvedColor(this.TraitCollection).CGColor;

                this.AccessibilityTraits = isSelected
                    ? (this.AccessibilityTraits | UIAccessibilityTrait.Selected)
                    : (this.AccessibilityTraits & ~UIAccessibilityTrait.Selected);
            }
        }

        // The chevron, and the tap target that toggles it: the row's whole leading span (its indent and the chevron's
        // column) at the row's full height, so a finger finds it at any depth. It handles its own touch (a self
        // subscription, which makes no cycle) and holds only its tree item, never its row or the tree.
        private sealed class ExpanderControl : UIControl
        {
            private readonly FlatTreeItem m_item;
            private readonly nfloat m_glyphCenterX;
            private readonly CAShapeLayer m_pressedFill = new CAShapeLayer();
            private readonly CAShapeLayer m_glyph = new CAShapeLayer();
            private bool m_isExpanded;
            private bool m_isDropHighlighted;

            public ExpanderControl (FlatTreeItem item, nfloat leadingWidth) : base(CGRect.Empty)
            {
                m_item = item;
                m_glyphCenterX = leadingWidth - (MobileLook.FlatTreeList.ExpanderColumnWidth / 2f);

                m_glyph.FillColor = UIColor.Clear.CGColor;
                m_glyph.LineWidth = MobileLook.FlatTreeList.ExpanderGlyphStrokeThickness;
                m_glyph.LineCap = CAShapeLayer.CapRound;
                m_glyph.LineJoin = CAShapeLayer.JoinRound;
                this.Layer.AddSublayer(m_pressedFill);
                this.Layer.AddSublayer(m_glyph);

                this.IsAccessibilityElement = true;
                this.AccessibilityTraits = UIAccessibilityTrait.Button;
                this.AddTarget(this.OnTapped, UIControlEvent.TouchUpInside);
            }

            public bool IsExpanded
            {
                get => m_isExpanded;
                set
                {
                    m_isExpanded = value;
                    this.AccessibilityLabel = value ? "Collapse" : "Expand";
                    this.SetNeedsLayout();
                }
            }

            public bool IsDropHighlighted
            {
                get => m_isDropHighlighted;
                set
                {
                    m_isDropHighlighted = value;
                    this.SetNeedsLayout();
                }
            }

            public override bool Highlighted
            {
                get => base.Highlighted;
                set
                {
                    base.Highlighted = value;
                    this.SetNeedsLayout();
                }
            }

            /// <summary>
            /// Where a drop connector meets this chevron, in its own coordinates: the tip of the down pointing (expanded)
            /// chevron, or the bottom of the right pointing one
            /// </summary>
            public CGPoint ConnectorAnchor
            {
                get
                {
                    nfloat half = MobileLook.FlatTreeList.ExpanderGlyphHalfLength;
                    nfloat belowCenter = m_isExpanded ? half / 2f : half;
                    return new CGPoint(m_glyphCenterX, this.Bounds.Height / 2f + belowCenter + MobileLook.FlatTreeList.ExpanderGlyphStrokeThickness / 2f);
                }
            }

            public void Teardown ()
            {
                this.RemoveTarget(this.OnTapped, UIControlEvent.TouchUpInside);
            }

            // The shape layers' colors are CGColors, which a light/dark switch does not reach on its own, so they are
            // resolved here, where UIKit lays the view out again on any appearance change
            public override void LayoutSubviews ()
            {
                base.LayoutSubviews();

                nfloat centerY = this.Bounds.Height / 2f;
                nfloat half = MobileLook.FlatTreeList.ExpanderGlyphHalfLength;
                var path = new CGPath();
                if (m_isExpanded)
                {
                    // Down
                    path.MoveToPoint(m_glyphCenterX - half, centerY - half / 2f);
                    path.AddLineToPoint(m_glyphCenterX, centerY + half / 2f);
                    path.AddLineToPoint(m_glyphCenterX + half, centerY - half / 2f);
                }
                else
                {
                    // Right
                    path.MoveToPoint(m_glyphCenterX - half / 2f, centerY - half);
                    path.AddLineToPoint(m_glyphCenterX + half / 2f, centerY);
                    path.AddLineToPoint(m_glyphCenterX - half / 2f, centerY + half);
                }

                m_glyph.Path = path;
                UIColor glyphColor = !this.Enabled
                    ? TreeColors.ExpanderGlyphDisabled
                    : (m_isDropHighlighted ? TreeColors.DragTargetHighlight(this) : TreeColors.ExpanderGlyph);
                m_glyph.StrokeColor = glyphColor.GetResolvedColor(this.TraitCollection).CGColor;

                // The pressed fill is a rounded square on the chevron's column, as WinUI3's chevron button is
                const float kPressedFillInset = 2f;
                nfloat side = (nfloat)Math.Min((double)MobileLook.FlatTreeList.ExpanderColumnWidth, (double)this.Bounds.Height) - (2f * kPressedFillInset);
                var square = new CGRect(m_glyphCenterX - side / 2f, centerY - side / 2f, side, side);
                m_pressedFill.Path = UIBezierPath.FromRoundedRect(square, MobileLook.FlatTreeList.ExpanderPressedCornerRadius).CGPath;
                m_pressedFill.FillColor = (this.Highlighted ? TreeColors.ExpanderPressedFill : UIColor.Clear).GetResolvedColor(this.TraitCollection).CGColor;
            }

            private void OnTapped (object sender, EventArgs e)
            {
                m_item.IsExpanded = !m_item.IsExpanded;
            }
        }

        // Draws the drop: the insertion line and the dashed connector from the parent's chevron. It sits over the rows,
        // in the table's content, and never takes a touch.
        private sealed class DropIndicatorView : UIView
        {
            private readonly CAShapeLayer m_line = new CAShapeLayer();
            private readonly CAShapeLayer m_connector = new CAShapeLayer();

            public DropIndicatorView () : base(CGRect.Empty)
            {
                m_connector.FillColor = UIColor.Clear.CGColor;
                m_connector.LineWidth = MobileLook.FlatTreeList.DragParentConnectorThickness;
                m_connector.LineDashPattern = new[]
                {
                    new NSNumber(MobileLook.FlatTreeList.DragParentConnectorDash),
                    new NSNumber(MobileLook.FlatTreeList.DragParentConnectorGap),
                };

                this.Layer.AddSublayer(m_line);
                this.Layer.AddSublayer(m_connector);
                this.Hidden = true;
            }

            /// <summary>Shows the line at (lineX, lineY) to the trailing edge, and the connector from the anchor to it when there is one</summary>
            public void Show (nfloat lineX, nfloat lineY, CGPoint? anchor, UIView colorSource)
            {
                nfloat thickness = MobileLook.FlatTreeList.DragInsertionLineThickness;
                nfloat lineWidth = (nfloat)Math.Max(0.0, (double)(this.Bounds.Width - lineX - MobileLook.FlatTreeList.DragInsertionLineEndInset));
                m_line.Path = CGPath.FromRect(new CGRect(lineX, lineY - thickness / 2f, lineWidth, thickness));
                m_line.FillColor = TreeColors.DragInsertionLine(colorSource).GetResolvedColor(colorSource.TraitCollection).CGColor;

                if (anchor is CGPoint anchorPoint)
                {
                    nfloat topY = (nfloat)Math.Min((double)anchorPoint.Y, (double)lineY);
                    nfloat bottomY = (nfloat)Math.Max((double)anchorPoint.Y, (double)lineY);
                    var path = new CGPath();
                    path.MoveToPoint(anchorPoint.X, topY);
                    path.AddLineToPoint(anchorPoint.X, bottomY);
                    path.AddLineToPoint(lineX, bottomY);
                    m_connector.Path = path;
                    m_connector.StrokeColor = TreeColors.DragParentConnector(colorSource).GetResolvedColor(colorSource.TraitCollection).CGColor;
                    m_connector.Hidden = false;
                }
                else
                {
                    m_connector.Hidden = true;
                }

                this.Hidden = false;
            }

            public void Clear ()
            {
                this.Hidden = true;
            }
        }

        // What the display link calls each frame while a drag is on. The link holds this strongly until it is
        // invalidated, so this holds the tree weakly, and stops the link itself if the tree is gone.
        private sealed class AutoScrollTicker
        {
            private readonly WeakReference<FlatTreeListView> m_owner;

            public AutoScrollTicker (FlatTreeListView owner)
            {
                m_owner = new WeakReference<FlatTreeListView>(owner);
            }

            /// <summary>The display link calling this, so a tick that finds the tree gone can stop it</summary>
            public CADisplayLink Link { get; set; }

            public void Tick ()
            {
                if (m_owner.TryGetTarget(out FlatTreeListView owner))
                {
                    owner.OnAutoScrollTick();
                }
                else
                {
                    this.Link?.Invalidate();
                    this.Link = null;
                }
            }
        }
    }

    /// <summary>A row content view that has something to let go of when its row leaves the table (bindings, subscriptions)</summary>
    public interface IFlatTreeRowContent
    {
        void Teardown ();
    }
}
