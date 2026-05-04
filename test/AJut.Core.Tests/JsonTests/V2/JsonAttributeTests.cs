namespace AJut.Core.UnitTests.AJsonV2
{
    using System;
    using System.Collections.Generic;
    using AJut.Text.AJson;
    using AJut.TypeManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class JsonAttributeTests
    {
        // ===========================[ Setup/Construction/Teardown ]===================================
        [ClassInitialize]
        public static void RegisterTestTypeIds (TestContext _)
        {
            // Manual registration so the TypeId-based read path resolves these without an
            //  assembly scan.
            TypeIdRegistrar.RegisterTypeId<RuntimeShape_Simple>("test-runtime-simple");
            TypeIdRegistrar.RegisterTypeId<RuntimeShape_WithExtra>("test-runtime-extra");
            TypeIdRegistrar.RegisterTypeId<RuntimeShape_Other>("test-runtime-other");
        }

        // ===========================[ Test Models ]===================================
        public class IgnoreCarrier
        {
            public string Visible { get; set; }

            [JsonIgnore]
            public string Hidden { get; set; }
        }

        public class AliasCarrier
        {
            public const string kAliasKey = "alias-key";

            [JsonPropertyAlias(kAliasKey)]
            public int Value { get; set; }

            public string Plain { get; set; }
        }

        [JsonPropertyAsSelf("Inner")]
        public class IntElevator
        {
            public IntElevator () { }
            public IntElevator (int v) { this.Inner = v; }
            public int Inner { get; set; }
        }

        public class HoldsIntElevator
        {
            public IntElevator Box { get; set; }
        }

        [JsonPropertyAsSelf("Inner")]
        public class StringElevator
        {
            public StringElevator () { }
            public StringElevator (string v) { this.Inner = v; }
            public string Inner { get; set; }
        }

        public class HoldsStringElevator
        {
            public StringElevator Greeting { get; set; }
        }

        public class ComplexInner
        {
            public int A { get; set; }
            public string B { get; set; }
        }

        [JsonPropertyAsSelf("Inner")]
        public class ComplexElevator
        {
            public ComplexInner Inner { get; set; } = new ComplexInner();
        }

        public class HoldsComplexElevator
        {
            public ComplexElevator Payload { get; set; }
        }

        public class OmitCarrier
        {
            [JsonOmitIfDefault]
            public int Number { get; set; }

            [JsonOmitIfDefault]
            public bool Flag { get; set; }

            [JsonOmitIfDefault]
            public string Label { get; set; }

            public int AlwaysWritten { get; set; }
        }

        public enum eAnchor { Left, Center, Right }

        public class OmitEnumOverride
        {
            [JsonOmitIfDefault(eAnchor.Center)]
            public eAnchor Anchor { get; set; } = eAnchor.Center;

            public string Marker { get; set; }
        }

        public class OmitWithCustomEquivalent
        {
            [JsonOmitIfDefault]
            public CountedThing Counter { get; set; }

            public string Marker { get; set; }
        }

        public class CountedThing : IEquatable<CountedThing>
        {
            public int Count { get; set; }

            public bool Equals (CountedThing other) => other != null && other.Count == this.Count;
            public override bool Equals (object obj) => this.Equals(obj as CountedThing);
            public override int GetHashCode () => this.Count.GetHashCode();
        }

        public class AliasOmitMix
        {
            public const string kAliasKey = "the-num";

            [JsonPropertyAlias(kAliasKey)]
            [JsonOmitIfDefault]
            public int Number { get; set; }
        }

        // For runtime-type-eval polymorphism. Three concrete types behind a base. Each has a
        //  [TypeId] so the writer's TypeIdAttributed lookup yields a stable string instead of
        //  falling through to the assembly-qualified name. ClassInitialize registers the same
        //  ids so the read-side TryGetType resolves them back to the concrete types.
        public abstract class RuntimeShape
        {
            public string Tag { get; set; }
        }

        [TypeId("test-runtime-simple")]
        public class RuntimeShape_Simple : RuntimeShape
        {
            public int Count { get; set; }
        }

        [TypeId("test-runtime-extra")]
        public class RuntimeShape_WithExtra : RuntimeShape
        {
            public int Count { get; set; }
            public string Extra { get; set; }
        }

        [TypeId("test-runtime-other")]
        public class RuntimeShape_Other : RuntimeShape
        {
            public double Ratio { get; set; }
        }

        public class PolymorphicHolder
        {
            [JsonRuntimeTypeEval]
            public RuntimeShape Shape { get; set; }
        }

        public class CustomCtorThing
        {
            // No parameterless constructor. The factory below is the only way to make one.
            public CustomCtorThing (int seed)
            {
                this.Seed = seed;
            }

            public int Seed { get; }

            public static CustomCtorThing FromJson (JsonValue value)
            {
                if (value != null && Int32.TryParse(value.StringValue, out int parsed))
                {
                    return new CustomCtorThing(parsed);
                }
                return new CustomCtorThing(-1);
            }
        }

        // ===========================[ JsonIgnore ]===================================
        [TestMethod]
        public void Ignore_PropertyOmittedFromOutput ()
        {
            IgnoreCarrier source = new IgnoreCarrier { Visible = "see me", Hidden = "do not write" };
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            JsonDocument doc = (JsonDocument)json.Data;
            Assert.IsTrue(doc.ContainsKey(nameof(IgnoreCarrier.Visible)));
            Assert.IsFalse(doc.ContainsKey(nameof(IgnoreCarrier.Hidden)));
        }

        [TestMethod]
        public void Ignore_PropertyNotConsumedOnRead ()
        {
            string raw = "{ Visible: \"hi\", Hidden: \"should-not-stick\" }";
            Json json = JsonHelper.ParseText(raw);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            IgnoreCarrier result = JsonHelper.BuildObjectForJson<IgnoreCarrier>(json);
            Assert.AreEqual("hi", result.Visible);
            Assert.IsNull(result.Hidden);
        }

        // ===========================[ JsonPropertyAlias ]===================================
        [TestMethod]
        public void Alias_WrittenUnderAliasKey ()
        {
            AliasCarrier source = new AliasCarrier { Value = 12, Plain = "p" };
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            JsonDocument doc = (JsonDocument)json.Data;
            Assert.IsTrue(doc.ContainsKey(AliasCarrier.kAliasKey));
            Assert.IsFalse(doc.ContainsKey(nameof(AliasCarrier.Value)));
            Assert.IsTrue(doc.TryGetValue(AliasCarrier.kAliasKey, out int found));
            Assert.AreEqual(12, found);
        }

        [TestMethod]
        public void Alias_ReadFromAliasKey ()
        {
            string raw = $"{{ \"{AliasCarrier.kAliasKey}\": 18, Plain: \"p\" }}";
            Json json = JsonHelper.ParseText(raw);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            AliasCarrier result = JsonHelper.BuildObjectForJson<AliasCarrier>(json);
            Assert.AreEqual(18, result.Value);
            Assert.AreEqual("p", result.Plain);
        }

        // ===========================[ JsonPropertyAsSelf ]===================================
        [TestMethod]
        public void AsSelf_IntElevation_RoundTrip ()
        {
            HoldsIntElevator source = new HoldsIntElevator { Box = new IntElevator(3) };
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            string text = json.ToString();
            Assert.IsFalse(text.Contains("Inner"), $"Expected Inner to be elevated away, got: {text}");

            HoldsIntElevator round = JsonHelper.BuildObjectForJson<HoldsIntElevator>(json);
            Assert.IsNotNull(round.Box);
            Assert.AreEqual(3, round.Box.Inner);
        }

        [TestMethod]
        public void AsSelf_StringElevation_StaysQuotedAndRoundTrips ()
        {
            HoldsStringElevator source = new HoldsStringElevator { Greeting = new StringElevator("hello") };
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            string text = json.ToString();
            Assert.IsTrue(text.Contains("\"hello\""), $"Elevated string lost its quotes: {text}");

            Json reparsed = JsonHelper.ParseText(text);
            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport());

            HoldsStringElevator round = JsonHelper.BuildObjectForJson<HoldsStringElevator>(reparsed);
            Assert.IsNotNull(round.Greeting);
            Assert.AreEqual("hello", round.Greeting.Inner);
        }

        [TestMethod]
        public void AsSelf_ComplexElevation_RoundTrip ()
        {
            HoldsComplexElevator source = new HoldsComplexElevator
            {
                Payload = new ComplexElevator { Inner = new ComplexInner { A = 11, B = "eleven" } },
            };

            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            string text = json.ToString();
            Assert.IsTrue(text.Contains("Payload"), "Outer holder property key should still be present.");
            Assert.IsFalse(text.Contains("Inner"), $"Inner should be elevated away, got: {text}");

            HoldsComplexElevator round = JsonHelper.BuildObjectForJson<HoldsComplexElevator>(json);
            Assert.IsNotNull(round.Payload);
            Assert.IsNotNull(round.Payload.Inner);
            Assert.AreEqual(11, round.Payload.Inner.A);
            Assert.AreEqual("eleven", round.Payload.Inner.B);
        }

        [TestMethod]
        public void AsSelf_LegacyNonElevatedShape_StillReads ()
        {
            // Older files (pre-elevation) wrote the inner property as a normal entry. The reader
            //  must still unwrap that shape so existing files don't break on load.
            string raw = "{ Box: { Inner: 7 } }";
            Json json = JsonHelper.ParseText(raw);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            HoldsIntElevator result = JsonHelper.BuildObjectForJson<HoldsIntElevator>(json);
            Assert.IsNotNull(result.Box);
            Assert.AreEqual(7, result.Box.Inner);
        }

        [TestMethod]
        public void AsSelf_NullElevatedValue_DoesNotThrow ()
        {
            HoldsStringElevator source = new HoldsStringElevator { Greeting = new StringElevator() };
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
        }

        // ===========================[ JsonOmitIfDefault ]===================================
        [TestMethod]
        public void OmitIfDefault_DefaultValuesSkipped ()
        {
            OmitCarrier source = new OmitCarrier();
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            JsonDocument doc = (JsonDocument)json.Data;
            Assert.IsNull(doc.ValueFor(nameof(OmitCarrier.Number)));
            Assert.IsNull(doc.ValueFor(nameof(OmitCarrier.Flag)));
            Assert.IsNull(doc.ValueFor(nameof(OmitCarrier.Label)));
            Assert.IsNotNull(doc.ValueFor(nameof(OmitCarrier.AlwaysWritten)));
        }

        [TestMethod]
        public void OmitIfDefault_NonDefaultValuesPresent ()
        {
            OmitCarrier source = new OmitCarrier { Number = 42, Flag = true, Label = "hi" };
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            JsonDocument doc = (JsonDocument)json.Data;
            Assert.AreEqual("42", doc.ValueFor(nameof(OmitCarrier.Number)).StringValue);
            Assert.AreEqual("true", doc.ValueFor(nameof(OmitCarrier.Flag)).StringValue);
            Assert.AreEqual("hi", doc.ValueFor(nameof(OmitCarrier.Label)).StringValue);
        }

        [TestMethod]
        public void OmitIfDefault_ReadStillAcceptsExplicitDefaults ()
        {
            // Read path is intentionally untouched - older files that do carry the default value
            //  still round-trip to the same default rather than being skipped or erroring.
            string raw = "{ Number: 0, Flag: false, AlwaysWritten: 5 }";
            Json json = JsonHelper.ParseText(raw);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            OmitCarrier result = JsonHelper.BuildObjectForJson<OmitCarrier>(json);
            Assert.AreEqual(0, result.Number);
            Assert.AreEqual(false, result.Flag);
            Assert.AreEqual(5, result.AlwaysWritten);
        }

        [TestMethod]
        public void OmitIfDefault_EnumOverride_OmitsExplicitValue ()
        {
            // The enum's intended default (Center) is not its zero value (Left). With the explicit
            //  override, Center should be omitted on write.
            OmitEnumOverride source = new OmitEnumOverride { Anchor = eAnchor.Center, Marker = "m" };
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            JsonDocument doc = (JsonDocument)json.Data;
            Assert.IsNull(doc.ValueFor(nameof(OmitEnumOverride.Anchor)), "Center should have been omitted.");
            Assert.IsNotNull(doc.ValueFor(nameof(OmitEnumOverride.Marker)));
        }

        [TestMethod]
        public void OmitIfDefault_EnumOverride_NonDefaultStillWritten ()
        {
            OmitEnumOverride source = new OmitEnumOverride { Anchor = eAnchor.Left };
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            JsonDocument doc = (JsonDocument)json.Data;
            Assert.IsNotNull(doc.ValueFor(nameof(OmitEnumOverride.Anchor)));
            Assert.AreEqual("Left", doc.ValueFor(nameof(OmitEnumOverride.Anchor)).StringValue);
        }

        [TestMethod]
        public void OmitIfDefault_EnumOverride_RoundTrip ()
        {
            // Round-trip through Center (which is omitted on write) - the property's class-side
            //  initializer puts it back at Center on read since the json doesn't carry the value.
            OmitEnumOverride source = new OmitEnumOverride { Anchor = eAnchor.Center, Marker = "stays" };
            Json json = JsonHelper.BuildJsonForObject(source);
            string text = json.ToString();

            Json reparsed = JsonHelper.ParseText(text);
            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport());

            OmitEnumOverride round = JsonHelper.BuildObjectForJson<OmitEnumOverride>(reparsed);
            Assert.AreEqual(eAnchor.Center, round.Anchor);
            Assert.AreEqual("stays", round.Marker);
        }

        [TestMethod]
        public void OmitIfDefault_RegisteredEquivalent_OmitsMatchingInstance ()
        {
            JsonBuilderSettings settings = new JsonBuilderSettings();
            settings.RegisterDefaultEquivalent(new CountedThing { Count = 7 });

            OmitWithCustomEquivalent source = new OmitWithCustomEquivalent
            {
                Counter = new CountedThing { Count = 7 },
                Marker = "m",
            };
            Json json = JsonHelper.BuildJsonForObject(source, settings);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            JsonDocument doc = (JsonDocument)json.Data;
            Assert.IsNull(doc.ValueFor(nameof(OmitWithCustomEquivalent.Counter)), "Registered equivalent should suppress write.");
            Assert.IsNotNull(doc.ValueFor(nameof(OmitWithCustomEquivalent.Marker)));
        }

        [TestMethod]
        public void OmitIfDefault_RegisteredEquivalent_NonMatchingStillWritten ()
        {
            JsonBuilderSettings settings = new JsonBuilderSettings();
            settings.RegisterDefaultEquivalent(new CountedThing { Count = 7 });

            OmitWithCustomEquivalent source = new OmitWithCustomEquivalent
            {
                Counter = new CountedThing { Count = 99 },
                Marker = "m",
            };
            Json json = JsonHelper.BuildJsonForObject(source, settings);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            JsonDocument doc = (JsonDocument)json.Data;
            Assert.IsNotNull(doc.ValueFor(nameof(OmitWithCustomEquivalent.Counter)));
        }

        // ===========================[ Multi-Attribute ]===================================
        [TestMethod]
        public void AliasPlusOmit_OmittedAtDefault ()
        {
            AliasOmitMix source = new AliasOmitMix();
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            JsonDocument doc = (JsonDocument)json.Data;
            Assert.IsFalse(doc.ContainsKey(AliasOmitMix.kAliasKey));
            Assert.IsFalse(doc.ContainsKey(nameof(AliasOmitMix.Number)));
        }

        [TestMethod]
        public void AliasPlusOmit_PresentUnderAliasWhenSet ()
        {
            AliasOmitMix source = new AliasOmitMix { Number = 4 };
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            JsonDocument doc = (JsonDocument)json.Data;
            Assert.IsTrue(doc.ContainsKey(AliasOmitMix.kAliasKey));
            Assert.AreEqual("4", doc.ValueFor(AliasOmitMix.kAliasKey).StringValue);

            AliasOmitMix round = JsonHelper.BuildObjectForJson<AliasOmitMix>(json);
            Assert.AreEqual(4, round.Number);
        }

        // ===========================[ JsonRuntimeTypeEval ]===================================
        [TestMethod]
        public void RuntimeTypeEval_PolymorphicProperty_ThreeConcreteTypes_RoundTrip ()
        {
            PolymorphicHolder a = new PolymorphicHolder { Shape = new RuntimeShape_Simple { Tag = "a", Count = 1 } };
            PolymorphicHolder b = new PolymorphicHolder { Shape = new RuntimeShape_WithExtra { Tag = "b", Count = 2, Extra = "x" } };
            PolymorphicHolder c = new PolymorphicHolder { Shape = new RuntimeShape_Other { Tag = "c", Ratio = 0.5 } };

            PolymorphicHolder roundA = RoundTripPolymorphic(a);
            PolymorphicHolder roundB = RoundTripPolymorphic(b);
            PolymorphicHolder roundC = RoundTripPolymorphic(c);

            Assert.IsInstanceOfType(roundA.Shape, typeof(RuntimeShape_Simple));
            Assert.AreEqual(1, ((RuntimeShape_Simple)roundA.Shape).Count);
            Assert.AreEqual("a", roundA.Shape.Tag);

            Assert.IsInstanceOfType(roundB.Shape, typeof(RuntimeShape_WithExtra));
            Assert.AreEqual(2, ((RuntimeShape_WithExtra)roundB.Shape).Count);
            Assert.AreEqual("x", ((RuntimeShape_WithExtra)roundB.Shape).Extra);

            Assert.IsInstanceOfType(roundC.Shape, typeof(RuntimeShape_Other));
            Assert.AreEqual(0.5, ((RuntimeShape_Other)roundC.Shape).Ratio);
        }

        private static PolymorphicHolder RoundTripPolymorphic (PolymorphicHolder source)
        {
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            string text = json.ToString();
            Json reparsed = JsonHelper.ParseText(text);
            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport());

            return JsonHelper.BuildObjectForJson<PolymorphicHolder>(reparsed);
        }

        // ===========================[ Custom constructor ]===================================
        [TestMethod]
        public void CustomConstructor_BuildsViaRegisteredFactory ()
        {
            JsonInterpreterSettings settings = new JsonInterpreterSettings();
            settings.RegisterCustomConstructor<CustomCtorThing>(CustomCtorThing.FromJson);

            Json json = JsonHelper.ParseText("42");
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            CustomCtorThing built = JsonHelper.BuildObjectForJson<CustomCtorThing>(json, settings);
            Assert.IsNotNull(built);
            Assert.AreEqual(42, built.Seed);
        }

        // ===========================[ KVP missing-value fold-in ]===================================
        [TestMethod]
        public void KeyValuePair_MissingValue_DoesNotThrow_RecordsError ()
        {
            // Pre-fix V1 behavior was to throw FormatException on a KVP doc missing its Value
            //  field, which aborted the whole load. V2 contract: never throw, route the failure
            //  through Json.Errors and return a default-constructed KVP.
            string raw = "{ \"Key\" : \"input_2\" }";
            Json json = JsonHelper.ParseText(raw);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            KeyValuePair<string, string> kvp = JsonHelper.BuildObjectForJson<KeyValuePair<string, string>>(json);
            Assert.IsTrue(json.HasErrors, "Expected missing-Value to land as a Json error.");
            Assert.AreEqual("input_2", kvp.Key);
            Assert.IsNull(kvp.Value);
        }

        [TestMethod]
        public void KeyValuePair_MissingKey_DoesNotThrow_RecordsError ()
        {
            string raw = "{ \"Value\" : \"only-value\" }";
            Json json = JsonHelper.ParseText(raw);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            KeyValuePair<string, string> kvp = JsonHelper.BuildObjectForJson<KeyValuePair<string, string>>(json);
            Assert.IsTrue(json.HasErrors, "Expected missing-Key to land as a Json error.");
            Assert.IsNull(kvp.Key);
            Assert.AreEqual("only-value", kvp.Value);
        }
    }
}
