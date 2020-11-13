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
    public class ObservableFlatTreeStore<TNode> : ReadOnlyObservableCollection<TNode>
        where TNode : class, IObservableTreeNode
    {
        private TNode m_rootNode;
        private bool m_includeRoot = true;

        static ObservableFlatTreeStore ()
        {
            TreeTraversal<IObservableTreeNode>.SetupDefaults(_ => _.Children, _ => _.Parent);
        }

        public ObservableFlatTreeStore () : base(new ObservableCollection<TNode>())
        {
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

                if (!this.IncludeRoot)
                {
                    this.Observe(m_rootNode);
                    for (int index = 0; index < m_rootNode.Children.Count; index++)
                    {
                        this.InsertNodeIntoFlatList(index, (TNode)m_rootNode.Children[index]);
                    }
                }
                else
                {
                    this.InsertNodeIntoFlatList(0, m_rootNode);
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

        protected virtual void OnObserve (TNode node) { }
        protected virtual void OnDisregard (TNode node) { }
        protected virtual bool OnInsertChildOverride (TNode parent, TNode child, int childIndex) => false;
        protected virtual IEnumerable<TNode> GetFullTreeFromRoot ()
        {
            if (m_rootNode == null)
            {
                return Enumerable.Empty<TNode>();
            }

            return TreeTraversal<IObservableTreeNode>.All(m_rootNode, includeSelf: this.IncludeRoot).OfType<TNode>();
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

        protected virtual int DetermineInsertIndex (TNode parent, int childStartIndex)
        {
            // =====================================================================================
            // = This is the crux of the entire flat tree store, determining where on a flat list
            // = to insert a heirarchy item. I have gone through several iterations of this, I have
            // = opted for this potentially slower approach because the others were a touch brittle.
            // = Ideally one day I can crack a reasonable way to cache descendant count so I can
            // = calculate index instead of finding the insert spot and then doing an IndexOf
            // =====================================================================================
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

            if (childStartIndex == 0)
            {
                return this.IndexOf(parent) + 1;
            }

            var target = parent.Children[childStartIndex];
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

        private void Node_ChildInserted (object sender, ChildInsertedEventArgs e)
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
