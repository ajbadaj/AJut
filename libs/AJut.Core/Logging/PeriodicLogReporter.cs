namespace AJut
{
    using System;
    using System.Threading;
    using AJut.Threading;

    /// <summary>
    /// Writes report lines through <see cref="Logger"/> on a period. Once every <see cref="Period"/>, on a timer thread, each
    /// registered report is asked for a line, and whatever it hands back is logged. Share one wherever reports should come out
    /// together, and build another for reports that want a different period.
    /// </summary>
    /// <remarks>
    /// This is how logging comes off of hot threads: they record into memory (a <see cref="SampleStatsAccumulator"/>, say), and
    /// one line per report comes out per period instead of one per event. The timer only exists while something is
    /// registered.
    /// <para/>
    /// A registration keeps its callback, and everything that callback holds on to, alive until it is disposed. Treat it like
    /// an event subscription and dispose it on teardown - or dispose the whole reporter, which drops every registration at once.
    /// </remarks>
    public sealed class PeriodicLogReporter : IDisposable
    {
        private static readonly TimeSpan kDefaultPeriod = TimeSpan.FromMinutes(5);

        // The longest period System.Threading.Timer accepts
        private static readonly TimeSpan kMaxPeriod = TimeSpan.FromMilliseconds(UInt32.MaxValue - 1.0);
        private const string kReportFailedFormat = "[Logger] Report '{0}' failed while producing its line";

        private readonly object m_registrationEditLock = new object();
        private readonly object m_reportingLock = new object();
        private volatile Registration[] m_registrations = Array.Empty<Registration>();
        private TimeSpan m_period = kDefaultPeriod;
        private Timer m_timer;
        private bool m_isDisposed;

        // ===========[ Setup/Construction/Teardown ]==========================

        /// <summary>
        /// Drops every registration and stops the timer. Nothing is reported on the way out - call <see cref="ReportNow"/>
        /// first for a final set of lines. Registering afterward throws.
        /// </summary>
        public void Dispose ()
        {
            lock (m_registrationEditLock)
            {
                if (m_isDisposed)
                {
                    return;
                }

                m_isDisposed = true;

                // Marked as well as dropped, so a round of reports already underway skips whatever it hasn't reached yet
                foreach (Registration registration in m_registrations)
                {
                    registration.MarkDisposed();
                }

                m_registrations = Array.Empty<Registration>();
                m_timer?.Dispose();
                m_timer = null;
            }
        }

        // ===========[ Properties ]===========================================

        /// <summary>
        /// How often every report is asked for a line. Default is five minutes. Changing it while the timer is running
        /// restarts the countdown, so the next round of reports is one new period from now.
        /// </summary>
        public TimeSpan Period
        {
            get
            {
                lock (m_registrationEditLock)
                {
                    return m_period;
                }
            }
            set
            {
                if (value <= TimeSpan.Zero || value > kMaxPeriod)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A report period has to be more than zero and at most about 49 days");
                }

                lock (m_registrationEditLock)
                {
                    m_period = value;
                    m_timer?.Change(value, value);
                }
            }
        }

        /// <summary>
        /// Whether the timer currently exists - it does exactly while something is registered.
        /// </summary>
        internal bool IsTimerRunning
        {
            get
            {
                lock (m_registrationEditLock)
                {
                    return m_timer != null;
                }
            }
        }

        // ===========[ Public Interface Methods ]=============================

        /// <summary>
        /// Adds a report that is asked for a line every <see cref="Period"/>, on the timer thread. Whatever it returns is logged
        /// as info at <paramref name="verbosity"/>; returning null logs nothing that time.
        /// </summary>
        /// <param name="name">Names the report when it fails - an exception out of <paramref name="produceLine"/> is logged as
        /// an error naming it, and the other reports still run</param>
        /// <param name="produceLine">Builds the line. Called on the timer thread (or wherever <see cref="ReportNow"/> is
        /// called), never on the thread that registered it, so anything it reads has to be safe to read from there</param>
        /// <param name="verbosity">The verbosity each line is logged at, so the usual verbosity gate and scenarios apply</param>
        /// <returns>The registration - dispose it to remove the report. The timer starts with the first registration and stops
        /// when the last is disposed.</returns>
        /// <exception cref="ObjectDisposedException">The reporter has been disposed</exception>
        public IDisposable Register (string name, Func<string> produceLine, eLogVerbosity verbosity = eLogVerbosity.Normal)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(produceLine);

            var registration = new Registration(this, name, produceLine, verbosity);
            lock (m_registrationEditLock)
            {
                ObjectDisposedException.ThrowIf(m_isDisposed, this);

                Registration[] current = m_registrations;
                var updated = new Registration[current.Length + 1];
                Array.Copy(current, updated, current.Length);
                updated[current.Length] = registration;
                m_registrations = updated;

                // Whichever thread registers first is arbitrary, so the timer mustn't carry its async locals into every round
                //  of reports, or keep them alive
                m_timer ??= FlowSuppressedTimer.Create(this.OnTimerTick, null, m_period, m_period);
            }

            return registration;
        }

        /// <summary>
        /// Adds a report of a <see cref="SampleStatsAccumulator"/>: every <see cref="Period"/> it takes
        /// <see cref="SampleStatsAccumulator.TakeSnapshotAndReset"/> and logs <c>"{name} {snapshot.Describe(unit)}"</c>. When
        /// nothing was recorded it logs nothing, though it still resets, so every report covers exactly one period.
        /// </summary>
        /// <param name="name">Starts the line and names the report when it fails. Anything a consumer wants in front of the
        /// numbers (a prefix to search on, for instance) goes here</param>
        /// <param name="stats">The accumulator to report. Recording into it from other threads carries on as normal</param>
        /// <param name="unit">Passed to <see cref="SampleStatsAccumulator.Snapshot.Describe"/>, appended to every sample value</param>
        /// <param name="verbosity">The verbosity each line is logged at</param>
        /// <returns>The registration - dispose it to remove the report</returns>
        /// <exception cref="ObjectDisposedException">The reporter has been disposed</exception>
        public IDisposable Register (string name, SampleStatsAccumulator stats, string unit = null, eLogVerbosity verbosity = eLogVerbosity.Normal)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(stats);

            var report = new StatsReport(name, stats, unit);
            return this.Register(name, report.ProduceLine, verbosity);
        }

        /// <summary>
        /// Asks every report for its line right now, on the calling thread, and logs them. For flushing everything out at the
        /// end of a session, and for tests. Never runs at the same time as the timer's round of reports, it waits for one that
        /// is underway.
        /// </summary>
        public void ReportNow ()
        {
            lock (m_reportingLock)
            {
                foreach (Registration registration in m_registrations)
                {
                    // Disposed partway through this round, by another report or another thread
                    if (registration.IsDisposed)
                    {
                        continue;
                    }

                    string line;
                    try
                    {
                        line = registration.ProduceLine();
                    }
                    catch (Exception exc)
                    {
                        Logger.LogError(String.Format(kReportFailedFormat, registration.Name), exc);
                        continue;
                    }

                    if (line != null)
                    {
                        Logger.LogInfo(line, registration.Verbosity);
                    }
                }
            }
        }

        // ===========[ Helpers ]==============================================

        private void OnTimerTick (object state)
        {
            try
            {
                this.ReportNow();
            }
            catch (Exception)
            {
                // ReportNow already logs a failing report, so this is only reachable when the logger itself is failing - there is
                //  nowhere left to report it, and an exception out of a timer callback takes the whole process down
            }
        }

        private void Unregister (Registration registration)
        {
            lock (m_registrationEditLock)
            {
                Registration[] current = m_registrations;
                int index = Array.IndexOf(current, registration);
                if (index < 0)
                {
                    return;
                }

                var updated = new Registration[current.Length - 1];
                Array.Copy(current, 0, updated, 0, index);
                Array.Copy(current, index + 1, updated, index, current.Length - index - 1);
                m_registrations = updated;

                if (updated.Length == 0 && m_timer != null)
                {
                    m_timer.Dispose();
                    m_timer = null;
                }
            }
        }

        // ===========[ Subclasses ]===========================================

        private sealed class Registration : IDisposable
        {
            private readonly PeriodicLogReporter m_owner;
            private volatile bool m_isDisposed;

            public Registration (PeriodicLogReporter owner, string name, Func<string> produceLine, eLogVerbosity verbosity)
            {
                m_owner = owner;
                this.Name = name;
                this.ProduceLine = produceLine;
                this.Verbosity = verbosity;
            }

            public string Name { get; }
            public Func<string> ProduceLine { get; }
            public eLogVerbosity Verbosity { get; }
            public bool IsDisposed => m_isDisposed;

            public void MarkDisposed () => m_isDisposed = true;

            public void Dispose ()
            {
                m_isDisposed = true;
                m_owner.Unregister(this);
            }
        }

        private sealed class StatsReport
        {
            private readonly string m_name;
            private readonly SampleStatsAccumulator m_stats;
            private readonly string m_unit;

            public StatsReport (string name, SampleStatsAccumulator stats, string unit)
            {
                m_name = name;
                m_stats = stats;
                m_unit = unit;
            }

            public string ProduceLine ()
            {
                SampleStatsAccumulator.Snapshot snapshot = m_stats.TakeSnapshotAndReset();
                return snapshot.Count == 0 ? null : $"{m_name} {snapshot.Describe(m_unit)}";
            }
        }
    }
}
