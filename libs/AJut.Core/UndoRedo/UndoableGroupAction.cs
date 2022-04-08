namespace AJut.UndoRedo
{
    using System.Collections.Generic;
    using System.Diagnostics;

    /// <summary>
    /// An undoable action which is comprised of a group of undoable actions (do all, undo all)
    /// </summary>
    [DebuggerDisplay("Group: {DisplayName}")]
    public class UndoableGroupAction : IUndoableAction
    {
        private readonly List<IUndoableAction> m_actions = new List<IUndoableAction>();

        public UndoableGroupAction (string name, object typing = null)
        {
            this.DisplayName = name;
            this.DisplayTyping = typing;

            this.Children = m_actions.AsReadOnly();
        }

        /// <summary>
        /// Create a group action for a substack (a substack is essentially a group of undoable actions)
        /// </summary>
        public UndoableGroupAction (string name, object typing, UndoRedoManager substack) : this(name, typing)
        {
            m_actions.AddRange(substack.UndoStack.EnumerateReversed());
        }

        public string DisplayName { get; }

        public object DisplayTyping { get; }

        public IReadOnlyList<IUndoableAction> Children { get; }

        public void Add (IUndoableAction action)
        {
            m_actions.Add(action);
        }

        public void Do ()
        {
            foreach(IUndoableAction action in m_actions)
            {
                action.Do();
            }
        }

        public void Undo ()
        {
            foreach (IUndoableAction action in m_actions.EnumerateReversed())
            {
                action.Undo();
            }
        }
    }
}
