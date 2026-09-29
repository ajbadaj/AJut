namespace AJut.Core.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using AJut;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    // =====================================================================================
    // PeriodicLogReporterTests
    // Each test owns and disposes its own reporter. Reports still write through the static
    // Logger, so every test resets it and captures output with SetSingleOverrideLogTarget.
    // Timer ticks land on another thread, so the captured lines are guarded by a lock.
    // =====================================================================================

    [TestClass]
    public class PeriodicLogReporterTests
    {
        private static readonly TimeSpan kShortPeriod = TimeSpan.FromMilliseconds(30);

        private readonly object m_loggedOutputLock = new object();
        private List<string> m_loggedOutput;

        [TestInitialize]
        public void TestSetup ()
        {
            Logger.ResetToDefaults();
            Logger.ShouldLogToConsole = false;
            Logger.ShouldLogToTrace = false;

            m_loggedOutput = new List<string>();
            Logger.SetSingleOverrideLogTarget(this.CaptureOutput);
        }

        [TestCleanup]
        public void TestCleanup ()
        {
            Logger.ResetToDefaults();
        }

        private void CaptureOutput (string output)
        {
            lock (m_loggedOutputLock)
            {
                m_loggedOutput.Add(output);
            }
        }

        private int CountCaptured (string fragment)
        {
            lock (m_loggedOutputLock)
            {
                return m_loggedOutput.FindAll(output => output.Contains(fragment)).Count;
            }
        }

        private string FindCaptured (string fragment)
        {
            lock (m_loggedOutputLock)
            {
                return m_loggedOutput.Find(output => output.Contains(fragment));
            }
        }

        // ---- ReportNow ----

        [TestMethod]
        public void ReportNow_WritesEveryReportsLine ()
        {
            using var reporter = new PeriodicLogReporter();
            using IDisposable first = reporter.Register("first", () => "REPORT_ONE");
            using IDisposable second = reporter.Register("second", () => "REPORT_TWO");

            reporter.ReportNow();
            Assert.AreEqual(1, this.CountCaptured("REPORT_ONE"));
            Assert.AreEqual(1, this.CountCaptured("REPORT_TWO"));
            Assert.IsTrue(this.FindCaptured("REPORT_ONE").Contains("[Info]"));
        }

        [TestMethod]
        public void ReportNow_NullLine_WritesNothing ()
        {
            using var reporter = new PeriodicLogReporter();
            using IDisposable registration = reporter.Register("quiet", () => null);

            reporter.ReportNow();
            Assert.AreEqual(0, m_loggedOutput.Count);
        }

        [TestMethod]
        public void ReportNow_WritesAtTheRegisteredVerbosity ()
        {
            using var reporter = new PeriodicLogReporter();
            using IDisposable verbose = reporter.Register("verbose", () => "VERBOSE_REPORT", eLogVerbosity.Verbose);
            using IDisposable forced = reporter.Register("forced", () => "FORCED_REPORT", eLogVerbosity.Force);

            Logger.VerbosityManager.BaseVerbosity = eLogVerbositySetting.None;
            reporter.ReportNow();
            Assert.AreEqual(0, this.CountCaptured("VERBOSE_REPORT"));
            Assert.AreEqual(1, this.CountCaptured("FORCED_REPORT"));

            Logger.VerbosityManager.BaseVerbosity = eLogVerbositySetting.Verbose;
            reporter.ReportNow();
            Assert.AreEqual(1, this.CountCaptured("VERBOSE_REPORT"));
        }

        [TestMethod]
        public void ReportNow_ThrowingReport_IsLoggedByName_AndTheRestStillRun ()
        {
            using var reporter = new PeriodicLogReporter();
            using IDisposable broken = reporter.Register("broken-report", () => throw new InvalidOperationException("BROKEN_REPORT_EXCEPTION"));
            using IDisposable healthy = reporter.Register("healthy", () => "HEALTHY_REPORT");

            reporter.ReportNow();
            string error = this.FindCaptured("broken-report");
            Assert.IsNotNull(error);
            Assert.IsTrue(error.Contains("[Error]"), error);
            Assert.IsTrue(error.Contains("BROKEN_REPORT_EXCEPTION"), error);
            Assert.AreEqual(1, this.CountCaptured("HEALTHY_REPORT"));

            reporter.ReportNow();
            Assert.AreEqual(2, this.CountCaptured("broken-report"), "A report that throws stays registered and is tried again");
            Assert.AreEqual(2, this.CountCaptured("HEALTHY_REPORT"));
        }

        // ---- Registrations ----

        [TestMethod]
        public void Dispose_RemovesTheReport ()
        {
            using var reporter = new PeriodicLogReporter();
            IDisposable registration = reporter.Register("gone", () => "GONE_REPORT");
            registration.Dispose();
            registration.Dispose();

            reporter.ReportNow();
            Assert.AreEqual(0, this.CountCaptured("GONE_REPORT"));
        }

        [TestMethod]
        public void Dispose_WhileReporting_SkipsThatReport ()
        {
            using var reporter = new PeriodicLogReporter();
            IDisposable later = null;
            using IDisposable earlier = reporter.Register("earlier", () =>
            {
                later.Dispose();
                return "EARLIER_REPORT";
            });
            later = reporter.Register("later", () => "LATER_REPORT");

            reporter.ReportNow();
            Assert.AreEqual(1, this.CountCaptured("EARLIER_REPORT"));
            Assert.AreEqual(0, this.CountCaptured("LATER_REPORT"));
        }

        [TestMethod]
        public void Register_NullArguments_Throw ()
        {
            using var reporter = new PeriodicLogReporter();
            Assert.ThrowsException<ArgumentNullException>(() => reporter.Register(null, () => "x"));
            Assert.ThrowsException<ArgumentNullException>(() => reporter.Register("name", (Func<string>)null));
            Assert.ThrowsException<ArgumentNullException>(() => reporter.Register("name", (SampleStatsAccumulator)null));
        }

        // ---- Timer ----

        [TestMethod]
        public void Timer_RunsOnlyWhileSomethingIsRegistered ()
        {
            using var reporter = new PeriodicLogReporter();
            Assert.IsFalse(reporter.IsTimerRunning);

            IDisposable first = reporter.Register("first", () => null);
            IDisposable second = reporter.Register("second", () => null);
            Assert.IsTrue(reporter.IsTimerRunning);

            first.Dispose();
            Assert.IsTrue(reporter.IsTimerRunning);

            second.Dispose();
            Assert.IsFalse(reporter.IsTimerRunning);

            using IDisposable third = reporter.Register("third", () => null);
            Assert.IsTrue(reporter.IsTimerRunning, "Registering again after going empty starts it again");
        }

        [TestMethod]
        public void Timer_TicksRepeatedly_OffTheRegisteringThread ()
        {
            using var reporter = new PeriodicLogReporter { Period = kShortPeriod };
            int registeringThreadId = Environment.CurrentManagedThreadId;
            int tickThreadId = registeringThreadId;
            using IDisposable registration = reporter.Register("timed", () =>
            {
                Volatile.Write(ref tickThreadId, Environment.CurrentManagedThreadId);
                return "TIMED_REPORT";
            });

            Assert.IsTrue(LoggerTestHelpers.WaitFor(() => this.CountCaptured("TIMED_REPORT") >= 2), "Expected at least two ticks");
            Assert.AreNotEqual(registeringThreadId, Volatile.Read(ref tickThreadId));
        }

        [TestMethod]
        public void Timer_ReportsDoNotSeeTheRegisteringThreadsAsyncLocals ()
        {
            var ambient = new AsyncLocal<string> { Value = "REGISTRANT_VALUE" };
            using var reporter = new PeriodicLogReporter { Period = kShortPeriod };
            using IDisposable registration = reporter.Register("ambient", () => $"AMBIENT_REPORT seen={ambient.Value ?? "none"}");

            Assert.IsTrue(LoggerTestHelpers.WaitFor(() => this.CountCaptured("AMBIENT_REPORT") >= 1));
            Assert.AreEqual(0, this.CountCaptured("seen=REGISTRANT_VALUE"));
            Assert.IsTrue(this.CountCaptured("seen=none") >= 1);
        }

        [TestMethod]
        public void Timer_PeriodChangedWhileRunning_TakesEffect ()
        {
            using var reporter = new PeriodicLogReporter();
            using IDisposable registration = reporter.Register("timed", () => "RETIMED_REPORT");

            reporter.Period = kShortPeriod;
            Assert.IsTrue(LoggerTestHelpers.WaitFor(() => this.CountCaptured("RETIMED_REPORT") >= 1), "The five minute default should have been replaced");
        }

        [TestMethod]
        public void Timer_StopsTicking_OnceTheLastRegistrationIsDisposed ()
        {
            using var reporter = new PeriodicLogReporter { Period = kShortPeriod };
            IDisposable registration = reporter.Register("timed", () => "STOPPING_REPORT");
            Assert.IsTrue(LoggerTestHelpers.WaitFor(() => this.CountCaptured("STOPPING_REPORT") >= 1));

            registration.Dispose();

            // A tick that was already underway can still land, so settle before counting
            Thread.Sleep(kShortPeriod * 2);
            int countAfterDispose = this.CountCaptured("STOPPING_REPORT");
            Thread.Sleep(kShortPeriod * 4);
            Assert.AreEqual(countAfterDispose, this.CountCaptured("STOPPING_REPORT"));
        }

        [TestMethod]
        public void Period_Default_IsFiveMinutes ()
        {
            using var reporter = new PeriodicLogReporter();
            Assert.AreEqual(TimeSpan.FromMinutes(5), reporter.Period);
        }

        [TestMethod]
        public void Period_ZeroOrNegative_Throws ()
        {
            using var reporter = new PeriodicLogReporter();
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => reporter.Period = TimeSpan.Zero);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => reporter.Period = TimeSpan.FromSeconds(-1));
        }

        // ---- SampleStatsAccumulator reports ----

        [TestMethod]
        public void StatsReport_WritesNameAndDescription_ThenResets ()
        {
            using var reporter = new PeriodicLogReporter();
            var stats = new SampleStatsAccumulator(threshold: 2.0);
            using IDisposable registration = reporter.Register("[PERF] frame-time", stats, "ms");

            stats.Record(1.0);
            stats.Record(3.0);
            reporter.ReportNow();

            string line = this.FindCaptured("[PERF] frame-time n=2 ");
            Assert.IsNotNull(line);
            Assert.IsTrue(line.Contains(" max=3ms "), line);
            Assert.IsTrue(line.EndsWith(" over=1"), line);
            Assert.AreEqual(0L, stats.TakeSnapshot().Count, "The report resets what it reported");
        }

        [TestMethod]
        public void StatsReport_NothingRecorded_WritesNothing ()
        {
            using var reporter = new PeriodicLogReporter();
            var stats = new SampleStatsAccumulator();
            using IDisposable registration = reporter.Register("idle-metric", stats);

            reporter.ReportNow();
            Assert.AreEqual(0, this.CountCaptured("idle-metric"));
        }

        // ---- Disposing the reporter ----

        [TestMethod]
        public void Dispose_DropsEveryReport_AndStopsTheTimer ()
        {
            var reporter = new PeriodicLogReporter { Period = kShortPeriod };
            IDisposable first = reporter.Register("first", () => "FIRST_REPORT");
            reporter.Register("second", () => "SECOND_REPORT");
            Assert.IsTrue(reporter.IsTimerRunning);

            reporter.Dispose();
            Assert.IsFalse(reporter.IsTimerRunning);

            // Nothing is left to report after, from ReportNow or the timer. A timer tick that was already producing a line when
            //  the dispose landed can still finish, so settle before counting.
            Thread.Sleep(kShortPeriod);
            int countAfterDispose = this.CountCaptured("_REPORT");
            reporter.ReportNow();
            Thread.Sleep(kShortPeriod * 3);
            Assert.AreEqual(countAfterDispose, this.CountCaptured("_REPORT"));

            // Harmless after the reporter already dropped it
            first.Dispose();
            reporter.Dispose();
        }

        [TestMethod]
        public void Dispose_WhileReporting_SkipsTheReportsNotYetReached ()
        {
            var reporter = new PeriodicLogReporter();
            reporter.Register("disposer", () =>
            {
                reporter.Dispose();
                return "DISPOSER_REPORT";
            });
            reporter.Register("skipped", () => "SKIPPED_REPORT");

            reporter.ReportNow();
            Assert.AreEqual(1, this.CountCaptured("DISPOSER_REPORT"));
            Assert.AreEqual(0, this.CountCaptured("SKIPPED_REPORT"));
        }

        [TestMethod]
        public void Register_AfterDispose_Throws ()
        {
            var reporter = new PeriodicLogReporter();
            reporter.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(() => reporter.Register("late", () => "LATE_REPORT"));
            Assert.ThrowsException<ObjectDisposedException>(() => reporter.Register("late-stats", new SampleStatsAccumulator()));
            Assert.IsFalse(reporter.IsTimerRunning);
        }
    }
}
