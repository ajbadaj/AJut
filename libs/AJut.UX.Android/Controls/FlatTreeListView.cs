namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using AJut.Storage;
    using AJut.Tree;
    using AJut.UX.Theming;
    using Android.Content;
    using Android.Content.Res;
    using Android.Graphics;
    using Android.OS;
    using Android.Runtime;
    using Android.Util;
    using Android.Views;
    using Android.Widget;
    using AndroidColor = Android.Graphics.Color;

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
    // A tree shown as a flat list of rows: the Android view over AJut.UX's FlatTreeItem (expand and
    // collapse, indent, visible versus hidden children) and AJut.Core's ObservableFlatTreeStore (the
    // visible rows in order). The rows are an SDK ListView's, with no AndroidX: the flat store already
    // is the tree's flattened form, so the list only has to show it, and only the rows on screen exist.
    //
    // A ListView adapter can only say "the data changed", never "one row was inserted", so every store
    // change reports the whole list changed and the list asks for every row on screen again. The adapter
    // keeps one view per shown tree item and hands the same one back, so a row on screen is never rebuilt
    // for a change somewhere else, and an editor in it keeps what it shows. Rows are never recycled into
    // other rows (ItemViewTypeIgnore): reuse is where the WinUI3 grid got stale editors from, and a
    // PropertyGrid row holds live editors. A row that leaves the list (scrolled off, collapsed away) is
    // torn down once the list has laid out or scrolled, and built fresh if it comes back, as the UIKit
    // tree does with its cells.
    //
    // A layout with no data change, as when the keyboard opens and the window shrinks, keeps every
    // shown row in place, a focused editor and its focus included. A store change makes the list take
    // every row out and put the shown ones back, which takes the focus from an editor, so the tree puts
    // the focus back once the list has laid out. A row rebuilt for a reload is only torn down once the
    // list has let go of it, so a focused field still writes its edit as it loses the focus.
    //
    // Selection is the tree's own (FlatTreeItem.IsSelected), drawn by each row: a tap selects or toggles
    // a row, and the ListView's own selection and item clicks are never used. A ListView item click does
    // not fire on a row holding a focusable editor, so each row takes its own taps.
    //
    // A long press lifts a row to drag it, over AJut.UX's FlatTreeDragDropManager, the same drop math as
    // every other stack. A finger picks the drop depth by how far it moves sideways from where it lifted
    // the row (ComputeTouchDragCursorX), and the list scrolls while it is held near the top or bottom. On
    // a row that can't be dragged, a long press opens the row's menu (RowMenuFactory) instead.
    //
    // The runtime's GC bridge collects a cycle that runs through Java views, so what matters for lifetime
    // here is what holds the tree from outside it: the store follows the source nodes, which may live far
    // longer than the view, and the root items collection may too. So out of a window the tree shows no
    // rows, tears down the ones it had, and follows neither; back in one it catches up and builds its rows
    // again, scrolled where it was.
    [Register("ajut.ux.controls.FlatTreeListView")]
    public class FlatTreeListView : FrameLayout
    {
        private readonly ObservableFlatTreeStore<FlatTreeItem> m_store = new ObservableFlatTreeStore<FlatTreeItem>();
        private readonly Dictionary<FlatTreeItem, RowView> m_liveRows = new Dictionary<FlatTreeItem, RowView>();
        private readonly List<RowView> m_retiredRows = new List<RowView>();
        private readonly ConditionalWeakTable<FlatTreeItem, StableId> m_rowIds = new ConditionalWeakTable<FlatTreeItem, StableId>();
        private ConditionalWeakTable<FlatTreeItem, StableId>.CreateValueCallback m_makeRowId = null!;
        private readonly List<FlatTreeItem> m_selectedItems = new List<FlatTreeItem>();
        private TreeListView m_list = null!;
        private RowAdapter m_adapter = null!;
        private IObservableTreeNode? m_root;
        private IEnumerable<IObservableTreeNode>? m_rootItemsSource;
        private IObservableTreeNode[]? m_builtFromRootItems;
        private INotifyCollectionChanged? m_listenedRootItems;
        private eFlatTreeSelectionMode m_selectionMode = eFlatTreeSelectionMode.Single;
        private eSiblingOrder m_siblingDisplayOrder = eSiblingOrder.Forward;
        private double m_treeDepthIndentSize = MobileLook.FlatTreeList.TreeDepthIndentSize;
        private double m_rowSpacing = MobileLook.FlatTreeList.RowSpacing;
        private double m_fixedRowHeight = double.NaN;
        private bool m_isDragDropReorderEnabled;
        private bool m_isFollowingStore;
        private bool m_isDisposed;
        private long m_nextRowId;
        private float m_density = 1f;
        private RowLook m_look = RowLook.Tree;
        private PopupMenu? m_shownMenu;

        // The editor that had the focus when the store changed, given it back once the list has laid out
        private View? m_focusToRestore;

        // Where the rows were scrolled to when the tree left its window
        private int m_firstShownWhenDetached;
        private int m_firstShownTopWhenDetached;

        // The drag in progress, if any; finger positions are in the list's own pixels
        private FlatTreeItem[]? m_draggedItems;
        private int m_draggedDepth;
        private float m_dragStartX;
        private float m_dragFingerX;
        private float m_dragFingerY;
        private FlatTreeDropTarget? m_shownDropTarget;
        private int m_shownHoverIndex;
        private double m_shownYFraction;
        private RowView? m_highlightedParentRow;
        private AutoScrollTicker? m_autoScrollTicker;

        // ===========[ Setup/Construction/Teardown ]===========================
        public FlatTreeListView (Context context) : base(context)
        {
            this.Initialize(context, null);
        }

        public FlatTreeListView (Context context, IAttributeSet? attrs) : base(context, attrs)
        {
            this.Initialize(context, attrs);
        }

        public FlatTreeListView (Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
            this.Initialize(context, attrs);
        }

        protected FlatTreeListView (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        private void Initialize (Context context, IAttributeSet? attrs)
        {
            m_density = context.Resources?.DisplayMetrics?.Density ?? 1f;
            m_makeRowId = this.MakeRowId;
            m_adapter = new RowAdapter(this);
            m_list = new TreeListView(context, this)
            {
                Divider = null,
                DividerHeight = 0,
                ItemsCanFocus = true,
                DescendantFocusability = DescendantFocusability.AfterDescendants,
                ChoiceMode = ChoiceMode.None,
                CacheColorHint = AndroidColor.Transparent,

                // Inside a scrolling parent (a page that scrolls, a collapsing toolbar) the list scrolls first
                NestedScrollingEnabled = true,

                // Each row draws its own press and selection, so the list's own highlight is nothing
                Selector = new Android.Graphics.Drawables.ColorDrawable(AndroidColor.Transparent),
            };

            m_list.Adapter = m_adapter;
            m_list.Scroll += this.OnListScrolled;
            this.AddView(m_list, new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            if (attrs != null)
            {
                using TypedArray values = context.ObtainStyledAttributes(attrs, Resource.Styleable.AJutFlatTreeListView);
                this.IncludeRoot = values.GetBoolean(Resource.Styleable.AJutFlatTreeListView_ajutIncludeRoot, false);
                this.StartItemsExpanded = values.GetBoolean(Resource.Styleable.AJutFlatTreeListView_ajutStartItemsExpanded, false);
                this.IsDragDropReorderEnabled = values.GetBoolean(Resource.Styleable.AJutFlatTreeListView_ajutIsDragDropReorderEnabled, false);
                this.Enabled = values.GetBoolean(Resource.Styleable.AJutFlatTreeListView_android_enabled, true);
                values.Recycle();
            }

            // However the tree's own Enabled was set while it was made, the list it holds follows it
            m_list.Enabled = base.Enabled;
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing && !m_isDisposed && (m_list != null))
            {
                m_isDisposed = true;
                this.EndDrag();
                this.FollowStore(false);
                this.ListenToRootItems(null);
                m_list.Scroll -= this.OnListScrolled;

                // A list still in a window would lay out against an adapter that suddenly has no rows
                m_list.Adapter = null;
                this.TeardownAllRows();
                m_store.RootNode?.DisposeTree();
            }

            base.Dispose(disposing);
        }

        // ===========[ Events ]================================================
        /// <summary>Raised when the selected rows change, by the user or from code</summary>
        public event EventHandler<SelectionChange<FlatTreeItem>>? SelectionChanged;

        /// <summary>Raised when a row is tapped, after any selection change the tap made</summary>
        public event EventHandler<FlatTreeItem>? ItemTapped;

        /// <summary>
        /// Raised when the user drops dragged rows somewhere valid. Set Cancel to do the move yourself (a PropertyGrid
        /// moves list elements in their list instead); otherwise the nodes are moved in their tree.
        /// </summary>
        public event EventHandler<FlatTreeReorderEventArgs>? DragDropReorderRequested;

        protected override void OnAttachedToWindow ()
        {
            base.OnAttachedToWindow();

            // A peer made again for a Java view that outlived its disposed one has none of the tree's state
            if (m_isDisposed || (m_list == null))
            {
                return;
            }

            this.FollowStore(true);
            this.ListenToRootItems(m_rootItemsSource as INotifyCollectionChanged);

            // While out of a window nothing followed the top-level items, so the tree is rebuilt if they changed then;
            // otherwise the list catches up on any row changes it missed
            if ((m_rootItemsSource != null) && RootItemsDiffer(m_rootItemsSource, m_builtFromRootItems))
            {
                this.RebuildFromRootItems();
            }

            this.PruneSelection();
            m_adapter.NotifyDataSetChanged();
            m_list.SetSelectionFromTop(Math.Min(m_firstShownWhenDetached, Math.Max(0, m_store.Count - 1)), m_firstShownTopWhenDetached);
        }

        // No rows out of a window (see the class comment): the adapter reports none once the store isn't followed, and
        // every row the list still holds is torn down here, since a list out of a window lays nothing out
        protected override void OnDetachedFromWindow ()
        {
            if (m_isDisposed || (m_list == null))
            {
                base.OnDetachedFromWindow();
                return;
            }

            // A menu left showing from a view that has left its window would hold that window
            m_shownMenu?.Dismiss();
            m_shownMenu = null;
            this.EndDrag();
            m_firstShownWhenDetached = m_list.FirstVisiblePosition;
            m_firstShownTopWhenDetached = (m_list.ChildCount > 0) ? (m_list.GetChildAt(0)?.Top ?? 0) : 0;
            this.FollowStore(false);
            this.ListenToRootItems(null);
            this.TeardownAllRows();
            m_adapter.NotifyDataSetChanged();
            base.OnDetachedFromWindow();
        }

        private void OnStoreCollectionChanged (object? sender, NotifyCollectionChangedEventArgs e)
        {
            // The list is about to take every row out and put the shown ones back, which takes the focus from an editor
            // in one; it is given back once the list has laid out (ReleaseRowsOffTheList)
            m_focusToRestore ??= m_list.FindFocus();
            m_adapter.NotifyDataSetChanged();
            if ((e.Action == NotifyCollectionChangedAction.Remove) && (e.OldItems != null))
            {
                foreach (object removed in e.OldItems)
                {
                    this.ForgetSelected((FlatTreeItem)removed);
                }
            }

            if (m_draggedItems != null)
            {
                this.UpdateDrag();
            }
        }

        private void OnRootItemsCollectionChanged (object? sender, NotifyCollectionChangedEventArgs e)
        {
            // The store does not watch a root it is not showing, so a change to the top-level items rebuilds the tree,
            // as the other stacks do
            this.RebuildFromRootItems();
        }

        private void OnListScrolled (object? sender, AbsListView.ScrollEventArgs e)
        {
            this.ReleaseRowsOffTheList();
        }

        // ===========[ Properties ]============================================
        /// <summary>One root node; its descendants are the rows (and the root itself, with <see cref="IncludeRoot"/>)</summary>
        public IObservableTreeNode? Root
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
        public IEnumerable<IObservableTreeNode>? RootItemsSource
        {
            get => m_rootItemsSource;
            set
            {
                m_root = null;
                m_rootItemsSource = value;
                this.ListenToRootItems(this.IsAttachedToWindow ? value as INotifyCollectionChanged : null);
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

        /// <summary>dp of indent per tree depth</summary>
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

                this.ReloadRows();
            }
        }

        /// <summary>Space between rows, in dp</summary>
        public double RowSpacing
        {
            get => m_rowSpacing;
            set
            {
                m_rowSpacing = value;
                this.ReloadRows();
            }
        }

        /// <summary>A fixed height for every row in dp, or NaN (the default) to size each row to its content, never under a comfortable touch target</summary>
        public double FixedRowHeight
        {
            get => m_fixedRowHeight;
            set
            {
                m_fixedRowHeight = value;
                this.ReloadRows();
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
        public FlatTreeItem? SelectedItem
        {
            get => m_selectedItems.Count > 0 ? m_selectedItems[0] : null;
            set => this.SetSelection(value != null ? new[] { value } : Array.Empty<FlatTreeItem>());
        }

        /// <summary>Every selected row</summary>
        public IReadOnlyList<FlatTreeItem> SelectedItems => m_selectedItems;

        /// <summary>
        /// Builds the content of a row, right of its indent and chevron. It is called each time a row comes onto the list;
        /// a returned view implementing <see cref="IFlatTreeRowContent"/> is torn down when its row leaves the list. By
        /// default a row shows its source's ToString. The view takes the rest of the row's width and is centered in its
        /// height at its own height (it is measured with no height limit), and a taller view makes the row taller.
        /// </summary>
        public Func<FlatTreeItem, View>? RowContentFactory { get; set; }

        /// <summary>
        /// Fills the menu a long press on a row brings up; a menu left empty is not shown. A row that can be dragged lifts
        /// to drag on a long press instead, so it gets no menu.
        /// </summary>
        public Action<FlatTreeItem, IMenu>? RowMenuFactory { get; set; }

        /// <summary>Lets the user press and hold a row, then drag it to reorder or reparent it</summary>
        public bool IsDragDropReorderEnabled
        {
            get => m_isDragDropReorderEnabled;
            set
            {
                m_isDragDropReorderEnabled = value;
                if (!value)
                {
                    this.EndDrag();
                }
            }
        }

        /// <summary>
        /// Whether the tree takes touches. False stops taps, expanding, selecting, dragging, row menus, and scrolling, and
        /// draws each row's chevron (and the tree's own row text) in the disabled color. A ViewGroup's Enabled never reaches
        /// its children, so the tree passes it to its rows itself. Changing it builds the rows again, so a RowContentFactory
        /// can read it as it builds a row.
        /// </summary>
        public override bool Enabled
        {
            get => base.Enabled;
            set
            {
                if ((base.Enabled == value) && ((m_list == null) || (m_list.Enabled == value)))
                {
                    return;
                }

                base.Enabled = value;
                if (m_list != null)
                {
                    m_list.Enabled = value;
                    this.EndDrag();
                    this.ReloadRows();
                }
            }
        }

        /// <summary>Whether a node may be dragged; all may when not set</summary>
        public Func<IObservableTreeNode, bool>? CanDragItem { get; set; }

        /// <summary>Whether a dragged node may land under a parent; any may when not set</summary>
        public Func<IObservableTreeNode, IObservableTreeNode, bool>? CanDropItem { get; set; }

        // Whether rows are lifted and following a finger
        private bool IsDragging => m_draggedItems != null;

        // Whether the tree runs right to left, so its rows are set out mirrored
        private bool IsRightToLeft => this.LayoutDirection == Android.Views.LayoutDirection.Rtl;

        /// <summary>
        /// The colors a row's selection and press are drawn with: the tree's own by default. A PropertyGrid draws its rows
        /// the way WinUI3's grid row does instead. Changing it builds the shown rows again.
        /// </summary>
        internal RowLook Look
        {
            get => m_look;
            set
            {
                m_look = value ?? RowLook.Tree;
                this.ReloadRows();
            }
        }

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Replaces the selection. Single mode keeps only the first item, None selects nothing.</summary>
        public void SetSelection (IEnumerable<FlatTreeItem>? items)
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
        public FlatTreeItem? FindItemForSource (object? source)
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
        public bool TrySelectItemForSource (object? source, bool scrollIntoView = true)
        {
            FlatTreeItem? item = this.FindItemForSource(source);
            if (item == null)
            {
                return false;
            }

            // Expand top down, so each expansion inserts its rows under an already visible parent
            var ancestors = new Stack<FlatTreeItem>();
            for (FlatTreeItem? parent = item.Parent; parent != null; parent = parent.Parent)
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
            this.ReloadRows();
        }

        /// <summary>Scrolls the rows so <paramref name="item"/>'s row shows; nothing happens for a collapsed row, or while the tree is out of a window</summary>
        public void ScrollIntoView (FlatTreeItem item)
        {
            int index = m_store.IndexOf(item);
            if ((index >= 0) && this.IsAttachedToWindow)
            {
                m_list.SmoothScrollToPosition(index);
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

        private int ToPixels (double dp) => (int)Math.Round(dp * m_density);

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

        // Every shown row is built fresh: the list asks for them all again. A focused editor is let go of first, so a field
        // writes its edit while its row can still listen. In a window the old rows are retired rather than torn down, and
        // let go once the list has taken them out; out of a window nothing lays out, so they go now.
        private void ReloadRows ()
        {
            m_list.FindFocus()?.ClearFocus();
            if (this.IsAttachedToWindow)
            {
                m_retiredRows.AddRange(m_liveRows.Values);
                m_liveRows.Clear();
                m_highlightedParentRow = null;
            }
            else
            {
                this.TeardownAllRows();
            }

            m_adapter.NotifyDataSetChanged();
        }

        private void TeardownAllRows ()
        {
            foreach (RowView row in m_liveRows.Values)
            {
                row.Teardown();
            }

            foreach (RowView row in m_retiredRows)
            {
                row.Teardown();
            }

            m_liveRows.Clear();
            m_retiredRows.Clear();
            m_highlightedParentRow = null;
            m_focusToRestore = null;
        }

        // A row the list no longer holds has scrolled off or left the store, so it lets go of its content now; it is built
        // fresh if it comes back. Only called once the list is done laying out or scrolling, since a row is briefly out of
        // the list while the list lays it out again. After a store change, the editor that had the focus gets it back.
        private void ReleaseRowsOffTheList ()
        {
            for (int index = m_retiredRows.Count - 1; index >= 0; --index)
            {
                if (!ReferenceEquals(m_retiredRows[index].Parent, m_list))
                {
                    m_retiredRows[index].Teardown();
                    m_retiredRows.RemoveAt(index);
                }
            }

            if (m_focusToRestore != null)
            {
                View focus = m_focusToRestore;
                m_focusToRestore = null;
                if (focus.IsAttachedToWindow && !focus.HasFocus)
                {
                    focus.RequestFocus();
                }
            }

            List<FlatTreeItem>? released = null;
            foreach (KeyValuePair<FlatTreeItem, RowView> live in m_liveRows)
            {
                if (!ReferenceEquals(live.Value.Parent, m_list))
                {
                    (released ??= new List<FlatTreeItem>()).Add(live.Key);
                }
            }

            if (released == null)
            {
                return;
            }

            foreach (FlatTreeItem item in released)
            {
                RowView row = m_liveRows[item];
                if (ReferenceEquals(row, m_highlightedParentRow))
                {
                    m_highlightedParentRow = null;
                }

                row.Teardown();
                m_liveRows.Remove(item);
            }
        }

        private void ReplaceStoreRoot (FlatTreeItem? newRoot)
        {
            // The old tree has to let go of its source subscriptions, or they duplicate children later
            this.EndDrag();
            this.ApplySelection(new List<FlatTreeItem>());
            m_store.RootNode?.DisposeTree();
            m_store.RootNode = newRoot!;
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
        private static bool RootItemsDiffer (IEnumerable<IObservableTreeNode> current, IObservableTreeNode[]? builtFrom)
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

        private void ListenToRootItems (INotifyCollectionChanged? rootItems)
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

        private void OnRowTapped (FlatTreeItem item)
        {
            if (!this.Enabled || (m_draggedItems != null) || !m_store.Contains(item))
            {
                return;
            }

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

        private long GetRowId (FlatTreeItem item) => m_rowIds.GetValue(item, m_makeRowId).Value;

        private StableId MakeRowId (FlatTreeItem item) => new StableId(++m_nextRowId);

        // The one view for a row: the one it already has, otherwise a new one. The list only asks for a row it is not
        // holding, so one it still holds (asked for between a store change and the layout that follows it) is retired and
        // built again rather than put in the list twice.
        private View GetRowView (int position)
        {
            FlatTreeItem item = m_store[position];
            if (m_liveRows.TryGetValue(item, out RowView? live))
            {
                if (live.Parent == null)
                {
                    return live;
                }

                m_retiredRows.Add(live);
                m_liveRows.Remove(item);
            }

            var row = new RowView(this, item, this.MakeRowContent(item));
            row.SetLifted(this.IsLifted(item));
            m_liveRows.Add(item, row);
            return row;
        }

        private View MakeRowContent (FlatTreeItem item)
        {
            if (this.RowContentFactory != null)
            {
                return this.RowContentFactory(item) ?? new View(this.Context);
            }

            var label = new TextView(this.Context)
            {
                Text = item.Source?.ToString() ?? string.Empty,
                Ellipsize = Android.Text.TextUtils.TruncateAt.End,
            };

            label.SetSingleLine(true);
            label.SetTextColor(ThemeColors.Resolve(this.Context!, this.Enabled ? TreeColors.RowText : TreeColors.RowTextDisabled).ToAndroidColor());
            return label;
        }

        private RowView? FindShownRow (int position)
        {
            if ((position < 0) || (position >= m_store.Count))
            {
                return null;
            }

            return (m_liveRows.TryGetValue(m_store[position], out RowView? row) && ReferenceEquals(row.Parent, m_list)) ? row : null;
        }

        // ------ Long press and drag
        // A press held on a row for the long press delay: it lifts the row to drag when the row can be dragged, and opens
        // the row's menu otherwise, if it has one
        private void OnRowLongPressed (int position)
        {
            if (!this.Enabled || (position < 0) || (position >= m_store.Count))
            {
                return;
            }

            FlatTreeItem pressed = m_store[position];
            if (this.CanStartDrag(pressed))
            {
                this.BeginDrag(pressed);
                return;
            }

            if ((this.RowMenuFactory != null) && (this.FindShownRow(position) is RowView row))
            {
                var menu = new PopupMenu(this.Context!, row);
                this.RowMenuFactory(pressed, menu.Menu!);
                if (menu.Menu!.HasVisibleItems)
                {
                    m_list.CancelTouch();
                    row.PerformHapticFeedback(FeedbackConstants.LongPress);
                    m_shownMenu?.Dismiss();
                    m_shownMenu = menu;
                    menu.Show();
                }
            }
        }

        private bool CanStartDrag (FlatTreeItem item)
        {
            // Roots never drag: a row at depth 0 has nowhere above it to go
            return m_isDragDropReorderEnabled
                && this.Enabled
                && (item.TreeDepth > 0)
                && !item.IsFalseRoot
                && ((this.CanDragItem == null) || this.CanDragItem(item.Source));
        }

        // A selected row drags the whole selection (the rows of it that can be dragged), another row drags alone
        private void BeginDrag (FlatTreeItem pressed)
        {
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
            m_dragStartX = m_list.PressX;
            m_dragFingerX = m_list.PressX;
            m_dragFingerY = m_list.PressY;
            m_list.CancelTouch();
            this.Parent?.RequestDisallowInterceptTouchEvent(true);
            this.RefreshLiftedRows();
            this.PerformHapticFeedback(FeedbackConstants.LongPress);

            m_autoScrollTicker = new AutoScrollTicker(this);
            m_list.PostOnAnimation(m_autoScrollTicker);
            this.UpdateDrag();
        }

        private void OnDragMoved (float x, float y)
        {
            m_dragFingerX = x;
            m_dragFingerY = y;
            this.UpdateDrag();
        }

        private void OnDragReleased ()
        {
            FlatTreeDropTarget? target = m_shownDropTarget;
            FlatTreeItem[]? dragged = m_draggedItems;
            this.EndDrag();
            if ((dragged != null) && (target != null) && target.IsValid)
            {
                this.CompleteDrop(dragged, target);
            }
        }

        private void EndDrag ()
        {
            if (m_autoScrollTicker != null)
            {
                m_list.RemoveCallbacks(m_autoScrollTicker);
                m_autoScrollTicker = null;
            }

            if (m_draggedItems == null)
            {
                return;
            }

            m_draggedItems = null;
            this.ClearDropIndicator();
            this.RefreshLiftedRows();
        }

        private bool IsLifted (FlatTreeItem item) => (m_draggedItems != null) && (Array.IndexOf(m_draggedItems, item) >= 0);

        private void RefreshLiftedRows ()
        {
            foreach (RowView row in m_liveRows.Values)
            {
                row.SetLifted(this.IsLifted(row.Item));
            }
        }

        // The finger's place against the rows, then the drop that place means, then the indicator for it
        private void UpdateDrag ()
        {
            if ((m_draggedItems == null) || (m_store.Count == 0) || !this.FindHoveredRow(m_dragFingerY, out int hoverIndex, out double yFraction))
            {
                this.ClearDropIndicator();
                return;
            }

            // Deeper is toward the content, which is leftward when the rows run right to left
            double fingerDeltaX = (m_dragFingerX - m_dragStartX) / m_density;
            double cursorX = FlatTreeDragDropManager.ComputeTouchDragCursorX(
                m_draggedDepth,
                this.IsRightToLeft ? -fingerDeltaX : fingerDeltaX,
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

            if (!FlatTreeDragDropManager.ValidateDropTarget(m_draggedItems, target, this.CanDropItem!))
            {
                target = FlatTreeDropTarget.Invalid;
            }

            m_shownDropTarget = target;
            m_shownHoverIndex = hoverIndex;
            m_shownYFraction = yFraction;
            this.ShowParentHighlight(target);
            m_list.Invalidate();
        }

        // Scrolls the rows while the finger is held near the top or bottom edge, then works the drop out again, since the
        // finger is now over a different row
        private void OnAutoScrollTick ()
        {
            if (m_draggedItems == null)
            {
                return;
            }

            // A scroll from code skips the layout a pending store change asks for, which a touch scroll does first, and
            // would have the list ask for rows against stale positions; so the scroll waits for that layout
            if (m_list.IsLayoutRequested)
            {
                m_list.PostOnAnimation(m_autoScrollTicker);
                return;
            }

            double step = FlatTreeDragDropManager.ComputeAutoScrollStep(
                m_dragFingerY / m_density,
                m_list.Height / m_density,
                MobileLook.FlatTreeList.DragAutoScrollEdge,
                MobileLook.FlatTreeList.DragAutoScrollMaxStep
            );

            if (step != 0.0)
            {
                // At least a pixel, so a slow step near the inner edge of the zone still moves
                int pixels = (int)Math.Round(step * m_density);
                if (pixels == 0)
                {
                    pixels = Math.Sign(step);
                }

                if (m_list.CanScrollList(pixels))
                {
                    m_list.ScrollListBy(pixels);
                    this.UpdateDrag();
                }
            }

            if (m_autoScrollTicker != null)
            {
                m_list.PostOnAnimation(m_autoScrollTicker);
            }
        }

        // The row under the finger, and how far down it the finger is. Above the first row is the first row's top, and past
        // the last row is the last row's bottom, as on the other stacks. Rows off screen are past the shown ones' edges.
        private bool FindHoveredRow (float y, out int hoverIndex, out double yFraction)
        {
            hoverIndex = 0;
            yFraction = 0.0;
            int shown = m_list.ChildCount;
            if (shown == 0)
            {
                return false;
            }

            View first = m_list.GetChildAt(0)!;
            View last = m_list.GetChildAt(shown - 1)!;
            if (y < first.Top)
            {
                hoverIndex = m_list.FirstVisiblePosition;
                yFraction = 0.0;
                return true;
            }

            if (y >= last.Bottom)
            {
                hoverIndex = Math.Min(m_list.FirstVisiblePosition + shown - 1, m_store.Count - 1);
                yFraction = 1.0;
                return true;
            }

            for (int index = 0; index < shown; ++index)
            {
                View row = m_list.GetChildAt(index)!;
                if (y < row.Bottom)
                {
                    hoverIndex = Math.Min(m_list.FirstVisiblePosition + index, m_store.Count - 1);
                    yFraction = row.Height > 0 ? Math.Clamp((y - row.Top) / row.Height, 0.0, 1.0) : 0.5;
                    return true;
                }
            }

            return false;
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

        private void ShowParentHighlight (FlatTreeDropTarget target)
        {
            RowView? parentRow = target.IsValid ? this.FindShownRow(FlatTreeDragDropManager.FindParentStoreIndex(m_store, target)) : null;
            if (ReferenceEquals(parentRow, m_highlightedParentRow))
            {
                return;
            }

            m_highlightedParentRow?.SetDropHighlight(false);
            m_highlightedParentRow = parentRow;
            m_highlightedParentRow?.SetDropHighlight(true);
        }

        private void ClearDropIndicator ()
        {
            m_shownDropTarget = null;
            m_highlightedParentRow?.SetDropHighlight(false);
            m_highlightedParentRow = null;
            m_list?.Invalidate();
        }

        // The insertion line starts at the content column of the target depth, and a dashed L runs to it from the new
        // parent's chevron, which is highlighted: the same pieces and rules as the WinUI3, AppKit, and UIKit trees' drag
        // overlays. It is drawn over the rows the list shows, so a line or parent off screen draws to the list's edge.
        private void DrawDropIndicator (Canvas canvas, DropPaints paints)
        {
            FlatTreeDropTarget? target = m_shownDropTarget;
            FlatTreeInsertionLine? line = (target != null)
                ? FlatTreeDragDropManager.ComputeInsertionLine(m_store, target, m_shownHoverIndex, m_shownYFraction, m_siblingDisplayOrder)
                : null;
            if ((target == null) || (line == null))
            {
                return;
            }

            float lineY;
            if (this.FindShownRow(line.Row) is RowView lineRow)
            {
                lineY = line.IsBelowRow ? lineRow.Bottom : lineRow.Top;
            }
            else
            {
                lineY = (line.Row < m_list.FirstVisiblePosition) ? 0f : m_list.Height;
            }

            // Measured from the leading edge, which is the right one when the rows run right to left
            bool isRightToLeft = this.IsRightToLeft;
            float lineStart = this.ToPixels(MobileLook.FlatTreeList.ContentLeadingInset + (target.TargetDepth * m_treeDepthIndentSize) + MobileLook.FlatTreeList.ExpanderColumnWidth);
            float lineEnd = m_list.Width - this.ToPixels(MobileLook.FlatTreeList.DragInsertionLineEndInset);
            float lineX = isRightToLeft ? m_list.Width - lineStart : lineStart;
            float thickness = this.ToPixels(MobileLook.FlatTreeList.DragInsertionLineThickness);
            if (lineEnd > lineStart)
            {
                float left = isRightToLeft ? m_list.Width - lineEnd : lineStart;
                float right = isRightToLeft ? m_list.Width - lineStart : lineEnd;
                canvas.DrawRect(left, lineY - (thickness / 2f), right, lineY + (thickness / 2f), paints.Line);
            }

            int parentIndex = FlatTreeDragDropManager.FindParentStoreIndex(m_store, target);
            if (parentIndex < 0)
            {
                return;
            }

            // The connector starts at the parent's chevron itself, measured from the drawn glyph, so it meets the tip; the
            // estimate is only for a parent row that is not on screen
            PointF anchor;
            if (this.FindShownRow(parentIndex) is RowView parentRow)
            {
                anchor = parentRow.GetChevronAnchor();
            }
            else
            {
                float chevronCenter = this.ToPixels(MobileLook.FlatTreeList.ContentLeadingInset + ((target.TargetDepth - 1) * m_treeDepthIndentSize) + (MobileLook.FlatTreeList.ExpanderColumnWidth / 2.0));
                anchor = new PointF(isRightToLeft ? m_list.Width - chevronCenter : chevronCenter, (parentIndex < m_list.FirstVisiblePosition) ? 0f : m_list.Height);
            }

            paints.Connector.Reset();
            paints.Connector.MoveTo(anchor.X, Math.Min(anchor.Y, lineY));
            paints.Connector.LineTo(anchor.X, Math.Max(anchor.Y, lineY));
            paints.Connector.LineTo(lineX, Math.Max(anchor.Y, lineY));
            canvas.DrawPath(paints.Connector, paints.ConnectorStroke);
        }

        // ===========[ Subclasses/structs ]====================================
        /// <summary>
        /// The colors a row's selection and press are drawn with. A null SelectionFill draws a selected row with its outline
        /// and strip alone, as WinUI3's PropertyGrid row does.
        /// </summary>
        internal sealed record RowLook (ThemedColor? SelectionFill, ThemedColor SelectionAccent, ThemedColor PressedFill, ThemedColor PressedBorder)
        {
            /// <summary>A plain tree's rows, as WinUI3's FlatTreeListControl draws them</summary>
            public static readonly RowLook Tree = new RowLook(
                TreeColors.SelectionFill,
                TreeColors.SelectionAccent,
                TreeColors.PressedFill,
                TreeColors.PressedBorder
            );
        }

        // Every color the tree paints: the AJut theme's for the look key when the theme is loaded, otherwise the literal
        // for a light or a dark UI. The accent parts take the blue the other AJut Android controls highlight with.
        internal static class TreeColors
        {
            public static readonly ThemedColor SelectionFill = ThemedColor.FromArgb(MobileLook.FlatTreeList.SelectionFill, 0x24186DBA, 0x3D4CC2FF);
            public static readonly ThemedColor SelectionAccent = ThemedColor.FromArgb(MobileLook.FlatTreeList.SelectionAccent, 0xFF186DBA, 0xFF4CC2FF);
            public static readonly ThemedColor PressedFill = ThemedColor.FromArgb(MobileLook.FlatTreeList.PressedFill, 0x0F000000, 0x14FFFFFF);
            public static readonly ThemedColor PressedBorder = ThemedColor.FromArgb(MobileLook.FlatTreeList.PressedBorder, 0x1F000000, 0x24FFFFFF);
            public static readonly ThemedColor ExpanderGlyph = ThemedColor.FromArgb(MobileLook.FlatTreeList.ExpanderGlyph, 0x9E000000, 0xC5FFFFFF);
            public static readonly ThemedColor ExpanderGlyphDisabled = ThemedColor.FromArgb(MobileLook.FlatTreeList.ExpanderGlyphDisabled, 0x5C000000, 0x5DFFFFFF);
            public static readonly ThemedColor ExpanderPressedFill = ThemedColor.FromArgb(MobileLook.FlatTreeList.ExpanderPressedFill, 0x0F000000, 0x17FFFFFF);
            public static readonly ThemedColor RowText = ThemedColor.FromArgb(MobileLook.FlatTreeList.RowText, 0xE4000000, 0xFFFFFFFF);
            public static readonly ThemedColor RowTextDisabled = ThemedColor.FromArgb(MobileLook.FlatTreeList.RowTextDisabled, 0x5C000000, 0x5DFFFFFF);
            public static readonly ThemedColor DragInsertionLine = ThemedColor.FromArgb(MobileLook.FlatTreeList.DragInsertionLine, 0xFF186DBA, 0xFF4CC2FF);
            public static readonly ThemedColor DragParentConnector = ThemedColor.FromArgb(MobileLook.FlatTreeList.DragParentConnector, 0x80186DBA, 0x804CC2FF);
            public static readonly ThemedColor DragTargetHighlight = ThemedColor.FromArgb(MobileLook.FlatTreeList.DragTargetHighlight, 0xFF186DBA, 0xFF4CC2FF);
        }

        // One id per tree item for the adapter's stable ids, kept beside the item and gone with it
        private sealed class StableId
        {
            public StableId (long value)
            {
                this.Value = value;
            }

            public long Value { get; }
        }

        // The paints the drop indicator draws with, made once per drag look
        private sealed class DropPaints
        {
            public DropPaints (Context context, float density)
            {
                this.Line = new Paint(PaintFlags.AntiAlias);
                this.Line.SetStyle(Paint.Style.Fill);
                this.Line.Color = ThemeColors.Resolve(context, TreeColors.DragInsertionLine).ToAndroidColor();

                this.ConnectorStroke = new Paint(PaintFlags.AntiAlias);
                this.ConnectorStroke.SetStyle(Paint.Style.Stroke);
                this.ConnectorStroke.StrokeWidth = MobileLook.FlatTreeList.DragParentConnectorThickness * density;
                this.ConnectorStroke.Color = ThemeColors.Resolve(context, TreeColors.DragParentConnector).ToAndroidColor();
                this.ConnectorStroke.SetPathEffect(new DashPathEffect(
                    new[] { MobileLook.FlatTreeList.DragParentConnectorDash * density, MobileLook.FlatTreeList.DragParentConnectorGap * density },
                    0f
                ));
            }

            public Paint Line { get; }
            public Paint ConnectorStroke { get; }
            public Path Connector { get; } = new Path();
        }

        // The adapter: as many rows as the store has while the tree follows it (none out of a window), each its tree
        // item's one view (see the class comment)
        private sealed class RowAdapter : BaseAdapter
        {
            private readonly FlatTreeListView m_owner;

            public RowAdapter (FlatTreeListView owner)
            {
                m_owner = owner;
            }

            public override int Count => m_owner.m_isFollowingStore ? m_owner.m_store.Count : 0;

            public override bool HasStableIds => true;

            public override int ViewTypeCount => 1;

            public override Java.Lang.Object? GetItem (int position) => null;

            public override long GetItemId (int position) => m_owner.GetRowId(m_owner.m_store[position]);

            // Never recycled into another row, so a row's editors only ever show their own property
            public override int GetItemViewType (int position) => AdapterView.ItemViewTypeIgnore;

            public override View GetView (int position, View? convertView, ViewGroup? parent) => m_owner.GetRowView(position);
        }

        // The list: lays the rows out, takes a long press to drag or open a menu, takes the drag's touches, takes no
        // touches while the tree is disabled, and draws the drop indicator over the rows
        private sealed class TreeListView : ListView
        {
            // The platform's slop is always there; this is only what a context with no view configuration falls back to
            private const int kFallbackTouchSlop = 8;

            private readonly FlatTreeListView m_owner;
            private readonly LongPressCheck m_longPressCheck;
            private readonly int m_touchSlop;
            private DropPaints? m_dropPaints;
            private bool m_isPressPending;

            public TreeListView (Context context, FlatTreeListView owner) : base(context)
            {
                m_owner = owner;
                m_longPressCheck = new LongPressCheck(this);
                m_touchSlop = ViewConfiguration.Get(context)?.ScaledTouchSlop ?? kFallbackTouchSlop;
            }

            /// <summary>Where across the list the current press went down, in pixels</summary>
            public float PressX { get; private set; }

            /// <summary>Where down the list the current press went down, in pixels</summary>
            public float PressY { get; private set; }

            public override bool OnInterceptTouchEvent (MotionEvent? e)
            {
                if ((e == null) || !m_owner.Enabled)
                {
                    return true;
                }

                // A drag takes every touch from the row it started on, which is sent a cancel. The touch that takes it is
                // the drag's own as well, since one the list takes here never reaches its OnTouchEvent.
                if (m_owner.IsDragging && this.HandleDragTouch(e))
                {
                    return true;
                }

                this.TrackPress(e);
                return base.OnInterceptTouchEvent(e);
            }

            public override bool OnTouchEvent (MotionEvent? e)
            {
                if ((e == null) || !m_owner.Enabled)
                {
                    return true;
                }

                if (m_owner.IsDragging && this.HandleDragTouch(e))
                {
                    return true;
                }

                this.TrackPress(e);
                return base.OnTouchEvent(e);
            }

            // A child that takes a gesture for itself (a switch's thumb, a scrolling child) stops the list seeing the rest
            // of it, so that press must not become a long press
            public override void RequestDisallowInterceptTouchEvent (bool disallowIntercept)
            {
                if (disallowIntercept)
                {
                    this.CancelPendingPress();
                }

                base.RequestDisallowInterceptTouchEvent(disallowIntercept);
            }

            /// <summary>Ends the list's own handling of the press under way, so it neither scrolls nor clicks for it</summary>
            public void CancelTouch ()
            {
                this.CancelPendingPress();
                long now = SystemClock.UptimeMillis();
                MotionEvent cancel = MotionEvent.Obtain(now, now, MotionEventActions.Cancel, this.PressX, this.PressY, MetaKeyStates.None)!;
                base.OnTouchEvent(cancel);
                cancel.Recycle();
                for (int index = 0; index < this.ChildCount; ++index)
                {
                    this.GetChildAt(index)?.CancelLongPress();
                }
            }

            protected override void LayoutChildren ()
            {
                base.LayoutChildren();
                m_owner.ReleaseRowsOffTheList();
            }

            protected override void DispatchDraw (Canvas canvas)
            {
                base.DispatchDraw(canvas);
                if (m_owner.IsDragging)
                {
                    m_dropPaints ??= new DropPaints(this.Context!, m_owner.m_density);
                    m_owner.DrawDropIndicator(canvas, m_dropPaints);
                }
                else
                {
                    // Made again for the next drag, in case the theme or night mode changed between them
                    m_dropPaints = null;
                }
            }

            protected override void OnDetachedFromWindow ()
            {
                this.CancelPendingPress();
                base.OnDetachedFromWindow();
            }

            // A touch while rows are lifted: a move moves them, a lift drops them, and a cancel puts them back. A new press
            // means the lift that ended the drag never came, so the drag ends and the press goes on as any other.
            private bool HandleDragTouch (MotionEvent e)
            {
                switch (e.ActionMasked)
                {
                    case MotionEventActions.Down:
                        m_owner.EndDrag();
                        return false;

                    case MotionEventActions.Move:
                        m_owner.OnDragMoved(e.GetX(), e.GetY());
                        break;

                    case MotionEventActions.Up:
                        m_owner.OnDragReleased();
                        break;

                    case MotionEventActions.Cancel:
                        m_owner.EndDrag();
                        break;
                }

                return true;
            }

            // The long press timer: started when a press goes down, and stopped once it moves past the slop, lifts, or is
            // canceled. A press lasting the delay is the row's long press. A press on something in the row that takes a
            // press of its own starts no timer, since holding it is that part's own (a number's step button repeats).
            private void TrackPress (MotionEvent e)
            {
                switch (e.ActionMasked)
                {
                    case MotionEventActions.Down:
                        this.PressX = e.GetX();
                        this.PressY = e.GetY();
                        this.CancelPendingPress();
                        if ((m_owner.IsDragDropReorderEnabled || (m_owner.RowMenuFactory != null)) && !this.IsOverAPressOfItsOwn(this.PressX, this.PressY))
                        {
                            m_isPressPending = true;
                            this.PostDelayed(m_longPressCheck, (long)MobileLook.FlatTreeList.DragLongPressDelay.TotalMilliseconds);
                        }

                        break;

                    case MotionEventActions.Move:
                        if (m_isPressPending && ((Math.Abs(e.GetX() - this.PressX) > m_touchSlop) || (Math.Abs(e.GetY() - this.PressY) > m_touchSlop)))
                        {
                            this.CancelPendingPress();
                        }

                        break;

                    case MotionEventActions.Up:
                    case MotionEventActions.Cancel:
                    case MotionEventActions.PointerDown:
                        this.CancelPendingPress();
                        break;
                }
            }

            private void CancelPendingPress ()
            {
                if (m_isPressPending)
                {
                    m_isPressPending = false;
                    this.RemoveCallbacks(m_longPressCheck);
                }
            }

            private bool IsOverAPressOfItsOwn (float x, float y)
            {
                int position = this.PointToPosition((int)x, (int)y);
                View? shown = (position != AdapterView.InvalidPosition) ? this.GetChildAt(position - this.FirstVisiblePosition) : null;
                return (shown is RowView row) && row.HasPressOfItsOwnAt(x - row.Left, y - row.Top);
            }

            private void OnLongPressDelayElapsed ()
            {
                if (!m_isPressPending)
                {
                    return;
                }

                m_isPressPending = false;
                int position = this.PointToPosition((int)this.PressX, (int)this.PressY);
                if (position != AdapterView.InvalidPosition)
                {
                    m_owner.OnRowLongPressed(position);
                }
            }

            // What the long press delay runs: the list's own check, with nothing captured
            private sealed class LongPressCheck : Java.Lang.Object, Java.Lang.IRunnable
            {
                private readonly TreeListView m_list;

                public LongPressCheck (TreeListView list)
                {
                    m_list = list;
                }

                public void Run () => m_list.OnLongPressDelayElapsed();
            }
        }

        // What runs each frame while a drag is on, to scroll the list near its edges. It is posted again each frame and
        // removed when the drag ends.
        private sealed class AutoScrollTicker : Java.Lang.Object, Java.Lang.IRunnable
        {
            private readonly FlatTreeListView m_owner;

            public AutoScrollTicker (FlatTreeListView owner)
            {
                m_owner = owner;
            }

            public void Run () => m_owner.OnAutoScrollTick();
        }

        // One row: the selection fill, outline and strip, the pressed tint, the chevron across the indent, then the row's
        // own content. It draws from its tree item's IsSelected and its own press, takes its own taps (a ListView item
        // click never comes for a row with a focusable editor in it), and lets go of its item and its content in Teardown.
        private sealed class RowView : ViewGroup
        {
            private readonly FlatTreeListView m_owner;
            private readonly FlatTreeItem m_item;
            private readonly View m_content;
            private readonly ExpanderView m_expander;
            private readonly RowLook m_look;
            private readonly Paint m_fill = new Paint(PaintFlags.AntiAlias);
            private readonly Paint m_outline = new Paint(PaintFlags.AntiAlias);
            private readonly float m_density;
            private readonly int m_leadingWidth;
            private readonly int m_trailingInset;
            private readonly int m_rowSpacing;
            private readonly int m_minHeight;
            private readonly int m_fixedHeight;
            private bool m_isTornDown;

            public RowView (FlatTreeListView owner, FlatTreeItem item, View content) : base(owner.Context)
            {
                m_owner = owner;
                m_item = item;
                m_content = content;
                m_look = owner.m_look;
                m_density = owner.m_density;
                m_leadingWidth = owner.ToPixels(MobileLook.FlatTreeList.ContentLeadingInset + item.IndentWidth + MobileLook.FlatTreeList.ExpanderColumnWidth);
                m_trailingInset = owner.ToPixels(MobileLook.FlatTreeList.ContentTrailingInset);
                m_rowSpacing = owner.ToPixels(owner.m_rowSpacing);
                m_minHeight = owner.ToPixels(MobileLook.FlatTreeList.MinRowHeight);
                m_fixedHeight = double.IsNaN(owner.m_fixedRowHeight) ? -1 : owner.ToPixels(owner.m_fixedRowHeight);

                this.LayoutParameters = new AbsListView.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
                this.SetWillNotDraw(false);
                this.Clickable = true;
                this.Enabled = owner.Enabled;
                m_outline.SetStyle(Paint.Style.Stroke);
                m_outline.StrokeWidth = MobileLook.FlatTreeList.OutlineThickness * m_density;

                m_expander = new ExpanderView(owner.Context!, item, m_density) { Enabled = owner.Enabled };
                this.AddView(m_expander);
                this.AddView(m_content);

                this.Click += this.OnClicked;
                m_item.PropertyChanged += this.OnItemPropertyChanged;
                this.RefreshExpander();
                this.RefreshSelected();
            }

            public FlatTreeItem Item => m_item;

            public void Teardown ()
            {
                if (m_isTornDown)
                {
                    return;
                }

                m_isTornDown = true;
                this.Click -= this.OnClicked;
                m_item.PropertyChanged -= this.OnItemPropertyChanged;
                m_expander.Teardown();
                (m_content as IFlatTreeRowContent)?.Teardown();
            }

            public void SetLifted (bool isLifted)
            {
                this.Alpha = isLifted ? MobileLook.FlatTreeList.DraggedRowOpacity : 1f;
            }

            /// <summary>Draws this row's chevron in the drag highlight while a drop would land under this row</summary>
            public void SetDropHighlight (bool isHighlighted) => m_expander.IsDropHighlighted = isHighlighted;

            /// <summary>The chevron's connector anchor (the tip of its glyph) in the list's pixels</summary>
            public PointF GetChevronAnchor ()
            {
                PointF inExpander = m_expander.ConnectorAnchor;
                return new PointF(this.Left + m_expander.Left + inExpander.X, this.Top + m_expander.Top + inExpander.Y);
            }

            /// <summary>
            /// Whether a press at (x, y), in this row's pixels, lands on a part of its content that takes a press of its own:
            /// anything clickable or long clickable (a button, a switch, an editor's step button), or a text field
            /// </summary>
            public bool HasPressOfItsOwnAt (float x, float y) => HasPressOfItsOwn(m_content, x - m_content.Left, y - m_content.Top);

            private static bool HasPressOfItsOwn (View view, float x, float y)
            {
                if ((view.Visibility != ViewStates.Visible) || (x < 0) || (y < 0) || (x >= view.Width) || (y >= view.Height))
                {
                    return false;
                }

                if (view.Clickable || view.LongClickable || (view is EditText))
                {
                    return true;
                }

                if (view is ViewGroup group)
                {
                    for (int index = group.ChildCount - 1; index >= 0; --index)
                    {
                        View? child = group.GetChildAt(index);
                        if ((child != null) && HasPressOfItsOwn(child, x + group.ScrollX - child.Left, y + group.ScrollY - child.Top))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            // The row is the list's width; its height is the content's (centered in it) or a comfortable touch target,
            // whichever is taller, or the fixed row height when there is one, plus the space between rows
            protected override void OnMeasure (int widthMeasureSpec, int heightMeasureSpec)
            {
                int width = MeasureSpec.GetSize(widthMeasureSpec);
                int contentWidth = Math.Max(0, width - m_leadingWidth - m_trailingInset);
                m_content.Measure(
                    MeasureSpec.MakeMeasureSpec(contentWidth, MeasureSpecMode.Exactly),
                    MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified)
                );

                int fillHeight = (m_fixedHeight >= 0)
                    ? Math.Max(0, m_fixedHeight - m_rowSpacing)
                    : Math.Max(m_minHeight - m_rowSpacing, m_content.MeasuredHeight);
                m_expander.Measure(
                    MeasureSpec.MakeMeasureSpec(m_leadingWidth, MeasureSpecMode.Exactly),
                    MeasureSpec.MakeMeasureSpec(fillHeight, MeasureSpecMode.Exactly)
                );

                this.SetMeasuredDimension(width, fillHeight + m_rowSpacing);
            }

            // The chevron's span leads and the content follows it: from the left, or mirrored from the right when the rows run
            // right to left
            protected override void OnLayout (bool changed, int left, int top, int right, int bottom)
            {
                int width = right - left;
                int fillTop = m_rowSpacing / 2;
                int fillHeight = m_expander.MeasuredHeight;
                int contentTop = fillTop + ((fillHeight - m_content.MeasuredHeight) / 2);
                int contentBottom = contentTop + m_content.MeasuredHeight;
                if (this.LayoutDirection == Android.Views.LayoutDirection.Rtl)
                {
                    m_expander.Layout(width - m_leadingWidth, fillTop, width, fillTop + fillHeight);
                    m_content.Layout(width - m_leadingWidth - m_content.MeasuredWidth, contentTop, width - m_leadingWidth, contentBottom);
                }
                else
                {
                    m_expander.Layout(0, fillTop, m_leadingWidth, fillTop + fillHeight);
                    m_content.Layout(m_leadingWidth, contentTop, m_leadingWidth + m_content.MeasuredWidth, contentBottom);
                }
            }

            // Selected: the selection fill, outlined, with the strip. Pressed: the pressed tint over that, outlined only when
            // the row isn't selected, as WinUI3's PressedSelected keeps the selection's outline alone.
            protected override void OnDraw (Canvas canvas)
            {
                base.OnDraw(canvas);
                Context context = this.Context!;
                float halfOutline = m_outline.StrokeWidth / 2f;
                float fillTop = m_rowSpacing / 2;
                float fillBottom = fillTop + m_expander.MeasuredHeight;
                bool isSelected = m_item.IsSelected;
                bool isPressed = this.Pressed && !m_owner.IsDragging;
                if (isSelected)
                {
                    AndroidColor accent = ThemeColors.Resolve(context, m_look.SelectionAccent).ToAndroidColor();
                    if (m_look.SelectionFill is ThemedColor selectionFill)
                    {
                        m_fill.Color = ThemeColors.Resolve(context, selectionFill).ToAndroidColor();
                        canvas.DrawRect(0, fillTop, this.Width, fillBottom, m_fill);
                    }

                    m_outline.Color = accent;
                    canvas.DrawRect(halfOutline, fillTop + halfOutline, this.Width - halfOutline, fillBottom - halfOutline, m_outline);
                    // The strip runs down the leading edge
                    float stripWidth = MobileLook.FlatTreeList.SelectionStripWidth * m_density;
                    float stripLeft = (this.LayoutDirection == Android.Views.LayoutDirection.Rtl) ? this.Width - stripWidth : 0f;
                    m_fill.Color = accent;
                    canvas.DrawRect(stripLeft, fillTop, stripLeft + stripWidth, fillBottom, m_fill);
                }

                if (isPressed)
                {
                    m_fill.Color = ThemeColors.Resolve(context, m_look.PressedFill).ToAndroidColor();
                    canvas.DrawRect(0, fillTop, this.Width, fillBottom, m_fill);
                    if (!isSelected)
                    {
                        m_outline.Color = ThemeColors.Resolve(context, m_look.PressedBorder).ToAndroidColor();
                        canvas.DrawRect(halfOutline, fillTop + halfOutline, this.Width - halfOutline, fillBottom - halfOutline, m_outline);
                    }
                }
            }

            protected override void DrawableStateChanged ()
            {
                base.DrawableStateChanged();
                this.Invalidate();
            }

            private void OnClicked (object? sender, EventArgs e)
            {
                m_owner.OnRowTapped(m_item);
            }

            private void OnItemPropertyChanged (object? sender, PropertyChangedEventArgs e)
            {
                switch (e.PropertyName)
                {
                    case nameof(FlatTreeItem.IsExpanded):
                    case nameof(FlatTreeItem.IsExpandable):
                        this.RefreshExpander();
                        break;

                    case nameof(FlatTreeItem.IsSelected):
                        this.RefreshSelected();
                        break;
                }
            }

            private void RefreshExpander ()
            {
                m_expander.Visibility = m_item.IsExpandable ? ViewStates.Visible : ViewStates.Invisible;
                m_expander.IsExpanded = m_item.IsExpanded;
            }

            private void RefreshSelected ()
            {
                this.Selected = m_item.IsSelected;
                this.Invalidate();
            }
        }

        // The chevron, and the tap target that toggles it: the row's whole leading span (its indent and the chevron's
        // column) at the row's full height, so a finger finds it at any depth. It handles its own taps (a self
        // subscription, removed in Teardown) and holds only its tree item, never its row or the tree.
        private sealed class ExpanderView : View
        {
            private readonly FlatTreeItem m_item;
            private readonly float m_density;
            private readonly Paint m_glyph = new Paint(PaintFlags.AntiAlias);
            private readonly Paint m_pressedFill = new Paint(PaintFlags.AntiAlias);
            private readonly Path m_glyphPath = new Path();
            private readonly RectF m_pressedRect = new RectF();
            private bool m_isExpanded;
            private bool m_isDropHighlighted;

            public ExpanderView (Context context, FlatTreeItem item, float density) : base(context)
            {
                m_item = item;
                m_density = density;
                m_glyph.SetStyle(Paint.Style.Stroke);
                m_glyph.StrokeWidth = MobileLook.FlatTreeList.ExpanderGlyphStrokeThickness * density;
                m_glyph.StrokeCap = Paint.Cap.Round;
                m_glyph.StrokeJoin = Paint.Join.Round;
                m_pressedFill.SetStyle(Paint.Style.Fill);

                this.Clickable = true;
                this.Click += this.OnClicked;
            }

            public bool IsExpanded
            {
                get => m_isExpanded;
                set
                {
                    m_isExpanded = value;
                    this.ContentDescription = value ? "Collapse" : "Expand";
                    this.Invalidate();
                }
            }

            public bool IsDropHighlighted
            {
                get => m_isDropHighlighted;
                set
                {
                    m_isDropHighlighted = value;
                    this.Invalidate();
                }
            }

            /// <summary>
            /// Where a drop connector meets this chevron, in its own pixels: the tip of the down pointing (expanded) chevron,
            /// or the bottom of the right pointing one
            /// </summary>
            public PointF ConnectorAnchor
            {
                get
                {
                    float half = MobileLook.FlatTreeList.ExpanderGlyphHalfLength * m_density;
                    float belowCenter = m_isExpanded ? half / 2f : half;
                    return new PointF(this.GlyphCenterX, (this.Height / 2f) + belowCenter + (m_glyph.StrokeWidth / 2f));
                }
            }

            // The chevron sits in the column at the end of the span, next to the row's content: the right end, or the left
            // when the rows run right to left
            private float GlyphCenterX
            {
                get
                {
                    float halfColumn = MobileLook.FlatTreeList.ExpanderColumnWidth * m_density / 2f;
                    return (this.LayoutDirection == Android.Views.LayoutDirection.Rtl) ? halfColumn : this.Width - halfColumn;
                }
            }

            public void Teardown ()
            {
                this.Click -= this.OnClicked;
            }

            protected override void OnDraw (Canvas canvas)
            {
                base.OnDraw(canvas);
                Context context = this.Context!;
                float centerX = this.GlyphCenterX;
                float centerY = this.Height / 2f;
                float half = MobileLook.FlatTreeList.ExpanderGlyphHalfLength * m_density;

                // The pressed fill is a rounded square on the chevron's column, as WinUI3's chevron button is
                if (this.Pressed)
                {
                    const float kPressedFillInset = 2f;
                    float side = Math.Min(MobileLook.FlatTreeList.ExpanderColumnWidth * m_density, this.Height) - (2f * kPressedFillInset * m_density);
                    float radius = MobileLook.FlatTreeList.ExpanderPressedCornerRadius * m_density;
                    m_pressedRect.Set(centerX - (side / 2f), centerY - (side / 2f), centerX + (side / 2f), centerY + (side / 2f));
                    m_pressedFill.Color = ThemeColors.Resolve(context, TreeColors.ExpanderPressedFill).ToAndroidColor();
                    canvas.DrawRoundRect(m_pressedRect, radius, radius, m_pressedFill);
                }

                m_glyphPath.Reset();
                if (m_isExpanded)
                {
                    // Down
                    m_glyphPath.MoveTo(centerX - half, centerY - (half / 2f));
                    m_glyphPath.LineTo(centerX, centerY + (half / 2f));
                    m_glyphPath.LineTo(centerX + half, centerY - (half / 2f));
                }
                else
                {
                    // Toward the content: right, or left when the rows run right to left
                    float toward = (this.LayoutDirection == Android.Views.LayoutDirection.Rtl) ? -1f : 1f;
                    m_glyphPath.MoveTo(centerX - (toward * half / 2f), centerY - half);
                    m_glyphPath.LineTo(centerX + (toward * half / 2f), centerY);
                    m_glyphPath.LineTo(centerX - (toward * half / 2f), centerY + half);
                }

                ThemedColor glyphColor = !this.Enabled
                    ? TreeColors.ExpanderGlyphDisabled
                    : (m_isDropHighlighted ? TreeColors.DragTargetHighlight : TreeColors.ExpanderGlyph);
                m_glyph.Color = ThemeColors.Resolve(context, glyphColor).ToAndroidColor();
                canvas.DrawPath(m_glyphPath, m_glyph);
            }

            protected override void DrawableStateChanged ()
            {
                base.DrawableStateChanged();
                this.Invalidate();
            }

            private void OnClicked (object? sender, EventArgs e)
            {
                m_item.IsExpanded = !m_item.IsExpanded;
            }
        }
    }

    /// <summary>A row content view that has something to let go of when its row leaves the list (bindings, subscriptions)</summary>
    public interface IFlatTreeRowContent
    {
        void Teardown ();
    }
}
