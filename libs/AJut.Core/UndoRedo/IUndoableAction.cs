namespace AJut.UndoRedo
{
    /// <summary>
    /// Interface of an undoable action
    /// </summary>
    public interface IUndoableAction
    {
        string DisplayName { get; }
        object DisplayTyping { get; }
        void Do ();
        void Undo ();
    }
}
