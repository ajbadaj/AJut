namespace AJut.UX.PropertyInteraction
{
    using System;
    using System.Windows.Input;

    /// <summary>
    /// Minimal <see cref="ICommand"/> wrapping an <see cref="Action"/>. Used by
    /// <see cref="PGButtonAttribute"/> targets to wire a method invocation to a button Command.
    /// </summary>
    public class ActionCommand : ICommand
    {
        private readonly Action m_action;

        public ActionCommand (Action action)
        {
            m_action = action;
        }

        public event EventHandler CanExecuteChanged { add { } remove { } }
        public bool CanExecute (object parameter) => true;
        public void Execute (object parameter) => m_action();
    }
}
