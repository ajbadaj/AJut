namespace AJut.Core.UnitTests.AJsonV2
{
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Text written from built json follows the formatting in the JsonBuilderSettings it was built with
    /// </summary>
    [TestClass]
    public class JsonWriterSettingsTests
    {
        // ===========================[ Test Models ]===========================
        public class Sample
        {
            public string Name { get; set; }
            public int Count { get; set; }
            public int[] Values { get; set; }
        }

        // ===========================[ Built Json ]===========================
        [TestMethod]
        public void BuiltJson_WithMinifiedSettings_IsWrittenMinified ()
        {
            JsonBuilderSettings minified = JsonBuilderSettings.BuildMinifiedSettings();
            string text = JsonHelper.BuildJsonForObject(MakeSample(), minified).ToString();

            Assert.IsFalse(text.Contains('\n'), "Minified text has no newlines:\n" + text);
            Assert.IsFalse(text.Contains('\t'), "Minified text has no tabbing:\n" + text);
            Assert.IsFalse(text.Contains(" : "), "Minified text has no spacing around the colon:\n" + text);
        }

        [TestMethod]
        public void BuiltJson_UsesItsTabbingAndNewline ()
        {
            JsonBuilderSettings settings = new JsonBuilderSettings
            {
                Tabbing = "  ",
                Newline = "\r\n",
                WriteAJsonVersion = false,
            };

            string text = JsonHelper.BuildJsonForObject(MakeSample(), settings).ToString();

            StringAssert.StartsWith(text, "{\r\n  \"Name\"", text);
            Assert.IsFalse(text.Contains('\t'), "Tabbing is two spaces, not a tab:\n" + text);
        }

        [TestMethod]
        public void BuiltJson_UsesItsQuoteCharacters ()
        {
            JsonBuilderSettings settings = new JsonBuilderSettings
            {
                PropertyValueQuoteChars = '\'',
                PropertyNameQuoteChars = '\'',
            };

            string text = JsonHelper.BuildJsonForObject(MakeSample(), settings).ToString();

            StringAssert.Contains(text, "'Name'", text);
            StringAssert.Contains(text, "'sample'", text);
            Assert.IsFalse(text.Contains('"'), "No double quotes should be written:\n" + text);
        }

        [TestMethod]
        public void BuiltJson_WithUnquotedPropertyNames_WritesThemUnquoted ()
        {
            JsonBuilderSettings settings = new JsonBuilderSettings { QuotePropertyNames = false };
            string text = JsonHelper.BuildJsonForObject(MakeSample(), settings).ToString();

            StringAssert.Contains(text, "Name : \"sample\"", text);
        }

        [TestMethod]
        public void BuiltJson_ThatQuotesEveryValue_QuotesNumbersToo ()
        {
            JsonBuilderSettings settings = new JsonBuilderSettings { PropertyValueQuoting = ePropertyValueQuoting.QuoteAll };
            string text = JsonHelper.BuildJsonForObject(MakeSample(), settings).ToString();

            StringAssert.Contains(text, "\"Count\" : \"2\"", text);
        }

        // ===========================[ Writing With Given Settings ]===========================
        [TestMethod]
        public void ReadJson_IsWrittenWithTheSettingsGiven ()
        {
            Json json = JsonHelper.ParseText("{ \"Name\": \"sample\", \"Values\": [ 1, 2 ] }");
            JsonBuilderSettings minified = JsonBuilderSettings.BuildMinifiedSettings();
            minified.WriteAJsonVersion = false;

            Assert.AreEqual("{\"Name\":\"sample\",\"Values\":[1,2]}", json.ToString(minified));
        }

        [TestMethod]
        public void BuiltJson_IsWrittenWithOtherSettings_WhenGiven ()
        {
            Json json = JsonHelper.BuildJsonForObject(MakeSample(), JsonBuilderSettings.BuildMinifiedSettings());
            JsonBuilderSettings pretty = new JsonBuilderSettings { WriteAJsonVersion = false };

            StringAssert.StartsWith(json.ToString(pretty), "{\n\t\"Name\" : \"sample\"");
        }

        [TestMethod]
        public void JsonValue_IsWrittenWithTheSettingsGiven ()
        {
            Json json = JsonHelper.ParseText("{ \"Inner\": { \"Name\": \"sample\" } }");
            JsonValue inner = ((JsonDocument)json.Data).ValueFor("Inner");
            JsonValue name = ((JsonDocument)inner).ValueFor("Name");
            JsonBuilderSettings minified = JsonBuilderSettings.BuildMinifiedSettings();

            Assert.AreEqual("{\"Name\":\"sample\"}", inner.ToString(minified));
            Assert.AreEqual("\"sample\"", name.ToString(minified), "A single value is written as it appears in json");
            Assert.AreEqual("sample", name.ToString(), "ToString() still gives a single value's raw text");
        }

        [TestMethod]
        public void VersionMarker_FollowsTheJson_ThenTheSettingsItIsWrittenWith ()
        {
            Json json = JsonHelper.BuildJsonForObject(MakeSample(), new JsonBuilderSettings { WriteAJsonVersion = false });
            Assert.IsFalse(json.ToString().Contains(JsonDocument.kAJsonVersionIndicator), "The build's settings turned it off");

            JsonBuilderSettings markerOn = new JsonBuilderSettings { WriteAJsonVersion = true };
            StringAssert.Contains(json.ToString(markerOn), JsonDocument.kAJsonVersionIndicator, "The settings given turn it on");

            json.WriteAJsonVersion = false;
            Assert.IsFalse(json.ToString(markerOn).Contains(JsonDocument.kAJsonVersionIndicator), "The json's own switch wins");
        }

        // ===========================[ Helpers ]===========================
        private static Sample MakeSample () => new Sample { Name = "sample", Count = 2, Values = new[] { 1, 2 } };
    }
}
