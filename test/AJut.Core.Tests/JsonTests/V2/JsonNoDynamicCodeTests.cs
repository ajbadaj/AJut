namespace AJut.Core.UnitTests.AJsonV2
{
    using System.Collections.Generic;
    using AJut.Text.AJson;
    using AJut.TypeManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Where dynamic code is not supported (an iOS build compiled ahead of time), AJson reads and writes a type's members
    /// only through its generated serializer, and reports a type without one. These run that platform by simulating it on
    /// the test's own thread; the test runner itself always has a JIT.
    /// </summary>
    [TestClass]
    public class JsonNoDynamicCodeTests
    {
        // ===========================[ Test Models ]===========================
        public class PlainPoco
        {
            public string Name { get; set; }
            public int Count { get; set; }
        }

        public class PlainNullable
        {
            public int? Maybe { get; set; }
        }

        // ===========================[ Tests ]===========================
        [TestMethod]
        public void NoDynamicCode_PlainTypeRead_FailsNamingTheGenerator ()
        {
            string text = JsonHelper.BuildJsonForObject(new PlainPoco { Name = "Kept", Count = 3 }).ToString();
            using (DynamicCodeAvailability.SimulateUnsupported())
            {
                Json reparsed = JsonHelper.ParseText(text);
                PlainPoco read = JsonHelper.BuildObjectForJson<PlainPoco>(reparsed);

                Assert.IsNull(read);
                Assert.IsTrue(reparsed.HasErrors);
                StringAssert.Contains(reparsed.GetErrorReport(), "[OptimizeAJson]");
                StringAssert.Contains(reparsed.GetErrorReport(), nameof(PlainPoco));
            }
        }

        [TestMethod]
        public void NoDynamicCode_PlainTypeWrite_WritesNoneOfItsMembers ()
        {
            using (DynamicCodeAvailability.SimulateUnsupported())
            {
                string text = JsonHelper.BuildJsonForObject(new PlainPoco { Name = "Kept", Count = 3 }).ToString();
                Assert.IsFalse(text.Contains("Kept"), text);
            }
        }

        [TestMethod]
        public void NoDynamicCode_GeneratedTypeWithCollectionsStillRoundTrips ()
        {
            var source = new JsonSourceGenTests.CollectionsGen
            {
                Numbers = new List<int> { 1, 2, 3 },
                Tags = new[] { "a", "b" },
            };

            using (DynamicCodeAvailability.SimulateUnsupported())
            {
                Json json = JsonHelper.BuildJsonForObject(source);
                Assert.IsFalse(json.HasErrors, json.GetErrorReport());

                Json reparsed = JsonHelper.ParseText(json.ToString());
                var round = JsonHelper.BuildObjectForJson<JsonSourceGenTests.CollectionsGen>(reparsed);
                Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport());
                CollectionAssert.AreEqual(source.Numbers, round.Numbers);
                CollectionAssert.AreEqual(source.Tags, round.Tags);
            }
        }

        [TestMethod]
        public void WithJit_PlainTypeStillRoundTripsByReflection ()
        {
            Json reparsed = JsonHelper.ParseText(JsonHelper.BuildJsonForObject(new PlainPoco { Name = "Kept", Count = 3 }).ToString());
            PlainPoco read = JsonHelper.BuildObjectForJson<PlainPoco>(reparsed);

            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport());
            Assert.AreEqual("Kept", read.Name);
            Assert.AreEqual(3, read.Count);
        }

        [TestMethod]
        public void NullableMember_ReadsItsValueOrNull ()
        {
            Json withValue = JsonHelper.ParseText(JsonHelper.BuildJsonForObject(new PlainNullable { Maybe = 5 }).ToString());
            Assert.AreEqual(5, JsonHelper.BuildObjectForJson<PlainNullable>(withValue).Maybe);

            Json withNull = JsonHelper.ParseText(JsonHelper.BuildJsonForObject(new PlainNullable { Maybe = null }).ToString());
            Assert.IsNull(JsonHelper.BuildObjectForJson<PlainNullable>(withNull).Maybe);
        }
    }
}
