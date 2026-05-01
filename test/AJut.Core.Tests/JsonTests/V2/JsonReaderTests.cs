namespace AJut.Core.UnitTests.AJsonV2
{
    using System;
    using System.Linq;
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class JsonReaderTests
    {
        // ===============================[ Basic Shape Detection ]===========================
        [TestMethod]
        public void Read_BareValue_ProducesJsonValue ()
        {
            Json json = JsonHelper.ParseText("dude");
            AssertNoErrors(json);
            Assert.IsTrue(json.Data.IsValue);
            Assert.AreEqual("dude", json.Data.StringValue);
            Assert.IsFalse(json.Data.IsQuoted);
        }

        [TestMethod]
        public void Read_SimpleDocument_ProducesJsonDocument ()
        {
            Json json = JsonHelper.ParseText("{ dude: ' sweet' }");
            AssertNoErrors(json);
            Assert.IsTrue(json.Data.IsDocument);
        }

        [TestMethod]
        public void Read_SimpleArray_ProducesJsonArray ()
        {
            Json json = JsonHelper.ParseText("[ {dude: sweet}, item ]");
            AssertNoErrors(json);
            Assert.IsTrue(json.Data.IsArray);
            Assert.AreEqual(2, ((JsonArray)json.Data).Count);
        }

        [TestMethod]
        public void Read_QuotedKeyAndValue_PreservesContents ()
        {
            Json json = JsonHelper.ParseText("{ \"key\" : \"value\" }");
            AssertNoErrors(json);
            JsonDocument doc = (JsonDocument)json.Data;
            JsonValue v = doc.ValueFor("key");
            Assert.AreEqual("value", v.StringValue);
            Assert.IsTrue(v.IsQuoted);
        }

        [TestMethod]
        public void Read_DuplicateKeys_AreAllPreserved ()
        {
            Json json = JsonHelper.ParseText("{ \"Test\" : 0, \"Test\" : 1 }");
            AssertNoErrors(json);
            JsonDocument doc = (JsonDocument)json.Data;
            Assert.AreEqual(2, doc.Count);
            Assert.AreEqual(2, doc.AllValuesForKey("Test").Length);
        }

        [TestMethod]
        public void Read_MixedTypes_ParseAsExpected ()
        {
            Json json = JsonHelper.ParseText("{ name: \"AJ\", count: 42, active: true }");
            AssertNoErrors(json);
            JsonDocument doc = (JsonDocument)json.Data;
            Assert.AreEqual("AJ", doc.ValueFor("name").StringValue);
            Assert.AreEqual("42", doc.ValueFor("count").StringValue);
            Assert.AreEqual("true", doc.ValueFor("active").StringValue);
        }

        // ===============================[ Lenient vs Strict ]===========================
        [TestMethod]
        public void Read_UnquotedKey_OkInLenient ()
        {
            Json json = JsonHelper.ParseText("{ thing: \"a\" }");
            AssertNoErrors(json);
        }

        [TestMethod]
        public void Read_UnquotedKey_ErroredInStrict ()
        {
            ParserRules rules = new ParserRules { StrictMode = true };
            Json json = JsonHelper.ParseText("{ thing: \"a\" }", rules);
            Assert.IsTrue(json.HasErrors, "Strict mode should reject unquoted keys");
        }

        [TestMethod]
        public void Read_UnquotedStringValue_ErroredInStrict ()
        {
            ParserRules rules = new ParserRules { StrictMode = true };
            Json json = JsonHelper.ParseText("{ \"thing\": some-value }", rules);
            Assert.IsTrue(json.HasErrors, "Strict mode should reject unquoted string values");
        }

        // ===============================[ Errors-or-Value Contract ]===========================
        [TestMethod]
        public void Read_NullText_ProducesError_NoThrow ()
        {
            Json json = JsonHelper.ParseText((string)null);
            Assert.IsTrue(json.HasErrors);
            Assert.IsNotNull(json);
        }

        [TestMethod]
        public void Read_UnterminatedDocument_ProducesError_NoThrow ()
        {
            Json json = JsonHelper.ParseText("{ a: 1");
            Assert.IsTrue(json.HasErrors);
        }

        [TestMethod]
        public void Read_UnterminatedArray_ProducesError_NoThrow ()
        {
            Json json = JsonHelper.ParseText("[ 1, 2");
            Assert.IsTrue(json.HasErrors);
        }

        // ===============================[ Round-Trip ]===========================
        [TestMethod]
        public void Read_DocumentRoundTrip_StructuralEquality ()
        {
            string input = "{ name: \"AJ\", count: 42, active: true }";
            Json json1 = JsonHelper.ParseText(input);
            AssertNoErrors(json1);

            string serialized = json1.ToString();
            Json json2 = JsonHelper.ParseText(serialized);
            AssertNoErrors(json2);

            JsonDocument d1 = (JsonDocument)json1.Data;
            JsonDocument d2 = (JsonDocument)json2.Data;
            Assert.AreEqual(d1.Count, d2.Count);
            Assert.AreEqual(d1.ValueFor("name").StringValue, d2.ValueFor("name").StringValue);
            Assert.AreEqual(d1.ValueFor("count").StringValue, d2.ValueFor("count").StringValue);
            Assert.AreEqual(d1.ValueFor("active").StringValue, d2.ValueFor("active").StringValue);
        }

        [TestMethod]
        public void Read_NestedDocumentRoundTrip ()
        {
            string input = "{ outer: { inner: \"v\" }, list: [1, 2, 3] }";
            Json json1 = JsonHelper.ParseText(input);
            AssertNoErrors(json1);

            string serialized = json1.ToString();
            Json json2 = JsonHelper.ParseText(serialized);
            AssertNoErrors(json2);

            JsonDocument d2 = (JsonDocument)json2.Data;
            JsonDocument inner = (JsonDocument)d2.ValueFor("outer");
            Assert.AreEqual("v", inner.ValueFor("inner").StringValue);

            JsonArray list = (JsonArray)d2.ValueFor("list");
            Assert.AreEqual(3, list.Count);
        }

        // ===============================[ Helpers ]===========================
        private static void AssertNoErrors (Json json)
        {
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n  " + String.Join("\n  ", json.Errors));
            Assert.IsNotNull(json.Data);
        }
    }
}
