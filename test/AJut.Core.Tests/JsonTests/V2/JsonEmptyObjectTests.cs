namespace AJut.Core.UnitTests.AJsonV2
{
    using System;
    using System.Collections.Generic;
    using AJut.Text.AJson;
    using AJut.TypeManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// A type with no data members still has to round-trip when it carries a [TypeId]. An empty
    /// marker type is a normal thing to put in a polymorphic slot, and the type is the whole value.
    /// </summary>
    [TestClass]
    public class JsonEmptyObjectTests
    {
        private const string kEmptyMarkerId = "test-empty-marker";
        private const string kFullMarkerId = "test-full-marker";

        // ===========================[ Setup/Construction/Teardown ]===================================
        [ClassInitialize]
        public static void RegisterTestTypeIds (TestContext _)
        {
            TypeIdRegistrar.RegisterTypeId<EmptyMarker>(kEmptyMarkerId);
            TypeIdRegistrar.RegisterTypeId<FullMarker>(kFullMarkerId);
        }

        // ===========================[ Test Models ]===================================
        public interface IMarker { }

        [TypeId(kEmptyMarkerId)]
        public class EmptyMarker : IMarker { }

        [TypeId(kFullMarkerId)]
        public class FullMarker : IMarker
        {
            public int Value { get; set; }
        }

        public class RuntimeTypeEvalHost
        {
            [JsonRuntimeTypeEval(eTypeIdInfo.TypeIdAttributed)]
            public IMarker Payload { get; set; }
        }

        [OptimizeAJson]
        public class RuntimeTypeEvalHostGen
        {
            [JsonRuntimeTypeEval(eTypeIdInfo.TypeIdAttributed)]
            public IMarker Payload { get; set; }
        }

        public class InterfaceHost
        {
            public IMarker Payload { get; set; }
        }

        public class ListHost
        {
            public List<IMarker> Items { get; set; }
        }

        // ===========================[ Tests ]===================================
        [TestMethod]
        public void EmptyTypeId_RuntimeTypeEvalProperty_RoundTrips ()
        {
            RuntimeTypeEvalHost round = RoundTrip(new RuntimeTypeEvalHost { Payload = new EmptyMarker() }, out string text);
            Assert.IsInstanceOfType(round.Payload, typeof(EmptyMarker), text);
        }

        [TestMethod]
        public void EmptyTypeId_RuntimeTypeEvalProperty_SourceGen_RoundTrips ()
        {
            bool isRegistered = AJsonGeneratedDispatch.IsRegistered(typeof(RuntimeTypeEvalHostGen));
            Assert.IsTrue(isRegistered, "Source generator did not register RuntimeTypeEvalHostGen");

            RuntimeTypeEvalHostGen source = new RuntimeTypeEvalHostGen { Payload = new EmptyMarker() };
            RuntimeTypeEvalHostGen round = RoundTrip(source, out string text);
            Assert.IsInstanceOfType(round.Payload, typeof(EmptyMarker), text);
        }

        [TestMethod]
        public void EmptyTypeId_RuntimeTypeEvalProperty_SourceGen_ReadsWrapperWithNoValue ()
        {
            // Text written before empty objects kept their document: the wrapper carries the type
            //  id and nothing under __value.
            string oldText = "{ \"Payload\": { \"__type\": \"" + kEmptyMarkerId + "\" } }";
            Json json = JsonHelper.ParseText(oldText);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            RuntimeTypeEvalHostGen read = JsonHelper.BuildObjectForJson<RuntimeTypeEvalHostGen>(json);
            Assert.IsInstanceOfType(read.Payload, typeof(EmptyMarker), oldText);
        }

        [TestMethod]
        public void EmptyTypeId_InterfaceProperty_RoundTrips ()
        {
            InterfaceHost round = RoundTrip(new InterfaceHost { Payload = new EmptyMarker() }, out string text);
            Assert.IsInstanceOfType(round.Payload, typeof(EmptyMarker), text);
        }

        [TestMethod]
        public void EmptyTypeId_InList_KeepsItsSlot ()
        {
            ListHost source = new ListHost
            {
                Items = new List<IMarker> { new FullMarker { Value = 1 }, new EmptyMarker(), new FullMarker { Value = 2 } },
            };

            ListHost round = RoundTrip(source, out string text);
            Assert.IsNotNull(round.Items, text);
            Assert.AreEqual(3, round.Items.Count, text);
            Assert.AreEqual(1, ((FullMarker)round.Items[0]).Value, text);
            Assert.IsInstanceOfType(round.Items[1], typeof(EmptyMarker), text);
            Assert.AreEqual(2, ((FullMarker)round.Items[2]).Value, text);
        }

        [TestMethod]
        public void EmptyTypeId_AtRoot_RoundTrips ()
        {
            Json json = JsonHelper.BuildJsonForObject(new EmptyMarker());
            string text = json.ToString();

            Json reparsed = JsonHelper.ParseText(text);
            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport());
            Assert.IsInstanceOfType(JsonHelper.BuildObjectForTypedJson(reparsed), typeof(EmptyMarker), text);
        }

        // ===========================[ Helpers ]===================================
        private static T RoundTrip<T> (T source, out string serialized)
        {
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            serialized = json.ToString();
            Json reparsed = JsonHelper.ParseText(serialized);
            string reparseErrors = String.Join("\n  ", reparsed.Errors);
            Assert.IsFalse(reparsed.HasErrors, "Reparse errors:\n  " + reparseErrors + "\nText:\n" + serialized);

            T round = JsonHelper.BuildObjectForJson<T>(reparsed);
            Assert.IsNotNull(round, serialized);
            return round;
        }
    }
}
