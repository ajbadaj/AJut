namespace AJut
{
    using System;
    using System.Collections.ObjectModel;
    using System.ComponentModel;

    /// <summary>
    /// Tracks a base verbosity level and a collection of <see cref="LogVerbosityScenario"/> instances
    /// that can temporarily raise the effective verbosity. <see cref="EffectiveVerbosity"/> is always
    /// the maximum of <see cref="BaseVerbosity"/>, the <see cref="LogVerbosityScenario.RaiseToLevel"/>
    /// of every currently-active scenario, and any <see cref="RaiseFor"/> that hasn't run out yet.
    /// </summary>
    /// <remarks>
    /// Any number of threads can log through this at once. Each line gets its own pass through the scenarios, and whether it
    /// is let through is decided by what that pass saw - there is no lock across scenarios, and none held while writing.
    /// <see cref="Scenarios"/> can be changed while other threads are logging (a line already partway through its pass
    /// finishes against the scenarios it started with), though changing it from several threads at once is still on the
    /// caller, as with any <see cref="ObservableCollection{T}"/>.
    /// </remarks>
    public class LogVerbosityManager : NotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs kEffectiveVerbosityChangedArgs = new PropertyChangedEventArgs(nameof(EffectiveVerbosity));

        private readonly object m_recalculateLock = new object();
        private readonly object m_timedRaiseEditLock = new object();
        private eLogVerbositySetting m_baseVerbosity = eLogVerbositySetting.Normal;
        private volatile eLogVerbositySetting m_effectiveVerbosity = eLogVerbositySetting.Normal;
        private volatile LogVerbosityScenario[] m_scenarioSnapshot = Array.Empty<LogVerbosityScenario>();
        private volatile TimedRaise[] m_timedRaises = Array.Empty<TimedRaise>();

        // ===========[ Setup/Construction/Teardown ]==========================

        public LogVerbosityManager ()
        {
            this.Scenarios = new ScenarioCollection(this);
        }

        // ===========[ Properties ]===========================================

        public eLogVerbositySetting BaseVerbosity
        {
            get => m_baseVerbosity;
            set
            {
                if (this.SetAndRaiseIfChanged(ref m_baseVerbosity, value))
                {
                    this.RecalculateEffectiveVerbosity();
                }
            }
        }

        public eLogVerbositySetting EffectiveVerbosity => m_effectiveVerbosity;

        public ObservableCollection<LogVerbosityScenario> Scenarios { get; }

        // ===========[ Public Interface Methods ]=============================

        /// <summary>
        /// Drives time-based criteria by passing an empty message through all scenarios, and lets any
        /// <see cref="RaiseFor"/> that has run out drop off. Call this from a periodic timer to allow
        /// <see cref="LogTimeCriteria"/> to fire without waiting for a real log line.
        /// </summary>
        public void EvaluateAllCriteria ()
        {
            this.ProcessLogLine(string.Empty, false);
        }

        /// <summary>
        /// Raises the verbosity to <paramref name="level"/> for <paramref name="duration"/>, then lets it drop back on its own.
        /// Several can overlap; the highest one still running wins. For a raise that only covers some lines, or that needs
        /// switching off early, use a <see cref="LogVerbosityScenario"/> and <see cref="LogVerbosityScenario.Activate"/> instead.
        /// </summary>
        /// <remarks>
        /// Expiry is noticed when the next line is logged (or on <see cref="EvaluateAllCriteria"/>), so
        /// <see cref="EffectiveVerbosity"/> can stay raised a little past the duration when nothing is logging.
        /// </remarks>
        public void RaiseFor (eLogVerbositySetting level, TimeSpan duration)
        {
            var raise = new TimedRaise(level, Environment.TickCount64 + (long)duration.TotalMilliseconds);
            lock (m_timedRaiseEditLock)
            {
                TimedRaise[] current = m_timedRaises;
                var updated = new TimedRaise[current.Length + 1];
                Array.Copy(current, updated, current.Length);
                updated[current.Length] = raise;
                m_timedRaises = updated;
            }

            this.RecalculateEffectiveVerbosity();
        }

        // ===========[ Internal Interface Methods ]===========================

        /// <summary>
        /// Processes a real log line through all scenarios and timed raises, and returns the verbosity that line is logged at.
        /// </summary>
        internal eLogVerbositySetting ProcessLogLine (string message, bool isError)
        {
            eLogVerbositySetting admitLevel = m_baseVerbosity;
            bool anyScenarioSwitched = false;
            foreach (LogVerbosityScenario scenario in m_scenarioSnapshot)
            {
                eLogVerbositySetting granted = scenario.ProcessLogLine(message, isError, out bool didSwitch);
                anyScenarioSwitched |= didSwitch;
                if (granted > admitLevel)
                {
                    admitLevel = granted;
                }
            }

            TimedRaise[] timedRaises = m_timedRaises;
            if (timedRaises.Length > 0)
            {
                long now = Environment.TickCount64;
                bool anyExpired = false;
                foreach (TimedRaise raise in timedRaises)
                {
                    if (raise.ExpiresAtMs <= now)
                    {
                        anyExpired = true;
                    }
                    else if (raise.Level > admitLevel)
                    {
                        admitLevel = raise.Level;
                    }
                }

                if (anyExpired)
                {
                    this.RemoveExpiredRaises(now);
                    anyScenarioSwitched = true;
                }
            }

            if (anyScenarioSwitched)
            {
                this.RecalculateEffectiveVerbosity();
            }

            return admitLevel;
        }

        /// <summary>
        /// Called by a scenario that was switched on or off by something other than a log line (Activate, Deactivate, Reset).
        /// </summary>
        internal void OnScenarioSwitchedOutsideOfLogging () => this.RecalculateEffectiveVerbosity();

        /// <summary>
        /// Clears every scenario and timed raise and puts the base verbosity back to its default.
        /// </summary>
        internal void ResetToDefaults ()
        {
            this.Scenarios.Clear();
            lock (m_timedRaiseEditLock)
            {
                m_timedRaises = Array.Empty<TimedRaise>();
            }

            this.BaseVerbosity = eLogVerbositySetting.Normal;
            this.RecalculateEffectiveVerbosity();
        }

        // ===========[ Helpers ]==============================================

        private void OnScenariosEdited ()
        {
            var snapshot = new LogVerbosityScenario[this.Scenarios.Count];
            this.Scenarios.CopyTo(snapshot, 0);
            m_scenarioSnapshot = Array.FindAll(snapshot, _IsNotNull);
            this.RecalculateEffectiveVerbosity();

            static bool _IsNotNull (LogVerbosityScenario scenario) => scenario != null;
        }

        private void RemoveExpiredRaises (long now)
        {
            lock (m_timedRaiseEditLock)
            {
                m_timedRaises = Array.FindAll(m_timedRaises, raise => raise.ExpiresAtMs > now);
            }
        }

        private void RecalculateEffectiveVerbosity ()
        {
            bool didChange;
            lock (m_recalculateLock)
            {
                eLogVerbositySetting effective = m_baseVerbosity;
                foreach (LogVerbosityScenario scenario in m_scenarioSnapshot)
                {
                    if (scenario.IsCurrentlyActive && scenario.RaiseToLevel > effective)
                    {
                        effective = scenario.RaiseToLevel;
                    }
                }

                long now = Environment.TickCount64;
                foreach (TimedRaise raise in m_timedRaises)
                {
                    if (raise.ExpiresAtMs > now && raise.Level > effective)
                    {
                        effective = raise.Level;
                    }
                }

                didChange = effective != m_effectiveVerbosity;
                m_effectiveVerbosity = effective;
            }

            // Raised outside the lock, since whatever listens may well log
            if (didChange)
            {
                this.RaisePropertyChanged(kEffectiveVerbosityChangedArgs);
            }
        }

        // ===========[ Subclasses ]===========================================

        private readonly record struct TimedRaise (eLogVerbositySetting Level, long ExpiresAtMs);

        /// <summary>
        /// Keeps the manager's snapshot of its scenarios current, and each scenario pointed at the manager that owns it.
        /// Logging reads the snapshot, never the collection, so editing the collection can't pull it out from under a line.
        /// </summary>
        private sealed class ScenarioCollection : ObservableCollection<LogVerbosityScenario>
        {
            private readonly LogVerbosityManager m_owner;

            public ScenarioCollection (LogVerbosityManager owner)
            {
                m_owner = owner;
            }

            protected override void InsertItem (int index, LogVerbosityScenario item)
            {
                base.InsertItem(index, item);
                this.Attach(item);
                m_owner.OnScenariosEdited();
            }

            protected override void SetItem (int index, LogVerbosityScenario item)
            {
                LogVerbosityScenario replaced = this[index];
                base.SetItem(index, item);
                this.Detach(replaced);
                this.Attach(item);
                m_owner.OnScenariosEdited();
            }

            protected override void RemoveItem (int index)
            {
                LogVerbosityScenario removed = this[index];
                base.RemoveItem(index);
                this.Detach(removed);
                m_owner.OnScenariosEdited();
            }

            protected override void MoveItem (int oldIndex, int newIndex)
            {
                base.MoveItem(oldIndex, newIndex);
                m_owner.OnScenariosEdited();
            }

            protected override void ClearItems ()
            {
                foreach (LogVerbosityScenario scenario in this)
                {
                    this.Detach(scenario);
                }

                base.ClearItems();
                m_owner.OnScenariosEdited();
            }

            private void Attach (LogVerbosityScenario scenario)
            {
                if (scenario != null)
                {
                    scenario.Owner = m_owner;
                }
            }

            private void Detach (LogVerbosityScenario scenario)
            {
                if (scenario?.Owner == m_owner)
                {
                    scenario.Owner = null;
                }
            }
        }
    }
}
