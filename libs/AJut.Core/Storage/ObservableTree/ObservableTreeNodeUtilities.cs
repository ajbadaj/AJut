namespace AJut.Storage
{
    using System;

    public class ChildInsertedEventArgs : EventArgs
    {
        public ChildInsertedEventArgs (int index, IObservableTreeNode node)
        {
            this.InsertIndex = index;
            this.Node = node;
        }

        public int InsertIndex { get; }
        public IObservableTreeNode Node { get; }
    }

    public class ParentChangedEventArgs : EventArgs
    {
        public ParentChangedEventArgs(IObservableTreeNode oldParent, IObservableTreeNode newParent)
        {
            this.OldParent = oldParent;
            this.NewParent = newParent;
        }

        public IObservableTreeNode OldParent { get; }
        public IObservableTreeNode NewParent { get; }
    }

    public static class ObservableTreeNodeXT
    {
        public static void AddChild<TNode> (this TNode node, TNode child)
            where TNode : IObservableTreeNode
        {
            node.InsertChild(node.Children.Count, child);
        }
    }
}
