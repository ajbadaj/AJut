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
