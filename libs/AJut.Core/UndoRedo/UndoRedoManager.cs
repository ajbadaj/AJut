namespace AJut.UndoRedo
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using AJut.Storage;

    /// <summary>
    /// Storage and management of <see cref="IUndoableAction"/> actions
    /// </summary>
    public class UndoRedoManager : NotifyPropertyChanged
    {
        private ObservableCollection<IUndoableAction> m_undoStack = new ObservableCollection<IUndoableAction>();
        private ObservableCollection<IUndoableAction> m_redoStack = new ObservableCollection<IUndoableAction>();
        private Dictionary<Guid, UndoRedoManager> m_subStacks = new Dictionary<Guid, UndoRedoManager>();
        private Guid? m_substackActingAsPrimary = null;

        public UndoRedoManager ()
        {
            this.UndoStack = new ReadOnlyObservableCollection<IUndoableAction>(m_undoStack);
            this.RedoStack = new ReadOnlyObservableCollection<IUndoableAction>(m_redoStack);
        }

        // ============== [Properties] ================
        public bool AnyUndos => m_undoStack.Count > 0;
        public bool AnyRedos => m_redoStack.Count > 0;

        /// <summary>
        /// The undo aspect of this undo/redo stack
        /// </summary>
        public ReadOnlyObservableCollection<IUndoableAction> UndoStack { get; }

        /// <summary>
        /// The redo aspect of this undo/redo stack
        /// </summary>
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

        /// <summary>
        /// Returns the substack for the given id (if any)
        /// </summary>
        public UndoRedoManager GetSubstack (Guid substackId)
        {
            if (m_subStacks.TryGetValue(substackId, out UndoRedoManager substack))
            {
                return substack;
            }

            return null;
        }

        /// <summary>
        /// Override this stack by forwarding all calls to a substack generated for this purpose. On <see cref="SubstackOverride.CommitAndEndOverride(string, object)"/>
        /// this created substack will be cleaned up. It is up to the user to ensure commit gets called on the override when complete.
        /// </summary>
        public SubstackOverride OverrideUndoRedoCallsToUseNewSubstack ()
        {
            Guid id = this.CreateSubsidiaryStack();
            if (m_substackActingAsPrimary == null)
            {
                m_substackActingAsPrimary = id;
            }

            return new SubstackOverride(this, id, true);
        }

        /// <summary>
        /// Override this stack by forwarding all calls to a substack (that has been previously generated). It is up to 
        /// the user to ensure <see cref="SubstackOverride.CommitAndEndOverride(string, object)"/> gets called on the override when complete.
        /// </summary>
        public SubstackOverride OverrideUndoRedoCallsToUse (Guid id)
        {
            if (m_subStacks.ContainsKey(id) && m_substackActingAsPrimary == null)
            {
                m_substackActingAsPrimary = id;
            }

            return new SubstackOverride(this, id, false);
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

            // Determine the stack target (incase it's a different target)
            UndoRedoManager stack = this;
            if (m_substackActingAsPrimary != null && m_subStacks.TryGetValue(m_substackActingAsPrimary.Value, out UndoRedoManager actingStack))
            {
                stack = actingStack;
            }

            stack.m_undoStack.Insert(0, action);
            stack.m_redoStack.Clear();

            // AnyUndos will change if we're adding the first one
            bool anyUndosChange = stack.m_undoStack.Count == 1;

            // AnyRedos will change if there used to be any (adding actions kills the redo stack, 
            //  because now we've changed the future)
            bool anyRedosChange = stack.AnyRedos;

            if (anyUndosChange)
            {
                stack.RaisePropertiesChanged(nameof(AnyUndos));
            }
            if (anyRedosChange)
            {
                stack.RaisePropertiesChanged(nameof(AnyRedos));
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
            if (m_substackActingAsPrimary != null)
            {
                return this.Undo(m_substackActingAsPrimary.Value);
            }

            if (!this.AnyUndos)
            {
                return false;
            }

            // AnyUndos changes if we had some undos, and we're about to undo the last one
            bool anyUndosChange = m_undoStack.Count == 1;

            // AnyRedos changes if we had NO redos, and we're about to create our first one by undoing
            bool anyRedosChange = m_redoStack.Count == 0;

            IUndoableAction action = m_undoStack[0];
            m_undoStack.RemoveAt(0);

            action.Undo();
            m_redoStack.Insert(0, action);

            if (anyUndosChange)
            {
                this.RaisePropertiesChanged(nameof(AnyUndos));
            }
            if (anyRedosChange)
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
            if (m_substackActingAsPrimary != null)
            {
                return this.Redo(m_substackActingAsPrimary.Value);
            }

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

        /// <summary>
        /// This is an incase of emergency situation, ideally using the <see cref="SubstackOverride"/> will be used to clear itself
        /// </summary>
        public void EnsureSubstackOverrideIsCleared ()
        {
            m_substackActingAsPrimary = null;
        }

        /// <summary>
        /// Represents a temporary override to a "main" undoredo stack, where add/remove/undo/redo commands target instead a substack.
        /// This culminates in the substack being committed to the mainstack, ending the override (via <see cref="CommitAndEndOverride(string, object)"/>).
        /// </summary>
        public class SubstackOverride
        {
            private readonly UndoRedoManager m_owner;
            private readonly bool m_createdSubstack;
            internal SubstackOverride (UndoRedoManager owner, Guid substackId, bool createdSubstack)
            {
                m_owner = owner;
                this.Id = substackId;
                m_createdSubstack = createdSubstack;
            }

            /// <summary>
            /// The substack's id
            /// </summary>
            public Guid Id { get; }

            /// <summary>
            /// Checks if the override is actively being applied
            /// </summary>
            public bool IsActive => m_owner.m_substackActingAsPrimary == this.Id;

            /// <summary>
            /// Commits the substack as a single undoable entry into the stack that generated this override. Returns if it succeeded, but either way this call should only be made once.
            /// </summary>
            /// <param name="name">The name of the undoable group entry</param>
            /// <param name="typing">The optional typing info of the undoable group entry</param>
            /// <returns>true if commit was successful, false otherwise.</returns>
            public bool CommitAndEndOverride (string name, object typing = null)
            {
                if (!this.IsActive)
                {
                    if (m_createdSubstack)
                    {
                        m_owner.m_subStacks.Remove(this.Id);
                    }

                    return false;
                }

                m_owner.m_substackActingAsPrimary = null;
                bool success = m_owner.CommitSubstack(this.Id, name, typing);
                if (m_createdSubstack)
                {
                    m_owner.m_subStacks.Remove(this.Id);
                }

                return success;
            }
        }
    }
}
