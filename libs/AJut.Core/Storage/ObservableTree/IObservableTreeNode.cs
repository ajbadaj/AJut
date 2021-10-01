namespace AJut.Storage
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.Tree;

    /// <summary>
    /// An interface for making a tree node observable
    /// </summary>
    public interface IObservableTreeNode
    {
        event EventHandler<ChildInsertedEventArgs> ChildInserted;
        event EventHandler<EventArgs<IObservableTreeNode>> ChildRemoved;
        event EventHandler<EventArgs<bool>> CanHaveChildrenChanged;
        event EventHandler<ParentChangedEventArgs> ParentChanged;

        IObservableTreeNode Parent { get; set; }
        IReadOnlyList<IObservableTreeNode> Children { get; }
        bool CanHaveChildren { get; }

        void InsertChild (int index, IObservableTreeNode child);
        bool RemoveChild (IObservableTreeNode child);
    }

    public static class IObservableTreeNodeXT
    {
        private static bool g_wasRegistered;
        public static void RegisterTreeTraversalDefaults()
        {
            if (g_wasRegistered)
            {
                return;
            }

            g_wasRegistered = true;
            TreeTraversal<IObservableTreeNode>.SetupDefaults(_ => _.Children, _ => _.Parent);

        }

        public static void AddChild<TNode> (this IObservableTreeNode parent, TNode child) where TNode : IObservableTreeNode
        {
            parent.InsertChild(parent.Children.Count, child);
        }

        public static void InsertChildren<TNode> (this IObservableTreeNode parent, int index, IEnumerable<TNode> children) where TNode : IObservableTreeNode
        {
            children.ForEach(c => parent.InsertChild(index++, c));
        }

        public static void AddChildren<TNode> (this IObservableTreeNode parent, IEnumerable<TNode> children) where TNode : IObservableTreeNode
        {
            parent.InsertChildren(parent.Children.Count, children);
        }

        public static void RemoveChildren<TNode> (this IObservableTreeNode parent, IEnumerable<TNode> children) where TNode : IObservableTreeNode
        {
            children.ForEach(c => parent.RemoveChild(c));
        }

        public static void Clear (this IObservableTreeNode parent)
        {
            parent.RemoveChildren(parent.Children.ToList());
        }
    }
}
