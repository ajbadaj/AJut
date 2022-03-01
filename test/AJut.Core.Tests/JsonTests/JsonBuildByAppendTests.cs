namespace AJut.Core.UnitTests.AJson
{
    using System;
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class JsonBuildByAppendTests
    {
        [TestMethod]
        public void AJson_JsonBuilder_JsonDocument_AppendNew_SimpleDoc_Succeeds()
        {
            Json json = JsonHelper.MakeRootBuilder()
                            .StartDocument()
                                .AddProperty("Sweet", "niblets")
                                .StartProperty("DocBase")
                                    .StartDocument()
                                        .AddProperty("DocChild", 2)
                            .Finalize();

            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            json.AssertSourceIsValid();

            var documentBase = json.Data as JsonDocument;
            Assert.IsNotNull(documentBase);

            var document = documentBase.FindValueByKey("DocBase") as JsonDocument;
            Assert.IsNotNull(document);

            JsonValue output = document.AppendNew("Sweeter", "niblets 2");
            Assert.IsNotNull(output);

            Assert.AreEqual("niblets 2", output.StringValue);
            Assert.IsTrue(json.TextTracking.HasChanges);

            Json reparsedBeans = JsonHelper.ParseText(json.Data.StringValue);
            Assert.IsNotNull(reparsedBeans);
            Assert.IsFalse(reparsedBeans.HasErrors, reparsedBeans.BuildJsonErrorReport());
            reparsedBeans.AssertSourceIsValid();

            JsonValue output2 = ((JsonDocument)reparsedBeans.Data).FindValueByKey("Sweeter");
            Assert.IsNotNull(output2);
            Assert.AreEqual("niblets 2", output2.StringValue);
        }

        [TestMethod]
        public void AJson_JsonBuilder_JsonArray_AppendNew_AddSimpleValue_Succeeds()
        {
            Json json = JsonHelper.MakeRootBuilder()
                            .StartDocument()
                                .AddProperty("Sweet", "niblets")
                                .StartProperty("DocBase")
                                    .StartDocument()
                                        .StartProperty("Arr")
                                            .StartArray()
                                                .AddArrayItem(4)
                            .Finalize();

            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            json.AssertSourceIsValid();

            var documentBase = json.Data as JsonDocument;
            Assert.IsNotNull(documentBase);

            var array = documentBase.FindValueByKey("Arr") as JsonArray;
            Assert.IsNotNull(array);

            JsonValue output = array.AppendNew("AppendTest");
            Assert.IsNotNull(output);

            Assert.AreEqual("AppendTest", output.StringValue);
            Assert.IsTrue(json.TextTracking.HasChanges);

            Json reparsedBeans = JsonHelper.ParseText(json.Data.StringValue);
            Assert.IsNotNull(reparsedBeans);
            Assert.IsFalse(reparsedBeans.HasErrors);
            reparsedBeans.AssertSourceIsValid();

            JsonArray finalTranslatedOutput = ((JsonDocument)reparsedBeans.Data).FindValueByKey("Arr") as JsonArray;
            Assert.IsNotNull(finalTranslatedOutput);
            Assert.AreEqual(2, finalTranslatedOutput.Count);
            Assert.AreEqual("4", finalTranslatedOutput[0].StringValue);
            Assert.AreEqual("AppendTest", finalTranslatedOutput[1].StringValue);
        }
        
        [TestMethod]
        public void AJson_JsonBuilder_JsonDocument_AppendNew_SimpleArray_Succeeds()
        {
            Json json = JsonHelper.MakeRootBuilder()
                            .StartDocument()
                                .AddProperty("Sweet", "niblets")
                            .Finalize();

            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            json.AssertSourceIsValid();

            var document = json.Data as JsonDocument;
            Assert.IsNotNull(document);

            JsonValue output = document.AppendNew("Sweeter", "niblets 2");
            Assert.IsNotNull(output);

            Assert.AreEqual("niblets 2", output.StringValue);

            Json reparsedBeans = JsonHelper.ParseText(json.Data.StringValue);
            Assert.IsNotNull(reparsedBeans);
            Assert.IsFalse(reparsedBeans.HasErrors);
            reparsedBeans.AssertSourceIsValid();
        }

        [TestMethod]
        public void AJson_JsonBuilder_JsonDocument_AppendNew_AdvancedSource_Succeeds()
        {
            Json json = JsonHelper.MakeRootBuilder()
                            .StartDocument()
                                .AddProperty("Sweet", "niblets")
                                .StartProperty("SubDoc")
                                    .StartDocument()
                                        .AddProperty("SubItem", "dang son!")
                                    .End()
                                .StartProperty("Yarr")
                                    .StartArray()
                                        .AddArrayItem(2)
                                        .AddArrayItem(3)
                                        .AddArrayItem(6)
                            .Finalize();

            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            json.AssertSourceIsValid();

            var rootDoc = json.Data as JsonDocument;
            Assert.IsNotNull(rootDoc);

            JsonArray array = rootDoc.FindValueByKey("Yarr") as JsonArray;
            Assert.IsNotNull(array);

            JsonDocument targetDoc = rootDoc.FindValueByKey("SubDoc") as JsonDocument;
            Assert.IsNotNull(targetDoc);

            JsonValue output = targetDoc.AppendNew("Sweeter", "niblets 2");
            Assert.IsNotNull(output);

            Assert.AreEqual("niblets 2", output.StringValue);

            Json reparsedBeans = JsonHelper.ParseText(json.Data.StringValue);
            Assert.IsNotNull(reparsedBeans);
            Assert.IsFalse(reparsedBeans.HasErrors);

            reparsedBeans.AssertSourceIsValid();
        }

        [TestMethod]
        public void AJson_JsonBuilder_JsonDocument_AppendNew_AdvancedSource_AdvancedNewItem_Succeeds()
        {
            Json json = JsonHelper.MakeRootBuilder()
                            .StartDocument()
                                .AddProperty("Sweet", "niblets")
                                .StartProperty("SubDoc")
                                    .StartDocument()
                                        .AddProperty("SubItem", "dang son!")
                                    .End()
                                .StartProperty("Yarr")
                                    .StartArray()
                                        .AddArrayItem(2)
                                        .AddArrayItem(3)
                                        .AddArrayItem(6)
                                    .End()
                            .Finalize();

            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            json.AssertSourceIsValid();

            var rootDoc = json.Data as JsonDocument;
            Assert.IsNotNull(rootDoc);

            JsonArray array = rootDoc.FindValueByKey("Yarr") as JsonArray;
            Assert.IsNotNull(array);

            JsonDocument targetDoc = rootDoc.FindValueByKey("SubDoc") as JsonDocument;
            Assert.IsNotNull(targetDoc);

            JsonValue output = targetDoc.AppendNew("Sweeter", JsonHelper.MakeRootBuilder()
                                                                .StartDocument()
                                                                    .AddProperty("catchPhrase", "Doood"));
            Assert.IsNotNull(output);

            JsonDocument outputCasted = output as JsonDocument;
            Assert.IsNotNull(outputCasted);

            JsonValue catchPhrase = outputCasted.FindValueByKey("catchPhrase");
            Assert.IsNotNull(catchPhrase);
            Assert.AreEqual("Doood", catchPhrase.StringValue);

            Assert.AreEqual(catchPhrase, rootDoc.FindValueByKey("catchPhrase"));


            Json reparsedBeans = JsonHelper.ParseText(json.Data.StringValue);
            Assert.IsNotNull(reparsedBeans);
            Assert.IsFalse(reparsedBeans.HasErrors);

            reparsedBeans.AssertSourceIsValid();
        }
    }
}
