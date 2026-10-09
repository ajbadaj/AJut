namespace AJut.Text.AJson.SourceGenerators.Tests
{
    using System;
    using System.Linq;
    using System.Reflection;
    using AJut.Text.AJson;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Runs the generated serializers for real. Each fixture is compiled together with the generator's output, loaded into the
    /// test process, and pushed through JsonHelper, which dispatches to the generated Write and Read. EmitCompileTests proves the
    /// output compiles; these prove it reads back what it wrote.
    /// </summary>
    [TestClass]
    public class GeneratedRoundTripTests
    {
        private const string kInitOnlySource = @"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public class InitOnly
    {
        public int Count { get; init; }
        public string Label { get; init; } = ""unset"";
        public double Ratio { get; set; }
    }
}";

        // Each parameter takes a different branch of the missing-key rule. Three of them also raise warnings: AJSON006 for unmatched
        //  and AJSON007 for omittedOverDeclared and bareOmitted.
        private const string kMissingKeysSource = @"
using AJut.Text.AJson;
namespace TestNs
{
    public enum eMode { Calm, Busy, Loud }

    [OptimizeAJson]
    public class MissingKeys
    {
        [AJsonConstructor]
        public MissingKeys (int omitted, int neither, eMode mode, float scale, int declared = 7, int omittedOverDeclared = 3, int bareOmitted = 4, string unmatched = ""fallback"")
        {
            Omitted = omitted;
            Neither = neither;
            Mode = mode;
            Scale = scale;
            Declared = declared;
            OmittedOverDeclared = omittedOverDeclared;
            BareOmitted = bareOmitted;
            Stored = unmatched;
        }

        [JsonOmitIfDefault(5)] public int Omitted { get; }
        public int Neither { get; }
        [JsonOmitIfDefault(eMode.Busy)] public eMode Mode { get; }
        [JsonOmitIfDefault(2.5f)] public float Scale { get; }
        public int Declared { get; }
        [JsonOmitIfDefault(9)] public int OmittedOverDeclared { get; }
        [JsonOmitIfDefault] public int BareOmitted { get; }
        public string Stored { get; }
    }
}";

        [TestMethod]
        public void GeneratedReader_RoundTrips_OneRuntimeTypeEvalProperty ()
        {
            Assembly fixture = CompileAndLoad(@"
using AJut.Text.AJson;
using AJut.TypeManagement;
namespace TestNs
{
    public abstract class Shape { }
    [TypeId(""rte-one.Circle"")] public class Circle : Shape { public double Radius { get; set; } }
    [OptimizeAJson] public class Holder { [JsonRuntimeTypeEval] public Shape Thing { get; set; } }
}", "RoundTrip_OneRuntimeTypeEval");

            object circle = Create(fixture, "TestNs.Circle");
            Set(circle, "Radius", 2.5);
            object holder = Create(fixture, "TestNs.Holder");
            Set(holder, "Thing", circle);

            object readBack = RoundTrip(holder);

            object? thing = Get(readBack, "Thing");
            Assert.AreEqual("TestNs.Circle", thing?.GetType().FullName);
            Assert.AreEqual(2.5, Get(thing!, "Radius"));
        }

        [TestMethod]
        public void GeneratedReader_RoundTrips_ThreeRuntimeTypeEvalProperties ()
        {
            Assembly fixture = CompileAndLoad(@"
using AJut.Text.AJson;
using AJut.TypeManagement;
namespace TestNs
{
    public abstract class Shape { }
    [TypeId(""rte-three.Circle"")] public class Circle : Shape { public double Radius { get; set; } }
    [TypeId(""rte-three.Square"")] public class Square : Shape { public double Side { get; set; } }
    [OptimizeAJson]
    public class ThreeHolder
    {
        [JsonRuntimeTypeEval] public Shape First { get; set; }
        [JsonRuntimeTypeEval] public Shape Second { get; set; }
        [JsonRuntimeTypeEval] public object Third { get; set; }
    }
}", "RoundTrip_ThreeRuntimeTypeEval");

            object circle = Create(fixture, "TestNs.Circle");
            Set(circle, "Radius", 1.5);
            object square = Create(fixture, "TestNs.Square");
            Set(square, "Side", 4.0);
            object holder = Create(fixture, "TestNs.ThreeHolder");
            Set(holder, "First", circle);
            Set(holder, "Second", square);
            Set(holder, "Third", 7);

            object readBack = RoundTrip(holder);

            object? first = Get(readBack, "First");
            object? second = Get(readBack, "Second");
            Assert.AreEqual("TestNs.Circle", first?.GetType().FullName);
            Assert.AreEqual(1.5, Get(first!, "Radius"));
            Assert.AreEqual("TestNs.Square", second?.GetType().FullName);
            Assert.AreEqual(4.0, Get(second!, "Side"));
            Assert.AreEqual(7, Get(readBack, "Third"));
        }

        [TestMethod]
        public void GeneratedReader_RoundTrips_InitOnlyProperties ()
        {
            Assembly fixture = CompileAndLoad(kInitOnlySource, "RoundTrip_InitOnly");

            object source = Create(fixture, "TestNs.InitOnly");
            Set(source, "Count", 3);
            Set(source, "Label", "three");
            Set(source, "Ratio", 1.5);

            object readBack = RoundTrip(source);

            Assert.AreEqual(3, Get(readBack, "Count"));
            Assert.AreEqual("three", Get(readBack, "Label"));
            Assert.AreEqual(1.5, Get(readBack, "Ratio"));
        }

        [TestMethod]
        public void GeneratedReader_InitOnlyPropertyMissingFromJson_KeepsItsInitializer ()
        {
            // Same as the reflection path: a key the json does not have leaves the property as construction left it
            Assembly fixture = CompileAndLoad(kInitOnlySource, "RoundTrip_InitOnlyMissingKey");
            Type type = fixture.GetType("TestNs.InitOnly", throwOnError: true)!;
            Assert.IsTrue(AJsonGeneratedDispatch.TryGet(type, out _));

            Json parsed = JsonHelper.ParseText("{ \"Count\": 4 }");
            object readBack = JsonHelper.BuildObjectForJson(type, parsed);
            Assert.IsFalse(parsed.HasErrors, parsed.GetErrorReport());

            Assert.AreEqual(4, Get(readBack, "Count"));
            Assert.AreEqual("unset", Get(readBack, "Label"));
            Assert.AreEqual(0.0, Get(readBack, "Ratio"));
        }

        [TestMethod]
        public void GeneratedReader_RoundTrips_PositionalRecord ()
        {
            Assembly fixture = CompileAndLoad(@"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public record Pair (int Left, string Right)
    {
        public Pair () : this(0, null) { }
        public double Extra { get; set; }
    }
}", "RoundTrip_PositionalRecord");

            Type type = fixture.GetType("TestNs.Pair", throwOnError: true)!;
            object source = Activator.CreateInstance(type, 5, "five")!;
            Set(source, "Extra", 0.25);

            object readBack = RoundTrip(source);

            Assert.AreEqual(source, readBack, "a record compares by value, so this checks every property");
        }

        [TestMethod]
        public void GeneratedReader_RoundTrips_ReadonlyRecordStruct ()
        {
            Assembly fixture = CompileAndLoad(@"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public readonly record struct Point (double X, double Y);
}", "RoundTrip_ReadonlyRecordStruct");

            Type type = fixture.GetType("TestNs.Point", throwOnError: true)!;
            object source = Activator.CreateInstance(type, 1.25, -3.5)!;

            object readBack = RoundTrip(source);

            Assert.AreEqual(source, readBack);
        }

        [TestMethod]
        public void GeneratedReader_RoundTrips_AJsonConstructorWithGetOnlyProperties ()
        {
            Assembly fixture = CompileAndLoad(@"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public class Sized
    {
        [AJsonConstructor]
        public Sized (string name, int SIZE)
        {
            Name = name;
            Size = SIZE;
        }

        public string Name { get; }
        [JsonPropertyAlias(""sz"")] public int Size { get; }
        public double Ratio { get; set; }
    }
}", "RoundTrip_AJsonConstructorGetOnly");

            Type type = fixture.GetType("TestNs.Sized", throwOnError: true)!;
            object source = Activator.CreateInstance(type, "box", 12)!;
            Set(source, "Ratio", 0.5);

            Json written = JsonHelper.BuildJsonForObject(source);
            CollectionAssert.Contains(((JsonDocument)written.Data).AllKeys().ToList(), "sz", "the parameter reads its property's aliased key");
            object readBack = RoundTrip(source);

            Assert.AreEqual("box", Get(readBack, "Name"));
            Assert.AreEqual(12, Get(readBack, "Size"));
            Assert.AreEqual(0.5, Get(readBack, "Ratio"));
        }

        [TestMethod]
        public void GeneratedReader_AJsonConstructorWithInitOnlyProperties_ConstructsOnce ()
        {
            Assembly fixture = CompileAndLoad(@"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public class Counted
    {
        public static int g_constructionCount;

        [AJsonConstructor]
        public Counted (int id)
        {
            ++g_constructionCount;
            Id = id;
        }

        public int Id { get; }
        public string Label { get; init; } = ""unset"";
        public string Note { get; init; }
        public double Ratio { get; set; }
    }
}", "RoundTrip_AJsonConstructorInitOnly");
            Type type = fixture.GetType("TestNs.Counted", throwOnError: true)!;
            FieldInfo constructionCount = type.GetField("g_constructionCount")!;
            Assert.IsTrue(AJsonGeneratedDispatch.TryGet(type, out _));

            constructionCount.SetValue(null, 0);
            Json full = JsonHelper.ParseText("{ \"Id\": 4, \"Label\": \"four\", \"Note\": \"noted\", \"Ratio\": 1.5 }");
            object read = JsonHelper.BuildObjectForJson(type, full);
            Assert.IsFalse(full.HasErrors, full.GetErrorReport());
            Assert.AreEqual(1, constructionCount.GetValue(null));
            Assert.AreEqual(4, Get(read, "Id"));
            Assert.AreEqual("four", Get(read, "Label"));
            Assert.AreEqual("noted", Get(read, "Note"));
            Assert.AreEqual(1.5, Get(read, "Ratio"));

            constructionCount.SetValue(null, 0);
            Json partial = JsonHelper.ParseText("{ \"Id\": 2, \"Note\": \"only\" }");
            read = JsonHelper.BuildObjectForJson(type, partial);
            Assert.IsFalse(partial.HasErrors, partial.GetErrorReport());
            Assert.AreEqual(1, constructionCount.GetValue(null));
            Assert.AreEqual("unset", Get(read, "Label"), "a missing key keeps the initializer");
            Assert.AreEqual("only", Get(read, "Note"));
        }

        [TestMethod]
        public void GeneratedReader_RoundTrips_PositionalRecordWithNoParameterlessConstructor ()
        {
            Assembly fixture = CompileAndLoad(@"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public record Pair (int Left, string Right)
    {
        public string Note { get; init; } = ""unset"";
        public double Extra { get; set; }
    }
}", "RoundTrip_PositionalRecordNoParameterless");

            Type type = fixture.GetType("TestNs.Pair", throwOnError: true)!;
            object source = Activator.CreateInstance(type, 5, "five")!;
            Set(source, "Note", "noted");
            Set(source, "Extra", 0.25);

            Assert.AreEqual(source, RoundTrip(source), "a record compares by value, so this checks every property");
        }

        [TestMethod]
        public void GeneratedReader_RoundTrips_PositionalRecordFromAReferencedAssembly ()
        {
            // A record the consumer only sees as metadata has no syntax to find its primary constructor by, so it is the one public
            //  constructor, other than the copy constructor, whose every parameter matches a property
            byte[] libraryImage = TestCompilation.EmitToImage(TestCompilation.Build(@"
namespace ForeignNs
{
    public class Marker { }
    public record ForeignPair (int Left, string Right)
    {
        public string Note { get; init; } = ""unset"";
    }
}", "RoundTrip_ForeignRecordLibrary"));

            CSharpCompilation consumer = TestCompilation.Build(@"
using AJut.Text.AJson;
[assembly: OptimizeAJson(typeof(ForeignNs.Marker))]
namespace ConsumerNs { public class Unrelated { } }", "RoundTrip_ForeignRecordConsumer", MetadataReference.CreateFromImage(libraryImage));

            FixtureLoadContext context = new FixtureLoadContext();
            Assembly library = FixtureAssemblies.Load(libraryImage, context);
            FixtureAssemblies.Load(FixtureAssemblies.CompileWithGeneratedSerializers(consumer), context);

            Type type = library.GetType("ForeignNs.ForeignPair", throwOnError: true)!;
            object source = Activator.CreateInstance(type, 6, "six")!;
            Set(source, "Note", "noted");

            Assert.AreEqual(source, RoundTrip(source));
        }

        [TestMethod]
        public void GeneratedReader_RoundTrips_StructsWithInitOnlyProperties ()
        {
            // The accessor takes a struct by ref, so the setter writes into the reader's copy
            Assembly fixture = CompileAndLoad(@"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public struct Point
    {
        public int X { get; init; }
        public string Label { get; init; }
        public double Z { get; set; }
    }

    [OptimizeAJson]
    public struct Span
    {
        [AJsonConstructor] public Span (int start) { Start = start; Length = -1; }
        public int Start { get; }
        public int Length { get; init; }
    }
}", "RoundTrip_StructInitOnly");

            object point = Create(fixture, "TestNs.Point");
            Set(point, "X", 3);
            Set(point, "Label", "three");
            Set(point, "Z", 1.5);
            object pointBack = RoundTrip(point);
            Assert.AreEqual(3, Get(pointBack, "X"));
            Assert.AreEqual("three", Get(pointBack, "Label"));
            Assert.AreEqual(1.5, Get(pointBack, "Z"));

            Type spanType = fixture.GetType("TestNs.Span", throwOnError: true)!;
            object span = Activator.CreateInstance(spanType, 10)!;
            Set(span, "Length", 4);
            object spanBack = RoundTrip(span);
            Assert.AreEqual(10, Get(spanBack, "Start"));
            Assert.AreEqual(4, Get(spanBack, "Length"));
        }

        [TestMethod]
        public void GeneratedReader_SetsAnInitOnlyPropertyInheritedFromABaseClass ()
        {
            // The accessor targets the base class, which is where the setter is declared
            Assembly fixture = CompileAndLoad(@"
using AJut.Text.AJson;
namespace TestNs
{
    public class Named { public string Name { get; init; } }

    [OptimizeAJson]
    public class Derived : Named { public int Count { get; set; } }
}", "RoundTrip_InheritedInitOnly");

            object source = Create(fixture, "TestNs.Derived");
            Set(source, "Name", "inherited");
            Set(source, "Count", 2);
            object readBack = RoundTrip(source);

            Assert.AreEqual("inherited", Get(readBack, "Name"));
            Assert.AreEqual(2, Get(readBack, "Count"));
        }

        [TestMethod]
        public void GeneratedReader_ConstructorParameterReadsARuntimeTypeEvalProperty ()
        {
            Assembly fixture = CompileAndLoad(@"
using AJut.Text.AJson;
using AJut.TypeManagement;
namespace TestNs
{
    public abstract class Shape { }
    [TypeId(""ctor-rte.Circle"")] public class Circle : Shape { public double Radius { get; set; } }

    [OptimizeAJson]
    public class Holder
    {
        [AJsonConstructor] public Holder (Shape thing) { Thing = thing; }
        [JsonRuntimeTypeEval] public Shape Thing { get; }
    }
}", "RoundTrip_ConstructorRuntimeTypeEval");

            object circle = Create(fixture, "TestNs.Circle");
            Set(circle, "Radius", 2.5);
            object holder = Activator.CreateInstance(fixture.GetType("TestNs.Holder", throwOnError: true)!, circle)!;
            object readBack = RoundTrip(holder);

            object? thing = Get(readBack, "Thing");
            Assert.AreEqual("TestNs.Circle", thing?.GetType().FullName);
            Assert.AreEqual(2.5, Get(thing!, "Radius"));
        }

        [TestMethod]
        public void GeneratedReader_MissingConstructorKey_TakesOmitValue_ThenDeclaredDefault_ThenTypeDefault ()
        {
            Assembly fixture = CompileAndLoad(kMissingKeysSource, "RoundTrip_MissingConstructorKeys");
            Type type = fixture.GetType("TestNs.MissingKeys", throwOnError: true)!;
            Assert.IsTrue(AJsonGeneratedDispatch.TryGet(type, out _));

            Json parsed = JsonHelper.ParseText("{ \"unmatched\": \"never read\" }");
            object read = JsonHelper.BuildObjectForJson(type, parsed);
            Assert.IsFalse(parsed.HasErrors, parsed.GetErrorReport());

            Assert.AreEqual(5, Get(read, "Omitted"), "the omit value");
            Assert.AreEqual("Busy", Get(read, "Mode")!.ToString(), "the omit value, an enum stored as its number");
            Assert.AreEqual(2.5f, Get(read, "Scale"), "the omit value, a float");
            Assert.AreEqual(7, Get(read, "Declared"), "the declared default");
            Assert.AreEqual(9, Get(read, "OmittedOverDeclared"), "the omit value wins over a declared default");
            Assert.AreEqual(0, Get(read, "BareOmitted"), "a bare omit attribute's type default wins over a declared default");
            Assert.AreEqual(0, Get(read, "Neither"), "the type default");
            Assert.AreEqual("fallback", Get(read, "Stored"), "a parameter that matches no property always gets its missing value");
        }

        [TestMethod]
        public void GeneratedReader_ConstructorValuesTheWriterLeftOut_ReadBackTheSame ()
        {
            Assembly fixture = CompileAndLoad(kMissingKeysSource, "RoundTrip_ConstructorOmittedValues");
            Type type = fixture.GetType("TestNs.MissingKeys", throwOnError: true)!;
            Type modeType = fixture.GetType("TestNs.eMode", throwOnError: true)!;
            object source = Activator.CreateInstance(type, 5, 2, Enum.ToObject(modeType, 1), 2.5f, 1, 9, 0, "ignored")!;

            Json written = JsonHelper.BuildJsonForObject(source);
            CollectionAssert.AreEquivalent(new[] { "Neither", "Declared", "Stored" }, ((JsonDocument)written.Data).AllKeys().ToArray(), written.ToString());

            object readBack = RoundTrip(source);
            Assert.AreEqual(5, Get(readBack, "Omitted"));
            Assert.AreEqual("Busy", Get(readBack, "Mode")!.ToString());
            Assert.AreEqual(2.5f, Get(readBack, "Scale"));
            Assert.AreEqual(9, Get(readBack, "OmittedOverDeclared"));
            Assert.AreEqual(0, Get(readBack, "BareOmitted"), "zero comes back as zero, not the declared default of 4");
            Assert.AreEqual(2, Get(readBack, "Neither"));
            Assert.AreEqual(1, Get(readBack, "Declared"));
        }

        [TestMethod]
        public void GeneratedReader_RegisteredCustomConstructor_WinsOnATypeWithAParameterlessConstructor ()
        {
            // The generated reader runs before ConstructInstanceFor would, so it has to ask for a registered constructor itself
            Assembly fixture = CompileAndLoad(@"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public class Plain
    {
        public string Origin { get; set; } = ""parameterless"";
        public int Count { get; set; }
    }
}", "RoundTrip_CustomConstructorParameterless");
            Type type = fixture.GetType("TestNs.Plain", throwOnError: true)!;
            Assert.IsTrue(AJsonGeneratedDispatch.TryGet(type, out _));

            JsonInterpreterSettings settings = new JsonInterpreterSettings();
            settings.Add(type, _BuildCustom);

            Json parsed = JsonHelper.ParseText("{ \"Count\": 3 }");
            object read = JsonHelper.BuildObjectForJson(type, parsed, settings);

            Assert.IsFalse(parsed.HasErrors, parsed.GetErrorReport());
            Assert.AreEqual("custom", Get(read, "Origin"));
            Assert.AreEqual(3, Get(read, "Count"));

            static object _BuildCustom (Type _type, JsonValue _value, JsonInterpreterSettings _settings, Json _owner)
            {
                object made = Activator.CreateInstance(_type)!;
                Set(made, "Origin", "custom");
                return made;
            }
        }

        [TestMethod]
        public void GeneratedReader_RegisteredCustomConstructor_WinsOverAnAJsonConstructor ()
        {
            // Nothing is consumed by a constructor then: an init-only property the route would have passed in is set through its
            //  accessor, a settable one is assigned, and a get-only one keeps what the registered constructor gave it
            Assembly fixture = CompileAndLoad(@"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public class Marked
    {
        public static int g_markedConstructorCalls;

        private Marked () { Count = -1; }

        [AJsonConstructor]
        public Marked (int count, string label, string note)
        {
            ++g_markedConstructorCalls;
            Count = count;
            Label = label;
            Note = note;
        }

        public int Count { get; }
        public string Label { get; init; }
        public string Note { get; set; }
        public string Tag { get; init; }
    }
}", "RoundTrip_CustomConstructorOverAJsonConstructor");
            Type type = fixture.GetType("TestNs.Marked", throwOnError: true)!;
            FieldInfo markedCalls = type.GetField("g_markedConstructorCalls")!;
            Assert.IsTrue(AJsonGeneratedDispatch.TryGet(type, out _));

            JsonInterpreterSettings settings = new JsonInterpreterSettings();
            settings.Add(type, _BuildThroughPrivateConstructor);

            markedCalls.SetValue(null, 0);
            Json parsed = JsonHelper.ParseText("{ \"Count\": 8, \"Label\": \"labeled\", \"Note\": \"noted\", \"Tag\": \"tagged\" }");
            object read = JsonHelper.BuildObjectForJson(type, parsed, settings);

            Assert.IsFalse(parsed.HasErrors, parsed.GetErrorReport());
            Assert.AreEqual(0, markedCalls.GetValue(null), "the marked constructor is never used");
            Assert.AreEqual(-1, Get(read, "Count"));
            Assert.AreEqual("labeled", Get(read, "Label"));
            Assert.AreEqual("noted", Get(read, "Note"));
            Assert.AreEqual("tagged", Get(read, "Tag"));

            static object _BuildThroughPrivateConstructor (Type _type, JsonValue _value, JsonInterpreterSettings _settings, Json _owner)
            {
                return Activator.CreateInstance(_type, nonPublic: true)!;
            }
        }

        [TestMethod]
        public void GeneratedReader_RoundTrips_PropertyAsSelfWithInitOnlyInner ()
        {
            Assembly fixture = CompileAndLoad(@"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    [JsonPropertyAsSelf(""Inner"")]
    public class InitElevator { public int Inner { get; init; } }

    [OptimizeAJson]
    public class ElevatorHolder { public InitElevator Elevator { get; set; } }
}", "RoundTrip_PropertyAsSelfInitOnly");

            object elevator = Create(fixture, "TestNs.InitElevator");
            Set(elevator, "Inner", 7);
            object holder = Create(fixture, "TestNs.ElevatorHolder");
            Set(holder, "Elevator", elevator);

            object readBack = RoundTrip(holder);

            object? readElevator = Get(readBack, "Elevator");
            Assert.IsNotNull(readElevator);
            Assert.AreEqual(7, Get(readElevator!, "Inner"));
        }

        [TestMethod]
        public void GeneratedReader_RoundTrips_TypesFromAReferencedAssembly ()
        {
            // The assembly-wide opt-in can name a marker in another assembly. The consumer then carries generated serializers for
            //  types it only sees as metadata, including skipping that assembly's static classes (AJU-1).
            byte[] libraryImage = TestCompilation.EmitToImage(TestCompilation.Build(@"
namespace ForeignNs
{
    public class Marker { }
    public class Payload
    {
        public int Count { get; set; }
        public string Label { get; init; } = ""unset"";
    }
    public static class Helpers { public static int Twice (int x) => x * 2; }
}", "RoundTrip_ForeignLibrary"));

            CSharpCompilation consumer = TestCompilation.Build(@"
using AJut.Text.AJson;
[assembly: OptimizeAJson(typeof(ForeignNs.Marker))]
namespace ConsumerNs { public class Unrelated { } }", "RoundTrip_ForeignConsumer", MetadataReference.CreateFromImage(libraryImage));

            FixtureLoadContext context = new FixtureLoadContext();
            Assembly library = FixtureAssemblies.Load(libraryImage, context);
            FixtureAssemblies.Load(FixtureAssemblies.CompileWithGeneratedSerializers(consumer), context);

            object payload = Create(library, "ForeignNs.Payload");
            Set(payload, "Count", 4);
            Set(payload, "Label", "four");

            object readBack = RoundTrip(payload);

            Assert.AreEqual(4, Get(readBack, "Count"));
            Assert.AreEqual("four", Get(readBack, "Label"));
        }

        [TestMethod]
        public void GeneratedWriter_OmitsNumericDefaultsOfEachWidth ()
        {
            Assembly fixture = CompileAndLoad(@"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public class Numbers
    {
        [JsonOmitIfDefault(2.5)] public double Ratio { get; set; }
        [JsonOmitIfDefault(0.1f)] public float Scale { get; set; }
        [JsonOmitIfDefault(5000000000L)] public long Big { get; set; }
        [JsonOmitIfDefault(7u)] public uint Unsigned { get; set; }
    }
}", "RoundTrip_NumericOmitDefaults");

            object atDefaults = Create(fixture, "TestNs.Numbers");
            Set(atDefaults, "Ratio", 2.5);
            Set(atDefaults, "Scale", 0.1f);
            Set(atDefaults, "Big", 5000000000L);
            Set(atDefaults, "Unsigned", 7u);
            Assert.IsTrue(AJsonGeneratedDispatch.TryGet(atDefaults.GetType(), out _));

            Json written = JsonHelper.BuildJsonForObject(atDefaults);
            Assert.IsFalse(written.HasErrors, written.GetErrorReport());
            Assert.AreEqual(0, ((JsonDocument)written.Data).AllKeys().Count(), $"every value sits at its default, so nothing should be written: {written}");

            Set(atDefaults, "Scale", 0.2f);
            written = JsonHelper.BuildJsonForObject(atDefaults);
            CollectionAssert.AreEqual(new[] { "Scale" }, ((JsonDocument)written.Data).AllKeys().ToArray(), written.ToString());
        }

        [TestMethod]
        public void GeneratedWriter_OmitsAFloatAtADoubleOmitValue ()
        {
            Assembly fixture = CompileAndLoad(@"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public class Scaled { [JsonOmitIfDefault(2.5)] public float Scale { get; set; } }
}", "RoundTrip_DoubleOmitValueOnFloat");

            object atDefault = Create(fixture, "TestNs.Scaled");
            Set(atDefault, "Scale", 2.5f);
            Json written = JsonHelper.BuildJsonForObject(atDefault);
            Assert.AreEqual(0, ((JsonDocument)written.Data).AllKeys().Count(), written.ToString());

            Set(atDefault, "Scale", 3f);
            written = JsonHelper.BuildJsonForObject(atDefault);
            CollectionAssert.AreEqual(new[] { "Scale" }, ((JsonDocument)written.Data).AllKeys().ToArray(), written.ToString());
        }

        [TestMethod]
        public void GeneratedReader_NestedReadError_ReachesTheOwningJson ()
        {
            // A json array cannot be read into a class that is not a collection. The nested read reports that to the Json the
            //  outer read was handed, and from inside a generated reader it has to get there too.
            Assembly fixture = CompileAndLoad(@"
using AJut.Text.AJson;
namespace TestNs
{
    public class Inner { public int A { get; set; } }
    [OptimizeAJson] public class Outer { public Inner Child { get; set; } }
}", "RoundTrip_NestedReadError");
            Type type = fixture.GetType("TestNs.Outer", throwOnError: true)!;
            Assert.IsTrue(AJsonGeneratedDispatch.TryGet(type, out _));

            Json parsed = JsonHelper.ParseText("{ \"Child\": [1, 2] }");
            Assert.IsFalse(parsed.HasErrors, parsed.GetErrorReport());
            JsonHelper.BuildObjectForJson(type, parsed);

            Assert.IsTrue(parsed.HasErrors, "the nested read's error was dropped instead of reaching the owning Json");
            StringAssert.Contains(parsed.GetErrorReport(), "Cannot interpret a json array as target type");
        }

        [TestMethod]
        public void GeneratedReader_PropertyAsSelfNestedReadError_ReachesTheOwningJson ()
        {
            // JsonHelper handles [JsonPropertyAsSelf] itself before it looks for a generated reader, so this calls the generated
            //  reader directly
            Assembly fixture = CompileAndLoad(@"
using AJut.Text.AJson;
namespace TestNs
{
    public class Inner { public int A { get; set; } }
    [OptimizeAJson]
    [JsonPropertyAsSelf(""Content"")]
    public class Wrapper { public Inner Content { get; set; } }
}", "RoundTrip_PropertyAsSelfNestedReadError");
            Type type = fixture.GetType("TestNs.Wrapper", throwOnError: true)!;
            Assert.IsTrue(AJsonGeneratedDispatch.TryGet(type, out AJsonGeneratedSerializer serializer));

            Json parsed = JsonHelper.ParseText("[1, 2]");
            Assert.IsFalse(parsed.HasErrors, parsed.GetErrorReport());
            serializer.Reader(parsed.Data, null, parsed);

            Assert.IsTrue(parsed.HasErrors, "the nested read's error was dropped instead of reaching the owning Json");
            StringAssert.Contains(parsed.GetErrorReport(), "Cannot interpret a json array as target type");
        }

        // ===========================[ Helpers ]===========================

        private static Assembly CompileAndLoad (string source, string assemblyName) => FixtureAssemblies.LoadWithGeneratedSerializers(source, assemblyName);

        /// <summary>
        /// Writes <paramref name="source"/> to json text and reads it back as the same type, through the generated serializer
        /// </summary>
        private static object RoundTrip (object source)
        {
            Type type = source.GetType();
            Assert.IsTrue(AJsonGeneratedDispatch.TryGet(type, out _), $"No generated serializer is registered for {type.Name}, so this would only test the reflection path");

            Json written = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(written.HasErrors, written.GetErrorReport());

            Json parsed = JsonHelper.ParseText(written.ToString());
            Assert.IsFalse(parsed.HasErrors, parsed.GetErrorReport());

            object readBack = JsonHelper.BuildObjectForJson(type, parsed);
            Assert.IsFalse(parsed.HasErrors, parsed.GetErrorReport());
            Assert.IsNotNull(readBack, $"Reading {type.Name} back produced nothing. Json was: {written}");
            return readBack;
        }

        private static object Create (Assembly fixture, string typeName) => Activator.CreateInstance(fixture.GetType(typeName, throwOnError: true)!)!;
        private static void Set (object target, string property, object? value) => target.GetType().GetProperty(property)!.SetValue(target, value);
        private static object? Get (object target, string property) => target.GetType().GetProperty(property)!.GetValue(target);
    }
}
