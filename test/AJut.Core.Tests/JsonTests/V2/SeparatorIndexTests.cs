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
            Assert.AreEqual("a\\\"b", doc.ValueFor("k").StringValue);
        }

        // Note on comment parity with V1: the indexer correctly drops comment regions from the
        // separator stream, so the parser does not stumble over them structurally - that is the
        // contract V1 carried and these tests verify (the no-errors / data-non-null shape used
        // by V1's ParserRulesTests). Comments embedded *inside* an unquoted key or value chunk
        // still leak into the resulting StringValue text the same way they did in V1, because the
        // reader extracts text slices from the original char span. Cleaning that up is a separate
        // concern not in any Phase B / Phase C fold-in scope.

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
