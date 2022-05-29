namespace AJut.UndoRedo
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// An undoable action which is populated by providing an <see cref="Action"/> (function)
    /// </summary>
    [DebuggerDisplay("Lambda: {DisplayName}")]
    public class LambdaUndoableAction : IUndoableAction
    {
        private Action m_do;
        private Action m_undo;

        public LambdaUndoableAction(string name, Action doAction, Action undoAction, object typing = null)
        {
            this.DisplayName = name;
            this.DisplayTyping = typing;
            m_do = doAction;
            m_undo = undoAction;
        }

        public string DisplayName { get; }

        public object DisplayTyping { get; }

        public void Do () => m_do();
        public void Undo () => m_undo();
    }
}
