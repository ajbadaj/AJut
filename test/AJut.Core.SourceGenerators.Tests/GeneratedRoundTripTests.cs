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
