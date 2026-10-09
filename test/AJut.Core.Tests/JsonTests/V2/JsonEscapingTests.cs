namespace AJut.Core.UnitTests.AJsonV2
{
    using System;
    using System.Text;
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Strings are escaped per the JSON spec on write and unescaped on read, so any string survives
    /// a round trip, AJson output is valid JSON, and valid JSON from elsewhere reads correctly. Text
    /// written before escaping, which escaped only quotes, still reads where it can be told apart.
    /// </summary>
    [TestClass]
    public class JsonEscapingTests
    {
        // ===========================[ Test Models ]===================================
        public class TextHolder
        {
            public string Text { get; set; }
        }

        // ===========================[ Round-Trips ]===================================
        [TestMethod]
        public void Escape_StringEndingInBackslash_RoundTrips ()
        {
            AssertRoundTrips(@"C:\dir\");
        }

        [TestMethod]
        public void Escape_EverySpecialCharacter_RoundTrips ()
        {
            AssertRoundTrips(
                "quote \" backslash \\ slash / backspace \b formfeed \f newline \n return \r tab \t"
                + " control \u0001 accent \u00e9 end"
            );
        }

        [TestMethod]
        public void Escape_BackslashBeforeQuote_RoundTrips ()
        {
            AssertRoundTrips("a\\\"b");
        }

        [TestMethod]
        public void Escape_Key_RoundTrips ()
        {
            const string kKey = "we\"ird\\key";
            JsonBuilder builder = JsonHelper.MakeRootBuilder().StartDocument();
            builder.AddProperty(kKey, 1);
            string text = builder.Finalize().ToString();

            Json json = JsonHelper.ParseText(text);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport() + "\nText:\n" + text);
            Assert.AreEqual("1", ((JsonDocument)json.Data).ValueFor(kKey)?.StringValue, text);
        }

        [TestMethod]
        public void Escape_NonDefaultQuoteChar_IsEscaped ()
        {
            // A quote char other than the default gets escaped like the default one would be, and
            //  the double quote still gets its usual escape
            StringBuilder output = new StringBuilder();
            JsonStringEscaping.AppendEscaped(output, "it's \"x\"", '\'');
            Assert.AreEqual("it\\'s \\\"x\\\"", output.ToString());
        }

        [TestMethod]
        public void Escape_NothingToEscape_AppendsAsIs ()
        {
            StringBuilder output = new StringBuilder();
            JsonStringEscaping.AppendEscaped(output, "plain text, no escapes", '"');
            Assert.AreEqual("plain text, no escapes", output.ToString());
        }

        // ===========================[ Valid JSON ]===================================
        [TestMethod]
        public void Escape_OutputIsValidJson ()
        {
            const string kValue = "line one\nline two\ttabbed \"quoted\" C:\\dir\\";
            string text = JsonHelper.BuildJsonForObject(new TextHolder { Text = kValue }).ToString();

            using (System.Text.Json.JsonDocument parsed = System.Text.Json.JsonDocument.Parse(text))
            {
                Assert.AreEqual(kValue, parsed.RootElement.GetProperty(nameof(TextHolder.Text)).GetString(), text);
            }
        }

        [TestMethod]
        public void Escape_ReadsValidJsonWithEscapedBackslash ()
        {
            Json json = JsonHelper.ParseText("{\"a\":\"x\\\\\",\"b\":\"y\"}");
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            JsonDocument doc = (JsonDocument)json.Data;
            Assert.AreEqual("x\\", doc.ValueFor("a")?.StringValue);
            Assert.AreEqual("y", doc.ValueFor("b")?.StringValue);
        }

        [TestMethod]
        public void Escape_ReadsUnicodeEscape ()
        {
            Json json = JsonHelper.ParseText("{ \"a\": \"caf\\u00e9\" }");
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            Assert.AreEqual("caf\u00e9", ((JsonDocument)json.Data).ValueFor("a")?.StringValue);
        }

        [TestMethod]
        public void Escape_ParsedStringValue_IsUnescaped ()
        {
            // The tree holds the string itself, not its escaped text, whether read or built
            Json json = JsonHelper.ParseText("{ \"a\": \"say \\\"hi\\\"\" }");
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            Assert.AreEqual("say \"hi\"", ((JsonDocument)json.Data).ValueFor("a")?.StringValue);
        }

        // ===========================[ Text Written Before Escaping ]===================================
        [TestMethod]
        public void Old_UnescapedBackslashes_ReadAsWritten ()
        {
            Assert.AreEqual(@"C:\dir\file", ReadText("{ \"Text\": \"C:\\dir\\file\" }"));
        }

        [TestMethod]
        public void Old_InvalidEscapeMarksStringAsOldText ()
        {
            // \d is not a JSON escape, so this string predates escaping, and its \t is a backslash
            //  and a t, not a tab
            Assert.AreEqual(@"C:\dir\temp", ReadText("{ \"Text\": \"C:\\dir\\temp\" }"));
        }

        [TestMethod]
        public void Old_EscapedQuotes_Read ()
        {
            Assert.AreEqual("say \"hi\"", ReadText("{ \"Text\": \"say \\\"hi\\\"\" }"));
        }

        [TestMethod]
        public void Old_TrailingBackslash_Reads ()
        {
            // The old writer wrote C:\dir\ raw, so its closing quote looks escaped
            Json json = JsonHelper.ParseText("{ \"a\": \"C:\\dir\\\", \"b\": \"y\" }");
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            JsonDocument doc = (JsonDocument)json.Data;
            Assert.AreEqual(@"C:\dir\", doc.ValueFor("a")?.StringValue);
            Assert.AreEqual("y", doc.ValueFor("b")?.StringValue);
        }

        // ===========================[ Helpers ]===================================
        private static void AssertRoundTrips (string value)
        {
            string text = JsonHelper.BuildJsonForObject(new TextHolder { Text = value }).ToString();
            Assert.AreEqual(value, ReadText(text), "Text:\n" + text);
        }

        private static string ReadText (string text)
        {
            Json json = JsonHelper.ParseText(text);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport() + "\nText:\n" + text);
            return JsonHelper.BuildObjectForJson<TextHolder>(json).Text;
        }
    }
}
