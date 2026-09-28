namespace AJut
{
    using System.ComponentModel;

    /// <summary>
    /// A scenario that raises the effective log verbosity to <see cref="RaiseToLevel"/> while active.
    /// Becomes active when <see cref="EnterCriteria"/> is satisfied or <see cref="Activate"/> is called; deactivates when
    /// <see cref="ExitCriteria"/> is satisfied or <see cref="Deactivate"/> is called (with no exit criteria it stays active
    /// until then). After deactivation the scenario automatically re-arms so it can activate again.
    /// </summary>
    /// <remarks>
    /// The line that activates a scenario is inside its window, and the line that satisfies its exit criteria is outside it.
    /// So an exit of <see cref="LogCountCriteria"/> with a <see cref="LogCountCriteria.CountThreshold"/> of N lets through the
    /// activating line plus the N-1 lines after it - a threshold of 1 is exactly the activating line. Lines from every thread
    /// count, in the order each reaches this scenario, which is not necessarily the order they land in the log file.
    /// <para/>
    /// Each scenario evaluates one line at a time, so its criteria never see two threads at once. Derive from this to narrow
    /// which lines the raise applies to - see <see cref="AppliesTo"/>.
    /// </remarks>
    public class LogVerbosityScenario : NotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs kIsCurrentlyActiveChangedArgs = new PropertyChangedEventArgs(nameof(IsCurrentlyActive));

        private readonly object m_evaluationLock = new object();
        private volatile bool m_isCurrentlyActive;

        // ===========[ Properties ]===========================================

        public bool IsEnabled { get; set; } = true;
        public eLogVerbositySetting RaiseToLevel { get; set; } = eLogVerbositySetting.Verbose;
        public LogScenarioCriteriaBase EnterCriteria { get; set; }
        public LogScenarioCriteriaBase ExitCriteria { get; set; }

        public bool IsCurrentlyActive => m_isCurrentlyActive;

        /// <summary>
        /// The manager whose <see cref="LogVerbosityManager.Scenarios"/> this is in, kept up to date by that collection, so a
        /// switch from code can update the manager's effective verbosity straight away.
        /// </summary>
        internal LogVerbosityManager Owner { get; set; }

        // ===========[ Public Interface Methods ]=============================

        /// <summary>
        /// Switches the scenario on from code, exactly as if its <see cref="EnterCriteria"/> had just been satisfied - its
        /// <see cref="ExitCriteria"/> starts watching from here. Does nothing if it is already active.
        /// </summary>
        public void Activate () => this.SetActive(true);

        /// <summary>
        /// Switches the scenario off from code, exactly as if its <see cref="ExitCriteria"/> had just been satisfied - it
        /// re-arms, and its <see cref="EnterCriteria"/> starts watching from here. Does nothing if it is already inactive.
        /// </summary>
        public void Deactivate () => this.SetActive(false);

        /// <summary>
        /// Fully resets this scenario to its initial state: deactivates it and resets all criteria.
        /// </summary>
        public void Reset ()
        {
            bool wasActive;
            lock (m_evaluationLock)
            {
                wasActive = m_isCurrentlyActive;
                m_isCurrentlyActive = false;
                this.EnterCriteria?.Reset();
                this.ExitCriteria?.Reset();
            }

            if (wasActive)
            {
                this.OnActiveStateSwitched();
            }
        }

        /// <summary>
        /// Decides whether this scenario's raise applies to a given line while the scenario is active - every line, by default.
        /// Override it to narrow a raise to the lines you care about, so switching it on doesn't also let through every other
        /// chatty line at that level.
        /// </summary>
        /// <remarks>
        /// Runs on whichever thread is logging, for every line that passes through while the scenario is active, so keep it
        /// cheap and thread safe. It only decides what the raise applies to: it doesn't feed the enter or exit criteria, and
        /// <see cref="Logger.WouldLog"/> doesn't consult it.
        /// </remarks>
        protected virtual bool AppliesTo (string message, bool isError) => true;

        // ===========[ Internal Interface Methods ]===========================

        /// <summary>
        /// Runs one line through this scenario, and returns the verbosity it grants that line (or <see cref="eLogVerbositySetting.None"/>
        /// if it grants nothing). The grant is decided entirely within this line's own pass, so another thread switching the
        /// scenario off a moment later can't take it back.
        /// </summary>
        internal eLogVerbositySetting ProcessLogLine (string message, bool isError, out bool didSwitch)
        {
            didSwitch = false;
            bool isLineInWindow;
            if (!this.IsEnabled)
            {
                // A disabled scenario stops watching lines, but one that was already active keeps its raise until switched off
                isLineInWindow = m_isCurrentlyActive;
            }
            else
            {
                lock (m_evaluationLock)
                {
                    if (!m_isCurrentlyActive)
                    {
                        // The line that opens the window is inside it
                        if (this.EnterCriteria != null && this.EnterCriteria.Evaluate(message, isError))
                        {
                            m_isCurrentlyActive = true;
                            this.ExitCriteria?.InitiateScenario();
                            didSwitch = true;
                        }
                    }
                    else if (this.ExitCriteria != null && this.ExitCriteria.Evaluate(message, isError))
                    {
                        // The line that closes the window is outside it
                        m_isCurrentlyActive = false;
                        this.EnterCriteria?.InitiateScenario();
                        didSwitch = true;
                    }

                    isLineInWindow = m_isCurrentlyActive;
                }

                // The manager recalculates off didSwitch, so only the property needs raising here
                if (didSwitch)
                {
                    this.RaisePropertyChanged(kIsCurrentlyActiveChangedArgs);
                }
            }

            return (isLineInWindow && this.AppliesTo(message, isError)) ? this.RaiseToLevel : eLogVerbositySetting.None;
        }

        // ===========[ Helpers ]==============================================

        private void SetActive (bool isActive)
        {
            lock (m_evaluationLock)
            {
                if (m_isCurrentlyActive == isActive)
                {
                    return;
                }

                m_isCurrentlyActive = isActive;
                if (isActive)
                {
                    this.ExitCriteria?.InitiateScenario();
                }
                else
                {
                    this.EnterCriteria?.InitiateScenario();
                }
            }

            this.OnActiveStateSwitched();
        }

        private void OnActiveStateSwitched ()
        {
            this.RaisePropertyChanged(kIsCurrentlyActiveChangedArgs);
            this.Owner?.OnScenarioSwitchedOutsideOfLogging();
        }
    }
}
