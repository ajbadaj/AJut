namespace AJut.Core.UnitTests.AJsonV2
{
    using System;
    using System.Collections.Generic;
    using AJut.Text.AJson;
    using AJut.TypeManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Round-trip tests for the source-generator path. The generator runs as part of the build
    /// for this test project (it is wired in via the AJut.Core nupkg's analyzer reference, or
    /// equivalently via the ProjectReference in the source-gen test fixture). When these tests
    /// run, types marked [OptimizeAJson] should hit the dispatch table fast path; types without
    /// stay on reflection.
    /// </summary>
    [TestClass]
    public class JsonSourceGenTests
    {
        // ===========================[ Test Models ]===========================
        [OptimizeAJson]
        public class SimpleGen
        {
            public string Name { get; set; }
            public int Count { get; set; }
            public bool Active { get; set; }
        }

        [OptimizeAJson]
        public class AliasedGen
        {
            [JsonPropertyAlias("friendly-name")]
            public string Name { get; set; }
        }

        public enum eAnchor { Left, Center, Right }

        [OptimizeAJson]
        public class OmitGen
        {
            [JsonOmitIfDefault(eAnchor.Center)]
            public eAnchor Anchor { get; set; } = eAnchor.Center;

            [JsonOmitIfDefault]
            public int Score { get; set; }

            public string Required { get; set; }
        }

        [OptimizeAJson]
        public class IgnoreGen
        {
            public string Visible { get; set; }
            [JsonIgnore] public string Hidden { get; set; }
        }

        [OptimizeAJson]
        public class NestedHostGen
        {
            public string Label { get; set; }
            public SimpleGen Inner { get; set; }
        }

        [OptimizeAJson]
        public class CollectionsGen
        {
            public List<int> Numbers { get; set; }
            public string[] Tags { get; set; }
        }

        // ===========================[ Sanity: dispatch is registered ]===========================
        [TestMethod]
        public void Generated_DispatchIsRegistered_ForOptedInType ()
        {
            // The source generator's [ModuleInitializer] should have populated the dispatch table
            //  by the time any test method runs. This sanity check tells us whether the generator
            //  actually fired during this project's build.
            Assert.IsTrue(AJsonGeneratedDispatch.IsRegistered(typeof(SimpleGen)),
                "Source generator did not register a serializer for SimpleGen - check that the analyzer DLL is loaded.");
        }

        // ===========================[ Round-trips ]===========================
        [TestMethod]
        public void Generated_Simple_RoundTrip ()
        {
            SimpleGen source = new SimpleGen { Name = "AJ", Count = 42, Active = true };
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors);

            string serialized = json.ToString();
            Json reparsed = JsonHelper.ParseText(serialized);
            Assert.IsFalse(reparsed.HasErrors);

            SimpleGen round = JsonHelper.BuildObjectForJson<SimpleGen>(reparsed);
            Assert.AreEqual(source.Name, round.Name);
            Assert.AreEqual(source.Count, round.Count);
            Assert.AreEqual(source.Active, round.Active);
        }

        [TestMethod]
        public void Generated_Alias_RoundTrip ()
        {
            AliasedGen source = new AliasedGen { Name = "value" };
            Json json = JsonHelper.BuildJsonForObject(source);
            string serialized = json.ToString();

            StringAssert.Contains(serialized, "friendly-name");
            Assert.IsFalse(serialized.Contains("\"Name\""), "Aliased property should not appear under the CLR name");

            Json reparsed = JsonHelper.ParseText(serialized);
            AliasedGen round = JsonHelper.BuildObjectForJson<AliasedGen>(reparsed);
            Assert.AreEqual("value", round.Name);
        }

        [TestMethod]
        public void Generated_OmitIfDefault_OmitsExplicitDefault ()
        {
            OmitGen source = new OmitGen { Anchor = eAnchor.Center, Score = 0, Required = "keep me" };
            Json json = JsonHelper.BuildJsonForObject(source);
            string serialized = json.ToString();

            Assert.IsFalse(serialized.Contains("Anchor"), "Anchor at explicit-default should be omitted");
            Assert.IsFalse(serialized.Contains("\"Score\""), "Score at zero-default should be omitted");
            StringAssert.Contains(serialized, "Required");
        }

        [TestMethod]
        public void Generated_OmitIfDefault_KeepsNonDefault ()
        {
            OmitGen source = new OmitGen { Anchor = eAnchor.Right, Score = 5, Required = "x" };
            Json json = JsonHelper.BuildJsonForObject(source);
            string serialized = json.ToString();

            StringAssert.Contains(serialized, "Anchor");
            StringAssert.Contains(serialized, "Score");
        }

        [TestMethod]
        public void Generated_Ignore_OmitsHidden ()
        {
            IgnoreGen source = new IgnoreGen { Visible = "see me", Hidden = "do not" };
            Json json = JsonHelper.BuildJsonForObject(source);
            string serialized = json.ToString();

            StringAssert.Contains(serialized, "see me");
            Assert.IsFalse(serialized.Contains("do not"));
        }

        [TestMethod]
        public void Generated_Nested_RoundTrip ()
        {
            NestedHostGen source = new NestedHostGen
            {
                Label = "outer",
                Inner = new SimpleGen { Name = "inner", Count = 7, Active = false },
            };
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors);

            string serialized = json.ToString();
            Json reparsed = JsonHelper.ParseText(serialized);
            NestedHostGen round = JsonHelper.BuildObjectForJson<NestedHostGen>(reparsed);

            Assert.AreEqual("outer", round.Label);
            Assert.IsNotNull(round.Inner);
            Assert.AreEqual("inner", round.Inner.Name);
            Assert.AreEqual(7, round.Inner.Count);
        }

        [TestMethod]
        public void Generated_Collections_RoundTrip ()
        {
            CollectionsGen source = new CollectionsGen
            {
                Numbers = new List<int> { 1, 2, 3 },
                Tags = new[] { "a", "b" },
            };
            Json json = JsonHelper.BuildJsonForObject(source);
            string serialized = json.ToString();
            Json reparsed = JsonHelper.ParseText(serialized);
            CollectionsGen round = JsonHelper.BuildObjectForJson<CollectionsGen>(reparsed);

            CollectionAssert.AreEqual(source.Numbers, round.Numbers);
            CollectionAssert.AreEqual(source.Tags, round.Tags);
        }
    }
}
