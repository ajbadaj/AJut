namespace AJut.UndoRedo
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    /// <summary>
    /// Storage and management of <see cref="IUndoableAction"/> actions
    /// </summary>
    public class UndoRedoManager : NotifyPropertyChanged
    {
        private ObservableCollection<IUndoableAction> m_undoStack = new ObservableCollection<IUndoableAction>();
        private ObservableCollection<IUndoableAction> m_redoStack = new ObservableCollection<IUndoableAction>();
        private Dictionary<Guid, UndoRedoManager> m_subStacks = new Dictionary<Guid, UndoRedoManager>();

        public UndoRedoManager ()
        {
            this.UndoStack = new ReadOnlyObservableCollection<IUndoableAction>(m_undoStack);
            this.RedoStack = new ReadOnlyObservableCollection<IUndoableAction>(m_redoStack);
        }

        // ============== [Properties] ================
        public bool AnyUndos => m_undoStack.Count > 0;
        public bool AnyRedos => m_redoStack.Count > 0;

        public ReadOnlyObservableCollection<IUndoableAction> UndoStack { get; }
        public ReadOnlyObservableCollection<IUndoableAction> RedoStack { get; }

        // ============== [Methods] ================
        
        /// <summary>
        /// Creates a substack, substacks executes, can undo, redo, everything. They can be 
        /// commited back to the mainstack, though when that is done they are not interleaved,
        /// they are committed as a group.
        /// </summary>
        /// <param name="substackId">The substack's id</param>
        public bool CreateSubsidiaryStack (Guid substackId)
        {
            if (m_subStacks.ContainsKey(substackId))
            {
                return false;
            }

            m_subStacks.Add(substackId, new UndoRedoManager());
            return true;
        }

        /// <summary>
        /// Creates a substack, substacks executes, can undo, redo, everything. They can be 
        /// commited back to the mainstack, though when that is done they are not interleaved,
        /// they are committed as a group.
        /// </summary>
        /// <returns>The id of the substack (used to execute undo/redo)</returns>
        public Guid CreateSubsidiaryStack ()
        {
            Guid id = Guid.NewGuid();
            m_subStacks.Add(id, new UndoRedoManager());
            return id;
        }

        public UndoRedoManager GetSubstack (Guid substackId)
        {
            if (m_subStacks.TryGetValue(substackId, out UndoRedoManager substack))
            {
                return substack;
            }

            return null;
        }

        /// <summary>
        /// Commits the substack as a single entry, that means that this will not interleve the undos,
        /// instead it will put all the undos on the stack as a single grouped undo/redo action.
        /// </summary>
        /// <param name="name">The undo entry name</param>
        /// <param name="typing">The undo entry typing</param>
        /// <param name="substackId">The substack id</param>
        /// <returns>True if the substack was found, committed, and removed, false otherwise</returns>
        public bool CommitSubstack (Guid substackId, string name, object typing = null)
        {
            if (m_subStacks.TryGetValue(substackId, out UndoRedoManager substack))
            {
                this.ApplySubstackAsSingleUndoableEntry(name, typing, substack);
                m_subStacks.Remove(substackId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Commits the substack as a single entry, that means that this will not interleve the undos,
        /// instead it will put all the undos on the stack as a single grouped undo/redo action.
        /// </summary>
        /// <param name="name">The undo entry name</param>
        /// <param name="type">The undo entry typing</param>
        /// <param name="substack">The substack to commit</param>
        public void ApplySubstackAsSingleUndoableEntry (string name, object type, UndoRedoManager substack)
        {
            this.AddAction(new UndoableGroupAction(name, type, substack));
        }

        /// <summary>
        /// Executes an action (calls <see cref="IUndoableAction.Do"/>) and then adds it to the stack for management
        /// </summary>
        public void ExecuteAction (IUndoableAction action)
        {
            action.Do();
            this.AddAction(action);
        }

        /// <summary>
        /// Executes an action on a substack (calls <see cref="IUndoableAction.Do"/>) and then adds it to the stack for management
        /// </summary>
        public void ExecuteAction (Guid substackId, IUndoableAction action)
        {
            this.GetSubstack(substackId)?.ExecuteAction(action);
        }

        /// <summary>
        /// Adds the action to the stack (does not execute)
        /// </summary>
        public void AddAction (IUndoableAction action)
        {
            if (action is UndoableGroupAction group)
            {
                switch (group.Children.Count)
                {
                    // No children, don't save anything
                    case 0:
                        return;

                    // One child, don't save the group
                    case 1:
                        action = group.Children.First();
                        break;
                }
            }

            bool anyUndosChange = !this.AnyUndos;
            bool anyRedosChange = this.AnyRedos;

            m_undoStack.Insert(0, action);
            m_redoStack.Clear();

            if (anyUndosChange)
            {
                this.RaisePropertiesChanged(nameof(AnyUndos));
            }
            else if (anyRedosChange)
            {
                this.RaisePropertiesChanged(nameof(AnyRedos));
            }
        }

        /// <summary>
        /// Adds an action to a substack's undo/redo
        /// </summary>
        /// <param name="substackId">The id of the substack</param>
        /// <param name="action">The action to add</param>
        public void AddAction (Guid substackId, IUndoableAction action)
        {
            this.GetSubstack(substackId)?.AddAction(action);
        }

        /// <summary>
        /// Performs an undo
        /// </summary>
        /// <returns>False if there weren't any undos, true otherwise</returns>
        public bool Undo()
        {
            if (!this.AnyUndos)
            {
                return false;
            }

            bool anyUndosChange = m_undoStack.Count == 1;
            bool anyRedosChange = m_redoStack.Count == 0;

            IUndoableAction action = m_undoStack[0];
            m_undoStack.RemoveAt(0);

            action.Undo();
            m_redoStack.Insert(0, action);

            if (anyUndosChange)
            {
                this.RaisePropertiesChanged(nameof(AnyUndos));
            }
            else if (anyRedosChange)
            {
                this.RaisePropertiesChanged(nameof(AnyRedos));
            }

            return true;
        }

        /// <summary>
        /// Performs an undo on a substack
        /// </summary>
        /// <returns>False if there weren't any undos or any substacks with the given id, true otherwise</returns>
        public bool Undo (Guid substackId)
        {
            return this.GetSubstack(substackId)?.Undo() ?? false;
        }

        /// <summary>
        /// Performs a redo
        /// </summary>
        /// <returns>False if there weren't any redos, true otherwise</returns>
        public bool Redo()
        {
            if (!this.AnyRedos)
            {
                return false;
            }

            bool anyUndosChange = m_undoStack.Count == 0;
            bool anyRedosChange = m_redoStack.Count == 1;

            IUndoableAction action = m_redoStack[0];
            m_redoStack.RemoveAt(0);

            action.Do();
            m_undoStack.Insert(0, action);


            if (anyUndosChange)
            {
                this.RaisePropertiesChanged(nameof(AnyUndos));
            }
            else if (anyRedosChange)
            {
                this.RaisePropertiesChanged(nameof(AnyRedos));
            }

            return true;
        }

        /// <summary>
        /// Performs a redo on a substack
        /// </summary>
        /// <returns>False if there weren't any redos, true otherwise</returns>
        public bool Redo (Guid substackId)
        {
            return this.GetSubstack(substackId)?.Redo() ?? false;
        }
    }
}
