namespace AJut.Storage
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// An interface for making a tree node observable
    /// </summary>
    public interface IObservableTreeNode
    {
        event EventHandler<TreeNodeInsertedEventArgs> ChildInserted;
        event EventHandler<EventArgs<IObservableTreeNode>> ChildRemoved;
        event EventHandler<EventArgs<bool>> CanHaveChildrenChanged;
        event EventHandler<TreeNodeParentChangedEventArgs> ParentChanged;

        IObservableTreeNode Parent { get; set; }
        IReadOnlyList<IObservableTreeNode> Children { get; }
        bool CanHaveChildren { get; }

        void InsertChild (int index, IObservableTreeNode child);
        bool RemoveChild (IObservableTreeNode child);
    }
}
