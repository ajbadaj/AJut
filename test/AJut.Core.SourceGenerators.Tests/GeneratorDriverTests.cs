namespace AJut.Text.AJson.SourceGenerators.Tests
{
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tier-2 integration tests - run the actual incremental generator via CSharpGeneratorDriver
    /// and inspect the resulting output. Lighter than Tier-1 (covers the wiring, not the
    /// per-attribute logic) but proves the pipeline ties together end-to-end.
    /// </summary>
    [TestClass]
    public class GeneratorDriverTests
    {
        [TestMethod]
        public void Generator_EmitsHelperPerOptimizeAJsonType ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson] public class Foo { public int A { get; set; } }
    [OptimizeAJson] public class Bar { public string B { get; set; } }
    public class Plain { public int X { get; set; } }   // not opted in
}";
            GeneratorDriverRunResult result = RunGenerator(src);

            ImmutableArray<GeneratedSourceResult> generated = result.Results.Single().GeneratedSources;
            Assert.AreEqual(2, generated.Length, "Should emit one helper per opted-in type");
            Assert.IsTrue(generated.Any(g => g.HintName.Contains("Foo")));
            Assert.IsTrue(generated.Any(g => g.HintName.Contains("Bar")));
            Assert.IsFalse(generated.Any(g => g.HintName.Contains("Plain")));
        }

        [TestMethod]
        public void Generator_ReportsAJSON001_OnMissingCtor ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson] public class NoCtor { public NoCtor(int x) { Score = x; } public int Score { get; set; } }
}";
            GeneratorDriverRunResult result = RunGenerator(src);
            Assert.IsTrue(result.Diagnostics.Any(d => d.Id == "AJSON001"));
        }

        [TestMethod]
        public void Generator_ReportsNothing_ForInitOnlyPropertiesOnAClassWithOnlyAnAJsonConstructor ()
        {
            // The generated reader once had no way to set an init-only property on this shape. It now sets it through an
            //  UnsafeAccessor after the one construction (GeneratedRoundTripTests runs it).
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public class OnlyAJsonCtor
    {
        [AJsonConstructor] public OnlyAJsonCtor (int count) { Count = count; }
        public int Count { get; init; }
        public string Label { get; init; }
    }

    [OptimizeAJson]
    public record OnlyAJsonCtorRecord
    {
        [AJsonConstructor] public OnlyAJsonCtorRecord (int count) { Count = count; }
        public int Count { get; init; }
    }
}";
            GeneratorDriverRunResult result = RunGenerator(src);
            Assert.AreEqual(0, result.Diagnostics.Length, string.Join("\n", result.Diagnostics.Select(d => d.GetMessage())));
        }

        [TestMethod]
        public void Generator_ReportsAJSON001_OnlyWhenNoConstructorRouteExists ()
        {
            // A record's positional constructor is a route with no attribute. A record whose only constructor is an ordinary one
            //  is not positional, and neither is a class.
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson] public record Positional (int Left, string Right);

    [OptimizeAJson]
    public record NotPositional
    {
        public NotPositional (int count) { Count = count; }
        public int Count { get; }
    }

    [OptimizeAJson]
    public class NoCtorAtAll
    {
        public NoCtorAtAll (int count) { Count = count; }
        public int Count { get; init; }
    }
}";
            GeneratorDriverRunResult result = RunGenerator(src);
            string[] ajson001 = result.Diagnostics.Where(d => d.Id == "AJSON001").Select(d => d.GetMessage()).ToArray();
            Assert.AreEqual(2, ajson001.Length, string.Join("\n", ajson001));
            Assert.IsTrue(ajson001.Any(m => m.Contains("'NotPositional'")));
            Assert.IsTrue(ajson001.Any(m => m.Contains("'NoCtorAtAll'")));
        }

        [TestMethod]
        public void Generator_ReportsAJSON001_ForAnInternalAJsonConstructorInAReferencedAssembly ()
        {
            // The compiler only imports the public and protected members of a referenced assembly, so the generator never sees
            //  an internal or private constructor there, marked or not
            byte[] libraryImage = TestCompilation.EmitToImage(TestCompilation.Build(@"
using AJut.Text.AJson;
namespace ForeignNs
{
    public class Marker { }
    public class Sealed
    {
        [AJsonConstructor] internal Sealed (int count) { Count = count; }
        public int Count { get; }
    }
}", "Driver_ForeignInternalCtorLibrary"));

            CSharpCompilation consumer = TestCompilation.Build(@"
using AJut.Text.AJson;
[assembly: OptimizeAJson(typeof(ForeignNs.Marker))]
namespace ConsumerNs { public class Unrelated { } }", "Driver_ForeignInternalCtorConsumer", MetadataReference.CreateFromImage(libraryImage));

            GeneratorDriverRunResult result = CSharpGeneratorDriver.Create(new AJsonSourceGenerator()).RunGenerators(consumer).GetRunResult();
            Assert.IsTrue(result.Diagnostics.Any(d => d.Id == "AJSON001" && d.GetMessage().Contains("'Sealed'")));
        }

        [TestMethod]
        public void Generator_ReportsAJSON001_ForPropertyAsSelfWithNoParameterlessConstructor ()
        {
            // [JsonPropertyAsSelf] builds the type around the one property's value with a parameterless constructor, so a
            //  constructor route does not satisfy it, and the generated code should not leave a compile error of its own
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    [JsonPropertyAsSelf(""Value"")]
    public record Wrapped (int Value);

    [OptimizeAJson]
    [JsonPropertyAsSelf(""Value"")]
    public class MarkedWrapper
    {
        [AJsonConstructor] public MarkedWrapper (int value) { Value = value; }
        public int Value { get; set; }
    }

    [OptimizeAJson]
    [JsonPropertyAsSelf(""Value"")]
    public record WrappedWithParameterless (int Value)
    {
        public WrappedWithParameterless () : this(0) { }
    }
}";
            CSharpCompilation compilation = TestCompilation.Build(src);
            GeneratorDriverRunResult result = CSharpGeneratorDriver.Create(new AJsonSourceGenerator()).RunGenerators(compilation).GetRunResult();

            string[] ajson001 = result.Diagnostics.Where(d => d.Id == "AJSON001").Select(d => d.GetMessage()).ToArray();
            Assert.AreEqual(2, ajson001.Length, string.Join("\n", ajson001));
            Assert.IsTrue(ajson001.Any(m => m.Contains("'Wrapped'") && m.Contains("[JsonPropertyAsSelf]")));
            Assert.IsTrue(ajson001.Any(m => m.Contains("'MarkedWrapper'") && m.Contains("[JsonPropertyAsSelf]")));

            Diagnostic[] compileErrors = compilation.AddSyntaxTrees(result.GeneratedTrees).GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            Assert.AreEqual(0, compileErrors.Length, string.Join("\n", compileErrors.Select(d => $"{d.Id}: {d.GetMessage()}")));
        }

        [TestMethod]
        public void Generator_ReportsAJSON005_OnMoreThanOneAJsonConstructor ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public class TwoMarked
    {
        [AJsonConstructor] public TwoMarked (int count) { }
        [AJsonConstructor] public TwoMarked (string count) { }
        public int Count { get; set; }
    }

    [OptimizeAJson]
    public struct TwoMarkedStruct
    {
        [AJsonConstructor] public TwoMarkedStruct (int count) { Count = count; }
        [AJsonConstructor] public TwoMarkedStruct (long count) { Count = (int)count; }
        public int Count { get; }
    }
}";
            GeneratorDriverRunResult result = RunGenerator(src);
            Diagnostic[] ajson005 = result.Diagnostics.Where(d => d.Id == "AJSON005").ToArray();
            Assert.AreEqual(2, ajson005.Length);
            Assert.IsTrue(ajson005.All(d => d.Severity == DiagnosticSeverity.Error));
            Assert.IsFalse(result.Diagnostics.Any(d => d.Id == "AJSON001"), "AJSON005 already says what is wrong");
        }

        [TestMethod]
        public void Generator_ReportsAJSON006_OnAParameterThatMatchesNoProperty ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public class Unmatched
    {
        [AJsonConstructor] public Unmatched (int COUNT, string stray = ""x"") { Count = COUNT; }
        public int Count { get; }
    }
}";
            GeneratorDriverRunResult result = RunGenerator(src);
            Diagnostic[] ajson006 = result.Diagnostics.Where(d => d.Id == "AJSON006").ToArray();
            Assert.AreEqual(1, ajson006.Length, "COUNT matches Count, ignoring case; stray matches nothing");
            Assert.AreEqual(DiagnosticSeverity.Warning, ajson006[0].Severity);
            StringAssert.Contains(ajson006[0].GetMessage(), "'stray'");
        }

        [TestMethod]
        public void Generator_ReportsAJSON007_WhenADeclaredDefaultDiffersFromTheOmitValue ()
        {
            // Values that agree once they are the parameter's type are not a mismatch: 5 and 5L for a long, 0.1 and 0.1f for a float.
            //  A bare [JsonOmitIfDefault] leaves the property out at the type's default, so a declared default of anything else is
            //  a mismatch too.
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    public enum eMode { Calm, Busy }

    [OptimizeAJson]
    public class Defaults
    {
        [AJsonConstructor]
        public Defaults (int differs = 3, long sameWider = 5, float sameFloat = 0.1f, eMode sameEnum = eMode.Busy, int bareDiffers = 2, int bareSame = 0, string bareText = null)
        {
            Differs = differs;
            SameWider = sameWider;
            SameFloat = sameFloat;
            SameEnum = sameEnum;
            BareDiffers = bareDiffers;
            BareSame = bareSame;
            BareText = bareText;
        }

        [JsonOmitIfDefault(9)] public int Differs { get; }
        [JsonOmitIfDefault(5)] public long SameWider { get; }
        [JsonOmitIfDefault(0.1)] public float SameFloat { get; }
        [JsonOmitIfDefault(eMode.Busy)] public eMode SameEnum { get; }
        [JsonOmitIfDefault] public int BareDiffers { get; }
        [JsonOmitIfDefault] public int BareSame { get; }
        [JsonOmitIfDefault] public string BareText { get; }
    }
}";
            GeneratorDriverRunResult result = RunGenerator(src);
            Diagnostic[] ajson007 = result.Diagnostics.Where(d => d.Id == "AJSON007").ToArray();
            Assert.AreEqual(2, ajson007.Length, string.Join("\n", ajson007.Select(d => d.GetMessage())));
            Assert.IsTrue(ajson007.All(d => d.Severity == DiagnosticSeverity.Warning));
            Assert.IsTrue(ajson007.Any(d => d.GetMessage().Contains("'differs'") && d.GetMessage().Contains("[JsonOmitIfDefault(9)]")));
            Assert.IsTrue(ajson007.Any(d => d.GetMessage().Contains("'bareDiffers'") && d.GetMessage().Contains("default(int)")));
        }

        [TestMethod]
        public void Generator_GeneratedCodeContainsModuleInitializer ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson] public class Foo { public int A { get; set; } }
}";
            GeneratorDriverRunResult result = RunGenerator(src);
            string emitted = result.Results.Single().GeneratedSources.Single().SourceText.ToString();

            StringAssert.Contains(emitted, "[ModuleInitializer]");
            StringAssert.Contains(emitted, "AJsonGeneratedDispatch.Register");
        }

        [TestMethod]
        public void Generator_HonorsAssemblyLevelOptIn ()
        {
            const string src = @"
using AJut.Text.AJson;
[assembly: OptimizeAJson(typeof(TestNs.Marker))]
namespace TestNs
{
    public class Marker { }
    public class Foo { public int A { get; set; } }
    public class Bar { public string B { get; set; } }
}";
            GeneratorDriverRunResult result = RunGenerator(src);

            ImmutableArray<GeneratedSourceResult> generated = result.Results.Single().GeneratedSources;
            // Marker, Foo, Bar all qualify (public, non-abstract, class). Plus everything else
            // public in the System assemblies that got pulled in by the test compilation, but
            // we only assert on what should be there.
            Assert.IsTrue(generated.Any(g => g.HintName.Contains("Foo")));
            Assert.IsTrue(generated.Any(g => g.HintName.Contains("Bar")));
            Assert.IsTrue(generated.Any(g => g.HintName.Contains("Marker")));
        }

        [TestMethod]
        public void Generator_AssemblyLevelOptIn_RegistersPublicEnumsByFullName ()
        {
            const string src = @"
using AJut.Text.AJson;
[assembly: OptimizeAJson(typeof(TestNs.Marker))]
namespace TestNs
{
    public class Marker { }
    public enum eColor { Red, Green, Blue }
    public enum eShape { Round, Square }
}";
            GeneratorDriverRunResult result = RunGenerator(src);

            ImmutableArray<GeneratedSourceResult> generated = result.Results.Single().GeneratedSources;
            GeneratedSourceResult[] enumRegs = generated.Where(g => g.HintName.Contains("EnumTypeIdRegistrations")).ToArray();
            Assert.AreEqual(1, enumRegs.Length, "Expected exactly one enum registration file.");

            string emitted = enumRegs[0].SourceText.ToString();
            StringAssert.Contains(emitted, "[ModuleInitializer]");
            StringAssert.Contains(emitted, "TypeIdRegistrar.RegisterTypeId");
            StringAssert.Contains(emitted, "typeof(global::TestNs.eColor).FullName");
            StringAssert.Contains(emitted, "typeof(global::TestNs.eShape).FullName");
        }

        [TestMethod]
        public void Generator_PerTypeOptInOnly_EmitsNoEnumRegistrations ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson] public class Foo { public int A { get; set; } }
    public enum eColor { Red, Green, Blue }   // no assembly-level marker, so never registered
}";
            GeneratorDriverRunResult result = RunGenerator(src);

            ImmutableArray<GeneratedSourceResult> generated = result.Results.Single().GeneratedSources;
            Assert.IsFalse(
                generated.Any(g => g.HintName.Contains("EnumTypeIdRegistrations")),
                "Enums must only be registered via the assembly-level opt-in, never from a bare enum."
            );
        }

        [TestMethod]
        public void Generator_AssemblyLevelOptIn_SkipsStaticClasses ()
        {
            // The assembly-wide opt-in takes every public type, and a static class can never satisfy AJSON001's constructor check,
            //  so one static helper in an opted-in assembly used to fail the whole build (AJU-1)
            const string src = @"
using AJut.Text.AJson;
[assembly: OptimizeAJson(typeof(TestNs.Marker))]
namespace TestNs
{
    public class Marker { }
    public class Foo { public int A { get; set; } }
    public static class Helpers { public static int Twice (int x) => x * 2; }
}";
            GeneratorDriverRunResult result = RunGenerator(src);

            Diagnostic[] ajson001 = result.Diagnostics.Where(d => d.Id == "AJSON001").ToArray();
            Assert.AreEqual(0, ajson001.Length, string.Join("\n", ajson001.Select(d => d.GetMessage())));

            ImmutableArray<GeneratedSourceResult> generated = result.Results.Single().GeneratedSources;
            Assert.IsFalse(generated.Any(g => g.HintName.Contains("Helpers")));
            Assert.IsTrue(generated.Any(g => g.HintName.Contains("Foo")));
        }

        [TestMethod]
        public void Generator_AssemblyLevelOptIn_SkipsTypesNestedInInternalOrGeneric ()
        {
            const string src = @"
using AJut.Text.AJson;
[assembly: OptimizeAJson(typeof(TestNs.Marker))]
namespace TestNs
{
    public class Marker { }
    internal class Hidden { public class Reachable { public int A { get; set; } } }   // public, but nested in internal
    public class Outer<T> { public class Inner { public int A { get; set; } } }        // nested in a generic
}";
            GeneratorDriverRunResult result = RunGenerator(src);

            ImmutableArray<GeneratedSourceResult> generated = result.Results.Single().GeneratedSources;
            Assert.IsFalse(generated.Any(g => g.HintName.Contains("Reachable")), "public-nested-in-internal is not reachable from another assembly - emitting typeof(...) for it would not compile");
            Assert.IsFalse(generated.Any(g => g.HintName.Contains("Inner")), "a type nested in a generic has no single typeof(...) form");
            Assert.IsTrue(generated.Any(g => g.HintName.Contains("Marker")), "top-level public types are still emitted");
        }

        // ===========================[ Helpers ]===========================
        private static GeneratorDriverRunResult RunGenerator (string source)
        {
            CSharpCompilation compilation = TestCompilation.Build(source);
            AJsonSourceGenerator generator = new AJsonSourceGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGenerators(compilation);
            return driver.GetRunResult();
        }
    }
}
