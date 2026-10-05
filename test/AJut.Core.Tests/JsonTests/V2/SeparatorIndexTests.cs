namespace AJut.Core.UnitTests.AJsonV2
{
    using System;
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// SeparatorIndex is internal - these tests exercise it indirectly via the parse path,
    /// which is the only place it's allowed to be used (lifetime ownership).
    /// </summary>
    [TestClass]
    public class SeparatorIndexTests
    {
        [TestMethod]
        public void Indexer_DocumentBraces_RecognizedThroughParse ()
        {
            Json json = JsonHelper.ParseText("{ a: 1 }");
            Assert.IsFalse(json.HasErrors, String.Join(", ", json.Errors));
            Assert.IsTrue(json.Data.IsDocument);
        }

        [TestMethod]
        public void Indexer_ArrayBrackets_RecognizedThroughParse ()
        {
            Json json = JsonHelper.ParseText("[1, 2, 3]");
            Assert.IsFalse(json.HasErrors, String.Join(", ", json.Errors));
            Assert.IsTrue(json.Data.IsArray);
            Assert.AreEqual(3, ((JsonArray)json.Data).Count);
        }

        [TestMethod]
        public void Indexer_EscapedQuote_StaysInsideString ()
        {
            Json json = JsonHelper.ParseText("{ k: \"a\\\"b\" }");
            Assert.IsFalse(json.HasErrors, String.Join(", ", json.Errors));
            JsonDocument doc = (JsonDocument)json.Data;
            Assert.AreEqual("a\"b", doc.ValueFor("k").StringValue);
        }

        // The indexer drops comment regions from the separator stream, so the parser does not
        // stumble over them structurally, and records where they were, so the reader can take them
        // back out of the unquoted key and value text it slices from the original text. The tests
        // for that are in JsonReaderTests.

        [TestMethod]
        public void Indexer_LineComment_BetweenProperties_ParsesWithoutErrors ()
        {
            ParserRules rules = ParserRules.WithDefaultComments();
            Json json = JsonHelper.ParseText("{ \"a\": 1,\n // separating comment\n \"b\": 2 }", rules);
            Assert.IsFalse(json.HasErrors, String.Join(", ", json.Errors));
            Assert.IsNotNull(json.Data);
            JsonDocument doc = (JsonDocument)json.Data;
            Assert.AreEqual("1", doc.ValueFor("a").StringValue);
            Assert.AreEqual("2", doc.ValueFor("b").StringValue);
        }

        [TestMethod]
        public void Indexer_BlockComment_BetweenProperties_ParsesWithoutErrors ()
        {
            ParserRules rules = ParserRules.WithDefaultComments();
            Json json = JsonHelper.ParseText("{ \"a\": 1, /* separating */ \"b\": 2 }", rules);
            Assert.IsFalse(json.HasErrors, String.Join(", ", json.Errors));
            Assert.IsNotNull(json.Data);
            JsonDocument doc = (JsonDocument)json.Data;
            Assert.AreEqual("1", doc.ValueFor("a").StringValue);
            Assert.AreEqual("2", doc.ValueFor("b").StringValue);
        }
    }
}
