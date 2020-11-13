namespace AJut.UndoRedo
{
    public interface IUndoableAction
    {
        string DisplayName { get; }
        object DisplayTyping { get; }
        void Do ();
        void Undo ();
    }
}
