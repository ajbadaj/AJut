namespace AJut.Text.AJson.SourceGenerators.Tests
{
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Compile-and-verify integration. Runs the generator on a fixture compilation, adds the
    /// generated sources back into the compilation, runs the C# compiler over the combined
    /// result, and asserts no compile errors. This proves the emitter's output is valid C# that
    /// references AJut.Core's public surface correctly.
    /// </summary>
    [TestClass]
    public class EmitCompileTests
    {
        [TestMethod]
        public void GeneratedCode_CompilesCleanly_ForBasicType ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public class Foo
    {
        public int Score { get; set; }
        public string Name { get; set; }
        public bool Active { get; set; }
    }
}";
            AssertCompilesCleanly(src);
        }

        [TestMethod]
        public void GeneratedCode_CompilesCleanly_WithAlias ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public class Aliased
    {
        [JsonPropertyAlias(""friendly"")] public string Name { get; set; }
    }
}";
            AssertCompilesCleanly(src);
        }

        [TestMethod]
        public void GeneratedCode_CompilesCleanly_WithOmitIfDefault ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    public enum eAnchor { Left, Center, Right }
    [OptimizeAJson]
    public class HasOmit
    {
        [JsonOmitIfDefault(eAnchor.Center)] public eAnchor Anchor { get; set; }
        [JsonOmitIfDefault] public int Score { get; set; }
    }
}";
            AssertCompilesCleanly(src);
        }

        [TestMethod]
        public void GeneratedCode_CompilesCleanly_WithRuntimeTypeEval ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    public abstract class Shape { }
    public class Circle : Shape { public double Radius { get; set; } }
    [OptimizeAJson]
    public class Holder { [JsonRuntimeTypeEval] public Shape Thing { get; set; } }
    [OptimizeAJson]
    public class HolderCircle { public Circle C { get; set; } }
}";
            AssertCompilesCleanly(src);
        }

        [TestMethod]
        public void GeneratedCode_CompilesCleanly_WithPropertyAsSelf ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    [JsonPropertyAsSelf(""Inner"")]
    public class Elevator { public int Inner { get; set; } }
}";
            AssertCompilesCleanly(src);
        }

        // ===========================[ Helpers ]===========================
        private static void AssertCompilesCleanly (string source)
        {
            CSharpCompilation compilation = TestCompilation.Build(source);
            AJsonSourceGenerator generator = new AJsonSourceGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
            driver = driver.RunGenerators(compilation);

            GeneratorDriverRunResult result = driver.GetRunResult();

            // Roll the generated sources back into a fresh compilation so we can compile-check
            // against the consumer's reference set (which includes AJut.Core).
            CSharpCompilation finalCompilation = compilation.AddSyntaxTrees(
                result.GeneratedTrees);

            ImmutableArray<Diagnostic> diags = finalCompilation.GetDiagnostics();
            Diagnostic[] errors = diags.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            if (errors.Length > 0)
            {
                string emitted = string.Join("\n---\n",
                    result.GeneratedTrees.Select(t => t.ToString()));
                string errorReport = string.Join("\n",
                    errors.Select(e => $"  {e.Id}: {e.GetMessage()} @ {e.Location}"));
                Assert.Fail($"Generated code did not compile cleanly. Errors:\n{errorReport}\n\nEmitted source:\n{emitted}");
            }
        }
    }
}
