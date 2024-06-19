namespace AJut.Core.UnitTests.AJson
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using AJut.IO;
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class JsonParserTests_FailureRegressions
    {
        private TestContext testContextInstance;
        /// <summary>
        /// Gets or sets the test context which provides
        /// information about and functionality for the current test run.
        /// </summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        [TestMethod]
        public void AJson_ExternalFailure_6_1_17()
        {
            Json json = JsonForEmbeddedResource("_TestData/Failure_6-1-17.json");

            this.testContextInstance.WriteLine("Json error text:");
            this.testContextInstance.WriteLine(String.Join("\n > ", json.Errors));
            Assert.IsTrue(json.HasErrors == false);

        }

        [TestMethod]
        public void AJson_ExternalFailure_2_17_20()
        {
            Json json = JsonForEmbeddedResource("_TestData/Failure_2-17-20.json");
            Assert.IsFalse(json.HasErrors, String.Join("\n >", json.Errors));
        }
        
        [TestMethod]
        public void AJson_ExternalFailure_5_26_20()
        {
            Json json = JsonForEmbeddedResource("_TestData/Failure_5-26-20.json");
            Assert.IsFalse(json.HasErrors, String.Join("\n >", json.Errors));
        }

        [TestMethod]
        public void AJson_DictionaryArrayFailure()
        {
            Dictionary<string, int>[] da = new Dictionary<string, int>[2];
            da[0] = new Dictionary<string, int> { { "Test0", 10 } };

            Json json = JsonHelper.BuildJsonForObject(da);
            Assert.IsFalse(json.HasErrors, json.BuildJsonErrorReport());
        }

        private static Json JsonForEmbeddedResource(string resourcePath)
        {
            string jsonText;
            using (var stream = FileHelpers.GetEmbeddedResourceStream(resourcePath))
            {
                using (var streamReader = new StreamReader(stream))
                {
                    Assert.IsNotNull(stream);
                    jsonText = streamReader.ReadToEnd();
                }
            }

            return JsonHelper.ParseText(jsonText);
        }

        public class ItemRoot
        {
            public Guid Id { get; set; }
        }
        public class Item
        {
            public Guid Id { get; set; }
            public string Text { get; set; }
        }

        [TestMethod]
        public void AJson_Failure_7_8_18()
        {
            string test =
@"{
    Writing: {
        Id: """ + Guid.NewGuid().ToString() + @""",
        Chapters: [
            Id: """ + Guid.NewGuid().ToString() + @""",
            Paragraphs:  [
                {
                    Id: """ + Guid.NewGuid().ToString() + @""",
                    Elements: [
                        {
                            __type: """ + typeof(ItemRoot).AssemblyQualifiedName + @""",
                            Id: """ + Guid.NewGuid().ToString() + @""",
                            Elements: [
                                {
                                    __type: """ + typeof(Item).AssemblyQualifiedName + @""",
                                    Id: """ + Guid.NewGuid().ToString() + @""",
                                    Text: ""This"",
                                },
                                {
                                    __type: """ + typeof(Item).AssemblyQualifiedName + @""",
                                    Id: """ + Guid.NewGuid().ToString() + @""",
                                    Text: "" "",
                                },
                                {
                                    __type: """ + typeof(Item).AssemblyQualifiedName + @""",
                                    Id: """ + Guid.NewGuid().ToString() + @""",
                                    Text: ""Sentence"",
                                },
                                {
                                    __type: """ + typeof(Item).AssemblyQualifiedName + @""",
                                    Id: """ + Guid.NewGuid().ToString() + @""",
                                    Text: "" "",
                                },
                                {
                                    __type: """ + typeof(Item).AssemblyQualifiedName + @""",
                                    Id: """ + Guid.NewGuid().ToString() + @""",
                                    Text: ""Rox"",
                                },
                                {
                                    __type: """ + typeof(Item).AssemblyQualifiedName + @""",
                                    Id: """ + Guid.NewGuid().ToString() + @""",
                                    Text: ""!"",
                                }
                            ]
                        }
                    ]
				}
			]
		]
    }
}";
            var json = JsonHelper.ParseText(test);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
        }

        [TestMethod]
        public void AJson_Failure_7_11_18()
        {
            string jsonText = "{ \"Text\": [}";
            Json json = JsonHelper.ParseText(jsonText);

            Assert.IsTrue(json.HasErrors);
        }

        /// <summary>
        /// Issue: Rather than parsing properly, there was some reliance on a JsonHelper.Trim method that would trim away
        /// spaces and other chars left behind, which could very well destroy user data, as it does in this case.
        /// 
        /// Root of the problem appears to be not taking into account quotes, two quotes makes it two spaces off, there needs
        /// to be some kind of:
        /// 
        /// parse found: "thing"   // offsets do not get determined from here
        /// parse uses: thing      // offsets get determiend from here
        /// 
        /// </summary>
        [TestMethod]
        public void AJson_Failure_7_13_18__from_string()
        {
            string jsonText = "{ \"Text\": \"   \"}";
            //                               ↑↑↑ <- Three spaces

            Json result = JsonHelper.ParseText(jsonText);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.HasErrors, "Json parse errors:\n" + String.Join("\n\t", result.Errors));
            Assert.IsTrue(result.Data.IsDocument);

            JsonDocument jsonDoc = (JsonDocument)result.Data;
            Assert.AreEqual(1, jsonDoc.Count);

            Assert.AreEqual("Text", jsonDoc.KeyAt(0).StringValue);
            Assert.AreEqual("   ", jsonDoc.ValueAt(0).StringValue);
            //               ↑↑↑ <- Three spaces
        }

        [TestMethod]
        public void AJson_Failure_7_13_18__from_builder()
        {
            Json json = JsonHelper.MakeRootBuilder()
                            .StartDocument()
                                .AddProperty("Text", "   ")
            //                                        ↑↑↑ <- Three spaces
                            .Finalize();

            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsTrue(json.Data.IsDocument);

            JsonDocument jsonDoc = (JsonDocument)json.Data;
            Assert.AreEqual(1, jsonDoc.Count);

            Assert.AreEqual("Text", jsonDoc.KeyAt(0).StringValue);
            Assert.AreEqual("   ", jsonDoc.ValueAt(0).StringValue);
        }

        [TestMethod]
        public void AJson_NegativeNumberParse_7_18_18__from_string()
        {
            Json json = JsonHelper.ParseText("{ \"Test\": -2 }");

            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsTrue(json.Data.IsDocument);

            JsonDocument jsonDoc = (JsonDocument)json.Data;
            Assert.AreEqual(1, jsonDoc.Count);

            Assert.AreEqual("Test", jsonDoc.KeyAt(0).StringValue);
            Assert.AreEqual("-2", jsonDoc.ValueAt(0).StringValue);
        }

        [TestMethod]
        public void AJson_NegativeNumberParse_7_18_18__from_builder()
        {
            Json json = JsonHelper.MakeRootBuilder()
                            .StartDocument()
                                .AddProperty("Test", "-2")
                            .Finalize();


            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsTrue(json.Data.IsDocument);

            JsonDocument jsonDoc = (JsonDocument)json.Data;
            Assert.AreEqual(1, jsonDoc.Count);

            Assert.AreEqual("Test", jsonDoc.KeyAt(0).StringValue);
            Assert.AreEqual("-2", jsonDoc.ValueAt(0).StringValue);
        }

        [TestMethod]
        public void AJson_NegativeNumberParseAsInt_7_18_18___from_builder()
        {
            Json json = JsonHelper.MakeRootBuilder()
                            .StartDocument()
                                .AddProperty("Test", -2)
                            .Finalize();


            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsTrue(json.Data.IsDocument);

            JsonDocument jsonDoc = (JsonDocument)json.Data;
            Assert.AreEqual(1, jsonDoc.Count);

            Assert.AreEqual("Test", jsonDoc.KeyAt(0).StringValue);
            Assert.AreEqual("-2", jsonDoc.ValueAt(0).StringValue);
        }


        [TestMethod]
        public void AJson_NewlineSeparatingUnquotedLast_JsonDocument_7_20_18__from_string()
        {
            Json json = JsonHelper.ParseText(@"{
                                        Text: ""This"",
                                        Test: Word
                                    }");
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsTrue(json.Data.IsDocument);

            JsonDocument doc = (JsonDocument)json.Data;
            Assert.AreEqual(2, doc.Count);
            Assert.AreEqual("Word", doc.ValueAt(1));
        }

        [TestMethod]
        public void AJson_NewlineSeparatingUnquotedLast_Array_7_20_18__from_string()
        {
            Json json = JsonHelper.ParseText(@"[
                                        ""This"",
                                        Word
                                    ]");
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsTrue(json.Data.IsArray);

            JsonArray arr = (JsonArray)json.Data;
            Assert.AreEqual(2, arr.Count);
            Assert.AreEqual("Word", arr[1]);
        }

        [TestMethod]
        public void AJson_NewlineSeparatingUnquotedLast_JsonDocument_7_20_18__from_builder()
        {
            Json json = JsonHelper.MakeRootBuilder(new JsonBuilder.Settings() { PropertyValueQuoting = ePropertyValueQuoting.NeverQuoteValues })
                            .StartDocument()
                                .AddProperty("Text", "\"This\"")
                                .AddProperty("Test", "Word")
                            .Finalize();

            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsTrue(json.Data.IsDocument);

            JsonDocument doc = (JsonDocument)json.Data;
            Assert.AreEqual(2, doc.Count);
            Assert.AreEqual("Word", doc.ValueAt(1));
        }

        [TestMethod]
        public void AJson_NewlineSeparatingUnquotedLast_Array_7_20_18__from_builder()
        {
            Json json = JsonHelper.MakeRootBuilder(new JsonBuilder.Settings() { PropertyValueQuoting = ePropertyValueQuoting.NeverQuoteValues })
                        .StartArray()
                            .AddArrayItem("\"This\"")
                            .AddArrayItem("Word")
                        .Finalize();
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsTrue(json.Data.IsArray);

            JsonArray arr = (JsonArray)json.Data;
            Assert.AreEqual(2, arr.Count);
            Assert.AreEqual("Word", arr[1]);
        }

        [TestMethod]
        public void AJson_NullableProperty_June_19_2024 ()
        {
            Guid value = Guid.NewGuid();
            var json = JsonHelper.ParseText($"{{ Value: \"{value}\"}}");
            Assert.IsTrue(json, "Failed to do basic json parsing, we're in trouble!");

            NullableGuidPropertyTest testOutput = JsonHelper.BuildObjectForJson<NullableGuidPropertyTest>(json);
            Assert.IsNotNull(testOutput);
            Assert.IsNotNull(testOutput.Value);
            Assert.AreEqual(value, testOutput.Value);
        }

        public class NullableGuidPropertyTest
        {
            public Guid? Value { get; set; }
        }
    }
}
