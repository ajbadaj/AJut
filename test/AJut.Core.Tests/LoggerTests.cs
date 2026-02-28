namespace AJut.Core.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using AJut;
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
            // Fresh Logger instance, no file
            Logger.CreateAndStartWritingToLogFileIn(null);
            Logger.ShouldLogToConsole = false;
            Logger.ShouldLogToTrace = false;

            m_loggedOutput = new List<string>();
            Logger.SetSingleOverrideLogTarget(msg => m_loggedOutput.Add(msg));
        }

        [TestCleanup]
        public void TestCleanup ()
        {
            Logger.SetSingleOverrideLogTarget(null);
            Logger.CreateAndStartWritingToLogFileIn(null);
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
                Logger.CreateAndStartWritingToLogFileIn(null);
                Logger.SetSingleOverrideLogTarget(msg => m_loggedOutput.Add(msg));

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
            Logger.LogFileSplitSizeBytes = kDefaultSplitSize;
            m_tempDir = Path.Combine(Path.GetTempPath(), $"AJut_SplitTests_{Guid.NewGuid():N}");
            Logger.SetSingleOverrideLogTarget(null);
        }

        [TestCleanup]
        public void TestCleanup ()
        {
            Logger.CreateAndStartWritingToLogFileIn(null);
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
}
