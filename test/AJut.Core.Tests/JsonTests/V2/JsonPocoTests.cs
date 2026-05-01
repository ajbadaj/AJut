namespace AJut.Core.UnitTests.AJsonV2
{
    using System;
    using System.Collections.Generic;
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class JsonPocoTests
    {
        // ===============================[ Test Models ]===========================
        public class SimplePoco
        {
            public string Name { get; set; }
            public int Count { get; set; }
            public bool Active { get; set; }
            public double Ratio { get; set; }
        }

        public class NestedPoco
        {
            public string Label { get; set; }
            public SimplePoco Inner { get; set; }
            public List<int> Numbers { get; set; }
            public string[] Tags { get; set; }
        }

        public class DictPoco
        {
            public Dictionary<string, int> Counts { get; set; }
        }

        public enum eFlavor { Vanilla, Chocolate, Strawberry }

        public class EnumPoco
        {
            public eFlavor Flavor { get; set; }
        }

        // ===============================[ Tests ]===========================
        [TestMethod]
        public void Poco_Simple_RoundTrip ()
        {
            SimplePoco source = new SimplePoco { Name = "AJ", Count = 42, Active = true, Ratio = 1.5 };
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors);

            string serialized = json.ToString();
            Json reparsed = JsonHelper.ParseText(serialized);
            Assert.IsFalse(reparsed.HasErrors, "Reparse errors:\n  " + String.Join("\n  ", reparsed.Errors));

            SimplePoco round = JsonHelper.BuildObjectForJson<SimplePoco>(reparsed);
            Assert.AreEqual(source.Name, round.Name);
            Assert.AreEqual(source.Count, round.Count);
            Assert.AreEqual(source.Active, round.Active);
            Assert.AreEqual(source.Ratio, round.Ratio);
        }

        [TestMethod]
        public void Poco_Nested_RoundTrip ()
        {
            NestedPoco source = new NestedPoco
            {
                Label = "Top",
                Inner = new SimplePoco { Name = "Child", Count = 7, Active = false, Ratio = 0.5 },
                Numbers = new List<int> { 1, 2, 3 },
                Tags = new[] { "x", "y" },
            };

            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors);

            string serialized = json.ToString();
            Json reparsed = JsonHelper.ParseText(serialized);
            Assert.IsFalse(reparsed.HasErrors);

            NestedPoco round = JsonHelper.BuildObjectForJson<NestedPoco>(reparsed);
            Assert.AreEqual(source.Label, round.Label);
            Assert.IsNotNull(round.Inner);
            Assert.AreEqual(source.Inner.Name, round.Inner.Name);
            Assert.AreEqual(source.Inner.Count, round.Inner.Count);
            CollectionAssert.AreEqual(source.Numbers, round.Numbers);
            CollectionAssert.AreEqual(source.Tags, round.Tags);
        }

        [TestMethod]
        public void Poco_Dictionary_RoundTrip ()
        {
            DictPoco source = new DictPoco
            {
                Counts = new Dictionary<string, int> { { "a", 1 }, { "b", 2 }, { "c", 3 } },
            };

            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors);

            string serialized = json.ToString();
            Json reparsed = JsonHelper.ParseText(serialized);
            Assert.IsFalse(reparsed.HasErrors, "Reparse errors:\n  " + String.Join("\n  ", reparsed.Errors));

            DictPoco round = JsonHelper.BuildObjectForJson<DictPoco>(reparsed);
            Assert.IsNotNull(round.Counts);
            Assert.AreEqual(3, round.Counts.Count);
            Assert.AreEqual(1, round.Counts["a"]);
            Assert.AreEqual(2, round.Counts["b"]);
            Assert.AreEqual(3, round.Counts["c"]);
        }

        [TestMethod]
        public void Poco_Enum_RoundTrip ()
        {
            EnumPoco source = new EnumPoco { Flavor = eFlavor.Strawberry };
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors);

            string serialized = json.ToString();
            Json reparsed = JsonHelper.ParseText(serialized);
            Assert.IsFalse(reparsed.HasErrors);

            EnumPoco round = JsonHelper.BuildObjectForJson<EnumPoco>(reparsed);
            Assert.AreEqual(eFlavor.Strawberry, round.Flavor);
        }

        [TestMethod]
        public void Poco_NullProperty_OmittedFromOutput ()
        {
            SimplePoco source = new SimplePoco { Name = null, Count = 5, Active = false, Ratio = 0 };
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors);

            JsonDocument doc = (JsonDocument)json.Data;
            Assert.IsFalse(doc.ContainsKey("Name"));
            Assert.IsTrue(doc.ContainsKey("Count"));
        }
    }
}
