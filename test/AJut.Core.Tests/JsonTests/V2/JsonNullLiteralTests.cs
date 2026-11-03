namespace AJut.Core.UnitTests.AJsonV2
{
    using System.Collections.Generic;
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Json's null literal: an unquoted null reads as null (or a value type's default) wherever a value goes, a quoted "null"
    /// is the string, and a dictionary pair holding a null value, a null list item and a null array slot each write as null
    /// </summary>
    [TestClass]
    public class JsonNullLiteralTests
    {
        private const string kNullEverywhere = "{ \"Name\" : null, \"Count\" : null, \"Maybe\" : null, \"Tags\" : [ \"a\", null, \"null\" ], \"ByName\" : { \"x\" : null, \"y\" : \"null\" } }";

        // ===========================[ Test Models ]===========================
        public class Members
        {
            public string Name { get; set; }
            public int Count { get; set; }
            public int? Maybe { get; set; }
            public List<string> Tags { get; set; }
            public Dictionary<string, string> ByName { get; set; }
        }

        [OptimizeAJson]
        public class MembersGen
        {
            public string Name { get; set; }
            public int Count { get; set; }
            public int? Maybe { get; set; }
            public List<string> Tags { get; set; }
            public Dictionary<string, string> ByName { get; set; }
        }

        /// <summary>A dictionary key that is more than one value, so its dictionary is written as Key and Value pairs</summary>
        public struct GridCell
        {
            public int Row { get; set; }
            public int Column { get; set; }
        }

        /// <summary>
        /// A collection of each kind a null can sit in: a list, and arrays of single values, of dictionaries, and of documents
        /// </summary>
        public class CollectionMembers
        {
            public List<string> Names { get; set; }
            public string[] Labels { get; set; }
            public Dictionary<string, int>[] Lookups { get; set; }
            public Note[] Notes { get; set; }
        }

        [OptimizeAJson]
        public class CollectionMembersGen
        {
            public List<string> Names { get; set; }
            public string[] Labels { get; set; }
            public Dictionary<string, int>[] Lookups { get; set; }
            public Note[] Notes { get; set; }
        }

        /// <summary>An array element written as a document</summary>
        public class Note
        {
            public string Text { get; set; }
        }

        // ===========================[ Tests ]===========================
        [TestMethod]
        public void NullLiteral_ReadsAsNull_ByReflection ()
        {
            Json json = JsonHelper.ParseText(kNullEverywhere);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            Members read = JsonHelper.BuildObjectForJson<Members>(json);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            Assert.IsNull(read.Name);
            Assert.AreEqual(0, read.Count);
            Assert.IsNull(read.Maybe);
            CollectionAssert.AreEqual(new[] { "a", null, "null" }, read.Tags);
            Assert.IsNull(read.ByName["x"]);
            Assert.AreEqual("null", read.ByName["y"]);
        }

        [TestMethod]
        public void NullLiteral_ReadsAsNull_ByAGeneratedReader ()
        {
            Json json = JsonHelper.ParseText(kNullEverywhere);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            MembersGen read = JsonHelper.BuildObjectForJson<MembersGen>(json);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            Assert.IsNull(read.Name);
            Assert.AreEqual(0, read.Count);
            Assert.IsNull(read.Maybe);
            CollectionAssert.AreEqual(new[] { "a", null, "null" }, read.Tags);
            Assert.IsNull(read.ByName["x"]);
            Assert.AreEqual("null", read.ByName["y"]);
        }

        [TestMethod]
        public void QuotedNull_IsTheString_AndRoundTrips ()
        {
            string text = JsonHelper.BuildJsonForObject(new Members { Name = "null" }).ToString();

            Json reparsed = JsonHelper.ParseText(text);
            Members round = JsonHelper.BuildObjectForJson<Members>(reparsed);
            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport());
            Assert.AreEqual("null", round.Name, text);
        }

        [TestMethod]
        public void DictionaryPair_NullValue_IsWrittenAsNull ()
        {
            var source = new Dictionary<GridCell, string> { { new GridCell { Row = 1, Column = 2 }, null } };

            string text = JsonHelper.BuildJsonForObject(source).ToString();

            StringAssert.Contains(text, "\"Value\" : null");
        }

        // An entry is data, where a member is shape: a dictionary entry holding null is written as null, not left out
        [TestMethod]
        public void DictionaryDocument_NullEntry_RoundTrips_ByReflection ()
        {
            var source = new Members { ByName = new Dictionary<string, string> { { "x", null }, { "y", "kept" } } };

            string text = JsonHelper.BuildJsonForObject(source).ToString();
            Json reparsed = JsonHelper.ParseText(text);
            Members round = JsonHelper.BuildObjectForJson<Members>(reparsed);

            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport());
            Assert.IsTrue(round.ByName.ContainsKey("x"), text);
            Assert.IsNull(round.ByName["x"]);
            Assert.AreEqual("kept", round.ByName["y"]);
            Assert.IsNull(round.Name, "A null member is still left out, and reads back as null");
            Assert.IsFalse(text.Contains("\"Name\""), text);
        }

        [TestMethod]
        public void DictionaryDocument_NullEntry_RoundTrips_ByAGeneratedSerializer ()
        {
            var source = new MembersGen { ByName = new Dictionary<string, string> { { "x", null }, { "y", "kept" } } };

            string text = JsonHelper.BuildJsonForObject(source).ToString();
            Json reparsed = JsonHelper.ParseText(text);
            MembersGen round = JsonHelper.BuildObjectForJson<MembersGen>(reparsed);

            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport());
            Assert.IsTrue(round.ByName.ContainsKey("x"), text);
            Assert.IsNull(round.ByName["x"]);
            Assert.AreEqual("kept", round.ByName["y"]);
        }

        [TestMethod]
        public void DictionaryDocument_NullEntry_IsWrittenAsNull ()
        {
            var source = new Dictionary<string, int?> { { "x", null }, { "y", 5 } };

            string text = JsonHelper.BuildJsonForObject(source).ToString();

            StringAssert.Contains(text, "\"x\" : null");
        }

        // Quoting every value quotes numbers and bools, which read back the same, but a quoted null is the string "null"
        [TestMethod]
        public void DictionaryPair_NullValue_IsWrittenAsNull_WhenQuotingEverything ()
        {
            var source = new Dictionary<GridCell, string> { { new GridCell { Row = 1, Column = 2 }, null } };
            var settings = new JsonBuilderSettings { PropertyValueQuoting = ePropertyValueQuoting.QuoteAll };

            string text = JsonHelper.BuildJsonForObject(source, settings).ToString();

            StringAssert.Contains(text, "\"Value\" : null");
            Assert.IsFalse(text.Contains("\"Value\" : \"null\""), text);
        }

        // A null item keeps its place in a list, so the list keeps its length and every item after it keeps its index
        [TestMethod]
        public void ListItem_Null_RoundTrips_ByReflection ()
        {
            var source = new CollectionMembers { Names = new List<string> { "x", null, "y" } };

            CollectionMembers round = RoundTrip(source, out string text);

            CollectionAssert.AreEqual(new[] { "x", null, "y" }, round.Names, text);
        }

        [TestMethod]
        public void ListItem_Null_RoundTrips_ByAGeneratedSerializer ()
        {
            var source = new CollectionMembersGen { Names = new List<string> { "x", null, "y" } };

            CollectionMembersGen round = RoundTrip(source, out string text);

            CollectionAssert.AreEqual(new[] { "x", null, "y" }, round.Names, text);
        }

        // A null slot in an array reads back as null, not as an empty stand-in for its element type
        [TestMethod]
        public void ArraySlot_Null_RoundTrips_ByReflection ()
        {
            var source = new CollectionMembers { Labels = new[] { "x", null } };

            CollectionMembers round = RoundTrip(source, out string text);

            CollectionAssert.AreEqual(new[] { "x", null }, round.Labels, text);
        }

        [TestMethod]
        public void ArraySlot_Null_RoundTrips_ByAGeneratedSerializer ()
        {
            var source = new CollectionMembersGen { Labels = new[] { "x", null } };

            CollectionMembersGen round = RoundTrip(source, out string text);

            CollectionAssert.AreEqual(new[] { "x", null }, round.Labels, text);
        }

        [TestMethod]
        public void DictionaryArraySlot_Null_RoundTrips_ByReflection ()
        {
            var source = new CollectionMembers { Lookups = new[] { new Dictionary<string, int> { { "a", 1 } }, null } };

            CollectionMembers round = RoundTrip(source, out string text);

            Assert.AreEqual(2, round.Lookups.Length, text);
            Assert.AreEqual(1, round.Lookups[0]["a"], text);
            Assert.IsNull(round.Lookups[1], text);
        }

        [TestMethod]
        public void DictionaryArraySlot_Null_RoundTrips_ByAGeneratedSerializer ()
        {
            var source = new CollectionMembersGen { Lookups = new[] { new Dictionary<string, int> { { "a", 1 } }, null } };

            CollectionMembersGen round = RoundTrip(source, out string text);

            Assert.AreEqual(2, round.Lookups.Length, text);
            Assert.AreEqual(1, round.Lookups[0]["a"], text);
            Assert.IsNull(round.Lookups[1], text);
        }

        [TestMethod]
        public void DocumentArraySlot_Null_RoundTrips_ByReflection ()
        {
            var source = new CollectionMembers { Notes = new[] { null, new Note { Text = "kept" } } };

            CollectionMembers round = RoundTrip(source, out string text);

            Assert.AreEqual(2, round.Notes.Length, text);
            Assert.IsNull(round.Notes[0], text);
            Assert.AreEqual("kept", round.Notes[1].Text, text);
        }

        [TestMethod]
        public void DocumentArraySlot_Null_RoundTrips_ByAGeneratedSerializer ()
        {
            var source = new CollectionMembersGen { Notes = new[] { null, new Note { Text = "kept" } } };

            CollectionMembersGen round = RoundTrip(source, out string text);

            Assert.AreEqual(2, round.Notes.Length, text);
            Assert.IsNull(round.Notes[0], text);
            Assert.AreEqual("kept", round.Notes[1].Text, text);
        }

        [TestMethod]
        public void ListItem_Null_IsWrittenAsNull ()
        {
            string text = JsonHelper.BuildJsonForObject(new List<string> { "x", null }).ToString();

            JsonArray reparsed = ParseArray(text);
            Assert.AreEqual(2, reparsed.Count, text);
            Assert.IsTrue(reparsed[1].IsNull, text);
        }

        [TestMethod]
        public void ArraySlot_Null_IsWrittenAsNull_WhenQuotingEverything ()
        {
            var settings = new JsonBuilderSettings { PropertyValueQuoting = ePropertyValueQuoting.QuoteAll };

            string text = JsonHelper.BuildJsonForObject(new[] { "x", null }, settings).ToString();

            JsonArray reparsed = ParseArray(text);
            Assert.AreEqual(2, reparsed.Count, text);
            Assert.IsTrue(reparsed[1].IsNull, text);
        }

        [TestMethod]
        public void BuilderArrayItem_Null_IsWrittenAsNull ()
        {
            Json json = JsonHelper.MakeRootBuilder()
                .StartArray()
                    .AddArrayItem("x")
                    .AddArrayItem(null)
                .Finalize();

            string text = json.ToString();

            JsonArray reparsed = ParseArray(text);
            Assert.AreEqual(2, reparsed.Count, text);
            Assert.IsTrue(reparsed[1].IsNull, text);
        }

        [TestMethod]
        public void DocumentSet_Null_IsWrittenAsNull ()
        {
            JsonDocument document = (JsonDocument)JsonHelper.ParseText("{ \"a\" : 1, \"b\" : 2 }").Data;
            document.Set("a", null);

            string text = document.StringValue;

            Json reparsed = JsonHelper.ParseText(text);
            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport() + "\nText:\n" + text);
            Assert.IsTrue(((JsonDocument)reparsed.Data).ValueFor("a")?.IsNull == true, text);
        }

        [TestMethod]
        public void ArrayAppendNew_Null_IsWrittenAsNull ()
        {
            // A bare null would pick the JsonBuilder overload
            JsonArray array = (JsonArray)JsonHelper.ParseText("[ \"x\" ]").Data;
            array.AppendNew((object)null);

            string text = array.StringValue;

            JsonArray reparsed = ParseArray(text);
            Assert.AreEqual(2, reparsed.Count, text);
            Assert.IsTrue(reparsed[1].IsNull, text);
        }

        [TestMethod]
        public void DocumentAppendNew_Null_IsWrittenAsNull ()
        {
            JsonDocument document = (JsonDocument)JsonHelper.ParseText("{ \"a\" : 1 }").Data;
            document.AppendNew("b", (object)null);

            string text = document.StringValue;

            Json reparsed = JsonHelper.ParseText(text);
            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport() + "\nText:\n" + text);
            Assert.IsTrue(((JsonDocument)reparsed.Data).ValueFor("b")?.IsNull == true, text);
        }

        // A bare null picks the JsonBuilder overload, and a missing builder is written as null, the same as a null value
        [TestMethod]
        public void ArrayAppendNew_NullBuilder_IsWrittenAsNull ()
        {
            JsonArray array = (JsonArray)JsonHelper.ParseText("[ \"x\" ]").Data;
            array.AppendNew(null);

            string text = array.StringValue;

            JsonArray reparsed = ParseArray(text);
            Assert.AreEqual(2, reparsed.Count, text);
            Assert.IsTrue(reparsed[1].IsNull, text);
        }

        [TestMethod]
        public void DocumentAppendNew_NullBuilder_IsWrittenAsNull ()
        {
            JsonDocument document = (JsonDocument)JsonHelper.ParseText("{ \"a\" : 1 }").Data;
            document.AppendNew("b", null);

            string text = document.StringValue;

            Json reparsed = JsonHelper.ParseText(text);
            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport() + "\nText:\n" + text);
            Assert.IsTrue(((JsonDocument)reparsed.Data).ValueFor("b")?.IsNull == true, text);
        }

        // ===========================[ Helpers ]===========================
        private static T RoundTrip<T> (T source, out string text)
        {
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            text = json.ToString();
            Json reparsed = JsonHelper.ParseText(text);
            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport() + "\nText:\n" + text);

            T round = JsonHelper.BuildObjectForJson<T>(reparsed);
            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport() + "\nText:\n" + text);
            return round;
        }

        private static JsonArray ParseArray (string text)
        {
            Json json = JsonHelper.ParseText(text);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport() + "\nText:\n" + text);
            Assert.IsInstanceOfType(json.Data, typeof(JsonArray), text);
            return (JsonArray)json.Data;
        }
    }
}
