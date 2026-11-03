namespace AJut.Core.UnitTests.AJsonV2
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Text and builds at the edges of what AJson reads and writes: hand-edited and minified text, deep nesting, a value of
    /// only spaces or one that looks like a number, an unquoted value right before a line break, values the builder is told
    /// never to quote, nullable members, and an array with a null slot.
    /// </summary>
    [TestClass]
    public class JsonEdgeCaseTests
    {
        private const string kAnyId = "0aa8db63-5d15-4434-8a7a-9abc1a828c2e";
        private const string kThreeSpaces = "   ";
        private const string kMinifiedText = "{\"Names\":[\"One\",\"Two\"],\"Pairs\":[{\"Foo\":\"Bar\"},{\"Foo2\":\"Bar2\"}],\"Grid\":[[\"1\",\"2\"],[\"3\",\"4\"]],\"Empty\":[]}";

        // Text from Windows and text from everywhere else, so neither line ending is the only one read
        private static readonly string[] kNewlines = { "\n", "\r\n" };

        private static readonly string[] kElementTexts = { "This", " ", "Sentence", " ", "Rox", "!" };

        private static readonly string[] kHandEditedItemLines =
        {
            "{",
            "\tTitle: \"Test Item\",",
            "\tType: \"Wondrous\",",
            "\tRarity: \"Uncommon\",",
            "\tWeight: \"1/2 pound\",",
            "\tDescription: \"A very odd bag, when turned over empties out light that you can hold.\",",
            "\tTable: {",
            "\t\tIsFirstRowTitleRow: true,",
            "\t\tRows: [",
            "\t\t\t[ \"d4\", \"Effect\" ],",
            "\t\t\t[ \"1\", \"Burn Hand\" ],",
            "\t\t\t[ \"2\", \"Lightup Hand\" ],",
            "\t\t\t[ \"3\", \"Miss Hand (light up floor)\" ],",
            "\t\t\t[ \"4\", \"Darken hand (light sucked into bag)\" ],",
            "\t\t]",
            "\t},",
            "\tSource: \"Homebrew: pg 123\"",
            "}",
        };

        // Each key with a nested document has a space after its colon, and its document opens on the next line
        private static readonly string[] kNestedOnNextLineLines =
        {
            "{",
            "\t\"IntThing\" : \"60\",",
            "\t\"FloatThing\" : \"0.25\",",
            "\t\"SubObj\" : ",
            "\t{",
            "\t\t\"Info\" : \"Some text\",",
            "\t\t\"SubSubObj\" : ",
            "\t\t{",
            "\t\t\t\"X\" : \"0\",",
            "\t\t\t\"Y\" : \"0\"",
            "\t\t}",
            "\t}",
            "}",
        };

        // ===========================[ Hand-Edited and Minified Text ]===========================
        /// <summary>
        /// Unquoted keys, tab indenting, a colon inside a quoted value, and a trailing comma after the last row of a nested
        /// array, which adds no row
        /// </summary>
        [TestMethod]
        public void Read_HandEditedText_WithATrailingCommaAfterANestedArray_ParsesWithoutErrors ()
        {
            foreach (string newline in kNewlines)
            {
                string text = String.Join(newline, kHandEditedItemLines);
                JsonDocument item = ParseDocument(text);

                Assert.AreEqual(7, item.Count, text);
                Assert.AreEqual("Test Item", item.ValueFor("Title")?.StringValue, text);
                Assert.AreEqual("Homebrew: pg 123", item.ValueFor("Source")?.StringValue, text);

                JsonDocument table = DocumentAt(item, "Table");
                Assert.AreEqual("true", table.ValueFor("IsFirstRowTitleRow")?.StringValue, text);

                JsonArray rows = ArrayAt(table, "Rows");
                Assert.AreEqual(5, rows.Count, text);

                JsonArray lastRow = rows[4] as JsonArray;
                Assert.IsNotNull(lastRow, text);
                Assert.AreEqual(2, lastRow.Count, text);
                Assert.AreEqual("Darken hand (light sucked into bag)", lastRow[1].StringValue, text);
            }
        }

        [TestMethod]
        public void Read_NestedDocumentOnTheLineAfterItsKey_ParsesWithoutErrors ()
        {
            foreach (string newline in kNewlines)
            {
                string text = String.Join(newline, kNestedOnNextLineLines);
                JsonDocument root = ParseDocument(text);

                Assert.AreEqual(3, root.Count, text);
                Assert.AreEqual("0.25", root.ValueFor("FloatThing")?.StringValue, text);

                JsonDocument subObj = DocumentAt(root, "SubObj");
                Assert.AreEqual("Some text", subObj.ValueFor("Info")?.StringValue, text);

                JsonDocument subSubObj = DocumentAt(subObj, "SubSubObj");
                Assert.AreEqual("0", subSubObj.ValueFor("X")?.StringValue, text);
                Assert.AreEqual("0", subSubObj.ValueFor("Y")?.StringValue, text);
            }
        }

        [TestMethod]
        public void Read_MinifiedArraysOfArraysAndDocuments_WithAnEmptyArrayLast_ParseWithoutErrors ()
        {
            JsonDocument root = ParseDocument(kMinifiedText);
            Assert.AreEqual(4, root.Count);

            JsonArray names = ArrayAt(root, "Names");
            Assert.AreEqual(2, names.Count);
            Assert.AreEqual("Two", names[1].StringValue);

            JsonArray pairs = ArrayAt(root, "Pairs");
            Assert.AreEqual(2, pairs.Count);
            JsonDocument secondPair = pairs[1] as JsonDocument;
            Assert.IsNotNull(secondPair);
            Assert.AreEqual("Bar2", secondPair.ValueFor("Foo2")?.StringValue);

            JsonArray grid = ArrayAt(root, "Grid");
            Assert.AreEqual(2, grid.Count);
            JsonArray secondRow = grid[1] as JsonArray;
            Assert.IsNotNull(secondRow);
            Assert.AreEqual("3", secondRow[0].StringValue);
            Assert.AreEqual("4", secondRow[1].StringValue);

            Assert.AreEqual(0, ArrayAt(root, "Empty").Count);
        }

        /// <summary>
        /// Documents and arrays several levels deep, type ids holding assembly-qualified names (commas, spaces and equals signs
        /// inside quotes), a trailing comma after every element's last value, and a value that is a single space. Chapters also
        /// holds two keys with no document around them, Id and Paragraphs. An array holds no keys, so the reader reports each
        /// one and reads it with its value as a document of that one entry, and every value under them still reads.
        /// </summary>
        [TestMethod]
        public void Read_DeeplyNestedText_WithTypeIdsAndTrailingCommas_ReadsEveryValue_AndReportsTheKeysInAnArray ()
        {
            foreach (string newline in kNewlines)
            {
                string text = MakeNestedElementText(newline);
                Json json = JsonHelper.ParseText(text);

                Assert.AreEqual(2, json.Errors.Count, json.GetErrorReport() + "\nText:\n" + text);
                StringAssert.Contains(json.Errors[0], "Key 'Id' inside an array", text);
                StringAssert.Contains(json.Errors[1], "Key 'Paragraphs' inside an array", text);
                Assert.IsInstanceOfType(json.Data, typeof(JsonDocument), text);

                List<string> found = new List<string>();
                CollectValuesFor(json.Data, nameof(Element.Text), found);
                CollectionAssert.AreEqual(kElementTexts, found, text);
            }
        }

        [TestMethod]
        public void Read_ArrayClosedByABrace_ProducesError_NoThrow ()
        {
            Json json = JsonHelper.ParseText("{ \"Text\": [}");
            Assert.IsTrue(json.HasErrors, "An array closed by a brace is never closed");
        }

        // ===========================[ Whitespace and Number-Like Text ]===========================
        [TestMethod]
        public void Read_QuotedValueOfOnlySpaces_IsKept ()
        {
            JsonDocument doc = ParseDocument("{ \"Text\": \"" + kThreeSpaces + "\"}");

            Assert.AreEqual(1, doc.Count);
            Assert.AreEqual("Text", doc.KeyAt(0));
            Assert.AreEqual(kThreeSpaces, doc.ValueAt(0).StringValue);
            Assert.IsTrue(doc.ValueAt(0).IsQuoted);
        }

        [TestMethod]
        public void Builder_ValueOfOnlySpaces_IsKept_AndReadsBack ()
        {
            Json json = JsonHelper.MakeRootBuilder()
                .StartDocument()
                    .AddProperty("Text", kThreeSpaces)
                .Finalize();
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            JsonDocument built = (JsonDocument)json.Data;
            Assert.AreEqual(1, built.Count);
            Assert.AreEqual("Text", built.KeyAt(0));
            Assert.AreEqual(kThreeSpaces, built.ValueAt(0).StringValue);

            string text = json.ToString();
            Assert.AreEqual(kThreeSpaces, ParseDocument(text).ValueFor("Text")?.StringValue, text);
        }

        [TestMethod]
        public void Builder_TextThatLooksLikeANumber_StaysQuoted_AndReadsBack ()
        {
            Json json = JsonHelper.MakeRootBuilder()
                .StartDocument()
                    .AddProperty("Test", "-2")
                .Finalize();
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            JsonValue built = ((JsonDocument)json.Data).ValueFor("Test");
            Assert.AreEqual("-2", built.StringValue);
            Assert.IsTrue(built.IsQuoted, "Text is written as a string, even when it reads like a number");

            string text = json.ToString();
            JsonValue reparsed = ParseDocument(text).ValueFor("Test");
            Assert.AreEqual("-2", reparsed.StringValue, text);
            Assert.IsTrue(reparsed.IsQuoted, text);
        }

        // ===========================[ Unquoted Values Before a Line Break ]===========================
        [TestMethod]
        public void Read_UnquotedLastValue_FollowedByALineBreak_InADocument_IsJustTheValue ()
        {
            foreach (string newline in kNewlines)
            {
                string text = String.Join(newline, "{", "\tText: \"This\",", "\tTest: Word", "}");
                JsonDocument doc = ParseDocument(text);

                Assert.AreEqual(2, doc.Count, text);
                Assert.AreEqual("Word", doc.ValueAt(1).StringValue, text);
                Assert.IsFalse(doc.ValueAt(1).IsQuoted, text);
            }
        }

        [TestMethod]
        public void Read_UnquotedLastValue_FollowedByALineBreak_InAnArray_IsJustTheValue ()
        {
            foreach (string newline in kNewlines)
            {
                string text = String.Join(newline, "[", "\t\"This\",", "\tWord", "]");
                JsonArray array = ParseArray(text);

                Assert.AreEqual(2, array.Count, text);
                Assert.AreEqual("Word", array[1].StringValue, text);
                Assert.IsFalse(array[1].IsQuoted, text);
            }
        }

        /// <summary>
        /// With NeverQuoteValues every value is written exactly as given, so quotes the caller put in are the only ones
        /// written, and a value given without any is written bare, as the last value before the closing brace
        /// </summary>
        [TestMethod]
        public void Builder_NeverQuoteValues_UnquotedLastValue_InADocument_ReadsBack ()
        {
            JsonBuilderSettings settings = new JsonBuilderSettings { PropertyValueQuoting = ePropertyValueQuoting.NeverQuoteValues };
            Json json = JsonHelper.MakeRootBuilder(settings)
                .StartDocument()
                    .AddProperty("Text", "\"This\"")
                    .AddProperty("Test", "Word")
                .Finalize();
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            string text = json.ToString();
            JsonDocument doc = ParseDocument(text);
            Assert.AreEqual(2, doc.Count, text);
            Assert.AreEqual("This", doc.ValueFor("Text")?.StringValue, text);
            Assert.IsTrue(doc.ValueFor("Text").IsQuoted, text);
            Assert.AreEqual("Word", doc.ValueFor("Test")?.StringValue, text);
            Assert.IsFalse(doc.ValueFor("Test").IsQuoted, text);
        }

        /// <summary>
        /// The array form of <see cref="Builder_NeverQuoteValues_UnquotedLastValue_InADocument_ReadsBack"/>: the bare value
        /// is the last item before the closing bracket
        /// </summary>
        [TestMethod]
        public void Builder_NeverQuoteValues_UnquotedLastValue_InAnArray_ReadsBack ()
        {
            JsonBuilderSettings settings = new JsonBuilderSettings { PropertyValueQuoting = ePropertyValueQuoting.NeverQuoteValues };
            Json json = JsonHelper.MakeRootBuilder(settings)
                .StartArray()
                    .AddArrayItem("\"This\"")
                    .AddArrayItem("Word")
                .Finalize();
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            string text = json.ToString();
            JsonArray array = ParseArray(text);
            Assert.AreEqual(2, array.Count, text);
            Assert.AreEqual("This", array[0].StringValue, text);
            Assert.IsTrue(array[0].IsQuoted, text);
            Assert.AreEqual("Word", array[1].StringValue, text);
            Assert.IsFalse(array[1].IsQuoted, text);
        }

        // ===========================[ Nullable Members ]===========================
        [TestMethod]
        public void NullableGuid_ReadsItsValue ()
        {
            Json json = JsonHelper.ParseText("{ Id: \"" + kAnyId + "\"}");
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            NullableMembers read = JsonHelper.BuildObjectForJson<NullableMembers>(json);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            Assert.IsNotNull(read);
            Assert.IsTrue(read.Id.HasValue);
            Assert.AreEqual(Guid.Parse(kAnyId), read.Id.Value);
            Assert.IsNull(read.Offset);
        }

        /// <summary>
        /// A Vector2 is written as a document of its components, so a nullable one has to come back from that document rather
        /// than from a single value. The null Guid beside it is left out, and reads back as null.
        /// </summary>
        [TestMethod]
        public void NullableVector2_RoundTripsAsADocument ()
        {
            NullableMembers source = new NullableMembers { Offset = new Vector2(0.5f, 3.14f) };

            JsonDocument written = WriteAndReparse(source);
            Assert.IsInstanceOfType(written.ValueFor(nameof(NullableMembers.Offset)), typeof(JsonDocument));
            Assert.IsFalse(written.ContainsKey(nameof(NullableMembers.Id)));

            NullableMembers round = RoundTrip(source, out string text);
            Assert.IsNull(round.Id, text);
            Assert.IsTrue(round.Offset.HasValue, text);
            Assert.AreEqual(source.Offset.Value, round.Offset.Value, text);
        }

        // ===========================[ Arrays ]===========================
        /// <summary>
        /// A null slot in an array keeps its place, so the items after it keep theirs
        /// </summary>
        [TestMethod]
        public void ArrayOfDictionaries_WithANullSlot_BuildsWithoutErrors_AndKeepsItsLength ()
        {
            Dictionary<string, int>[] source = new Dictionary<string, int>[2];
            source[0] = new Dictionary<string, int> { ["Test0"] = 10 };

            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            string text = json.ToString();
            Assert.AreEqual(2, ((JsonArray)json.Data).Count, text);

            Json reparsed = JsonHelper.ParseText(text);
            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport() + "\nText:\n" + text);

            Dictionary<string, int>[] round = JsonHelper.BuildObjectForJson<Dictionary<string, int>[]>(reparsed);
            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport() + "\nText:\n" + text);
            Assert.IsNotNull(round, text);
            Assert.AreEqual(2, round.Length, text);
            Assert.AreEqual(10, round[0]["Test0"], text);
        }

        // ===========================[ Helpers ]===========================
        private static JsonDocument ParseDocument (string text)
        {
            Json json = JsonHelper.ParseText(text);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport() + "\nText:\n" + text);
            Assert.IsInstanceOfType(json.Data, typeof(JsonDocument), text);
            return (JsonDocument)json.Data;
        }

        private static JsonArray ParseArray (string text)
        {
            Json json = JsonHelper.ParseText(text);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport() + "\nText:\n" + text);
            Assert.IsInstanceOfType(json.Data, typeof(JsonArray), text);
            return (JsonArray)json.Data;
        }

        private static JsonDocument DocumentAt (JsonDocument parent, string key)
        {
            JsonDocument child = parent.ValueFor(key) as JsonDocument;
            Assert.IsNotNull(child, $"'{key}' should be a document");
            return child;
        }

        private static JsonArray ArrayAt (JsonDocument parent, string key)
        {
            JsonArray child = parent.ValueFor(key) as JsonArray;
            Assert.IsNotNull(child, $"'{key}' should be an array");
            return child;
        }

        /// <summary>
        /// Every single value held under <paramref name="key"/>, anywhere under <paramref name="value"/>, depth first in
        /// document order
        /// </summary>
        private static void CollectValuesFor (JsonValue value, string key, List<string> found)
        {
            if (value is JsonDocument document)
            {
                foreach (KeyValuePair<string, JsonValue> entry in document)
                {
                    if (entry.Key == key && entry.Value.IsValue)
                    {
                        found.Add(entry.Value.StringValue);
                    }
                    else
                    {
                        CollectValuesFor(entry.Value, key, found);
                    }
                }
            }
            else if (value is JsonArray array)
            {
                foreach (JsonValue item in array)
                {
                    CollectValuesFor(item, key, found);
                }
            }
        }

        private static string MakeNestedElementText (string newline)
        {
            string groupTypeId = typeof(ElementGroup).AssemblyQualifiedName;
            string elementTypeId = typeof(Element).AssemblyQualifiedName;

            List<string> lines = new List<string>
            {
                "{",
                "    Writing: {",
                "        Id: \"" + kAnyId + "\",",
                "        Chapters: [",
                "            Id: \"" + kAnyId + "\",",
                "            Paragraphs:  [",
                "                {",
                "                    Id: \"" + kAnyId + "\",",
                "                    Elements: [",
                "                        {",
                "                            __type: \"" + groupTypeId + "\",",
                "                            Id: \"" + kAnyId + "\",",
                "                            Elements: [",
            };

            for (int index = 0; index < kElementTexts.Length; ++index)
            {
                lines.Add("                                {");
                lines.Add("                                    __type: \"" + elementTypeId + "\",");
                lines.Add("                                    Id: \"" + kAnyId + "\",");
                lines.Add("                                    Text: \"" + kElementTexts[index] + "\",");
                lines.Add(index < kElementTexts.Length - 1 ? "                                }," : "                                }");
            }

            lines.Add("                            ]");
            lines.Add("                        }");
            lines.Add("                    ]");
            lines.Add("\t\t\t\t}");
            lines.Add("\t\t\t]");
            lines.Add("\t\t]");
            lines.Add("    }");
            lines.Add("}");
            return String.Join(newline, lines);
        }

        private static JsonDocument WriteAndReparse (object source)
        {
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            Json reparsed = JsonHelper.ParseText(json.ToString());
            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport());
            return (JsonDocument)reparsed.Data;
        }

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

        // ===========================[ Test Models ]===========================
        /// <summary>
        /// Named in text by its assembly-qualified name, as a type id
        /// </summary>
        public class ElementGroup
        {
            public Guid Id { get; set; }
            public List<Element> Elements { get; set; }
        }

        /// <summary>
        /// Named in text by its assembly-qualified name, as a type id
        /// </summary>
        public class Element
        {
            public Guid Id { get; set; }
            public string Text { get; set; }
        }

        public class NullableMembers
        {
            public Guid? Id { get; set; }
            public Vector2? Offset { get; set; }
        }
    }
}
