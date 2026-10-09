namespace AJut.Core.UnitTests.AJsonV2
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using AJut;
    using AJut.Text.AJson;
    using AJut.TypeManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The reflection path's constructor route: a type built through its [AJsonConstructor] or a record's positional
    /// constructor, what a parameter gets when its key is missing, and a registered custom constructor winning over both. None of
    /// these types is opted into the source generator.
    /// </summary>
    [TestClass]
    public class JsonConstructorRouteTests
    {
        private const string kCircleTypeId = "ctor-route-circle";

        private readonly List<string> m_loggedOutput = new List<string>();

        // ===========================[ Test Models ]===========================
        public class Sized
        {
            [AJsonConstructor]
            public Sized (string name, int size)
            {
                this.Name = name;
                this.Size = size;
            }

            public string Name { get; }
            public int Size { get; }
            public double Ratio { get; set; }
        }

        public class Aliased
        {
            [AJsonConstructor]
            public Aliased (string label)
            {
                this.Label = label;
            }

            [JsonPropertyAlias("friendly")]
            public string Label { get; }
        }

        public class CountedWithInitOnly
        {
            public static int g_constructionCount;

            [AJsonConstructor]
            public CountedWithInitOnly (int id)
            {
                ++g_constructionCount;
                this.Id = id;
            }

            public int Id { get; }
            public string Label { get; init; } = "unset";
            public double Ratio { get; set; }
        }

        public record Pair (int Left, string Right);

        public struct Span
        {
            [AJsonConstructor]
            public Span (int start, int length)
            {
                this.Start = start;
                this.Length = length;
            }

            public int Start { get; }
            public int Length { get; }
        }

        public enum eMode { Calm, Busy, Loud }

        public class MissingKeys
        {
            [AJsonConstructor]
            public MissingKeys (int omitted, int neither, eMode mode, int declared = 7, int omittedOverDeclared = 3, int bareOmitted = 4, string unmatched = "fallback")
            {
                this.Omitted = omitted;
                this.Neither = neither;
                this.Mode = mode;
                this.Declared = declared;
                this.OmittedOverDeclared = omittedOverDeclared;
                this.BareOmitted = bareOmitted;
                this.Stored = unmatched;
            }

            [JsonOmitIfDefault(5)]
            public int Omitted { get; }

            public int Neither { get; }

            [JsonOmitIfDefault(eMode.Busy)]
            public eMode Mode { get; }

            public int Declared { get; }

            [JsonOmitIfDefault(9)]
            public int OmittedOverDeclared { get; }

            [JsonOmitIfDefault]
            public int BareOmitted { get; }

            public string Stored { get; }
        }

        public class PlainWithParameterless
        {
            public string Origin { get; set; } = "parameterless";
            public int Count { get; set; }
        }

        public class MarkedButCustomBuilt
        {
            public static int g_markedConstructorCalls;

            private MarkedButCustomBuilt ()
            {
                this.Count = -1;
            }

            [AJsonConstructor]
            public MarkedButCustomBuilt (int count)
            {
                ++g_markedConstructorCalls;
                this.Count = count;
            }

            public int Count { get; }
            public string Note { get; set; }
            public string Tag { get; init; }

            public static MarkedButCustomBuilt FromJson (JsonValue value) => new MarkedButCustomBuilt();
        }

        public class ParameterlessAndMarked
        {
            public static int g_markedConstructorCalls;

            public ParameterlessAndMarked () { }

            [AJsonConstructor]
            public ParameterlessAndMarked (int count)
            {
                ++g_markedConstructorCalls;
                this.Count = count;
            }

            public int Count { get; set; }
        }

        public class TwoMarked
        {
            [AJsonConstructor]
            public TwoMarked (int count) { }

            [AJsonConstructor]
            public TwoMarked (string count) { }

            public int Count { get; set; }
        }

        public abstract class Shape { }

        [TypeId(kCircleTypeId)]
        public class Circle : Shape
        {
            [AJsonConstructor]
            public Circle (double radius)
            {
                this.Radius = radius;
            }

            public double Radius { get; }
        }

        public class RuntimeShapeHolder
        {
            [JsonRuntimeTypeEval]
            public Shape Thing { get; set; }
        }

        public class ShapeHolder
        {
            public Shape Thing { get; set; }
        }

        [ClassInitialize]
        public static void RegisterTestTypeIds (TestContext _)
        {
            TypeIdRegistrar.RegisterTypeId<Circle>(kCircleTypeId);
        }

        // ===========================[ Constructor route ]===========================
        [TestMethod]
        public void ConstructorRoute_MarkedConstructor_RoundTripsGetOnlyProperties ()
        {
            Sized round = RoundTrip(new Sized("box", 12) { Ratio = 0.5 });

            Assert.AreEqual("box", round.Name);
            Assert.AreEqual(12, round.Size);
            Assert.AreEqual(0.5, round.Ratio);
        }

        [TestMethod]
        public void ConstructorRoute_ParameterReadsTheAliasedKey ()
        {
            Json written = JsonHelper.BuildJsonForObject(new Aliased("shown"));
            CollectionAssert.Contains(((JsonDocument)written.Data).AllKeys().ToList(), "friendly");

            Assert.AreEqual("shown", RoundTrip(new Aliased("shown")).Label);
        }

        [TestMethod]
        public void ConstructorRoute_InitOnlyPropertiesOutsideTheConstructor_AreSetAndConstructionRunsOnce ()
        {
            CountedWithInitOnly.g_constructionCount = 0;
            Json parsed = JsonHelper.ParseText("{ \"Id\": 4, \"Label\": \"four\", \"Ratio\": 1.5 }");
            CountedWithInitOnly read = JsonHelper.BuildObjectForJson<CountedWithInitOnly>(parsed);

            Assert.IsFalse(parsed.HasErrors, parsed.GetErrorReport());
            Assert.AreEqual(1, CountedWithInitOnly.g_constructionCount);
            Assert.AreEqual(4, read.Id);
            Assert.AreEqual("four", read.Label);
            Assert.AreEqual(1.5, read.Ratio);
        }

        [TestMethod]
        public void ConstructorRoute_MissingInitOnlyKey_KeepsItsInitializer ()
        {
            Json parsed = JsonHelper.ParseText("{ \"Id\": 2 }");
            CountedWithInitOnly read = JsonHelper.BuildObjectForJson<CountedWithInitOnly>(parsed);

            Assert.IsFalse(parsed.HasErrors, parsed.GetErrorReport());
            Assert.AreEqual(2, read.Id);
            Assert.AreEqual("unset", read.Label);
        }

        [TestMethod]
        public void ConstructorRoute_PositionalRecordWithNoParameterlessConstructor_RoundTrips ()
        {
            Pair source = new Pair(3, "three");
            Assert.AreEqual(source, RoundTrip(source));
        }

        [TestMethod]
        public void ConstructorRoute_StructWithMarkedConstructor_RoundTrips ()
        {
            Span round = RoundTrip(new Span(10, 4));

            Assert.AreEqual(10, round.Start);
            Assert.AreEqual(4, round.Length);
        }

        [TestMethod]
        public void ConstructorRoute_ParameterlessConstructor_WinsOverAMarkedOne ()
        {
            ParameterlessAndMarked.g_markedConstructorCalls = 0;
            ParameterlessAndMarked read = JsonHelper.BuildObjectForJson<ParameterlessAndMarked>(JsonHelper.ParseText("{ \"Count\": 6 }"));

            Assert.AreEqual(0, ParameterlessAndMarked.g_markedConstructorCalls);
            Assert.AreEqual(6, read.Count);
        }

        [TestMethod]
        public void ConstructorRoute_TwoMarkedConstructors_ReportsAnError ()
        {
            Json parsed = JsonHelper.ParseText("{ \"Count\": 1 }");

            // With no constructor to choose, it is built like any type with no parameterless constructor, which throws
            Assert.ThrowsException<MissingMethodException>(() => JsonHelper.BuildObjectForJson<TwoMarked>(parsed));
            StringAssert.Contains(parsed.GetErrorReport(), "more than one constructor marked [AJsonConstructor]");
        }

        // ===========================[ Missing keys ]===========================
        [TestMethod]
        public void MissingKey_TakesOmitValue_ThenDeclaredDefault_ThenTypeDefault ()
        {
            Json parsed = JsonHelper.ParseText("{ \"unmatched\": \"never read\" }");
            MissingKeys read = JsonHelper.BuildObjectForJson<MissingKeys>(parsed);

            Assert.IsFalse(parsed.HasErrors, parsed.GetErrorReport());
            Assert.AreEqual(5, read.Omitted, "the omit value");
            Assert.AreEqual(eMode.Busy, read.Mode, "the omit value, an enum stored as its number");
            Assert.AreEqual(7, read.Declared, "the declared default");
            Assert.AreEqual(9, read.OmittedOverDeclared, "the omit value wins over a declared default");
            Assert.AreEqual(0, read.BareOmitted, "a bare omit attribute's type default wins over a declared default");
            Assert.AreEqual(0, read.Neither, "the type default");
            Assert.AreEqual("fallback", read.Stored, "a parameter that matches no property always gets its missing value");
        }

        [TestMethod]
        public void MissingKey_ValuesTheWriterLeftOut_ReadBackTheSame ()
        {
            MissingKeys source = new MissingKeys(omitted: 5, neither: 2, mode: eMode.Busy, declared: 1, omittedOverDeclared: 9, bareOmitted: 0);
            Json written = JsonHelper.BuildJsonForObject(source);
            List<string> keys = ((JsonDocument)written.Data).AllKeys().ToList();
            CollectionAssert.DoesNotContain(keys, "Omitted");
            CollectionAssert.DoesNotContain(keys, "Mode");
            CollectionAssert.DoesNotContain(keys, "OmittedOverDeclared");
            CollectionAssert.DoesNotContain(keys, "BareOmitted");

            MissingKeys round = RoundTrip(source);

            Assert.AreEqual(5, round.Omitted);
            Assert.AreEqual(eMode.Busy, round.Mode);
            Assert.AreEqual(9, round.OmittedOverDeclared);
            Assert.AreEqual(0, round.BareOmitted, "zero comes back as zero, not the declared default of 4");
            Assert.AreEqual(2, round.Neither);
            Assert.AreEqual(1, round.Declared);
        }

        // ===========================[ Custom constructor ]===========================
        [TestMethod]
        public void CustomConstructor_WinsOnATypeWithAParameterlessConstructor ()
        {
            JsonInterpreterSettings settings = new JsonInterpreterSettings();
            settings.RegisterCustomConstructor<PlainWithParameterless>(_ => new PlainWithParameterless { Origin = "custom" });

            Json parsed = JsonHelper.ParseText("{ \"Count\": 3 }");
            PlainWithParameterless read = JsonHelper.BuildObjectForJson<PlainWithParameterless>(parsed, settings);

            Assert.AreEqual("custom", read.Origin);
            Assert.AreEqual(3, read.Count);
        }

        [TestMethod]
        public void CustomConstructor_WinsOverAMarkedConstructor_AndWarnsOnce ()
        {
            Logger.ResetToDefaults();
            Logger.ShouldLogToConsole = false;
            Logger.ShouldLogToTrace = false;
            Logger.SetSingleOverrideLogTarget(this.CaptureLog);
            try
            {
                MarkedButCustomBuilt.g_markedConstructorCalls = 0;
                JsonInterpreterSettings settings = new JsonInterpreterSettings();
                settings.RegisterCustomConstructor<MarkedButCustomBuilt>(MarkedButCustomBuilt.FromJson);

                const string kJson = "{ \"Count\": 8, \"Note\": \"noted\", \"Tag\": \"tagged\" }";
                MarkedButCustomBuilt first = JsonHelper.BuildObjectForJson<MarkedButCustomBuilt>(JsonHelper.ParseText(kJson), settings);
                MarkedButCustomBuilt second = JsonHelper.BuildObjectForJson<MarkedButCustomBuilt>(JsonHelper.ParseText(kJson), settings);

                Assert.AreEqual(0, MarkedButCustomBuilt.g_markedConstructorCalls, "the marked constructor is never used");
                foreach (MarkedButCustomBuilt read in new[] { first, second })
                {
                    Assert.AreEqual(-1, read.Count, "a get-only property is left as the custom constructor made it");
                    Assert.AreEqual("noted", read.Note);
                    Assert.AreEqual("tagged", read.Tag);
                }

                string[] warnings = m_loggedOutput.Where(line => line.Contains("[WARNING]") && line.Contains(nameof(MarkedButCustomBuilt))).ToArray();
                Assert.AreEqual(1, warnings.Length, string.Join("\n", m_loggedOutput));
            }
            finally
            {
                Logger.ResetToDefaults();
            }
        }

        // ===========================[ Polymorphic ]===========================
        [TestMethod]
        public void Polymorphic_RuntimeTypeEvalConcreteTypeWithOnlyAMarkedConstructor_RoundTrips ()
        {
            RuntimeShapeHolder round = RoundTrip(new RuntimeShapeHolder { Thing = new Circle(2.5) });

            Assert.IsInstanceOfType(round.Thing, typeof(Circle));
            Assert.AreEqual(2.5, ((Circle)round.Thing).Radius);
        }

        [TestMethod]
        public void Polymorphic_TypeIdOnTheDocument_BuildsTheConcreteTypeThroughItsMarkedConstructor ()
        {
            Json parsed = JsonHelper.ParseText($"{{ \"Thing\": {{ \"__type\": \"{kCircleTypeId}\", \"Radius\": 4 }} }}");
            ShapeHolder read = JsonHelper.BuildObjectForJson<ShapeHolder>(parsed);

            Assert.IsFalse(parsed.HasErrors, parsed.GetErrorReport());
            Assert.IsInstanceOfType(read.Thing, typeof(Circle));
            Assert.AreEqual(4.0, ((Circle)read.Thing).Radius);
        }

        // ===========================[ Helpers ]===========================
        private static T RoundTrip<T> (T source)
        {
            Json written = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(written.HasErrors, written.GetErrorReport());

            Json parsed = JsonHelper.ParseText(written.ToString());
            Assert.IsFalse(parsed.HasErrors, parsed.GetErrorReport());

            T read = JsonHelper.BuildObjectForJson<T>(parsed);
            Assert.IsFalse(parsed.HasErrors, parsed.GetErrorReport());
            return read;
        }

        private void CaptureLog (string line)
        {
            m_loggedOutput.Add(line);
        }
    }
}
