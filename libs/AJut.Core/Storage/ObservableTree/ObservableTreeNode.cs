namespace AJut.Storage
{
    using System;
    using System.Collections.Generic;
    using AJut.Tree;

    /// <summary>
    /// An implementation of <see cref="IObservableTreeNode"/>. This could be used as a base class, a fully realized <see cref="IObservableTreeNode"/>, or just 
    /// as an example on how to implement <see cref="IObservableTreeNode"/>
    /// </summary>
    public class ObservableTreeNode<TNode> : NotifyPropertyChanged, IObservableTreeNode
        where TNode : class, IObservableTreeNode
    {
        private readonly List<TNode> m_children = new List<TNode>();
        private readonly IReadOnlyList<TNode> m_readOnlyChildren;
        private bool m_canHaveChildren;
        private TNode m_parent;

        static ObservableTreeNode ()
        {
            TreeTraversal<TNode>.SetupDefaults(n => (IEnumerable<TNode>)n.Children, n => (TNode)n.Parent);
        }
        public ObservableTreeNode ()
        {
            m_readOnlyChildren = m_children.AsReadOnly();
        }

        // ===============[Events]================
        public event EventHandler<TreeNodeInsertedEventArgs> ChildInserted;
        public event EventHandler<EventArgs<IObservableTreeNode>> ChildRemoved;
        public event EventHandler<EventArgs<bool>> CanHaveChildrenChanged;
        public event EventHandler<TreeNodeParentChangedEventArgs> ParentChanged;

        // =============[Properties]==============
        public virtual TNode Parent
        {
            get => m_parent;
            set
            {
                var oldParent = m_parent;
                if (this.SetAndRaiseIfChanged(ref m_parent, value))
                {
                    // Tell the old parent that this is no more its child
                    oldParent?.RemoveChild(this);

                    // Don't worry about the new parent add, it's the new parent's job
                    // to step up and claim their children - don't enable dead-beat-ism!

                    // Since we're observable, tell the people who care that we have a new parent!
                    this.RaiseParentChangedEvent(oldParent, value);

                    this.OnParentChanged(oldParent, value);
                }
            }
        }
        public virtual IReadOnlyList<TNode> Children => m_readOnlyChildren;

        public virtual bool CanHaveChildren
        {
            get => m_canHaveChildren;
            set
            {
                if (this.SetAndRaiseIfChanged(ref m_canHaveChildren, value))
                {
                    this.CanHaveChildrenChanged?.Invoke(this, new EventArgs<bool>(value));
                }
            }
        }

        // ==============[Methods]===============
        public virtual void InsertChild (int index, TNode child)
        {
            child.Parent = this;
            m_children.Insert(index, child);

            this.RaiseChildInsertedEvent(index, child);
            this.OnChildInserted(index, child);
        }

        public virtual bool RemoveChild (TNode child)
        {
            if (m_children.Remove(child))
            {
                this.RaiseChildRemovedEvent(child);
                this.OnChildRemoved(child);
                return true;
            }

            return false;
        }

        // ==========[IObservableTreeNode - implicit implementations]================
        IObservableTreeNode IObservableTreeNode.Parent
        {
            get => this.Parent;
            set => this.Parent = (TNode)value;
        }
        IReadOnlyList<IObservableTreeNode> IObservableTreeNode.Children => m_readOnlyChildren;

        void IObservableTreeNode.InsertChild (int index, IObservableTreeNode child) => this.InsertChild(index, (TNode)child);
        bool IObservableTreeNode.RemoveChild (IObservableTreeNode child) => this.RemoveChild((TNode)child);

        // ==========[Protected Utilties]================
        protected virtual void OnChildInserted (int index, TNode child) { }
        protected virtual void OnChildRemoved (TNode child) { }
        protected virtual void OnParentChanged (TNode oldParent, TNode newParent) { }

        protected void RaiseChildInsertedEvent (int index, TNode child) => this.ChildInserted?.Invoke(this, new TreeNodeInsertedEventArgs(index, child));
        protected void RaiseChildRemovedEvent (TNode node) => this.ChildRemoved?.Invoke(this, new EventArgs<IObservableTreeNode>(node));
        protected void RaiseCanHaveChildrenChangedEvent () => this.CanHaveChildrenChanged?.Invoke(this, new EventArgs<bool>(this.CanHaveChildren));
        protected void RaiseParentChangedEvent (TNode oldParent, TNode newParent) => this.ParentChanged?.Invoke(this, new TreeNodeParentChangedEventArgs(oldParent, newParent));
    }

    /// <summary>
    /// A fully standalone implementation of <see cref="IObservableTreeNode"/>. This could be used as is, as
    /// a base class, or just as an example on how to implement <see cref="IObservableTreeNode"/>.
    /// </summary>
    public class ObservableTreeNode : ObservableTreeNode<ObservableTreeNode>
    {
    }
}
