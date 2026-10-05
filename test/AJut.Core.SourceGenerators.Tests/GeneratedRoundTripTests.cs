namespace AJut.Text.AJson.SourceGenerators.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using AJut.Text.AJson;
    using AJut.TypeManagement;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Emit;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Runs the generated serializers for real. Each fixture is compiled together with the generator's output, loaded into the
    /// test process, and pushed through JsonHelper, which dispatches to the generated Write and Read. EmitCompileTests proves the
    /// output compiles; these prove it reads back what it wrote.
    /// </summary>
    [TestClass]
    public class GeneratedRoundTripTests
    {
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

        // ===========================[ Helpers ]===========================

        /// <summary>
        /// Compiles the fixture with the generator's output and loads it. Loading runs the module constructor, which is where
        /// the generated [ModuleInitializer] registers each serializer with AJsonGeneratedDispatch.
        /// </summary>
        private static Assembly CompileAndLoad (string source, string assemblyName)
        {
            CSharpCompilation compilation = TestCompilation.Build(source, assemblyName);
            GeneratorDriver driver = CSharpGeneratorDriver.Create(new AJsonSourceGenerator()).RunGenerators(compilation);
            GeneratorDriverRunResult result = driver.GetRunResult();

            Diagnostic[] generatorErrors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            if (generatorErrors.Length > 0)
            {
                Assert.Fail($"Generator reported errors for '{assemblyName}':\n{_Report(generatorErrors)}");
            }

            using MemoryStream image = new MemoryStream();
            EmitResult emit = compilation.AddSyntaxTrees(result.GeneratedTrees).Emit(image);
            if (!emit.Success)
            {
                string emitted = string.Join("\n---\n", result.GeneratedTrees.Select(t => t.ToString()));
                Diagnostic[] compileErrors = emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
                Assert.Fail($"Fixture '{assemblyName}' did not compile with its generated serializers. Errors:\n{_Report(compileErrors)}\n\nEmitted source:\n{emitted}");
            }

            Assembly loaded = Assembly.Load(image.ToArray());
            RuntimeHelpers.RunModuleConstructor(loaded.ManifestModule.ModuleHandle);
            TypeIdRegistrar.RegisterAllTypeIds(loaded);
            return loaded;

            static string _Report (Diagnostic[] _diagnostics) => string.Join("\n", _diagnostics.Select(d => $"  {d.Id}: {d.GetMessage()} @ {d.Location}"));
        }

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
