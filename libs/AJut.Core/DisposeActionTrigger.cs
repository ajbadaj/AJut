namespace AJut
{
    using System;

    /// <summary>
    /// A simple storage mechanism to build something that runs an action when disposed
    /// </summary>
    public class DisposeActionTrigger : IDisposable
    {
        private Action m_action;
        public DisposeActionTrigger (Action action)
        {
            m_action = action;
        }

        public void Dispose ()
        {
            m_action?.Invoke();
            m_action = null;
        }
    }
}
