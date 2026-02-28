namespace AJut
{
    using System.Collections.ObjectModel;

    /// <summary>
    /// Tracks a base verbosity level and a collection of <see cref="LogVerbosityScenario"/> instances
    /// that can temporarily raise the effective verbosity. <see cref="EffectiveVerbosity"/> is always
    /// the maximum of <see cref="BaseVerbosity"/> and the <see cref="LogVerbosityScenario.RaiseToLevel"/>
    /// of every currently-active scenario.
    /// </summary>
    public class LogVerbosityManager : NotifyPropertyChanged
    {
        private eLogVerbositySetting m_baseVerbosity = eLogVerbositySetting.Normal;
        private eLogVerbositySetting m_effectiveVerbosity = eLogVerbositySetting.Normal;

        public eLogVerbositySetting BaseVerbosity
        {
            get => m_baseVerbosity;
            set
            {
                if (this.SetAndRaiseIfChanged(ref m_baseVerbosity, value))
                {
                    RecalculateEffectiveVerbosity();
                }
            }
        }

        public eLogVerbositySetting EffectiveVerbosity
        {
            get => m_effectiveVerbosity;
            private set => this.SetAndRaiseIfChanged(ref m_effectiveVerbosity, value);
        }

        public ObservableCollection<LogVerbosityScenario> Scenarios { get; } = new ObservableCollection<LogVerbosityScenario>();

        /// <summary>
        /// Processes a real log line through all scenarios, then recalculates effective verbosity.
        /// </summary>
        internal void ProcessLogLine (string message, bool isError)
        {
            foreach (var scenario in Scenarios)
            {
                scenario.ProcessLogLine(message, isError);
            }

            RecalculateEffectiveVerbosity();
        }

        /// <summary>
        /// Drives time-based criteria by passing an empty message through all scenarios.
        /// Call this from a periodic timer to allow <see cref="LogTimeCriteria"/> to fire
        /// without waiting for a real log line.
        /// </summary>
        public void EvaluateAllCriteria ()
        {
            ProcessLogLine(string.Empty, false);
        }

        private void RecalculateEffectiveVerbosity ()
        {
            var effective = m_baseVerbosity;
            foreach (var scenario in Scenarios)
            {
                if (scenario.IsCurrentlyActive && scenario.RaiseToLevel > effective)
                {
                    effective = scenario.RaiseToLevel;
                }
            }

            EffectiveVerbosity = effective;
        }
    }
}
