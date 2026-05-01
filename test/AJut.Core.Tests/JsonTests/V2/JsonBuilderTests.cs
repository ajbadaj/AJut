namespace AJut.Core.UnitTests.AJsonV2
{
    using System;
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class V2JsonBuilderTests
    {
        [TestMethod]
        public void Builder_BuildSimpleDocument_RoundTrips ()
        {
            Json json = JsonHelper
                .MakeRootBuilder()
                .StartDocument()
                    .AddProperty("name", "AJ")
                    .AddProperty("count", 42)
                .End()
                .Finalize();

            Assert.IsFalse(json.HasErrors);
            JsonDocument doc = (JsonDocument)json.Data;
            Assert.AreEqual("AJ", doc.ValueFor("name").StringValue);
            Assert.AreEqual("42", doc.ValueFor("count").StringValue);
        }

        [TestMethod]
        public void Doc_AppendNew_AddsEntry ()
        {
            JsonDocument doc = new JsonDocument();
            doc.AppendNew("a", 1);
            doc.AppendNew("b", "two");

            Assert.AreEqual(2, doc.Count);
            Assert.AreEqual("1", doc.ValueFor("a").StringValue);
            Assert.AreEqual("two", doc.ValueFor("b").StringValue);
        }

        [TestMethod]
        public void Doc_AppendNew_DuplicatesAreAppended ()
        {
            JsonDocument doc = new JsonDocument();
            doc.AppendNew("k", 1);
            doc.AppendNew("k", 2);

            Assert.AreEqual(2, doc.Count);
            Assert.AreEqual(2, doc.AllValuesForKey("k").Length);
        }

        [TestMethod]
        public void Doc_Set_UpsertsExistingKey ()
        {
            JsonDocument doc = new JsonDocument();
            doc.AppendNew("k", 1);
            doc.Set("k", 99);

            Assert.AreEqual(1, doc.Count);
            Assert.AreEqual("99", doc.ValueFor("k").StringValue);
        }

        [TestMethod]
        public void Doc_Set_AppendsWhenKeyMissing ()
        {
            JsonDocument doc = new JsonDocument();
            doc.Set("brand-new", "value");

            Assert.AreEqual(1, doc.Count);
            Assert.AreEqual("value", doc.ValueFor("brand-new").StringValue);
        }

        [TestMethod]
        public void Doc_AppendNew_RoundTripsAfterReserialize ()
        {
            Json json = JsonHelper.ParseText("{ a: 1 }");
            Assert.IsFalse(json.HasErrors);
            JsonDocument doc = (JsonDocument)json.Data;
            doc.AppendNew("b", 2);

            string serialized = json.ToString();
            Json reparsed = JsonHelper.ParseText(serialized);
            Assert.IsFalse(reparsed.HasErrors, "Reparse errors:\n  " + String.Join("\n  ", reparsed.Errors));

            JsonDocument round = (JsonDocument)reparsed.Data;
            Assert.AreEqual("1", round.ValueFor("a").StringValue);
            Assert.AreEqual("2", round.ValueFor("b").StringValue);
        }

        [TestMethod]
        public void Array_AppendNew_RoundTrips ()
        {
            JsonArray arr = new JsonArray();
            arr.AppendNew(1);
            arr.AppendNew("two");
            arr.AppendNew(3.5);

            Assert.AreEqual(3, arr.Count);

            string serialized = arr.ToString();
            Json reparsed = JsonHelper.ParseText(serialized);
            Assert.IsFalse(reparsed.HasErrors, "Reparse errors:\n  " + String.Join("\n  ", reparsed.Errors));

            JsonArray round = (JsonArray)reparsed.Data;
            Assert.AreEqual(3, round.Count);
        }
    }
}
