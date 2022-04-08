namespace AJut.Storage
{
    using System;

    public class TreeNodeInsertedEventArgs : EventArgs
    {
        public TreeNodeInsertedEventArgs (int index, IObservableTreeNode node)
        {
            this.InsertIndex = index;
            this.Node = node;
        }

        public int InsertIndex { get; }
        public IObservableTreeNode Node { get; }
    }
}
