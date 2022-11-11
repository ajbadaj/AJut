namespace AJut.Core.UnitTests.AJson
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Web;
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class JsonParserTests
    {
        const string test_jsonValue = "dude";
        const string test_simpleDoc = "{ dude: ' sweet' }";
        const string test_simpleArray = "[ {dude: sweet}, item ]";
        const string test_keyDuplicate = "{ \"Test\" : 0, \"Test\" : 1 }";

        [TestMethod]
        public void AJson_GeneralParsing_BasicValues_JsonValue ()
        {
            Json json = JsonHelper.ParseText(test_jsonValue);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsNotNull(json.Data);
            Assert.IsTrue(json.Data.IsValue);
            Assert.IsFalse(json.Data.IsDocument);
            Assert.IsFalse(json.Data.IsArray);

            json.AssertSourceIsValid();
        }

        [TestMethod]
        public void AJson_GeneralParsing_BasicValues_JsonDocument ()
        {
            Json json = JsonHelper.ParseText(test_simpleDoc);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsNotNull(json.Data);

            Assert.IsTrue(json.Data.IsDocument);
            Assert.IsFalse(json.Data.IsValue);
            Assert.IsFalse(json.Data.IsArray);

            json.AssertSourceIsValid();
        }

        [TestMethod]
        public void AJson_GeneralParsing_BasicValues_JsonArray ()
        {
            Json json = JsonHelper.ParseText(test_simpleArray);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsNotNull(json.Data);

            Assert.IsTrue(json.Data.IsArray);
            Assert.IsFalse(json.Data.IsDocument);
            Assert.IsFalse(json.Data.IsValue);

            json.AssertSourceIsValid();
        }

        [TestMethod]
        public void AJson_GeneralParsing_JsonArray_ValidCount ()
        {
            Json json = JsonHelper.ParseText(test_simpleArray);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsNotNull(json.Data);

            Assert.IsTrue(json.Data.IsArray);

            Assert.AreEqual(2, ((JsonArray)json.Data).Count);
        }

        [TestMethod]
        public void AJson_GeneralParsing_JsonDocument_RespectsQutoedText ()
        {
            Json json = JsonHelper.ParseText(test_simpleDoc);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsNotNull(json.Data);

            Assert.IsTrue(json.Data.IsDocument);

            ((JsonDocument)json.Data).First().Value.StringValue.StartsWith(" ");
        }

        [TestMethod]
        public void AJson_GeneralParsing_JsonDocument_AllowsDuplicateKeys ()
        {
            Json json = JsonHelper.ParseText(test_keyDuplicate);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));

            JsonDocument jsonDoc = json.Data as JsonDocument;
            Assert.IsNotNull(json.Data);

            Assert.IsTrue(jsonDoc.All(kvp => kvp.Key == "Test"));
            Assert.AreEqual(2, jsonDoc.Count);
            Assert.AreEqual(2, jsonDoc.AllValuesForKey("Test").Length);
        }

        [TestMethod]
        public void AJson_GeneralParsing_JsonDocument_StoresDuplicateKeysCorrectly ()
        {
            Json json = JsonHelper.ParseText(test_keyDuplicate);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));


            JsonDocument doc = json.Data as JsonDocument;
            Assert.IsNotNull(doc);

            JsonValue[] dupes = doc.AllValuesForKey("Test");
            Assert.IsNotNull(dupes);
            Assert.AreEqual(2, dupes.Length);
            Assert.AreEqual("0", dupes[0].StringValue);
            Assert.AreEqual("1", dupes[1].StringValue);
        }

        [TestMethod]
        public void AJson_GeneralParsing_JsonDocument_QuoteParsingSucceeds ()
        {
            Json json = JsonHelper.ParseText(
@"
{
    Test1 : 2,
    ""Test"" : {
        ""Test2"" : ""Value""
    }
}
");
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
        }

        [TestMethod]
        public void AJson_GeneralParsing_JsonArray_MultiArray ()
        {
            Json json = JsonHelper.ParseText("[1, 2]");
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
        }
        [TestMethod]
        public void AJson_GeneralParsing_JsonArray_QuoteParsingSucceeds ()
        {
            Json json = JsonHelper.ParseText(
@"
[
    ""Test1"",
    ""Test2""
]
");
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.AreEqual(2, ((JsonArray)json.Data).Count);
            Assert.AreEqual("Test1", ((JsonArray)json.Data)[0].StringValue);
            Assert.AreEqual("Test2", ((JsonArray)json.Data)[1].StringValue);

        }

        [TestMethod]
        public void AJson_GeneralParsing_JsonArray_ArrayOfArray ()
        {
            string jsonText = "[ [3] ]";

            var json = JsonHelper.ParseText(jsonText);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));

            var parsedArr = json.Data as JsonArray;
            Assert.AreEqual(1, parsedArr.Count);
            Assert.IsTrue(parsedArr[0].IsArray);

            var innerArray = parsedArr[0] as JsonArray;
            Assert.AreEqual(1, innerArray.Count);
            Assert.IsTrue(innerArray[0].IsValue);
            Assert.AreEqual("3", innerArray[0].StringValue);
        }

        [TestMethod]
        public void AJson_GeneralParsing_JsonArray_ArrayOfArrayMulti ()
        {
            string jsonText = "[ [3, 4] ]";

            var json = JsonHelper.ParseText(jsonText);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));

            var parsedArr = json.Data as JsonArray;
            Assert.AreEqual(1, parsedArr.Count);

            Assert.IsTrue(parsedArr[0].IsArray);
            var innerArray = parsedArr[0] as JsonArray;
            Assert.AreEqual(2, innerArray.Count, "\nActual JSon Array: " + innerArray.ToString());

            Assert.IsTrue(innerArray[0].IsValue);
            Assert.AreEqual("3", innerArray[0].StringValue);
            Assert.IsTrue(innerArray[1].IsValue);
            Assert.AreEqual("4", innerArray[1].StringValue);
        }

        [TestMethod]
        public void AJson_GeneralParsing_JsonArray_ArrayOfArrayMultiAndMultiInnerArray ()
        {
            string jsonText = "[ [3, 4], [5] ]";

            var json = JsonHelper.ParseText(jsonText);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));

            var parsedArr = json.Data as JsonArray;
            Assert.AreEqual(2, parsedArr.Count);

            Assert.IsTrue(parsedArr[0].IsArray);
            var firstInnerArray = parsedArr[0] as JsonArray;
            Assert.AreEqual(2, firstInnerArray.Count);
            Assert.IsTrue(firstInnerArray[0].IsValue);
            Assert.AreEqual("3", firstInnerArray[0].StringValue);

            Assert.IsTrue(firstInnerArray[1].IsValue);
            Assert.AreEqual("4", firstInnerArray[1].StringValue);

            Assert.IsTrue(parsedArr[1].IsArray);
            var secondInnnerArray = parsedArr[1] as JsonArray;
            Assert.AreEqual(1, secondInnnerArray.Count);
            Assert.IsTrue(secondInnnerArray[0].IsValue);
            Assert.AreEqual("5", secondInnnerArray[0].StringValue);
        }


        [TestMethod]
        public void AJson_GeneralParsing_NewlineAfterColon_ParseSucceeds ()
        {
            Json json = JsonHelper.ParseText(
@"
{
    ""Test1"" : 
    [
        2, 3, 4
    ]
}
");
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
        }

        [TestMethod]
        public void AJson_GeneralParsing_EmptyStringTests_CanParseEmptyDoubleQuoteString ()
        {
            Json json = JsonHelper.ParseText(
@"
{
    ""Test"" : """",
    dude : 3
}
");
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsTrue(json.Data.IsDocument);
            Assert.AreEqual("", ((JsonDocument)json.Data).ValueFor("Test").StringValue);
        }

        [TestMethod]
        public void AJson_GeneralParsing_FileParsing_CanParseBasicFile ()
        {
            string jsonText = ResourceFetcher.GetText("_TestData/Basic.json");
            Json json = JsonHelper.ParseText(jsonText);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));

            JsonDocument root = json.Data as JsonDocument;
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.Count);

            JsonDocument first = root.ValueAt(0) as JsonDocument;
            Assert.IsNotNull(first);
            Assert.AreEqual(1, first.Count);
            Assert.AreEqual("OuterThing", root.KeyAt(0));

            JsonArray arr = first.ValueAt(0) as JsonArray;
            Assert.IsNotNull(arr);
            Assert.AreEqual(2, arr.Count);

            Assert.AreEqual("Item 1", arr[0]);
            Assert.AreEqual("Item 2", arr[1]);

            json.AssertSourceIsValid();
        }

        [TestMethod]
        public void AJson_GeneralParsing_FileParsing_CanParseLargeFile ()
        {
            // Currently 771,347 characters - 81,312 words - 38,306 lines
            string jsonText = ResourceFetcher.GetText("_TestData/Large.json");

            Json json = JsonHelper.ParseText(jsonText);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));

            JsonArray arr = json.Data as JsonArray;
            Assert.IsNotNull(arr);
            Assert.AreEqual(6384, arr.Count);

            json.AssertSourceIsValid();
        }

        [TestMethod]
        public void AJson_GeneralParsing_FileParsing_CanParseComplexFile ()
        {
            string jsonText = ResourceFetcher.GetText("_TestData/Complex.json");

            var json = JsonHelper.ParseText(jsonText);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));

            var parsedDoc = json.Data as JsonDocument;


            Assert.AreEqual(1, parsedDoc.Count);

            var glossEntry = parsedDoc.FindValueByKey("GlossEntry");
            Assert.IsNotNull(glossEntry);
            Assert.AreEqual(7, ((JsonDocument)glossEntry).Count);

            var paraEntry = parsedDoc.FindValueByKey("para");
            Assert.IsNotNull(glossEntry);
            Assert.AreEqual("A meta-markup language, used to create markup languages such as DocBook.", paraEntry.StringValue);

            json.AssertSourceIsValid();
        }

        public void AJson_GeneralParsing_JsonArry_NotConfusedByLeadingTrailingExtraChars ()
        {
            var json = JsonHelper.ParseText(" [0, 1, 2] ");
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));

            Assert.IsTrue(json.Data.IsArray);

            JsonArray array = (JsonArray)json.Data;
            Assert.AreEqual(3, array.Count);
            Assert.AreEqual("0", array[0]);
            Assert.AreEqual("1", array[1]);
            Assert.AreEqual("2", array[2]);
        }

        private void TestSpecialCharacter_FromString (char specialCharacter)
        {
            Logger.LogInfo($"Testing special character: '{specialCharacter}' inside document, parsing from string");
            string jsonText = "{ \"Text\": \"" + specialCharacter + "\"}";

            Json result = JsonHelper.ParseText(jsonText);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.HasErrors, "Json parse errors:\n" + String.Join("\n\t", result.Errors));
            Assert.IsTrue(result.Data.IsDocument);

            JsonDocument jsonDoc = (JsonDocument)result.Data;
            Assert.AreEqual(1, jsonDoc.Count);

            Assert.AreEqual("Text", jsonDoc.KeyAt(0).StringValue);
            Assert.AreEqual(specialCharacter.ToString(), jsonDoc.ValueAt(0).StringValue);
        }

        [TestMethod]
        public void AJson_GeneralParsing_NotConfusedBySpecialCharsInValueString_FromString_ParsesCorectly ()
        {
            TestSpecialCharacter_FromString('{');
            TestSpecialCharacter_FromString('}');
            TestSpecialCharacter_FromString('[');
            TestSpecialCharacter_FromString(']');
            TestSpecialCharacter_FromString(',');
        }

        [TestMethod]
        public void AJson_GeneralParsing_JsonDocument_DocumentKeysWithNoQuotes_TrimCorrectly___FromString ()
        {
            Json json = JsonHelper.ParseText("{   Text: 1 }");
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsTrue(json.Data.IsDocument);

            JsonDocument jsonDoc = (JsonDocument)json.Data;
            Assert.AreEqual(1, jsonDoc.Count);

            Assert.AreEqual("Text", jsonDoc.KeyAt(0));
            Assert.AreEqual("1", jsonDoc.ValueAt(0));
        }

        [TestMethod]
        public void AJson_GeneralParsing_JsonDocument_DocumentKeysWithNoQuotes_TrimCorrectly___FromBuilder ()
        {
            Json json = JsonHelper.MakeRootBuilder()
                            .StartDocument()
                                .AddProperty(" Text", 1)
                            .Finalize();

            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsTrue(json.Data.IsDocument);

            JsonDocument jsonDoc = (JsonDocument)json.Data;
            Assert.AreEqual(1, jsonDoc.Count);

            Assert.AreEqual(" Text", jsonDoc.KeyAt(0));
            Assert.AreEqual("1", jsonDoc.ValueAt(0));
        }

        enum eTest { Sweet, Nuggets };

        [TestMethod]
        public void AJson_GeneralParsing_JsonValue_EnumValuesCanBeWritten__FromBuilder ()
        {
            Json json = JsonHelper.MakeRootBuilder()
                            .StartArray()
                                .AddArrayItem(eTest.Sweet)
                                .AddArrayItem(eTest.Nuggets)
                            .Finalize();

            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsTrue(json.Data.IsArray);

            JsonArray arr = (JsonArray)json.Data;
            Assert.AreEqual(2, arr.Count);

            Assert.AreEqual(eTest.Sweet.ToString(), arr[0]);
            Assert.AreEqual(eTest.Nuggets.ToString(), arr[1]);
        }

        class Test
        {
            public eTest Item { get; set; }
        }

        [TestMethod]
        public void AJson_GeneralParsing_JsonValue_EnumValuesCanBeRead ()
        {
            Json json = JsonHelper.ParseText("{ \"Item\": Sweet }");

            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsTrue(json.Data.IsDocument);

            Test t = JsonHelper.BuildObjectForJson<Test>(json);

            Assert.IsNotNull(t);

            Assert.AreEqual(eTest.Sweet, t.Item);
        }

        [TestMethod]
        public void AJson_ErrorHandling_NullFileInput_DoesntThrow ()
        {
            Json json = JsonHelper.ParseFile((string)null);
            Assert.IsNotNull(json);
            Assert.IsTrue(json.HasErrors);
        }

        [TestMethod]
        public void AJson_ErrorHandling_InvalidPathProvided_DoesntThrow ()
        {
            Json json = JsonHelper.ParseFile("invalid|path<>chars*");
            Assert.IsNotNull(json);
            Assert.IsTrue(json.HasErrors);
        }

        [TestMethod]
        public void AJson_ErrorHandling_NullTextInput_DoesntThrow ()
        {
            Json json = JsonHelper.ParseText((string)null);
            Assert.IsTrue(json.HasErrors);
        }

        [TestMethod]
        public void AJson_ErrorHandling_InvalidTextInput_DoesntThrow ()
        {
            Json json = JsonHelper.ParseText(" { test ]");
            Assert.IsTrue(json.HasErrors);
        }


        [TestMethod]
        public void AJson_JsonHelper_ParseJsonForDictionary ()
        {
            var d = new Dictionary<string, string>();
            d.Add("Test1", "V1");
            d.Add("Test2", "V2");
            d.Add("Test3", "V3");
            Json json = JsonHelper.BuildJsonForObject(d);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));


            var parsed = JsonHelper.BuildObjectForJson<Dictionary<string, string>>(json);
            Assert.IsNotNull(parsed);
            Assert.AreEqual(d.Count, parsed.Count);
            Assert.AreEqual(d["Test1"], parsed["Test1"]);
            Assert.AreEqual(d["Test2"], parsed["Test2"]);
            Assert.AreEqual(d["Test3"], parsed["Test3"]);
        }

        [TestMethod]
        public void AJson_JsonParsing_ParseKeyWithQuote ()
        {
            const string key = "ke\\\"y";
            const string value = "value with no quotes";
            string jsonText = $"{{\"{key}\": \"{value}\"}}";
            Json json = JsonHelper.ParseText(jsonText);
            Assert.IsTrue(json, json.GetErrorReport());
            Assert.IsTrue(json.Data.IsDocument);

            var doc = (JsonDocument)json.Data;
            Assert.IsTrue(doc.ContainsKey(key));
            Assert.AreEqual(value, doc.ValueFor(key).StringValue);
        }

        [TestMethod]
        public void AJson_JsonParsing_ParseValueWithQuote ()
        {
            const string key = "key";
            const string value = "value with a quote → \\\" ← there";
            string jsonText = $"{{\"{key}\": \"{value}\"}}";
            Json json = JsonHelper.ParseText(jsonText);
            Assert.IsTrue(json, json.GetErrorReport());
            Assert.IsTrue(json.Data.IsDocument);

            var doc = (JsonDocument)json.Data;
            Assert.IsTrue(doc.ContainsKey(key));
            Assert.AreEqual(value, doc.ValueFor(key).StringValue);
        }

        [TestMethod]
        public void AJson_JsonParsing_CanParseJsonWithTimezone ()
        {
            const string key = "timezone";
            TimeZoneInfo value = TimeZoneInfo.Local;
            string jsonText = $"{{\"{key}\": \"{value.Id}\"}}";

            Json json = JsonHelper.ParseText(jsonText);
            Assert.IsTrue(json, json.GetErrorReport());
            Assert.IsTrue(json.Data.IsDocument);

            var doc = (JsonDocument)json.Data;
            Assert.IsTrue(doc.ContainsKey(key));
            Assert.IsTrue(doc.TryGetValue(key, out TimeZoneInfo parsedValue));
            Assert.AreEqual(value, parsedValue);
        }
    }
}
