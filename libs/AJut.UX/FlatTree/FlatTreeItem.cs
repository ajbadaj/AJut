namespace AJut.UX
{
    using AJut.Storage;
    using AJut.Tree;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // ===========[ FlatTreeItem ]===============================================
    // View model for a single node in a flat-rendered tree. Wraps an
    // IObservableTreeNode source, tracking tree structure (depth, parent,
    // children) and display state (expanded, selected, selectable).
    //
    // Extends ObservableTreeNode<FlatTreeItem> so it IS an IObservableTreeNode.
    // This allows ObservableFlatTreeStore<FlatTreeItem> to manage the visible
    // flat list automatically via ChildInserted / ChildRemoved events.
    //
    // Key invariant: base.Children (= m_readOnlyChildren via IObservableTreeNode)
    // holds ONLY the currently-visible children. Hidden children live in
    // m_hiddenChildren. Expand/collapse moves items between the two lists via
    // base.InsertChild / base.RemoveChild, which fire the events the store needs.
    //
    // Use the static factory methods to create instances:
    //   FlatTreeItem.CreateRoot(source)          -- single-root tree
    //   FlatTreeItem.CreateUberRoot(roots)       -- multi-root tree (false root)

    public sealed class FlatTreeItem : ObservableTreeNode<FlatTreeItem>
    {
        // ===========[ Statics ]==========================================
        private static readonly IReadOnlyList<FlatTreeItem> kEmpty = Array.Empty<FlatTreeItem>();

        static FlatTreeItem ()
        {
            // Register traversal so TreeTraversal<FlatTreeItem> uses AllChildren
            // (visible + hidden). This ensures AllItems() traversals reach collapsed nodes.
            TreeTraversal<FlatTreeItem>.SetupDefaults(i => i.AllChildren, i => i.Parent);
        }

        // ===========[ Instance fields ]==========================================
        private readonly List<FlatTreeItem> m_hiddenChildren = new();
        private FlatTreeItem m_parentItem;
        private IObservableTreeNode m_source;
        private bool m_isFalseRoot;
        private bool m_isExpanded;
        private bool m_isExpandable;
        private bool m_isSelected;
        private bool m_isSelectable = true;
        private double m_tabbingSize = 16.0;

        // ===========[ Construction ]=============================================
        // Private - use factory methods.
        private FlatTreeItem () { }

        private FlatTreeItem (IObservableTreeNode source, FlatTreeItem parent, double tabbingSize, bool startExpanded)
        {
            m_source = source;
            m_parentItem = parent;
            m_tabbingSize = tabbingSize;
            m_isExpandable = source.CanHaveChildren;

            source.CanHaveChildrenChanged += (s, e) => this.IsExpandable = e.Value;
            source.ChildInserted += this.Source_ChildInserted;
            source.ChildRemoved += this.Source_ChildRemoved;

            // Build the child hierarchy. If startExpanded, put children into
            // base.Children (visible); otherwise into m_hiddenChildren.
            foreach (IObservableTreeNode child in source.Children)
            {
                var childItem = new FlatTreeItem(child, this, tabbingSize, startExpanded);
                if (startExpanded)
                {
                    base.InsertChild(base.Children.Count, childItem);
                }
                else
                {
                    m_hiddenChildren.Add(childItem);
                }
            }

            // Set AFTER building children so the constructor-time InsertChild calls
            // don't accidentally use the wrong branch before m_isExpanded is ready.
            m_isExpanded = startExpanded;
        }

        // ===========[ Factory methods ]==========================================

        /// <summary>
        /// Creates a single-root flat tree item hierarchy from the given observable source tree.
        /// </summary>
        public static FlatTreeItem CreateRoot (IObservableTreeNode source, double tabbingSize = 16.0, bool startExpanded = false)
        {
            return new FlatTreeItem(source, null, tabbingSize, startExpanded);
        }

        /// <summary>
        /// Creates an invisible false root whose children are the given source roots (multi-root scenario).
        /// The false root itself is never shown; IncludeRoot=false on the store omits it.
        /// </summary>
        public static FlatTreeItem CreateUberRoot (IEnumerable<IObservableTreeNode> roots, double tabbingSize = 16.0, bool startExpanded = false)
        {
            var uber = new FlatTreeItem
            {
                m_isFalseRoot = true,
                m_tabbingSize = tabbingSize,
                m_isExpandable = true,
                m_isExpanded = true,   // uber root always expanded (its children are the visible roots)
            };

            int index = 0;
            foreach (IObservableTreeNode root in roots)
            {
                var childItem = new FlatTreeItem(root, uber, tabbingSize, startExpanded);
                // Uber root is always expanded, so children go into base.Children.
                uber.InsertChild(index++, childItem);
            }

            return uber;
        }

        // ===========[ Events ]===================================================
        public event EventHandler IsSelectedChanged;

        // ===========[ Properties ]===============================================

        /// <summary>The wrapped source tree node.</summary>
        public IObservableTreeNode Source => m_source;

        /// <summary>True for the invisible uber-root used in multi-root scenarios.</summary>
        public bool IsFalseRoot => m_isFalseRoot;

        /// <summary>
        /// Overrides parent tracking to use our own field (m_parentItem) rather than
        /// the base class's private field. This lets us set the parent without triggering
        /// the base class side-effect of calling oldParent?.RemoveChild(this).
        /// </summary>
        public override FlatTreeItem Parent
        {
            get => m_parentItem;
            set
            {
                if (m_parentItem != value)
                {
                    m_parentItem = value;
                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged(nameof(TreeDepth));
                    this.RaisePropertyChanged(nameof(IndentWidth));
                }
            }
        }

        /// <summary>
        /// Depth in the tree. False roots return -1 so their children (the real roots) start at 0.
        /// </summary>
        public int TreeDepth => m_isFalseRoot ? -1 : (m_parentItem?.TreeDepth + 1 ?? 0);

        /// <summary>Pixel indent width for this row: TreeDepth × TabbingSize.</summary>
        public double IndentWidth => this.TreeDepth * m_tabbingSize;

        public double TabbingSize
        {
            get => m_tabbingSize;
            set
            {
                if (this.SetAndRaiseIfChanged(ref m_tabbingSize, value))
                {
                    this.RaisePropertyChanged(nameof(IndentWidth));
                }
            }
        }

        public bool IsExpandable
        {
            get => m_isExpandable;
            private set
            {
                if (this.SetAndRaiseIfChanged(ref m_isExpandable, value) && !value)
                {
                    this.IsExpanded = false;
                }
            }
        }

        public bool IsExpanded
        {
            get => m_isExpanded;
            set
            {
                if (this.SetAndRaiseIfChanged(ref m_isExpanded, value))
                {
                    if (value)
                    {
                        // Expanding: move hidden children back into base.Children (fires ChildInserted).
                        foreach (FlatTreeItem child in m_hiddenChildren)
                        {
                            base.InsertChild(base.Children.Count, child);
                        }

                        m_hiddenChildren.Clear();
                    }
                    else
                    {
                        // Collapsing: move visible children into hidden list (fires ChildRemoved).
                        m_hiddenChildren.AddRange(base.Children);
                        foreach (FlatTreeItem child in m_hiddenChildren.ToList())
                        {
                            base.RemoveChild(child);
                        }
                    }
                }
            }
        }

        public bool IsSelected
        {
            get => m_isSelected;
            set
            {
                if (this.IsSelectable || !value)
                {
                    if (this.SetAndRaiseIfChanged(ref m_isSelected, value))
                    {
                        this.IsSelectedChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        public bool IsSelectable
        {
            get => m_isSelectable;
            set
            {
                if (this.SetAndRaiseIfChanged(ref m_isSelectable, value) && !value)
                {
                    this.IsSelected = false;
                }
            }
        }

        public override bool CanHaveChildren => m_isExpandable;

        // ===========[ Children access ]==========================================

        // AllChildren: visible + hidden. Used by TreeTraversal<FlatTreeItem> for
        // full-tree traversal (find by source, selection restore, etc.).
        public IEnumerable<FlatTreeItem> AllChildren
            => m_isExpanded ? (IEnumerable<FlatTreeItem>)base.Children : m_hiddenChildren;

        // ===========[ InsertChild / RemoveChild overrides ]=======================
        // Route to hidden list when collapsed; fire events normally when expanded.

        public override void InsertChild (int index, FlatTreeItem child)
        {
            if (m_isExpanded)
            {
                base.InsertChild(index, child);     // fires ChildInserted → store adds to flat list
            }
            else
            {
                child.Parent = this;                // keep parent ref correct (no ChildInserted)
                m_hiddenChildren.Insert(index, child);
            }
        }

        public override bool RemoveChild (FlatTreeItem child)
        {
            if (m_isExpanded)
            {
                return base.RemoveChild(child);     // fires ChildRemoved → store removes from flat list
            }

            return m_hiddenChildren.Remove(child);  // just remove from hidden list, no event needed
        }

        // ===========[ Source event handlers ]====================================
        private void Source_ChildInserted (object sender, TreeNodeInsertedEventArgs e)
        {
            var child = new FlatTreeItem((IObservableTreeNode)e.Node, this, m_tabbingSize, false);
            this.InsertChild(e.InsertIndex, child);
        }

        private void Source_ChildRemoved (object sender, EventArgs<IObservableTreeNode> e)
        {
            FlatTreeItem toRemove = base.Children.FirstOrDefault(i => i.Source == e.Value)
                                 ?? m_hiddenChildren.FirstOrDefault(i => i.Source == e.Value);
            if (toRemove != null)
            {
                this.RemoveChild(toRemove);
            }
        }
    }
}
