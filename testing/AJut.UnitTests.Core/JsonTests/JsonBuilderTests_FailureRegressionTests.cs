namespace AJut.UnitTests.Core.AJson
{
    using System;
    using System.Collections.Generic;
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class JsonBuilderTests_FailureRegressionTests
    {
        class TestingClass
        {
            public int NumberValue { get; set; }
            public OtherTestingClass ObjectValue { get; set; }
        }

        class OtherTestingClass
        {
            public int NumberValue { get; set; }
        }

        private static readonly JsonBuilder.Settings m_testSettings = new JsonBuilder.Settings();
        [TestMethod]
        public void AJson_JsonBuilding_CreatingJson_UsingNullValues()
        {
            var test = new TestingClass();
            test.NumberValue = 2;
            test.ObjectValue = null;
            Json json = JsonHelper.BuildJsonForObject(test, m_testSettings);
            var doc = json.Data as JsonDocument;
            Assert.IsNotNull(doc);
            Assert.AreEqual(1, doc.Count);

            Assert.AreEqual("NumberValue", doc.KeyAt(0));
            Assert.IsTrue(doc.ValueAt(0).IsValue);
            Assert.AreEqual("2", doc.ValueAt(0).StringValue);
        }

        public class TestFillOutDirectly
        {
            public Guid Id { get; set; }
            public string Title { get; set; }
        }

        [TestMethod]
        public void AJson_JsonBuilding_FilloutObjDirectly()
        {
            string testJson =
@"{
	""Id"" : ""d03e16e7-c15a-459c-aeb4-0a043b0b3c21"",
    ""Title"" : ""Test"",
}";

            Json json = JsonHelper.ParseText(testJson);

            object test = new TestFillOutDirectly();
            JsonHelper.FillOutObjectWithJson(ref test, (JsonDocument)json.Data);
            Assert.IsInstanceOfType(test, typeof(TestFillOutDirectly));
            Assert.AreEqual("Test", ((TestFillOutDirectly)test).Title);
            if (test is TestFillOutDirectly gameInfo)
            {
                return;
            }
        }

        private class ThingWithArray
        {
            public string[] Names { get; set; }
        }

        [TestMethod]
        public void AJson_JsonBuilding_CreatingArraysNotList()
        {
            var twa_input = new ThingWithArray()
            {
                Names = new[] 
                {
                    "Bob",
                    "Joe",
                    "McFartpantz"
                }
            };

            Json json = JsonHelper.BuildJsonForObject(twa_input);
            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));

            var twa_output = JsonHelper.BuildObjectForJson<ThingWithArray>(json);
            Assert.IsNotNull(twa_output);
            Assert.IsInstanceOfType(twa_output, typeof(ThingWithArray));
            Assert.IsNotNull(twa_output.Names);

            Assert.AreEqual(3, twa_output.Names.Length);
            Assert.AreEqual("Bob", twa_output.Names[0]);
            Assert.AreEqual("Joe", twa_output.Names[1]);
            Assert.AreEqual("McFartpantz", twa_output.Names[2]);
        }

        public class ThingWithSpecialStuff
        {
            public Dictionary<int,bool> DictionaryItem { get; set; }
            public Guid[] BunchaGuids { get; set; }
            public Guid GuidItem { get; set; }
            public DateTime DateTimeItem { get; set; }
            public TimeSpan TimeSpanItem { get; set; }
        }

        public class ThingWithId { public Guid[] Ids { get; set; } }

        [TestMethod]
        public void AJson_JsonHelper_IOBuiltInItems_TempFocused ()
        {
            var testGuids = new[] { Guid.NewGuid() };
            var test = new ThingWithId { Ids = testGuids };

            Json json = JsonHelper.BuildJsonForObject(test);
            Assert.IsFalse(json.HasErrors, json.BuildJsonErrorReport());

            string jsonText = json.Data.StringValue;
            json = JsonHelper.ParseText(jsonText);
            Assert.IsFalse(json.HasErrors, json.BuildJsonErrorReport());

            var found = JsonHelper.BuildObjectForJson<ThingWithId>(json);
            Assert.IsNotNull(found?.Ids);
            Assert.AreEqual(testGuids.Length, found.Ids.Length);
            Assert.AreEqual(testGuids[0], found.Ids[0]);
        }

        [TestMethod]
        public void AJson_JsonHelper_IOBuiltInItems ()
        {
            var testDictionary = new Dictionary<int, bool>
            {
                { 3, true },
                { 4, false }
            };
            var testDateTime = DateTime.Now;
            var testTimeSpan = TimeSpan.FromMilliseconds(2200.4);
            var testGuid = Guid.NewGuid();
            var bunchaGuids = new[] { Guid.NewGuid(), Guid.NewGuid() };

            var test = new ThingWithSpecialStuff();
            test.DictionaryItem = testDictionary;
            test.DateTimeItem = testDateTime;
            test.TimeSpanItem = testTimeSpan;
            test.GuidItem = testGuid;
            test.BunchaGuids = bunchaGuids;

            Json json = JsonHelper.BuildJsonForObject(test);
            Assert.IsFalse(json.HasErrors, json.BuildJsonErrorReport());

            var found = JsonHelper.BuildObjectForJson<ThingWithSpecialStuff>(json);
            Assert.IsNotNull(found);

            // Dictionary
            Assert.AreEqual(2, found.DictionaryItem.Count);
            Assert.IsTrue(found.DictionaryItem.ContainsKey(3));
            Assert.IsTrue(found.DictionaryItem.ContainsKey(4));
            Assert.AreEqual(true, found.DictionaryItem[3]);
            Assert.AreEqual(false, found.DictionaryItem[4]);

            // Buncha Guids
            Assert.AreEqual(2, found.BunchaGuids.Length);
            Assert.AreEqual(bunchaGuids[0], found.BunchaGuids[0]);
            Assert.AreEqual(bunchaGuids[1], found.BunchaGuids[1]);

            // Reading is a bit funky compared to normal creation, they have internal things
            //  that show as different even though they look *EXACTLY* the same
            Assert.AreEqual(testDateTime.ToString(), found.DateTimeItem.ToString());
            Assert.AreEqual(testTimeSpan, found.TimeSpanItem);
            Assert.AreEqual(testGuid, found.GuidItem);
        }


        [TestMethod]
        public void AJson_JsonBuilding_CanBuildWithNullProperty ()
        {
            Json json = JsonHelper.BuildJsonForObject(
                new NullTester<object>
                {
                    NumberProp = 8,
                    NullProp = null
                }
            );

            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsTrue(json.Data.IsDocument, "Json should have parsed an array");

            NullTester<object> created = JsonHelper.BuildObjectForJson<NullTester<object>>(json);
            Assert.IsNotNull(created);
            Assert.AreEqual(8, created.NumberProp);
            Assert.IsNull(created.NullProp);
        }

        [TestMethod]
        public void AJson_JsonBuilding_CanBuildWithNullSimpleProperty ()
        {
            Json json = JsonHelper.BuildJsonForObject(
                new NullTester<string>
                {
                    NumberProp = 8,
                    NullProp = null
                }
            );

            Assert.IsNotNull(json);
            Assert.IsFalse(json.HasErrors, "Json parse errors:\n" + String.Join("\n\t", json.Errors));
            Assert.IsTrue(json.Data.IsDocument, "Json should have parsed an array");

            NullTester<string> created = JsonHelper.BuildObjectForJson<NullTester<string>>(json);
            Assert.IsNotNull(created);
            Assert.AreEqual(8, created.NumberProp);
            Assert.IsNull(created.NullProp);
        }

        public class NullTester<TNullThing>
        {
            public int NumberProp { get; set; }
            public TNullThing NullProp { get; set; }
        }

    }
}
