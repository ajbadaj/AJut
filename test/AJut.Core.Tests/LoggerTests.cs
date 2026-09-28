namespace AJut.Core.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using System.Threading;
    using AJut;
    using AJut.Core.CrashHost;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    // =====================================================================================
    // LoggerCriteriaTests
    // Pure unit tests for criteria, scenario, and manager classes.
    // No Logger, no files, no static state touched.
    // =====================================================================================

    [TestClass]
    public class LoggerCriteriaTests
    {
        // ---- LogTextMatchCriteria ----

        [TestMethod]
        public void TextMatch_Contains_MatchesWhenPresent ()
        {
            var c = new LogTextMatchCriteria { SearchText = "foo", SearchType = eLogSearch.Contains };
            Assert.IsTrue(c.Evaluate("has foo in it", false));
        }

        [TestMethod]
        public void TextMatch_Contains_NoMatchWhenAbsent ()
        {
            var c = new LogTextMatchCriteria { SearchText = "foo", SearchType = eLogSearch.Contains };
            Assert.IsFalse(c.Evaluate("nothing here", false));
        }

        [TestMethod]
        public void TextMatch_StartsWith_Matches ()
        {
            var c = new LogTextMatchCriteria { SearchText = "START", SearchType = eLogSearch.StartsWith };
            Assert.IsTrue(c.Evaluate("START of message", false));
            Assert.IsFalse(c.Evaluate("not START", false));
        }

        [TestMethod]
        public void TextMatch_EndsWith_Matches ()
        {
            var c = new LogTextMatchCriteria { SearchText = "END", SearchType = eLogSearch.EndsWith };
            Assert.IsTrue(c.Evaluate("message END", false));
            Assert.IsFalse(c.Evaluate("END not here", false));
        }

        [TestMethod]
        public void TextMatch_Regex_Matches ()
        {
            var c = new LogTextMatchCriteria { SearchText = @"\d{4}", SearchType = eLogSearch.Regex };
            Assert.IsTrue(c.Evaluate("code 2026 found", false));
            Assert.IsFalse(c.Evaluate("no digits", false));
        }

        [TestMethod]
        public void TextMatch_CaseInsensitive_ByDefault ()
        {
            var c = new LogTextMatchCriteria { SearchText = "TRIGGER", SearchType = eLogSearch.Contains };
            Assert.IsTrue(c.Evaluate("trigger found", false), "Default should be case-insensitive.");
        }

        [TestMethod]
        public void TextMatch_CaseSensitive_DoesNotMatchWrongCase ()
        {
            var c = new LogTextMatchCriteria { SearchText = "TRIGGER", SearchType = eLogSearch.Contains, CaseSensitive = true };
            Assert.IsFalse(c.Evaluate("trigger found", false), "Case-sensitive should not match different case.");
            Assert.IsTrue(c.Evaluate("TRIGGER found", false));
        }

        [TestMethod]
        public void TextMatch_RequiredMatchCount_DelaysReturn ()
        {
            var c = new LogTextMatchCriteria { SearchText = "HIT", SearchType = eLogSearch.Contains, RequiredMatchCount = 3 };
            Assert.IsFalse(c.Evaluate("HIT", false));
            Assert.IsFalse(c.Evaluate("HIT", false));
            Assert.IsTrue(c.Evaluate("HIT", false));
        }

        [TestMethod]
        public void TextMatch_InitiateScenario_ResetsMatchCount ()
        {
            var c = new LogTextMatchCriteria { SearchText = "HIT", SearchType = eLogSearch.Contains, RequiredMatchCount = 2 };
            c.Evaluate("HIT", false);    // count = 1
            c.InitiateScenario();        // reset to 0
            Assert.IsFalse(c.Evaluate("HIT", false), "After InitiateScenario, count should reset - needs 2 more matches.");
            Assert.IsTrue(c.Evaluate("HIT", false));
        }

        // ---- LogTimeCriteria ----

        [TestMethod]
        public void TimeCriteria_NotSatisfiedBeforeDuration ()
        {
            var c = new LogTimeCriteria { Duration = TimeSpan.FromSeconds(60) };
            c.InitiateScenario();
            Assert.IsFalse(c.Evaluate("", false));
        }

        [TestMethod]
        public void TimeCriteria_SatisfiedAfterDuration ()
        {
            var c = new LogTimeCriteria { Duration = TimeSpan.FromMilliseconds(30) };
            c.InitiateScenario();
            Thread.Sleep(60);
            Assert.IsTrue(c.Evaluate("", false));
        }

        [TestMethod]
        public void TimeCriteria_NotArmed_NeverSatisfied ()
        {
            var c = new LogTimeCriteria { Duration = TimeSpan.FromMilliseconds(0) };
            // Duration is 0 but never armed - m_armedAt is null
            Assert.IsFalse(c.Evaluate("", false));
        }

        [TestMethod]
        public void TimeCriteria_Evaluate_IgnoresMessageChecksTime ()
        {
            var c = new LogTimeCriteria { Duration = TimeSpan.FromMilliseconds(30) };
            c.InitiateScenario();
            Thread.Sleep(60);
            // Evaluate should return true based on elapsed time, regardless of message content
            Assert.IsTrue(c.Evaluate("any message", false));
        }

        [TestMethod]
        public void TimeCriteria_Reset_ClearsArmedTime ()
        {
            var c = new LogTimeCriteria { Duration = TimeSpan.FromMilliseconds(30) };
            c.InitiateScenario();
            Thread.Sleep(60);
            Assert.IsTrue(c.Evaluate("", false));
            c.Reset();
            Assert.IsFalse(c.Evaluate("", false), "After Reset, m_armedAt is null and Evaluate should return false.");
        }

        // ---- LogCriteriaCombination ----

        [TestMethod]
        public void Combination_And_AllMustPass ()
        {
            var combo = LogCriteriaCombination.And(
                new LogTextMatchCriteria { SearchText = "ALPHA", SearchType = eLogSearch.Contains },
                new LogTextMatchCriteria { SearchText = "BETA", SearchType = eLogSearch.Contains });

            Assert.IsFalse(combo.Evaluate("only ALPHA", false));
            Assert.IsFalse(combo.Evaluate("only BETA", false));
            Assert.IsTrue(combo.Evaluate("ALPHA and BETA", false));
        }

        [TestMethod]
        public void Combination_Or_AnyOneSuffices ()
        {
            var combo = LogCriteriaCombination.Or(
                new LogTextMatchCriteria { SearchText = "PATH_A", SearchType = eLogSearch.Contains },
                new LogTextMatchCriteria { SearchText = "PATH_B", SearchType = eLogSearch.Contains });

            Assert.IsFalse(combo.Evaluate("neither", false));
            Assert.IsTrue(combo.Evaluate("has PATH_A", false));
        }

        [TestMethod]
        public void Combination_Or_SecondPathAlsoSuffices ()
        {
            var combo = LogCriteriaCombination.Or(
                new LogTextMatchCriteria { SearchText = "PATH_A", SearchType = eLogSearch.Contains },
                new LogTextMatchCriteria { SearchText = "PATH_B", SearchType = eLogSearch.Contains });

            Assert.IsTrue(combo.Evaluate("only PATH_B", false));
        }

        [TestMethod]
        public void Combination_Nested_AndOfOrs ()
        {
            // AND( OR(A, B), OR(C, D) ) - needs one from each group
            var combo = LogCriteriaCombination.And(
                LogCriteriaCombination.Or(
                    new LogTextMatchCriteria { SearchText = "A", SearchType = eLogSearch.Contains },
                    new LogTextMatchCriteria { SearchText = "B", SearchType = eLogSearch.Contains }),
                LogCriteriaCombination.Or(
                    new LogTextMatchCriteria { SearchText = "C", SearchType = eLogSearch.Contains },
                    new LogTextMatchCriteria { SearchText = "D", SearchType = eLogSearch.Contains }));

            Assert.IsFalse(combo.Evaluate("only A - group 2 missing", false));
            Assert.IsFalse(combo.Evaluate("only C - group 1 missing", false));
            Assert.IsTrue(combo.Evaluate("A and D - one from each group", false));
        }

        [TestMethod]
        public void Combination_And_EvaluatesAllChildren_NoShortCircuit ()
        {
            // Verify that when A fails, B is still evaluated (so its RequiredMatchCount accumulates).
            // A: requires "ALPHA", count=1. B: requires "BETA", count=2.
            // Logging "BETA" lines when A fails should still accumulate B's count.
            var criteriaB = new LogTextMatchCriteria { SearchText = "BETA", SearchType = eLogSearch.Contains, RequiredMatchCount = 2 };
            var combo = LogCriteriaCombination.And(
                new LogTextMatchCriteria { SearchText = "ALPHA", SearchType = eLogSearch.Contains },
                criteriaB);

            combo.Evaluate("BETA only - A fails", false); // B count = 1
            combo.Evaluate("BETA only - A fails", false); // B count = 2 (satisfied), but A still fails -> AND = false
            Assert.IsFalse(combo.Evaluate("BETA only - A fails", false), "AND should be false when A fails, even if B is satisfied.");

            // Now A matches too - B count is already >= 2 from prior evaluations
            Assert.IsTrue(combo.Evaluate("ALPHA and BETA", false), "Should satisfy AND now: A matches (count=1>=1) and B was already counted to >= 2.");
        }

        [TestMethod]
        public void Combination_DefaultCtor_AllowsSerializationStyle ()
        {
            // Verify the default constructor + property assignment works (for serialization)
            var combo = new LogCriteriaCombination();
            combo.Combination = eLogCombination.Or;
            combo.Criteria.Add(new LogTextMatchCriteria { SearchText = "X", SearchType = eLogSearch.Contains });

            Assert.IsTrue(combo.Evaluate("has X", false));
        }

        // ---- LogVerbosityScenario ----

        [TestMethod]
        public void Scenario_ActivatesOnEnterCriteria ()
        {
            var manager = new LogVerbosityManager();
            var scenario = new LogVerbosityScenario
            {
                RaiseToLevel = eLogVerbositySetting.Verbose,
                EnterCriteria = new LogTextMatchCriteria { SearchText = "GO", SearchType = eLogSearch.Contains },
            };
            manager.Scenarios.Add(scenario);

            Assert.IsFalse(scenario.IsCurrentlyActive);
            manager.ProcessLogLine("GO signal", false);
            Assert.IsTrue(scenario.IsCurrentlyActive);
        }

        [TestMethod]
        public void Scenario_DoesNotActivateBeforeEnterCriteria ()
        {
            var manager = new LogVerbosityManager();
            var scenario = new LogVerbosityScenario
            {
                RaiseToLevel = eLogVerbositySetting.Verbose,
                EnterCriteria = new LogTextMatchCriteria { SearchText = "GO", SearchType = eLogSearch.Contains },
            };
            manager.Scenarios.Add(scenario);

            manager.ProcessLogLine("not yet", false);
            Assert.IsFalse(scenario.IsCurrentlyActive);
        }

        [TestMethod]
        public void Scenario_DeactivatesOnExitCriteria ()
        {
            var manager = new LogVerbosityManager();
            var scenario = new LogVerbosityScenario
            {
                RaiseToLevel = eLogVerbositySetting.Verbose,
                EnterCriteria = new LogTextMatchCriteria { SearchText = "START", SearchType = eLogSearch.Contains },
                ExitCriteria  = new LogTextMatchCriteria { SearchText = "STOP", SearchType = eLogSearch.Contains },
            };
            manager.Scenarios.Add(scenario);

            manager.ProcessLogLine("START", false);
            Assert.IsTrue(scenario.IsCurrentlyActive);

            manager.ProcessLogLine("STOP", false);
            Assert.IsFalse(scenario.IsCurrentlyActive);
        }

        [TestMethod]
        public void Scenario_NoExitCriteria_StaysActive ()
        {
            var manager = new LogVerbosityManager();
            var scenario = new LogVerbosityScenario
            {
                RaiseToLevel = eLogVerbositySetting.Verbose,
                EnterCriteria = new LogTextMatchCriteria { SearchText = "START", SearchType = eLogSearch.Contains },
            };
            manager.Scenarios.Add(scenario);

            manager.ProcessLogLine("START", false);
            Assert.IsTrue(scenario.IsCurrentlyActive);

            manager.ProcessLogLine("anything", false);
            manager.ProcessLogLine("else", false);
            Assert.IsTrue(scenario.IsCurrentlyActive, "No exit criteria - should stay active indefinitely.");
        }

        [TestMethod]
        public void Scenario_ReArmsAutomaticallyAfterDeactivation ()
        {
            var manager = new LogVerbosityManager();
            var scenario = new LogVerbosityScenario
            {
                RaiseToLevel = eLogVerbositySetting.Verbose,
                EnterCriteria = new LogTextMatchCriteria { SearchText = "START", SearchType = eLogSearch.Contains },
                ExitCriteria  = new LogTextMatchCriteria { SearchText = "STOP", SearchType = eLogSearch.Contains },
            };
            manager.Scenarios.Add(scenario);

            manager.ProcessLogLine("START", false);
            manager.ProcessLogLine("STOP", false);
            Assert.IsFalse(scenario.IsCurrentlyActive);

            // Should be re-armed - can activate again
            manager.ProcessLogLine("START", false);
            Assert.IsTrue(scenario.IsCurrentlyActive, "Scenario should re-activate after being automatically re-armed by deactivation.");
        }

        [TestMethod]
        public void Scenario_Reset_ManuallyRearmsFromActiveState ()
        {
            var manager = new LogVerbosityManager();
            var scenario = new LogVerbosityScenario
            {
                RaiseToLevel = eLogVerbositySetting.Verbose,
                EnterCriteria = new LogTextMatchCriteria { SearchText = "START", SearchType = eLogSearch.Contains },
            };
            manager.Scenarios.Add(scenario);

            manager.ProcessLogLine("START", false);
            Assert.IsTrue(scenario.IsCurrentlyActive);

            scenario.Reset();
            Assert.IsFalse(scenario.IsCurrentlyActive, "Reset() should deactivate the scenario.");

            manager.ProcessLogLine("START", false);
            Assert.IsTrue(scenario.IsCurrentlyActive, "Scenario should be re-activatable after Reset().");
        }

        [TestMethod]
        public void Scenario_IsEnabled_False_PreventsActivation ()
        {
            var manager = new LogVerbosityManager();
            var scenario = new LogVerbosityScenario
            {
                RaiseToLevel = eLogVerbositySetting.Verbose,
                IsEnabled = false,
                EnterCriteria = new LogTextMatchCriteria { SearchText = "GO", SearchType = eLogSearch.Contains },
            };
            manager.Scenarios.Add(scenario);

            manager.ProcessLogLine("GO signal", false);
            Assert.IsFalse(scenario.IsCurrentlyActive, "Disabled scenario should not activate.");
        }

        // ---- LogVerbosityManager ----

        [TestMethod]
        public void Manager_EffectiveVerbosity_EqualsBaseWhenNoScenarios ()
        {
            var manager = new LogVerbosityManager { BaseVerbosity = eLogVerbositySetting.Detailed };
            Assert.AreEqual(eLogVerbositySetting.Detailed, manager.EffectiveVerbosity);
        }

        [TestMethod]
        public void Manager_EffectiveVerbosity_IsMaxOfBaseAndActiveScenarios ()
        {
            var manager = new LogVerbosityManager { BaseVerbosity = eLogVerbositySetting.Normal };
            var scenario = new LogVerbosityScenario
            {
                RaiseToLevel = eLogVerbositySetting.Verbose,
                EnterCriteria = new LogTextMatchCriteria { SearchText = "RAISE", SearchType = eLogSearch.Contains },
            };
            manager.Scenarios.Add(scenario);

            Assert.AreEqual(eLogVerbositySetting.Normal, manager.EffectiveVerbosity);
            manager.ProcessLogLine("RAISE verbosity", false);
            Assert.AreEqual(eLogVerbositySetting.Verbose, manager.EffectiveVerbosity);
        }

        [TestMethod]
        public void Manager_EffectiveVerbosity_MultipleScenarios_UsesHighest ()
        {
            var manager = new LogVerbosityManager { BaseVerbosity = eLogVerbositySetting.Normal };

            var s1 = new LogVerbosityScenario
            {
                RaiseToLevel = eLogVerbositySetting.Detailed,
                EnterCriteria = new LogTextMatchCriteria { SearchText = "RAISE_DETAILED", SearchType = eLogSearch.Contains },
            };
            var s2 = new LogVerbosityScenario
            {
                RaiseToLevel = eLogVerbositySetting.Verbose,
                EnterCriteria = new LogTextMatchCriteria { SearchText = "RAISE_VERBOSE", SearchType = eLogSearch.Contains },
            };
            manager.Scenarios.Add(s1);
            manager.Scenarios.Add(s2);

            manager.ProcessLogLine("RAISE_DETAILED", false);
            Assert.AreEqual(eLogVerbositySetting.Detailed, manager.EffectiveVerbosity);

            manager.ProcessLogLine("RAISE_VERBOSE", false);
            Assert.AreEqual(eLogVerbositySetting.Verbose, manager.EffectiveVerbosity, "EffectiveVerbosity should be the highest across all active scenarios.");
        }

        [TestMethod]
        public void Manager_EvaluateAllCriteria_ActivatesTimeBasedScenario ()
        {
            var manager = new LogVerbosityManager();
            var timeCriteria = new LogTimeCriteria { Duration = TimeSpan.FromMilliseconds(30) };
            var scenario = new LogVerbosityScenario
            {
                RaiseToLevel = eLogVerbositySetting.Verbose,
                EnterCriteria = timeCriteria,
            };
            manager.Scenarios.Add(scenario);

            timeCriteria.InitiateScenario();
            Assert.IsFalse(scenario.IsCurrentlyActive);

            Thread.Sleep(60);
            manager.EvaluateAllCriteria();
            Assert.IsTrue(scenario.IsCurrentlyActive, "EvaluateAllCriteria should activate time-based scenario after duration elapses.");
        }

        // ---- Count exit window ----

        [TestMethod]
        public void Scenario_CountExit_AdmitsTheTriggerLinePlusCountMinusOneMore ()
        {
            var manager = new LogVerbosityManager { BaseVerbosity = eLogVerbositySetting.Normal };
            manager.Scenarios.Add(new LogVerbosityScenario
            {
                RaiseToLevel = eLogVerbositySetting.Verbose,
                EnterCriteria = new LogTextMatchCriteria { SearchText = "[TRIGGER]" },
                ExitCriteria = new LogCountCriteria { CountThreshold = 3 },
            });

            Assert.AreEqual(eLogVerbositySetting.Verbose, manager.ProcessLogLine("[TRIGGER] opens it", false), "The trigger line is inside the window.");
            Assert.AreEqual(eLogVerbositySetting.Verbose, manager.ProcessLogLine("second", false));
            Assert.AreEqual(eLogVerbositySetting.Verbose, manager.ProcessLogLine("third", false));
            Assert.AreEqual(eLogVerbositySetting.Normal, manager.ProcessLogLine("fourth", false), "Count(3) is the trigger plus two more, the fourth line closes the window.");
        }

        [TestMethod]
        public void Scenario_CountExitOfOne_IsExactlyTheTriggerLine ()
        {
            var manager = new LogVerbosityManager { BaseVerbosity = eLogVerbositySetting.Normal };
            manager.Scenarios.Add(new LogVerbosityScenario
            {
                RaiseToLevel = eLogVerbositySetting.Verbose,
                EnterCriteria = new LogTextMatchCriteria { SearchText = "[TRIGGER]" },
                ExitCriteria = new LogCountCriteria { CountThreshold = 1 },
            });

            Assert.AreEqual(eLogVerbositySetting.Verbose, manager.ProcessLogLine("[TRIGGER] opens it", false));
            Assert.AreEqual(eLogVerbositySetting.Normal, manager.ProcessLogLine("next", false));
        }

        // ---- Switching scenarios from code ----

        [TestMethod]
        public void Scenario_Activate_RaisesWithoutALogLine ()
        {
            var manager = new LogVerbosityManager { BaseVerbosity = eLogVerbositySetting.Normal };
            var scenario = new LogVerbosityScenario { RaiseToLevel = eLogVerbositySetting.Verbose };
            manager.Scenarios.Add(scenario);

            scenario.Activate();

            Assert.IsTrue(scenario.IsCurrentlyActive);
            Assert.AreEqual(eLogVerbositySetting.Verbose, manager.EffectiveVerbosity, "Activating from code should update the manager straight away.");
            Assert.AreEqual(eLogVerbositySetting.Verbose, manager.ProcessLogLine("any line", false));
        }

        [TestMethod]
        public void Scenario_Activate_StartsTheExitCriteriaWatchingFromThere ()
        {
            var manager = new LogVerbosityManager { BaseVerbosity = eLogVerbositySetting.Normal };
            var scenario = new LogVerbosityScenario
            {
                RaiseToLevel = eLogVerbositySetting.Verbose,
                ExitCriteria = new LogCountCriteria { CountThreshold = 2 },
            };
            manager.Scenarios.Add(scenario);

            scenario.Activate();

            Assert.AreEqual(eLogVerbositySetting.Verbose, manager.ProcessLogLine("first line after activating", false));
            Assert.AreEqual(eLogVerbositySetting.Normal, manager.ProcessLogLine("second line closes it", false));
            Assert.IsFalse(scenario.IsCurrentlyActive);
        }

        [TestMethod]
        public void Scenario_Deactivate_DropsTheRaiseAndReArms ()
        {
            var manager = new LogVerbosityManager { BaseVerbosity = eLogVerbositySetting.Normal };
            var scenario = new LogVerbosityScenario
            {
                RaiseToLevel = eLogVerbositySetting.Verbose,
                EnterCriteria = new LogTextMatchCriteria { SearchText = "GO" },
            };
            manager.Scenarios.Add(scenario);

            scenario.Activate();
            scenario.Deactivate();

            Assert.IsFalse(scenario.IsCurrentlyActive);
            Assert.AreEqual(eLogVerbositySetting.Normal, manager.EffectiveVerbosity, "Deactivating from code should update the manager straight away.");

            manager.ProcessLogLine("GO again", false);
            Assert.IsTrue(scenario.IsCurrentlyActive, "Deactivating should leave the scenario armed to activate again.");
        }

        [TestMethod]
        public void Scenario_ActivateAndDeactivate_RaiseIsCurrentlyActiveChanged ()
        {
            var scenario = new LogVerbosityScenario();
            var recorder = new PropertyChangeRecorder();
            scenario.PropertyChanged += recorder.OnPropertyChanged;

            scenario.Activate();
            scenario.Activate();
            scenario.Deactivate();

            scenario.PropertyChanged -= recorder.OnPropertyChanged;
            CollectionAssert.AreEqual(
                new[] { nameof(LogVerbosityScenario.IsCurrentlyActive), nameof(LogVerbosityScenario.IsCurrentlyActive) },
                recorder.PropertyNames,
                "Expected one change on, one change off, and nothing for activating twice."
            );
        }

        [TestMethod]
        public void Scenarios_RemovingAnActiveScenario_DropsTheRaiseRightAway ()
        {
            var manager = new LogVerbosityManager { BaseVerbosity = eLogVerbositySetting.Normal };
            var scenario = new LogVerbosityScenario { RaiseToLevel = eLogVerbositySetting.Verbose };
            manager.Scenarios.Add(scenario);
            scenario.Activate();

            manager.Scenarios.Remove(scenario);

            Assert.AreEqual(eLogVerbositySetting.Normal, manager.EffectiveVerbosity);
            Assert.AreEqual(eLogVerbositySetting.Normal, manager.ProcessLogLine("any line", false));
        }

        // ---- Narrowing a raise ----

        [TestMethod]
        public void Scenario_AppliesTo_NarrowsWhichLinesTheRaiseCovers ()
        {
            var manager = new LogVerbosityManager { BaseVerbosity = eLogVerbositySetting.Normal };
            var scenario = new TagOnlyScenario("[MINE]") { RaiseToLevel = eLogVerbositySetting.Verbose };
            manager.Scenarios.Add(scenario);
            scenario.Activate();

            Assert.AreEqual(eLogVerbositySetting.Verbose, manager.ProcessLogLine("[MINE] covered by the raise", false));
            Assert.AreEqual(eLogVerbositySetting.Normal, manager.ProcessLogLine("[OTHER] not covered", false));
            Assert.AreEqual(eLogVerbositySetting.Verbose, manager.EffectiveVerbosity, "EffectiveVerbosity stays the broad answer, it doesn't consult AppliesTo.");
        }

        // ---- Timed raises ----

        [TestMethod]
        public void RaiseFor_RaisesUntilItRunsOut ()
        {
            var manager = new LogVerbosityManager { BaseVerbosity = eLogVerbositySetting.Normal };

            manager.RaiseFor(eLogVerbositySetting.Verbose, TimeSpan.FromMilliseconds(50));
            Assert.AreEqual(eLogVerbositySetting.Verbose, manager.EffectiveVerbosity);
            Assert.AreEqual(eLogVerbositySetting.Verbose, manager.ProcessLogLine("inside the raise", false));

            Thread.Sleep(120);
            Assert.AreEqual(eLogVerbositySetting.Normal, manager.ProcessLogLine("after it ran out", false));
            Assert.AreEqual(eLogVerbositySetting.Normal, manager.EffectiveVerbosity);
        }

        [TestMethod]
        public void RaiseFor_Overlapping_HighestStillRunningWins ()
        {
            var manager = new LogVerbosityManager { BaseVerbosity = eLogVerbositySetting.Normal };

            manager.RaiseFor(eLogVerbositySetting.Detailed, TimeSpan.FromMinutes(5));
            manager.RaiseFor(eLogVerbositySetting.Verbose, TimeSpan.FromMilliseconds(50));
            Assert.AreEqual(eLogVerbositySetting.Verbose, manager.EffectiveVerbosity);

            Thread.Sleep(120);
            manager.EvaluateAllCriteria();
            Assert.AreEqual(eLogVerbositySetting.Detailed, manager.EffectiveVerbosity, "Once the short raise runs out, the longer one should still hold.");
        }

        /// <summary>
        /// A scenario whose raise only covers lines carrying a given tag.
        /// </summary>
        private sealed class TagOnlyScenario : LogVerbosityScenario
        {
            private readonly string m_tag;

            public TagOnlyScenario (string tag)
            {
                m_tag = tag;
            }

            protected override bool AppliesTo (string message, bool isError) => message.Contains(m_tag);
        }

        private sealed class PropertyChangeRecorder
        {
            public List<string> PropertyNames { get; } = new List<string>();

            public void OnPropertyChanged (object sender, System.ComponentModel.PropertyChangedEventArgs e)
            {
                this.PropertyNames.Add(e.PropertyName);
            }
        }
    }


    // =====================================================================================
    // LoggerGateTests
    // Tests Logger's verbosity gate using SetSingleOverrideLogTarget.
    // No files, no temp dirs. TestInitialize resets Logger to a clean instance.
    // =====================================================================================

    [TestClass]
    public class LoggerGateTests
    {
        private List<string> m_loggedOutput;

        [TestInitialize]
        public void TestSetup ()
        {
            // Default settings, no file
            Logger.ResetToDefaults();
            Logger.ShouldLogToConsole = false;
            Logger.ShouldLogToTrace = false;

            m_loggedOutput = new List<string>();
            Logger.SetSingleOverrideLogTarget(msg => m_loggedOutput.Add(msg));
        }

        [TestCleanup]
        public void TestCleanup ()
        {
            Logger.ResetToDefaults();
        }

        private bool Captured (string fragment) => m_loggedOutput.Exists(s => s.Contains(fragment));

        // ---- Verbosity gate ----

        [TestMethod]
        public void Gate_DetailedMessage_SuppressedAtNormal ()
        {
            Logger.VerbosityManager.BaseVerbosity = eLogVerbositySetting.Normal;
            Logger.LogInfo("DETAILED_MSG", eLogVerbosity.Detailed);
            Assert.IsFalse(Captured("DETAILED_MSG"));
        }

        [TestMethod]
        public void Gate_DetailedMessage_AppearsAtDetailed ()
        {
            Logger.VerbosityManager.BaseVerbosity = eLogVerbositySetting.Detailed;
            Logger.LogInfo("DETAILED_MSG", eLogVerbosity.Detailed);
            Assert.IsTrue(Captured("DETAILED_MSG"));
        }

        [TestMethod]
        public void Gate_DetailedMessage_AppearsAtVerbose ()
        {
            Logger.VerbosityManager.BaseVerbosity = eLogVerbositySetting.Verbose;
            Logger.LogInfo("DETAILED_MSG", eLogVerbosity.Detailed);
            Assert.IsTrue(Captured("DETAILED_MSG"));
        }

        [TestMethod]
        public void Gate_VerboseMessage_SuppressedAtDetailed ()
        {
            Logger.VerbosityManager.BaseVerbosity = eLogVerbositySetting.Detailed;
            Logger.LogInfo("VERBOSE_MSG", eLogVerbosity.Verbose);
            Assert.IsFalse(Captured("VERBOSE_MSG"));
        }

        [TestMethod]
        public void Gate_NormalInfo_SuppressedAtNone ()
        {
            Logger.VerbosityManager.BaseVerbosity = eLogVerbositySetting.None;
            Logger.LogInfo("NORMAL_MSG");
            Assert.IsFalse(Captured("NORMAL_MSG"));
        }

        [TestMethod]
        public void Gate_NormalInfo_SuppressedAtErrorsOnly ()
        {
            Logger.VerbosityManager.BaseVerbosity = eLogVerbositySetting.ErrorsOnly;
            Logger.LogInfo("NORMAL_MSG");
            Assert.IsFalse(Captured("NORMAL_MSG"));
        }

        [TestMethod]
        public void Gate_Error_AppearsAtErrorsOnly ()
        {
            Logger.VerbosityManager.BaseVerbosity = eLogVerbositySetting.ErrorsOnly;
            Logger.LogError("ERROR_MSG");
            Assert.IsTrue(Captured("ERROR_MSG"));
        }

        [TestMethod]
        public void Gate_Error_AppearsAtNormalAndAbove ()
        {
            foreach (var setting in new[] { eLogVerbositySetting.Normal, eLogVerbositySetting.Detailed, eLogVerbositySetting.Verbose })
            {
                m_loggedOutput.Clear();
                Logger.VerbosityManager.BaseVerbosity = setting;
                Logger.LogError($"ERROR_AT_{setting}");
                Assert.IsTrue(Captured($"ERROR_AT_{setting}"), $"LogError should appear at {setting}.");
            }
        }

        [TestMethod]
        public void Gate_Error_SuppressedAtNone ()
        {
            Logger.VerbosityManager.BaseVerbosity = eLogVerbositySetting.None;
            Logger.LogError("ERROR_MSG");
            Assert.IsFalse(Captured("ERROR_MSG"));
        }

        // ---- Scenario triggers before gate check ----

        [TestMethod]
        public void Gate_ScenarioRaisesVerbosityBeforeGateCheck_MessageIsLogged ()
        {
            Logger.VerbosityManager.BaseVerbosity = eLogVerbositySetting.None;

            var scenario = new LogVerbosityScenario
            {
                RaiseToLevel = eLogVerbositySetting.Verbose,
                EnterCriteria = new LogTextMatchCriteria { SearchText = "RAISE_FROM_NONE", SearchType = eLogSearch.Contains },
            };
            Logger.VerbosityManager.Scenarios.Add(scenario);

            // This is at Normal verbosity, manager is at None - would normally be suppressed.
            // Scenario processes the message first, raises EffectiveVerbosity to Verbose, gate passes.
            Logger.LogInfo("RAISE_FROM_NONE triggers scenario before gate check.");

            Assert.IsTrue(Captured("RAISE_FROM_NONE"), "Message should be logged: scenario raised EffectiveVerbosity before gate was evaluated.");
        }

        [TestMethod]
        public void Gate_RaiseFor_LetsHigherVerbosityThrough ()
        {
            Logger.VerbosityManager.BaseVerbosity = eLogVerbositySetting.Normal;
            Logger.VerbosityManager.RaiseFor(eLogVerbositySetting.Verbose, TimeSpan.FromMinutes(1));
            Logger.LogInfo("VERBOSE_MSG", eLogVerbosity.Verbose);
            Assert.IsTrue(Captured("VERBOSE_MSG"));
        }

        // ---- WouldLog ----

        [TestMethod]
        public void WouldLog_FollowsTheEffectiveVerbosity ()
        {
            Logger.VerbosityManager.BaseVerbosity = eLogVerbositySetting.Normal;
            Assert.IsTrue(Logger.WouldLog(eLogVerbosity.Normal));
            Assert.IsFalse(Logger.WouldLog(eLogVerbosity.Detailed));

            Logger.VerbosityManager.RaiseFor(eLogVerbositySetting.Verbose, TimeSpan.FromMinutes(1));
            Assert.IsTrue(Logger.WouldLog(eLogVerbosity.Verbose), "A raise should show up in WouldLog straight away.");
        }

        [TestMethod]
        public void WouldLog_NoneMeansNoExceptForce ()
        {
            Logger.VerbosityManager.BaseVerbosity = eLogVerbositySetting.None;
            Assert.IsFalse(Logger.WouldLog(eLogVerbosity.Normal));
            Assert.IsTrue(Logger.WouldLog(eLogVerbosity.Force));
        }

        [TestMethod]
        public void WouldLog_NothingWhileDisabled ()
        {
            Logger.Disable();
            Assert.IsFalse(Logger.WouldLog(eLogVerbosity.Force));
        }
    }


    // =====================================================================================
    // LoggerSplitTests
    // Tests file-split behavior. Uses temp dirs since we are testing actual stream/file logic.
    // Tests verify LogFilePath changes and file existence only - no file content reads.
    // =====================================================================================

    [TestClass]
    public class LoggerSplitTests
    {
        private const long kDefaultSplitSize = 5L * 1024L * 1024L;
        private string m_tempDir;

        [TestInitialize]
        public void TestSetup ()
        {
            Logger.ResetToDefaults();
            Logger.LogFileSplitSizeBytes = kDefaultSplitSize;
            m_tempDir = Path.Combine(Path.GetTempPath(), $"AJut_SplitTests_{Guid.NewGuid():N}");
        }

        [TestCleanup]
        public void TestCleanup ()
        {
            Logger.ResetToDefaults();
            Logger.LogFileSplitSizeBytes = kDefaultSplitSize;
            try
            {
                if (Directory.Exists(m_tempDir))
                {
                    Directory.Delete(m_tempDir, true);
                }
            }
            catch { }
        }

        [TestMethod]
        public void LogSplit_PathChangesToDashOne_AfterThreshold ()
        {
            Logger.LogFileSplitSizeBytes = 10;
            Logger.CreateAndStartWritingToLogFileIn(m_tempDir);

            string originalPath = Logger.LogFilePath;
            Logger.LogInfo("First message - long enough to exceed the 10-byte threshold.");

            string splitPath = Logger.LogFilePath;
            Assert.AreNotEqual(originalPath, splitPath, "LogFilePath should change after a split.");

            string ext = Path.GetExtension(originalPath);
            string baseName = originalPath.Substring(0, originalPath.Length - ext.Length);
            Assert.AreEqual($"{baseName}-1{ext}", splitPath, "Split file should be base + '-1' + extension.");
        }

        [TestMethod]
        public void LogSplit_BothFilesExistAfterSplit ()
        {
            Logger.LogFileSplitSizeBytes = 10;
            Logger.CreateAndStartWritingToLogFileIn(m_tempDir);

            string originalPath = Logger.LogFilePath;
            Logger.LogInfo("Trigger split.");
            string splitPath = Logger.LogFilePath;

            Assert.IsTrue(File.Exists(originalPath), "Original file should still exist after split.");
            Assert.IsTrue(File.Exists(splitPath), "Split file should be created.");
        }

        [TestMethod]
        public void LogSplit_MultipleSpits_IndexIncrementsCorrectly ()
        {
            Logger.LogFileSplitSizeBytes = 10;
            Logger.CreateAndStartWritingToLogFileIn(m_tempDir);

            string originalPath = Logger.LogFilePath;
            string ext = Path.GetExtension(originalPath);
            string baseName = originalPath.Substring(0, originalPath.Length - ext.Length);

            Logger.LogInfo("Message 1.");
            Logger.LogInfo("Message 2.");
            Logger.LogInfo("Message 3.");

            Assert.AreEqual($"{baseName}-3{ext}", Logger.LogFilePath, "LogFilePath should point to the -3 file after 3 splits.");
            Assert.IsTrue(File.Exists(originalPath),           "Original file should exist.");
            Assert.IsTrue(File.Exists($"{baseName}-1{ext}"),   "-1 file should exist.");
            Assert.IsTrue(File.Exists($"{baseName}-2{ext}"),   "-2 file should exist.");
            Assert.IsTrue(File.Exists($"{baseName}-3{ext}"),   "-3 file should exist.");
        }

        [TestMethod]
        public void LogSplit_DisabledWhenSizeIsZero ()
        {
            Logger.LogFileSplitSizeBytes = 0;
            Logger.CreateAndStartWritingToLogFileIn(m_tempDir);

            string originalPath = Logger.LogFilePath;
            for (int i = 0; i < 50; i++)
            {
                Logger.LogInfo($"Entry {i}: padding to ensure this would split if splitting were enabled.");
            }

            Assert.AreEqual(originalPath, Logger.LogFilePath, "LogFilePath should not change when splitting is disabled.");
            Assert.AreEqual(1, Directory.GetFiles(m_tempDir, "*.txt").Length, "Only one log file should exist.");
        }
    }


    // =====================================================================================
    // LoggerScenarioThreadingTests
    // Scenario evaluation with more than one thread logging. A PauseCriteria parks one
    // thread partway through its scenario pass, so each interleaving here is exact rather
    // than something a stress loop might happen to hit.
    // =====================================================================================

    [TestClass]
    public class LoggerScenarioThreadingTests
    {
        private readonly List<string> m_loggedOutput = new List<string>();

        [TestInitialize]
        public void TestSetup ()
        {
            Logger.ResetToDefaults();
            Logger.ShouldLogToConsole = false;
            Logger.ShouldLogToTrace = false;
            Logger.SetSingleOverrideLogTarget(this.CaptureOutput);
        }

        [TestCleanup]
        public void TestCleanup ()
        {
            Logger.ResetToDefaults();
        }

        [TestMethod]
        public void Scenario_TriggerLineIsLogged_WhenAnotherThreadClosesTheWindowMidPass ()
        {
            // A Count(1) exit means the window is exactly the line that opened it. Thread A opens the window and is
            //  parked before its line reaches the verbosity gate, thread B's line closes the window, then A resumes.
            //  A's own pass opened that window, so A's line has to come out no matter what B did in the meantime.
            Logger.VerbosityManager.BaseVerbosity = eLogVerbositySetting.Normal;
            var window = new LogVerbosityScenario
            {
                RaiseToLevel = eLogVerbositySetting.Verbose,
                EnterCriteria = new LogTextMatchCriteria { SearchText = "[TRIGGER]" },
                ExitCriteria = new LogCountCriteria { CountThreshold = 1 },
            };
            var pause = new PauseCriteria("[TRIGGER]");
            Logger.VerbosityManager.Scenarios.Add(window);
            Logger.VerbosityManager.Scenarios.Add(new LogVerbosityScenario { EnterCriteria = pause });

            var threadA = new LoggingThread("[TRIGGER] the line the window was opened for", eLogVerbosity.Verbose);
            Assert.IsTrue(pause.WaitUntilParked(), "Thread A never reached the pause point.");

            var threadB = new LoggingThread("[OTHER] a line from some other thread", eLogVerbosity.Verbose);
            Assert.IsTrue(LoggerTestHelpers.WaitFor(() => !window.IsCurrentlyActive), "Thread B never closed the window.");

            pause.Release();
            Assert.IsTrue(threadA.Join() && threadB.Join(), "The logging threads did not finish.");
            Assert.IsNull(threadA.Failure, $"Thread A threw: {threadA.Failure}");
            Assert.IsNull(threadB.Failure, $"Thread B threw: {threadB.Failure}");

            Assert.IsTrue(this.Captured("[TRIGGER]"), "The line that opened the window was dropped.");
        }

        [TestMethod]
        public void Scenarios_AddedWhileAnotherThreadIsMidPass_DoesNotThrowIntoThatThread ()
        {
            var pause = new PauseCriteria("[PARKED]");
            Logger.VerbosityManager.Scenarios.Add(new LogVerbosityScenario { EnterCriteria = pause });

            var parked = new LoggingThread("[PARKED] partway through the scenario pass", eLogVerbosity.Normal);
            Assert.IsTrue(pause.WaitUntilParked(), "The logging thread never reached the pause point.");

            Logger.VerbosityManager.Scenarios.Add(
                new LogVerbosityScenario { EnterCriteria = new LogTextMatchCriteria { SearchText = "[ADDED]" } }
            );
            pause.Release();

            Assert.IsTrue(parked.Join(), "The logging thread did not finish.");
            Assert.IsNull(parked.Failure, $"Adding a scenario threw into a thread that was logging: {parked.Failure}");
            Assert.IsTrue(this.Captured("[PARKED]"), "The parked line never made it out.");
        }

        private void CaptureOutput (string output)
        {
            lock (m_loggedOutput)
            {
                m_loggedOutput.Add(output);
            }
        }

        private bool Captured (string fragment)
        {
            lock (m_loggedOutput)
            {
                return m_loggedOutput.Exists(s => s.Contains(fragment));
            }
        }

        /// <summary>
        /// Criteria that never passes, but parks whichever thread evaluates a line containing the given text until
        /// released, so a test can hold one thread in the middle of its scenario pass.
        /// </summary>
        private sealed class PauseCriteria : LogScenarioCriteriaBase
        {
            private readonly string m_pauseOnText;
            private readonly ManualResetEventSlim m_parked = new ManualResetEventSlim(false);
            private readonly ManualResetEventSlim m_released = new ManualResetEventSlim(false);

            public PauseCriteria (string pauseOnText)
            {
                m_pauseOnText = pauseOnText;
            }

            public bool WaitUntilParked () => m_parked.Wait(LoggerTestHelpers.kWaitTimeout);

            public void Release () => m_released.Set();

            public override bool Evaluate (string message, bool isError)
            {
                if (message.Contains(m_pauseOnText))
                {
                    m_parked.Set();
                    m_released.Wait(LoggerTestHelpers.kWaitTimeout);
                }

                return false;
            }

            public override void InitiateScenario () { }

            public override void Reset () { }
        }

        /// <summary>
        /// Logs one line on its own thread, and holds on to anything the log call throws instead of letting it take
        /// down the test host.
        /// </summary>
        private sealed class LoggingThread
        {
            private readonly Thread m_thread;
            private readonly string m_message;
            private readonly eLogVerbosity m_verbosity;

            public LoggingThread (string message, eLogVerbosity verbosity)
            {
                m_message = message;
                m_verbosity = verbosity;
                m_thread = new Thread(this.Run) { IsBackground = true };
                m_thread.Start();
            }

            public Exception Failure { get; private set; }

            public bool Join () => m_thread.Join(LoggerTestHelpers.kWaitTimeout);

            private void Run ()
            {
                try
                {
                    Logger.LogInfo(m_message, m_verbosity);
                }
                catch (Exception exc)
                {
                    this.Failure = exc;
                }
            }
        }
    }


    // =====================================================================================
    // LoggerFileBehaviorTests
    // What happens to the log file, and to the logger's settings, across retargets and
    // reads. Uses temp dirs, and reads each file back after the logger has let go of it.
    // =====================================================================================

    [TestClass]
    public class LoggerFileBehaviorTests
    {
        private const string kSortableTimestampFormat = "HH:mm:ss.fffffff";
        private const string kSlowWriteReportTag = "[Logger] Slow log write:";
        private const int kClockAdvanceMs = 50;
        private const int kLockHoldMs = 100;
        private const int kTimerSlopMs = 5;

        private string m_tempDir;
        private bool m_defaultLogToConsole;
        private bool m_defaultLogToTrace;

        [TestInitialize]
        public void TestSetup ()
        {
            Logger.ResetToDefaults();
            m_defaultLogToConsole = Logger.ShouldLogToConsole;
            m_defaultLogToTrace = Logger.ShouldLogToTrace;

            Logger.ShouldLogToConsole = false;
            Logger.ShouldLogToTrace = false;
            m_tempDir = Path.Combine(Path.GetTempPath(), $"AJut_LoggerFileTests_{Guid.NewGuid():N}");
        }

        [TestCleanup]
        public void TestCleanup ()
        {
            Logger.ResetToDefaults();
            try
            {
                if (Directory.Exists(m_tempDir))
                {
                    Directory.Delete(m_tempDir, true);
                }
            }
            catch { }
        }

        // ---- Retargeting keeps settings ----

        [TestMethod]
        public void Retarget_KeepsDateTimeFormat ()
        {
            Logger.DateTimeFormat = "[dd/MMM/yyyy HH:mm:ss.ffff]";
            Logger.CreateAndStartWritingToLogFileIn(m_tempDir);
            Assert.AreEqual("[dd/MMM/yyyy HH:mm:ss.ffff]", Logger.DateTimeFormat, "Starting a log file threw away the timestamp format.");
        }

        [TestMethod]
        public void Retarget_KeepsVerbosityManagerAndScenarios ()
        {
            LogVerbosityManager manager = Logger.VerbosityManager;
            var scenario = new LogVerbosityScenario { EnterCriteria = new LogTextMatchCriteria { SearchText = "[ANY]" } };
            manager.BaseVerbosity = eLogVerbositySetting.Verbose;
            manager.Scenarios.Add(scenario);

            Logger.CreateAndStartWritingToLogFileIn(m_tempDir);

            Assert.AreSame(manager, Logger.VerbosityManager, "Starting a log file swapped out the verbosity manager.");
            Assert.AreEqual(eLogVerbositySetting.Verbose, Logger.VerbosityManager.BaseVerbosity);
            CollectionAssert.Contains(Logger.VerbosityManager.Scenarios, scenario);
        }

        [TestMethod]
        public void Retarget_KeepsOutputSwitches ()
        {
            // Flip each switch away from its default, so a reset to defaults can't pass by luck
            bool logToConsole = !m_defaultLogToConsole;
            bool logToTrace = !m_defaultLogToTrace;
            Logger.ShouldLogToConsole = logToConsole;
            Logger.ShouldLogToTrace = logToTrace;
            Logger.FlushToFileAfterEach = false;

            Logger.CreateAndStartWritingToLogFileIn(m_tempDir);

            Assert.AreEqual(logToConsole, Logger.ShouldLogToConsole, "Starting a log file reset ShouldLogToConsole.");
            Assert.AreEqual(logToTrace, Logger.ShouldLogToTrace, "Starting a log file reset ShouldLogToTrace.");
            Assert.IsFalse(Logger.FlushToFileAfterEach, "Starting a log file reset FlushToFileAfterEach.");
        }

        // ---- Reading the live log ----

        [TestMethod]
        public void ReadCurrentLogFromDisk_LaterLinesAppendRatherThanOverwrite ()
        {
            Logger.CreateAndStartWritingToLogFileIn(m_tempDir);
            Logger.LogInfo("FIRST-LINE");
            Logger.LogInfo("SECOND-LINE");

            StringAssert.Contains(Logger.ReadCurrentLogFromDisk(), "FIRST-LINE");

            // Longer than the first two lines together, so writing from the start of the file would wipe both out
            Logger.LogInfo("THIRD-LINE, logged after the read, and long enough to cover everything written before it");
            string logPath = Logger.LogFilePath;
            Logger.CreateAndStartWritingToLogFileIn(null);

            string logText = File.ReadAllText(logPath);
            StringAssert.Contains(logText, "FIRST-LINE", "A line logged after reading the log overwrote the start of the file.");
            StringAssert.Contains(logText, "SECOND-LINE");
            StringAssert.Contains(logText, "THIRD-LINE");
        }

        // ---- Line order ----

        [TestMethod]
        public void LineOrder_TimestampsNeverRunBackward_WhenALineWaitedOnTheWriteLock ()
        {
            // Format goes on after the retarget, so this test only ever trips on line order
            Logger.CreateAndStartWritingToLogFileIn(m_tempDir);
            Logger.DateTimeFormat = kSortableTimestampFormat;

            // Gets the first-call costs out of the way, so the waiting thread goes straight to the lock
            Logger.LogInfo("WARM-UP-LINE");

            object writeLock = GetWriteLock();
            var waiter = new Thread(LogTheWaitingLine) { IsBackground = true };
            lock (writeLock)
            {
                waiter.Start();
                Assert.IsTrue(
                    LoggerTestHelpers.WaitFor(() => (waiter.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0),
                    "The waiting thread never blocked on the write lock."
                );
                Thread.Sleep(kClockAdvanceMs);

                // The lock is re-entrant, so this line goes straight into the file ahead of the one that is waiting
                Logger.LogInfo("HOLDER-LINE");
            }

            Assert.IsTrue(waiter.Join(LoggerTestHelpers.kWaitTimeout), "The waiting thread did not finish.");
            string logPath = Logger.LogFilePath;
            Logger.CreateAndStartWritingToLogFileIn(null);

            List<DateTime> stamps = ReadTimestamps(File.ReadAllText(logPath));
            Assert.AreEqual(3, stamps.Count, "Expected the warm-up, holder, and waiting lines.");
            for (int index = 1; index < stamps.Count; ++index)
            {
                Assert.IsTrue(stamps[index] >= stamps[index - 1], $"Line {index} is stamped earlier than the line written before it.");
            }
        }

        // ---- Flush modes ----

        [TestMethod]
        public void FlushMode_DefaultsToFlushToDisk ()
        {
            Assert.AreEqual(eLogFlushMode.FlushToDisk, Logger.FlushMode);
        }

        [TestMethod]
        public void FlushToFileAfterEach_MapsOntoFlushMode ()
        {
            Logger.FlushToFileAfterEach = false;
            Assert.AreEqual(eLogFlushMode.Buffered, Logger.FlushMode);

            Logger.FlushToFileAfterEach = true;
            Assert.AreEqual(eLogFlushMode.FlushToDisk, Logger.FlushMode);

            Logger.FlushMode = eLogFlushMode.FlushToOS;
            Assert.IsTrue(Logger.FlushToFileAfterEach, "FlushToOS still flushes after each line.");
        }

        [TestMethod]
        public void FlushToOS_LineIsInTheFileTheMomentTheCallReturns ()
        {
            Logger.FlushMode = eLogFlushMode.FlushToOS;
            Logger.CreateAndStartWritingToLogFileIn(m_tempDir);

            Logger.LogInfo("ALREADY-OUT");
            StringAssert.Contains(ReadLiveLog(), "ALREADY-OUT");
        }

        [TestMethod]
        public void Buffered_LineWaitsInTheProcessUntilFlushed ()
        {
            Logger.FlushMode = eLogFlushMode.Buffered;
            Logger.CreateAndStartWritingToLogFileIn(m_tempDir);

            Logger.LogInfo("STILL-BUFFERED");
            Assert.IsFalse(ReadLiveLog().Contains("STILL-BUFFERED"), "A buffered line should not be in the file yet.");

            Logger.ForceFlushToFile();
            StringAssert.Contains(ReadLiveLog(), "STILL-BUFFERED");
        }

        // ---- Slow write reporting ----

        [TestMethod]
        public void SlowWriteThreshold_OffByDefault ()
        {
            Assert.AreEqual(TimeSpan.Zero, Logger.SlowWriteThreshold);
            Logger.CreateAndStartWritingToLogFileIn(m_tempDir);
            Logger.LogInfo("LINE-ONE");
            Logger.LogInfo("LINE-TWO");

            Assert.AreEqual(0, CountOccurrences(ReadLiveLog(), kSlowWriteReportTag));
        }

        [TestMethod]
        public void SlowWriteThreshold_EachSlowLineGetsExactlyOneReport ()
        {
            // Every write takes longer than a single tick
            Logger.SlowWriteThreshold = TimeSpan.FromTicks(1);
            Logger.CreateAndStartWritingToLogFileIn(m_tempDir);
            Logger.LogInfo("LINE-ONE");
            Logger.LogInfo("LINE-TWO");

            Assert.AreEqual(2, CountOccurrences(ReadLiveLog(), kSlowWriteReportTag), "One report per slow line, and a report is never itself reported on.");
        }

        [TestMethod]
        public void SlowWriteThreshold_CountsTimeSpentWaitingOnTheWriteLock ()
        {
            Logger.SlowWriteThreshold = TimeSpan.FromMilliseconds(kLockHoldMs / 2);
            Logger.CreateAndStartWritingToLogFileIn(m_tempDir);

            object writeLock = GetWriteLock();
            var waiter = new Thread(LogTheWaitingLine) { IsBackground = true, Name = "Waiting logger" };
            lock (writeLock)
            {
                waiter.Start();
                Assert.IsTrue(
                    LoggerTestHelpers.WaitFor(() => (waiter.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0),
                    "The waiting thread never blocked on the write lock."
                );
                Thread.Sleep(kLockHoldMs);
            }

            Assert.IsTrue(waiter.Join(LoggerTestHelpers.kWaitTimeout), "The waiting thread did not finish.");
            string logText = ReadLiveLog();
            StringAssert.Contains(logText, "on thread", "No slow write report was logged.");
            StringAssert.Contains(logText, "'Waiting logger'", "The report should name the thread that waited.");

            Match waited = Regex.Match(logText, @"\(([\d.]+)ms waiting for the write lock");
            Assert.IsTrue(waited.Success, "The report should break out the time spent waiting on the lock.");
            double waitedMs = Double.Parse(waited.Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.IsTrue(waitedMs >= kLockHoldMs - kTimerSlopMs, $"Reported {waitedMs}ms waiting, but the lock was held for {kLockHoldMs}ms.");
        }

        // ---- Default formats ----

        [TestMethod]
        public void DefaultDateTimeFormat_TellsMorningFromAfternoon ()
        {
            var morning = new DateTime(2026, 9, 28, 2, 15, 30);
            Assert.AreNotEqual(
                morning.ToString(Logger.DateTimeFormat),
                morning.AddHours(12).ToString(Logger.DateTimeFormat),
                "2am and 2pm come out the same with the default timestamp format."
            );
        }

        [TestMethod]
        public void DefaultLogFilenameFormat_TellsMorningFromAfternoon ()
        {
            var morning = new DateTime(2026, 9, 28, 2, 15, 30);
            Assert.AreNotEqual(
                String.Format(Logger.LogFilenameFormat, morning),
                String.Format(Logger.LogFilenameFormat, morning.AddHours(12)),
                "Logs started at 2am and 2pm get the same file name with the default format."
            );
        }

        private static void LogTheWaitingLine () => Logger.LogInfo("WAITING-LINE");

        private static string ReadLiveLog () => LoggerTestHelpers.ReadFileShared(Logger.LogFilePath);

        private static int CountOccurrences (string text, string fragment) => LoggerTestHelpers.CountOccurrences(text, fragment);

        private static object GetWriteLock ()
        {
            object loggerInstance = typeof(Logger)
                .GetField("g_LoggerInstance", BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(null);

            return typeof(Logger)
                .GetField("m_logWritingLock", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(loggerInstance);
        }

        private static List<DateTime> ReadTimestamps (string logText)
        {
            var stamps = new List<DateTime>();
            foreach (string line in logText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
            {
                // Lines look like: [Info] <timestamp> |   <message>
                int start = line.IndexOf("] ") + 2;
                int end = line.IndexOf(" |");
                stamps.Add(
                    DateTime.ParseExact(line.Substring(start, end - start), kSortableTimestampFormat, CultureInfo.InvariantCulture)
                );
            }

            return stamps;
        }
    }


    // =====================================================================================
    // LoggerCrashSafetyTests
    // Runs the crash host, which logs a known run of lines and then dies with none of the
    // normal shutdown, and counts what made it into the file. A line the OS has been handed
    // survives the process dying by any means, so with either flush mode every line has to
    // be there - right up to the last one before the process went down.
    // =====================================================================================

    [TestClass]
    public class LoggerCrashSafetyTests
    {
        private const string kCrashHostFileName = "AJut.Core.CrashHost.exe";
        private static readonly TimeSpan kCrashHostTimeout = TimeSpan.FromSeconds(60);

        // What each death leaves as the exit code, so a death that quietly turned into another kind fails the test
        private const uint kFailFastExitCode = 0x80131623;           // COR_E_FAILFAST
        private const uint kStackOverflowExitCode = 0xC00000FD;      // STATUS_STACK_OVERFLOW
        private const uint kUnhandledExceptionExitCode = 0xE0434352; // The runtime's own exception code
        private const uint kAccessViolationExitCode = 0xC0000005;    // STATUS_ACCESS_VIOLATION

        private string m_tempDir;

        [TestInitialize]
        public void TestSetup ()
        {
            m_tempDir = Path.Combine(Path.GetTempPath(), $"AJut_LoggerCrashTests_{Guid.NewGuid():N}");
        }

        [TestCleanup]
        public void TestCleanup ()
        {
            try
            {
                if (Directory.Exists(m_tempDir))
                {
                    Directory.Delete(m_tempDir, true);
                }
            }
            catch { }
        }

        [DataTestMethod]
        [DataRow(eCrashHostDeath.FailFast)]
        [DataRow(eCrashHostDeath.StackOverflow)]
        [DataRow(eCrashHostDeath.UnhandledException)]
        [DataRow(eCrashHostDeath.AccessViolation)]
        public void FlushToOS_EveryLineSurvivesTheProcessDying (eCrashHostDeath death)
        {
            this.AssertEveryLineSurvives(death, eLogFlushMode.FlushToOS);
        }

        [DataTestMethod]
        [DataRow(eCrashHostDeath.FailFast)]
        [DataRow(eCrashHostDeath.StackOverflow)]
        [DataRow(eCrashHostDeath.UnhandledException)]
        [DataRow(eCrashHostDeath.AccessViolation)]
        public void FlushToDisk_EveryLineSurvivesTheProcessDying (eCrashHostDeath death)
        {
            this.AssertEveryLineSurvives(death, eLogFlushMode.FlushToDisk);
        }

        private void AssertEveryLineSurvives (eCrashHostDeath death, eLogFlushMode flushMode)
        {
            var startInfo = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, kCrashHostFileName))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add(death.ToString());
            startInfo.ArgumentList.Add(flushMode.ToString());
            startInfo.ArgumentList.Add(m_tempDir);

            using (Process crashHost = Process.Start(startInfo))
            {
                if (!crashHost.WaitForExit((int)kCrashHostTimeout.TotalMilliseconds))
                {
                    crashHost.Kill();
                    Assert.Fail($"The crash host never died from {death}.");
                }

                uint exitCode = unchecked((uint)crashHost.ExitCode);
                Assert.AreEqual(ExpectedExitCode(death), exitCode, $"The crash host exited with 0x{exitCode:X8}, which is not what a {death} leaves.");
            }

            string[] logFiles = Directory.GetFiles(m_tempDir);
            Assert.AreEqual(1, logFiles.Length, "Expected the crash host to leave exactly one log file.");
            string logText = LoggerTestHelpers.ReadFileShared(logFiles[0]);

            Assert.AreEqual(CrashHostScript.kBurstLineCount, LoggerTestHelpers.CountOccurrences(logText, CrashHostScript.kBurstTag), "Burst lines were lost.");
            Assert.AreEqual(1, LoggerTestHelpers.CountOccurrences(logText, CrashHostScript.kErrorTag), "The error line was lost.");
            Assert.AreEqual(CrashHostScript.kTrailingLineCount, LoggerTestHelpers.CountOccurrences(logText, CrashHostScript.kTrailingTag), "Trailing lines were lost.");
            Assert.AreEqual(1, LoggerTestHelpers.CountOccurrences(logText, CrashHostScript.kLastTag), $"The last line before the {death} was lost.");
        }

        private static uint ExpectedExitCode (eCrashHostDeath death)
        {
            return death switch
            {
                eCrashHostDeath.FailFast => kFailFastExitCode,
                eCrashHostDeath.StackOverflow => kStackOverflowExitCode,
                eCrashHostDeath.UnhandledException => kUnhandledExceptionExitCode,
                eCrashHostDeath.AccessViolation => kAccessViolationExitCode,
                _ => throw new ArgumentOutOfRangeException(nameof(death)),
            };
        }
    }


    /// <summary>
    /// Shared bits for the logger tests: bounded waits (so a broken interleaving fails the test instead of hanging the run),
    /// and reading log files back.
    /// </summary>
    internal static class LoggerTestHelpers
    {
        public static readonly TimeSpan kWaitTimeout = TimeSpan.FromSeconds(5);

        public static bool WaitFor (Func<bool> condition)
        {
            var timer = Stopwatch.StartNew();
            while (!condition())
            {
                if (timer.Elapsed > kWaitTimeout)
                {
                    return false;
                }

                Thread.Sleep(1);
            }

            return true;
        }

        /// <summary>
        /// Reads a log file even while something still has it open for writing, the way a log viewer would.
        /// </summary>
        public static string ReadFileShared (string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        public static int CountOccurrences (string text, string fragment)
        {
            int count = 0;
            int index = text.IndexOf(fragment, StringComparison.Ordinal);
            while (index >= 0)
            {
                ++count;
                index = text.IndexOf(fragment, index + fragment.Length, StringComparison.Ordinal);
            }

            return count;
        }
    }
}
