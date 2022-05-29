namespace AJut.Storage
{
    using AJut.Tree;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;

    /// <summary>
    /// An observable storage container for a flattened tree. Listens to insertion and removal of the <see cref="IObservableTreeNode"/> nodes it represents.
    /// </summary>
    /// <remarks>
    /// To reduce setup time by orders of magnitude, simply construct trees *before* setting them into <see cref="ObservableCollection{T}"/>'s root.
    /// </remarks>
    public class ObservableFlatTreeStore<TNode> : ReadOnlyObservableCollection<TNode>
        where TNode : class, IObservableTreeNode
    {
        private delegate int IndexGenerator (TNode parent, int childIndexOnParent);
        private TNode m_rootNode;
        private bool m_includeRoot = true;
        private IndexGenerator m_indexGenerator;

        static ObservableFlatTreeStore ()
        {
            IObservableTreeNodeXT.RegisterTreeTraversalDefaults();
        }

        public ObservableFlatTreeStore () : base(new ObservableCollection<TNode>())
        {
            m_indexGenerator = this.DetermineInsertIndex_ByCountCalculation;
        }

        public ObservableFlatTreeStore (TNode root, bool includeRoot = true) : this()
        {
            this.IncludeRoot = includeRoot;
            this.RootNode = root;
        }

        public event EventHandler<EventArgs<List<TNode>>> NodesAdded;
        public event EventHandler<EventArgs<List<TNode>>> NodesRemoved;

        public TNode RootNode
        {
            get => m_rootNode;
            set
            {
                if (m_rootNode == value)
                {
                    return;
                }

                this.Clear();
                m_rootNode = value;
                if (m_rootNode == null)
                {
                    return;
                }

                foreach (TNode child in this.GetFullTreeFromRoot())
                {
                    this.Items.Add(this.Observe(child));
                }
            }
        }

        public bool IncludeRoot
        {
            get => m_includeRoot;
            set
            {
                if (m_includeRoot == value)
                {
                    return;
                }

                m_includeRoot = value;
                if (this.RootNode == null)
                {
                    return;
                }

                if (m_includeRoot)
                {
                    if (!this.Items.Contains(this.RootNode))
                    {
                        this.Items.Insert(0, this.RootNode);
                    }
                }
                else
                {
                    this.Items.RemoveAt(0);
                }
            }
        }
        public void Clear ()
        {
            foreach (TNode item in this.Items)
            {
                this.Disregard(item);
            }

            m_rootNode = null;
            this.Items.Clear();
        }

        /// <summary>
        /// Switch to the variable-time methodology of index generation. This is the default. This performs well
        /// in practically all situations, but may incur moderate slow-downs with extrememly deep or complex trees.
        /// </summary>
        public void SwitchToVariableTimeIndexGeneration ()
        {
            m_indexGenerator = this.DetermineInsertIndex_ByCountCalculation;
        }

        /// <summary>
        /// Switch to fixed time index generation methodology. This version can be a bit slower, but it's calculation time
        /// is relatively insulated to change in timing as tree complexity grows. Default is variable time generation.
        /// </summary>
        public void SwitchToFixedTimeIndexGeneration ()
        {
            m_indexGenerator = this.DetermineInsertIndex_InFixedTime;
        }

        protected virtual void OnObserve (TNode node) { }
        protected virtual void OnDisregard (TNode node) { }
        protected virtual bool OnInsertChildOverride (TNode parent, TNode child, int childIndex) => false;
        protected virtual IEnumerable<TNode> GetFullTreeFromRoot ()
        {
            if (m_rootNode == null)
            {
                return Enumerable.Empty<TNode>();
            }

            return TreeTraversal<IObservableTreeNode>.All(m_rootNode, includeSelf: this.IncludeRoot, strategy: eTraversalStrategy.DepthFirst).OfType<TNode>();
        }

        protected TNode Observe (TNode node)
        {
            node.ChildInserted += this.Node_ChildInserted;
            node.ChildRemoved += this.Node_ChildRemoved;
            this.OnObserve(node);
            return node;
        }

        protected TNode Disregard (TNode node)
        {
            node.ChildInserted -= this.Node_ChildInserted;
            node.ChildRemoved -= this.Node_ChildRemoved;
            this.OnDisregard(node);
            return node;
        }

        // =====================================================================================
        // = Flat tree index calculation utilities
        // =====================================================================================
        // = This is the crux of the entire flat tree store, determining where on a flat list
        // = to insert a heirarchy item. I have gone through several iterations of this, after
        // = extensive testing I've determined that for the most part, the first 10K or so items
        // = can be added in fastest if the variable time count determination is used. After it's
        // = a bit of a mixed bag. While average case best scenario is 'by count calculation' I
        // = didn't want to block all user options, and I didn't want to add a branching conditional
        // = in this performance critical zone - so I have instead opted to allow the caller to
        // = decide for themselves, and default to the best average time case.
        // =====================================================================================

        /// <summary>
        /// Determine the flat list index location where a tree node child should be inserted
        /// </summary>
        /// <param name="parent">The parent who has had a child added to it</param>
        /// <param name="childIndexOnParent">The index of the added child in the parent's chilren list</param>
        /// <returns>The index inside the flat hierarchy that the child should live in</returns>
        protected virtual int DetermineInsertIndex (TNode parent, int childIndexOnParent)
        {
            return m_indexGenerator(parent, childIndexOnParent);
        }

        /// <summary>
        /// Variable but minimal time generation - tally up descendant count of all sibilings before where this is being inserted
        /// </summary>
        private int DetermineInsertIndex_ByCountCalculation (TNode parent, int childIndexOnParent)
        {
            if (childIndexOnParent == 0)
            {
                return this.IndexOf(parent) + 1;
            }

            int index = this.IndexOf(parent);
            for (int childIndex = 0; childIndex < childIndexOnParent; ++childIndex)
            {
                index += 1 + TreeTraversal<IObservableTreeNode>.CountAllDescendants(parent.Children[childIndex]);
            }

            return 1+ index;
        }

        /// <summary>
        /// Fixed time generation - find next sibiling or cousin (next element at same breadth level)
        /// </summary>
        private int DetermineInsertIndex_InFixedTime (TNode parent, int childIndexOnParent)
        {
            /*
             *      A
             *    /   \
             *   B      C
             *  / \     |
             *  D  E    F
             *  
             *  For this approach to work, a breadth first iterator for add child to B would have to give me...
             *      Start at D
             *      iter += 2
             *      result == C
             *      
             *  This approach has to work if B has no children (if insert 0 check should work)
             */

            if (childIndexOnParent == 0)
            {
                return this.IndexOf(parent) + 1;
            }

            var target = parent.Children[childIndexOnParent];
            IObservableTreeNode nextSiblingOrCousin = TreeTraversal<IObservableTreeNode>.FindNextSiblingOrCousin(m_rootNode, target);
            if (nextSiblingOrCousin == null)
            {
                return this.Count;
            }

            return this.IndexOf((TNode)nextSiblingOrCousin);
        }

        protected void OnNodeAdded (TNode parent, int childIndex, TNode node)
        {
            int insertIndex = this.DetermineInsertIndex(parent, childIndex);
            this.InsertNodeIntoFlatList(insertIndex, node);
        }

        protected void OnNodeRemoved (TNode node)
        {
            this.RemoveNodeFromFlatList(node);
        }

        protected void InsertNodeIntoFlatList (int index, TNode node)
        {
            Debug.Assert(index >= 0 && index <= this.Items.Count, $"Index {index} is out of range for {nameof(ObservableFlatTreeStore)} with item count of {this.Items.Count}");

            List<TNode> addedNodes = new List<TNode>();
            foreach (TNode child in TreeTraversal<IObservableTreeNode>.All(node).OfType<TNode>())
            {
                this.Items.Insert(index++, this.Observe(child));
            }

            this.NodesAdded?.Invoke(this, new EventArgs<List<TNode>>(addedNodes));
        }

        protected void RemoveNodeFromFlatList (TNode node)
        {
            List<TNode> removedNodes = new List<TNode>();

            foreach (TNode toRemove in TreeTraversal<IObservableTreeNode>.All(node).OfType<TNode>())
            {
                this.Items.Remove(this.Disregard(toRemove));
                removedNodes.Add(toRemove);
            }

            this.NodesRemoved?.Invoke(this, new EventArgs<List<TNode>>(removedNodes));
        }

        private void Node_ChildInserted (object sender, TreeNodeInsertedEventArgs e)
        {
            this.OnNodeAdded((TNode)sender, e.InsertIndex, (TNode)e.Node);
        }

        private void Node_ChildRemoved (object sender, EventArgs<IObservableTreeNode> e)
        {
            this.OnNodeRemoved((TNode)e.Value);
        }

    }

    public class ObservableFlatTreeStore : ObservableFlatTreeStore<IObservableTreeNode>
    {
    }
}
