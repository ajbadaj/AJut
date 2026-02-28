namespace AJut.UX
{
    /// <summary>
    /// Optional interface for <see cref="AJut.Storage.IObservableTreeNode"/> sources that want
    /// to persist expansion state across tree rebuilds. When <see cref="AJut.UX.FlatTreeItem"/>
    /// wraps a source that implements this interface, it reads the initial expanded state from
    /// <see cref="IsExpanded"/> and writes back whenever the user expands or collapses the row.
    /// </summary>
    public interface IExpandableNode
    {
        bool IsExpanded { get; set; }
    }
}
