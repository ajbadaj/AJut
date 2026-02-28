namespace AJut
{
    /// <summary>
    /// A scenario that raises the effective log verbosity to <see cref="RaiseToLevel"/> while active.
    /// Becomes active when <see cref="EnterCriteria"/> is satisfied; deactivates when
    /// <see cref="ExitCriteria"/> is satisfied (or stays active indefinitely if no exit criteria is set).
    /// After deactivation the scenario automatically re-arms so it can activate again.
    /// </summary>
    public class LogVerbosityScenario : NotifyPropertyChanged
    {
        private bool m_isCurrentlyActive;

        public bool IsEnabled { get; set; } = true;
        public eLogVerbositySetting RaiseToLevel { get; set; } = eLogVerbositySetting.Verbose;
        public LogScenarioCriteriaBase EnterCriteria { get; set; }
        public LogScenarioCriteriaBase ExitCriteria { get; set; }

        public bool IsCurrentlyActive
        {
            get => m_isCurrentlyActive;
            private set => this.SetAndRaiseIfChanged(ref m_isCurrentlyActive, value);
        }

        internal void ProcessLogLine (string message, bool isError)
        {
            if (!IsEnabled) return;

            if (!m_isCurrentlyActive)
            {
                if (EnterCriteria != null && EnterCriteria.Evaluate(message, isError))
                {
                    IsCurrentlyActive = true;
                    ExitCriteria?.InitiateScenario();
                }
            }
            else
            {
                if (ExitCriteria != null && ExitCriteria.Evaluate(message, isError))
                {
                    IsCurrentlyActive = false;
                    EnterCriteria?.InitiateScenario();
                }
            }
        }

        /// <summary>
        /// Fully resets this scenario to its initial state: deactivates it and resets all criteria.
        /// </summary>
        public void Reset ()
        {
            IsCurrentlyActive = false;
            EnterCriteria?.Reset();
            ExitCriteria?.Reset();
        }
    }
}
