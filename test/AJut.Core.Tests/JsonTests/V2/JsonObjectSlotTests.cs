namespace AJut.Core.UnitTests.AJsonV2
{
    using System;
    using System.Collections.Generic;
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// A single value read into a slot typed exactly object (an object member, a Dictionary&lt;string, object&gt; value or a
    /// List&lt;object&gt; item) comes back as the type its json says: quoted text is a string, true and false are a bool, and a
    /// number is a long when it is a whole number that fits one, and a double otherwise. Anything written as text, a Guid
    /// included, comes back as that text. Each slot is read by reflection and through a generated reader, and a dictionary in
    /// both shapes it has been written in.
    /// </summary>
    [TestClass]
    public class JsonObjectSlotTests
    {
        private const string kText = "20";
        private const bool kFlag = true;
        private const double kReal = 12.5;
        private const int kWhole = 42;
        private const string kIdText = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";
        private static readonly Guid kId = Guid.Parse(kIdText);

        // Before 1.6 every dictionary was written as an array of Key and Value documents. Since then, one whose keys are each a
        //  single value is written as a document of its entries.
        private const string kPairsText = "{ \"Values\" : [ "
            + "{ \"Key\" : \"text\", \"Value\" : \"20\" }, "
            + "{ \"Key\" : \"flag\", \"Value\" : true }, "
            + "{ \"Key\" : \"real\", \"Value\" : 12.5 }, "
            + "{ \"Key\" : \"whole\", \"Value\" : 42 }, "
            + "{ \"Key\" : \"id\", \"Value\" : \"" + kIdText + "\" } ] }";

        // ===========================[ Test Models ]===========================
        public class Slots
        {
            public object Text { get; set; }
            public object Flag { get; set; }
            public object Real { get; set; }
            public object Whole { get; set; }
            public object Id { get; set; }
            public Dictionary<string, object> Values { get; set; }
            public List<object> Items { get; set; }
        }

        [OptimizeAJson]
        public class SlotsGen
        {
            public object Text { get; set; }
            public object Flag { get; set; }
            public object Real { get; set; }
            public object Whole { get; set; }
            public object Id { get; set; }
            public Dictionary<string, object> Values { get; set; }
            public List<object> Items { get; set; }
        }

        // ===========================[ Object members ]===========================
        [TestMethod]
        public void ObjectMember_ReadsStringBoolDoubleLong_AndAGuidAsItsString_ByReflection ()
        {
            var source = new Slots { Text = kText, Flag = kFlag, Real = kReal, Whole = kWhole, Id = kId };

            Slots read = RoundTrip(source, out string text);

            AssertEachScalar(read.Text, read.Flag, read.Real, read.Whole, read.Id, text);
        }

        [TestMethod]
        public void ObjectMember_ReadsStringBoolDoubleLong_AndAGuidAsItsString_ByAGeneratedReader ()
        {
            var source = new SlotsGen { Text = kText, Flag = kFlag, Real = kReal, Whole = kWhole, Id = kId };

            SlotsGen read = RoundTrip(source, out string text);

            AssertEachScalar(read.Text, read.Flag, read.Real, read.Whole, read.Id, text);
        }

        // ===========================[ Dictionary values ]===========================
        [TestMethod]
        public void ObjectDictionaryValue_AsADocument_ReadsStringBoolDoubleLong_AndAGuidAsItsString_ByReflection ()
        {
            Slots read = RoundTrip(new Slots { Values = MakeValues() }, out string text);

            AssertWrittenAsADocument(text);
            AssertEachValue(read.Values, text);
        }

        [TestMethod]
        public void ObjectDictionaryValue_AsADocument_ReadsStringBoolDoubleLong_AndAGuidAsItsString_ByAGeneratedReader ()
        {
            SlotsGen read = RoundTrip(new SlotsGen { Values = MakeValues() }, out string text);

            AssertWrittenAsADocument(text);
            AssertEachValue(read.Values, text);
        }

        [TestMethod]
        public void ObjectDictionaryValue_AsKeyValuePairs_ReadsStringBoolDoubleLong_AndAGuidAsItsString_ByReflection ()
        {
            Json json = JsonHelper.ParseText(kPairsText);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            Slots read = JsonHelper.BuildObjectForJson<Slots>(json);

            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            AssertEachValue(read.Values, kPairsText);
        }

        [TestMethod]
        public void ObjectDictionaryValue_AsKeyValuePairs_ReadsStringBoolDoubleLong_AndAGuidAsItsString_ByAGeneratedReader ()
        {
            Json json = JsonHelper.ParseText(kPairsText);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            SlotsGen read = JsonHelper.BuildObjectForJson<SlotsGen>(json);

            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            AssertEachValue(read.Values, kPairsText);
        }

        // ===========================[ List items ]===========================
        [TestMethod]
        public void ObjectListItem_ReadsStringBoolDoubleLong_AndAGuidAsItsString_ByReflection ()
        {
            Slots read = RoundTrip(new Slots { Items = MakeItems() }, out string text);

            Assert.AreEqual(5, read.Items.Count, text);
            AssertEachScalar(read.Items[0], read.Items[1], read.Items[2], read.Items[3], read.Items[4], text);
        }

        [TestMethod]
        public void ObjectListItem_ReadsStringBoolDoubleLong_AndAGuidAsItsString_ByAGeneratedReader ()
        {
            SlotsGen read = RoundTrip(new SlotsGen { Items = MakeItems() }, out string text);

            Assert.AreEqual(5, read.Items.Count, text);
            AssertEachScalar(read.Items[0], read.Items[1], read.Items[2], read.Items[3], read.Items[4], text);
        }

        // ===========================[ What decides the type ]===========================
        [TestMethod]
        public void ObjectSlot_WholeNumberIsALong_UntilItIsPastLongRange_AnythingElseADouble ()
        {
            Assert.AreEqual(-7L, ReadSingleValue("-7"));
            Assert.AreEqual(Int64.MaxValue, ReadSingleValue("9223372036854775807"));
            Assert.AreEqual(9223372036854775808d, ReadSingleValue("9223372036854775808"));
            Assert.AreEqual(1E+20, ReadSingleValue("1E+20"));
            Assert.AreEqual(0.25, ReadSingleValue("0.25"));
        }

        // Before 1.6 a list's items were all written quoted, numbers and bools included. Quoted text is a string, so there is
        //  no telling those apart from text that was a string all along, and they come back as the text.
        [TestMethod]
        public void ObjectSlot_QuotedText_IsAString_EvenWhenItReadsAsABoolOrNumber ()
        {
            const string kQuotedItems = "{ \"Items\" : [ \"20\", \"true\", \"12.5\", \"42\" ] }";
            Json json = JsonHelper.ParseText(kQuotedItems);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            Slots read = JsonHelper.BuildObjectForJson<Slots>(json);

            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            CollectionAssert.AreEqual(new object[] { "20", "true", "12.5", "42" }, read.Items);
        }

        // A lenient read takes a value with no quotes, and one that is neither a bool nor a number is still text
        [TestMethod]
        public void ObjectSlot_UnquotedTextThatIsNoBoolOrNumber_IsAString ()
        {
            Json json = JsonHelper.ParseText("{ \"Text\" : hello }");
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            Slots read = JsonHelper.BuildObjectForJson<Slots>(json);

            Assert.AreEqual("hello", read.Text);
        }

        // A caller filling an object straight from a single value gets the same read an object slot does, whether what it hands
        //  in is null or a bare object
        [TestMethod]
        public void FillOutObjectWithJson_IntoAnObject_ReadsStringBoolDoubleLong_AndAGuidAsItsString ()
        {
            Json json = JsonHelper.ParseText("[ \"20\", true, 12.5, 42, \"" + kIdText + "\" ]");
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            JsonArray values = (JsonArray)json.Data;

            AssertEachScalar(
                FillObject(null, values[0]),
                FillObject(null, values[1]),
                FillObject(null, values[2]),
                FillObject(null, values[3]),
                FillObject(null, values[4]),
                "filled from null"
            );
            Assert.AreEqual<object>((long)kWhole, FillObject(new object(), values[3]), "filled over a bare object");
        }

        // A constructor registered for object is the more specific ask, so it decides what the value is
        [TestMethod]
        public void ObjectSlot_RegisteredConstructorForObject_StillDecides ()
        {
            var settings = new JsonInterpreterSettings();
            settings.RegisterCustomConstructor<object>(value => "built:" + value.StringValue);
            Json json = JsonHelper.ParseText("{ \"Text\" : \"20\", \"Whole\" : 42 }");

            Slots read = JsonHelper.BuildObjectForJson<Slots>(json, settings);

            Assert.AreEqual("built:20", read.Text);
            Assert.AreEqual("built:42", read.Whole);
        }

        // ===========================[ Helpers ]===========================
        private static Dictionary<string, object> MakeValues ()
        {
            return new Dictionary<string, object>
            {
                { "text", kText },
                { "flag", kFlag },
                { "real", kReal },
                { "whole", kWhole },
                { "id", kId },
            };
        }

        private static List<object> MakeItems () => new List<object> { kText, kFlag, kReal, kWhole, kId };

        private static T RoundTrip<T> (T source, out string text)
        {
            text = JsonHelper.BuildJsonForObject(source).ToString();
            Json reparsed = JsonHelper.ParseText(text);
            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport());

            T read = JsonHelper.BuildObjectForJson<T>(reparsed);
            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport());
            return read;
        }

        private static object FillObject (object target, JsonValue value)
        {
            JsonHelper.FillOutObjectWithJson(ref target, typeof(object), value);
            return target;
        }

        private static object ReadSingleValue (string unquotedText)
        {
            return JsonHelper.BuildObjectForJson<object>(new JsonValue(unquotedText, isQuoted: false));
        }

        private static void AssertWrittenAsADocument (string text)
        {
            Json json = JsonHelper.ParseText(text);
            Assert.IsTrue(((JsonDocument)json.Data).ValueFor(nameof(Slots.Values)).IsDocument, text);
        }

        private static void AssertEachValue (Dictionary<string, object> values, string text)
        {
            Assert.IsNotNull(values, text);
            Assert.AreEqual(5, values.Count, text);
            AssertEachScalar(values["text"], values["flag"], values["real"], values["whole"], values["id"], text);
        }

        // Assert.AreEqual given two objects compares them with Equals, so a boxed int does not pass for a long, nor a float for
        //  a double, and the type is checked along with the value
        private static void AssertEachScalar (object text, object flag, object real, object whole, object id, string context)
        {
            Assert.AreEqual<object>(kText, text, context);
            Assert.AreEqual<object>(kFlag, flag, context);
            Assert.AreEqual<object>(kReal, real, context);
            Assert.AreEqual<object>((long)kWhole, whole, context);
            Assert.AreEqual<object>(kIdText, id, context);
        }
    }
}
