namespace AJut.Text.AJson.SourceGenerators.Tests
{
    using System.Collections.Immutable;
    using System.Globalization;
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
        public void GeneratedCode_CompilesCleanly_WithFractionalOmitDefault_UnderACommaDecimalCulture ()
        {
            // The generator runs inside the build, under whatever culture the build machine has
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public class Ratio { [JsonOmitIfDefault(2.5)] public double Value { get; set; } }
}";
            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                AssertCompilesCleanly(src);
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        [TestMethod]
        public void GeneratedCode_CompilesCleanly_WithAnEnumOmitValueGivenAsItsNumber ()
        {
            // AJSON003 accepts an enum's underlying number, but only a constant zero converts to an enum on its own
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    public enum eAnchor { Left, Center, Right }
    [OptimizeAJson]
    public class Anchored { [JsonOmitIfDefault(1)] public eAnchor Anchor { get; set; } }
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
