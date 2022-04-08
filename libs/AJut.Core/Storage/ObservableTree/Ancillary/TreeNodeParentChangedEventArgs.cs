namespace AJut.Storage
{
    using System;

    public class TreeNodeParentChangedEventArgs : EventArgs
    {
        public TreeNodeParentChangedEventArgs (IObservableTreeNode oldParent, IObservableTreeNode newParent)
        {
            this.OldParent = oldParent;
            this.NewParent = newParent;
        }

        public IObservableTreeNode OldParent { get; }
        public IObservableTreeNode NewParent { get; }
    }
}
