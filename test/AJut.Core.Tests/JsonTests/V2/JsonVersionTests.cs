namespace AJut.Core.UnitTests.AJsonV2
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The AJson version marker: written as the root document's first key, read back out into
    /// <see cref="Json.AJsonVersion"/>, and switchable per write and per read.
    /// </summary>
    [TestClass]
    public class JsonVersionTests
    {
        private const string kMarkerKey = "\"" + JsonDocument.kAJsonVersionIndicator + "\"";

        private List<string> m_loggedOutput;

        // ===========================[ Setup/Construction/Teardown ]===================================
        [TestInitialize]
        public void Setup ()
        {
            JsonHelper.WriteAJsonVersion = true;
            JsonHelper.WarnIfAJsonVersionBelow = 0;

            Logger.ResetToDefaults();
            Logger.ShouldLogToConsole = false;
            Logger.ShouldLogToTrace = false;
            m_loggedOutput = new List<string>();
            Logger.SetSingleOverrideLogTarget(this.CaptureLog);
        }

        [TestCleanup]
        public void Cleanup ()
        {
            JsonHelper.WriteAJsonVersion = true;
            JsonHelper.WarnIfAJsonVersionBelow = 0;
            Logger.ResetToDefaults();
        }

        // ===========================[ Test Models ]===================================
        public class TextHolder
        {
            public string Text { get; set; }
        }

        // ===========================[ Writing ]===================================
        [TestMethod]
        public void Write_RootDocument_StartsWithTheMarker ()
        {
            string text = JsonHelper.BuildJsonForObject(new TextHolder { Text = "x" }).ToString();

            int markerAt = text.IndexOf(kMarkerKey);
            Assert.IsTrue(markerAt != -1, text);
            Assert.IsTrue(markerAt < text.IndexOf("\"Text\""), "The marker comes first: " + text);
            StringAssert.Contains(text, kMarkerKey + " : " + JsonHelper.kCurrentAJsonVersion, text);
        }

        [TestMethod]
        public void Write_EmptyRootDocument_StillCarriesTheMarker ()
        {
            string text = JsonHelper.MakeRootBuilder().StartDocument().Finalize().ToString();
            Json json = JsonHelper.ParseText(text);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport() + "\nText:\n" + text);
            Assert.AreEqual(JsonHelper.kCurrentAJsonVersion, json.AJsonVersion, text);
            Assert.AreEqual(0, ((JsonDocument)json.Data).Count, text);
        }

        [TestMethod]
        public void Write_RootArray_HasNoMarker ()
        {
            string text = JsonHelper.BuildJsonForObject(new[] { "a", "b" }).ToString();
            Assert.IsFalse(text.Contains(kMarkerKey), text);
        }

        [TestMethod]
        public void Write_GlobalSwitchOff_NoMarker ()
        {
            JsonHelper.WriteAJsonVersion = false;
            string text = JsonHelper.BuildJsonForObject(new TextHolder { Text = "x" }).ToString();
            Assert.IsFalse(text.Contains(kMarkerKey), text);
        }

        [TestMethod]
        public void Write_BuildSettings_OverrideTheGlobalSwitch ()
        {
            JsonBuilderSettings markerOff = new JsonBuilderSettings { WriteAJsonVersion = false };
            string withoutMarker = JsonHelper.BuildJsonForObject(new TextHolder { Text = "x" }, markerOff).ToString();
            Assert.IsFalse(withoutMarker.Contains(kMarkerKey), withoutMarker);

            JsonHelper.WriteAJsonVersion = false;
            JsonBuilderSettings markerOn = new JsonBuilderSettings { WriteAJsonVersion = true };
            string withMarker = JsonHelper.BuildJsonForObject(new TextHolder { Text = "x" }, markerOn).ToString();
            StringAssert.Contains(withMarker, kMarkerKey, withMarker);
        }

        [TestMethod]
        public void Write_JsonSwitch_OverridesTheBuildSettings ()
        {
            Json json = JsonHelper.BuildJsonForObject(new TextHolder { Text = "x" });
            json.WriteAJsonVersion = false;
            Assert.IsFalse(json.ToString().Contains(kMarkerKey), json.ToString());
        }

        // ===========================[ Reading ]===================================
        [TestMethod]
        public void Read_MarkedText_ReportsTheVersion_AndTakesTheMarkerOut ()
        {
            Json json = JsonHelper.ParseText("{ \"__ajson\": 2, \"a\": 1 }");
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            Assert.AreEqual(2, json.AJsonVersion);

            JsonDocument doc = (JsonDocument)json.Data;
            Assert.IsFalse(doc.ContainsKey(JsonDocument.kAJsonVersionIndicator));
            CollectionAssert.AreEqual(new[] { "a" }, doc.Select(kvp => kvp.Key).ToArray());
        }

        [TestMethod]
        public void Read_UnmarkedText_ReportsVersionZero ()
        {
            Json json = JsonHelper.ParseText("{ \"a\": 1 }");
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            Assert.AreEqual(0, json.AJsonVersion);
        }

        [TestMethod]
        public void Read_MarkerKeyNotFirst_IsOrdinaryData ()
        {
            Json json = JsonHelper.ParseText("{ \"a\": 1, \"__ajson\": 2 }");
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            Assert.AreEqual(0, json.AJsonVersion);
            Assert.IsTrue(((JsonDocument)json.Data).ContainsKey(JsonDocument.kAJsonVersionIndicator));
        }

        [TestMethod]
        public void Read_WrittenText_RoundTripsTheVersion ()
        {
            string text = JsonHelper.BuildJsonForObject(new TextHolder { Text = "x" }).ToString();
            Json json = JsonHelper.ParseText(text);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            Assert.AreEqual(JsonHelper.kCurrentAJsonVersion, json.AJsonVersion, text);
            Assert.AreEqual("x", JsonHelper.BuildObjectForJson<TextHolder>(json).Text);
        }

        [TestMethod]
        public void Read_MarkedText_DecodesStrings_EvenBesideAnEscapeJsonLacks ()
        {
            // A marked document is escaped text, so it is not judged as old text as a whole. A
            //  string in it holding an escape JSON lacks (only a hand edit could put one there)
            //  still reads as written on its own.
            Json json = JsonHelper.ParseText("{ \"__ajson\": 2, \"a\": \"C:\\Users\\data\", \"b\": \"x\\ty\" }");
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            JsonDocument doc = (JsonDocument)json.Data;
            Assert.AreEqual(@"C:\Users\data", doc.ValueFor("a")?.StringValue);
            Assert.AreEqual("x\ty", doc.ValueFor("b")?.StringValue);
        }

        [TestMethod]
        public void Read_AssumeVersionBelowTwo_ReadsUnmarkedStringsAsWritten ()
        {
            ParserRules rules = new ParserRules { AssumeAJsonVersion = 0 };
            Json json = JsonHelper.ParseText("{ \"p\": \"C:\\temp\\new\" }", rules);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            Assert.AreEqual(@"C:\temp\new", ((JsonDocument)json.Data).ValueFor("p")?.StringValue);
        }

        [TestMethod]
        public void Read_AssumeVersionTwo_DecodesUnmarkedStrings ()
        {
            // Without the assumption, the \U here would mark the whole text as old
            ParserRules rules = new ParserRules { AssumeAJsonVersion = 2 };
            Json json = JsonHelper.ParseText("{ \"a\": \"C:\\Users\\data\", \"b\": \"x\\ty\" }", rules);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            Assert.AreEqual("x\ty", ((JsonDocument)json.Data).ValueFor("b")?.StringValue);
        }

        [TestMethod]
        public void Read_AssumeVersion_DoesNotOverrideAMarker ()
        {
            ParserRules rules = new ParserRules { AssumeAJsonVersion = 0 };
            Json json = JsonHelper.ParseText("{ \"__ajson\": 2, \"b\": \"x\\ty\" }", rules);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            Assert.AreEqual("x\ty", ((JsonDocument)json.Data).ValueFor("b")?.StringValue);
        }

        // ===========================[ Warning ]===================================
        [TestMethod]
        public void Warn_OffByDefault ()
        {
            JsonHelper.ParseText("{ \"a\": 1 }");
            Assert.AreEqual(0, this.WarningCount());
        }

        [TestMethod]
        public void Warn_PerRead_UnmarkedText_Logs ()
        {
            JsonHelper.ParseText("{ \"a\": 1 }", new ParserRules { WarnIfAJsonVersionBelow = 2 });
            Assert.AreEqual(1, this.WarningCount(), String.Join("\n", m_loggedOutput));
        }

        [TestMethod]
        public void Warn_PerRead_MarkedTextAtTheVersion_DoesNotLog ()
        {
            JsonHelper.ParseText("{ \"__ajson\": 2, \"a\": 1 }", new ParserRules { WarnIfAJsonVersionBelow = 2 });
            Assert.AreEqual(0, this.WarningCount(), String.Join("\n", m_loggedOutput));
        }

        [TestMethod]
        public void Warn_Global_Logs_AndAReadCanTurnItOff ()
        {
            JsonHelper.WarnIfAJsonVersionBelow = 2;
            JsonHelper.ParseText("{ \"a\": 1 }");
            Assert.AreEqual(1, this.WarningCount(), String.Join("\n", m_loggedOutput));

            JsonHelper.ParseText("{ \"a\": 1 }", new ParserRules { WarnIfAJsonVersionBelow = 0 });
            Assert.AreEqual(1, this.WarningCount(), "A read that sets 0 does not warn");
        }

        [TestMethod]
        public void Warn_RootArray_DoesNotLog ()
        {
            // Only a root document can carry the marker
            JsonHelper.ParseText("[ 1, 2 ]", new ParserRules { WarnIfAJsonVersionBelow = 2 });
            Assert.AreEqual(0, this.WarningCount(), String.Join("\n", m_loggedOutput));
        }

        // ===========================[ Helpers ]===================================
        private void CaptureLog (string message) => m_loggedOutput.Add(message);

        private int WarningCount () => m_loggedOutput.Count(line => line.Contains("[WARNING] AJson"));
    }
}
