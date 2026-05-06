namespace AJut.Text.AJson.SourceGenerators.Tests
{
    using System.Linq;
    using AJut.Text.AJson.SourceGenerators.Analysis;
    using AJut.Text.AJson.SourceGenerators.Model;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Pure-logic tests for the analyzer. Each test builds a CSharpCompilation from a source
    /// string, calls TypeAnalyzer.Analyze on the relevant symbol, and asserts properties of the
    /// returned model. No source-generator driver, no AddSource calls - just the analysis IR.
    /// </summary>
    [TestClass]
    public class TypeAnalyzerTests
    {
        // ===========================[ Aliases / keys ]===========================
        [TestMethod]
        public void Alias_ProducesAliasedKey ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public class Aliased
    {
        [JsonPropertyAlias(""friendly-name"")]
        public string Name { get; set; }
    }
}";
            CSharpCompilation compilation = TestCompilation.Build(src);
            INamedTypeSymbol? type = TestCompilation.GetType(compilation, "TestNs.Aliased");
            Assert.IsNotNull(type);

            TypeAnalyzer.AnalysisResult result = TypeAnalyzer.Analyze(type!);
            PropertyModel prop = result.Model.Properties.Single(p => p.Name == "Name");
            Assert.AreEqual("friendly-name", prop.JsonKey);
        }

        [TestMethod]
        public void NoAlias_FallsBackToClrName ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs { [OptimizeAJson] public class Plain { public int Score { get; set; } } }";
            CSharpCompilation compilation = TestCompilation.Build(src);
            INamedTypeSymbol? type = TestCompilation.GetType(compilation, "TestNs.Plain");
            Assert.IsNotNull(type);

            TypeAnalyzer.AnalysisResult result = TypeAnalyzer.Analyze(type!);
            PropertyModel prop = result.Model.Properties.Single(p => p.Name == "Score");
            Assert.AreEqual("Score", prop.JsonKey);
        }

        // ===========================[ JsonIgnore ]===========================
        [TestMethod]
        public void JsonIgnore_DropsProperty ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public class IgnoreCarrier
    {
        public string Visible { get; set; }
        [JsonIgnore] public string Hidden { get; set; }
    }
}";
            CSharpCompilation compilation = TestCompilation.Build(src);
            INamedTypeSymbol? type = TestCompilation.GetType(compilation, "TestNs.IgnoreCarrier");
            Assert.IsNotNull(type);

            TypeAnalyzer.AnalysisResult result = TypeAnalyzer.Analyze(type!);
            Assert.IsTrue(result.Model.Properties.Any(p => p.Name == "Visible"));
            Assert.IsFalse(result.Model.Properties.Any(p => p.Name == "Hidden"));
        }

        // ===========================[ Property kinds ]===========================
        [TestMethod]
        public void SimpleValueKinds_Classified ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public class Simples
    {
        public string S { get; set; }
        public int I { get; set; }
        public bool B { get; set; }
        public double D { get; set; }
    }
}";
            CSharpCompilation compilation = TestCompilation.Build(src);
            INamedTypeSymbol? type = TestCompilation.GetType(compilation, "TestNs.Simples");
            TypeAnalyzer.AnalysisResult result = TypeAnalyzer.Analyze(type!);

            Assert.AreEqual(ePropertyKind.SimpleValue, result.Model.Properties.Single(p => p.Name == "S").Kind);
            Assert.AreEqual(ePropertyKind.SimpleValue, result.Model.Properties.Single(p => p.Name == "I").Kind);
            Assert.AreEqual(ePropertyKind.SimpleValue, result.Model.Properties.Single(p => p.Name == "B").Kind);
            Assert.AreEqual(ePropertyKind.SimpleValue, result.Model.Properties.Single(p => p.Name == "D").Kind);
        }

        [TestMethod]
        public void EnumProperty_ClassifiedAsEnum ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    public enum eFlavor { A, B }
    [OptimizeAJson]
    public class HasEnum { public eFlavor F { get; set; } }
}";
            CSharpCompilation compilation = TestCompilation.Build(src);
            INamedTypeSymbol? type = TestCompilation.GetType(compilation, "TestNs.HasEnum");
            TypeAnalyzer.AnalysisResult result = TypeAnalyzer.Analyze(type!);

            PropertyModel prop = result.Model.Properties.Single(p => p.Name == "F");
            Assert.AreEqual(ePropertyKind.Enum, prop.Kind);
            Assert.IsTrue(prop.IsUsuallyQuoted);
        }

        [TestMethod]
        public void CollectionProperty_ClassifiedAsCollection ()
        {
            const string src = @"
using System.Collections.Generic;
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public class HasList { public List<int> Numbers { get; set; } }
}";
            CSharpCompilation compilation = TestCompilation.Build(src);
            INamedTypeSymbol? type = TestCompilation.GetType(compilation, "TestNs.HasList");
            TypeAnalyzer.AnalysisResult result = TypeAnalyzer.Analyze(type!);

            PropertyModel prop = result.Model.Properties.Single(p => p.Name == "Numbers");
            Assert.AreEqual(ePropertyKind.Collection, prop.Kind);
            Assert.IsTrue(prop.ElementTypeFullName.Contains("Int32")
                            || prop.ElementTypeFullName.Contains("int"), $"Was expecting {prop.ElementTypeFullName} to be 'Int32'"
            );
        }

        [TestMethod]
        public void DictionaryProperty_ClassifiedAsDictionary ()
        {
            const string src = @"
using System.Collections.Generic;
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public class HasDict { public Dictionary<string, int> Counts { get; set; } }
}";
            CSharpCompilation compilation = TestCompilation.Build(src);
            INamedTypeSymbol? type = TestCompilation.GetType(compilation, "TestNs.HasDict");
            TypeAnalyzer.AnalysisResult result = TypeAnalyzer.Analyze(type!);

            PropertyModel prop = result.Model.Properties.Single(p => p.Name == "Counts");
            Assert.AreEqual(ePropertyKind.Dictionary, prop.Kind);
        }

        // ===========================[ Omit-if-default ]===========================
        [TestMethod]
        public void OmitIfDefault_NoArg_HasOmitButNoExplicit ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public class HasOmit { [JsonOmitIfDefault] public int Score { get; set; } }
}";
            CSharpCompilation compilation = TestCompilation.Build(src);
            INamedTypeSymbol? type = TestCompilation.GetType(compilation, "TestNs.HasOmit");
            TypeAnalyzer.AnalysisResult result = TypeAnalyzer.Analyze(type!);

            PropertyModel prop = result.Model.Properties.Single(p => p.Name == "Score");
            Assert.IsTrue(prop.HasOmitIfDefault);
            Assert.IsFalse(prop.HasExplicitOmitDefault);
        }

        [TestMethod]
        public void OmitIfDefault_WithEnumValue_CapturesLiteral ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    public enum eAnchor { Left, Center, Right }
    [OptimizeAJson]
    public class HasOmit { [JsonOmitIfDefault(eAnchor.Center)] public eAnchor Anchor { get; set; } }
}";
            CSharpCompilation compilation = TestCompilation.Build(src);
            INamedTypeSymbol? type = TestCompilation.GetType(compilation, "TestNs.HasOmit");
            TypeAnalyzer.AnalysisResult result = TypeAnalyzer.Analyze(type!);

            PropertyModel prop = result.Model.Properties.Single(p => p.Name == "Anchor");
            Assert.IsTrue(prop.HasOmitIfDefault);
            Assert.IsTrue(prop.HasExplicitOmitDefault);
            Assert.IsTrue(prop.ExplicitOmitDefaultLiteral.Contains("eAnchor"));
            Assert.IsTrue(prop.ExplicitOmitDefaultLiteral.EndsWith("1"));   // Center is the second member
        }

        // ===========================[ RuntimeTypeEval ]===========================
        [TestMethod]
        public void RuntimeTypeEval_MarksPropertyKind ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    public interface IThing { }
    [OptimizeAJson]
    public class HasRTE { [JsonRuntimeTypeEval] public IThing Polymorphic { get; set; } }
}";
            CSharpCompilation compilation = TestCompilation.Build(src);
            INamedTypeSymbol? type = TestCompilation.GetType(compilation, "TestNs.HasRTE");
            TypeAnalyzer.AnalysisResult result = TypeAnalyzer.Analyze(type!);

            PropertyModel prop = result.Model.Properties.Single(p => p.Name == "Polymorphic");
            Assert.AreEqual(ePropertyKind.RuntimeTypeEval, prop.Kind);
            Assert.IsTrue(prop.RuntimeTypeEvalFlagLiteral.Contains("eTypeIdInfo"));
        }

        // ===========================[ JsonPropertyAsSelf ]===========================
        [TestMethod]
        public void PropertyAsSelf_CapturesElevatedName ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    [JsonPropertyAsSelf(""Inner"")]
    public class Elevator { public int Inner { get; set; } }
}";
            CSharpCompilation compilation = TestCompilation.Build(src);
            INamedTypeSymbol? type = TestCompilation.GetType(compilation, "TestNs.Elevator");
            TypeAnalyzer.AnalysisResult result = TypeAnalyzer.Analyze(type!);

            Assert.AreEqual("Inner", result.Model.PropertyAsSelfName);
        }

        // ===========================[ Diagnostics ]===========================
        [TestMethod]
        public void AJSON001_FiresOnNoParameterlessCtor ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public class NoCtor { public NoCtor(int x) { Score = x; } public int Score { get; set; } }
}";
            CSharpCompilation compilation = TestCompilation.Build(src);
            INamedTypeSymbol? type = TestCompilation.GetType(compilation, "TestNs.NoCtor");
            TypeAnalyzer.AnalysisResult result = TypeAnalyzer.Analyze(type!);

            Assert.IsTrue(result.Diagnostics.Any(d => d.Id == "AJSON001"), "Expected AJSON001 to fire");
        }

        [TestMethod]
        public void AJSON001_DoesNotFire_WhenAJsonConstructorPresent ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public class HasAJsonCtor { [AJsonConstructor] public HasAJsonCtor(int x) { Score = x; } public int Score { get; set; } }
}";
            CSharpCompilation compilation = TestCompilation.Build(src);
            INamedTypeSymbol? type = TestCompilation.GetType(compilation, "TestNs.HasAJsonCtor");
            TypeAnalyzer.AnalysisResult result = TypeAnalyzer.Analyze(type!);

            Assert.IsFalse(result.Diagnostics.Any(d => d.Id == "AJSON001"));
        }

        [TestMethod]
        public void AJSON002_FiresOnAbstractPropertyType ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    public abstract class AbstractBase { }
    [OptimizeAJson]
    public class HasAbstract { public AbstractBase Thing { get; set; } }
}";
            CSharpCompilation compilation = TestCompilation.Build(src);
            INamedTypeSymbol? type = TestCompilation.GetType(compilation, "TestNs.HasAbstract");
            TypeAnalyzer.AnalysisResult result = TypeAnalyzer.Analyze(type!);

            Assert.IsTrue(result.Diagnostics.Any(d => d.Id == "AJSON002"));
        }

        [TestMethod]
        public void AJSON002_DoesNotFire_WhenRuntimeTypeEvalApplied ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    public abstract class AbstractBase { }
    [OptimizeAJson]
    public class HasRTEAbstract { [JsonRuntimeTypeEval] public AbstractBase Thing { get; set; } }
}";
            CSharpCompilation compilation = TestCompilation.Build(src);
            INamedTypeSymbol? type = TestCompilation.GetType(compilation, "TestNs.HasRTEAbstract");
            TypeAnalyzer.AnalysisResult result = TypeAnalyzer.Analyze(type!);

            Assert.IsFalse(result.Diagnostics.Any(d => d.Id == "AJSON002"));
        }

        [TestMethod]
        public void AJSON003_FiresOnOmitDefaultTypeMismatch ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    [OptimizeAJson]
    public class Mismatch { [JsonOmitIfDefault(""string-but-prop-is-int"")] public int Score { get; set; } }
}";
            CSharpCompilation compilation = TestCompilation.Build(src);
            INamedTypeSymbol? type = TestCompilation.GetType(compilation, "TestNs.Mismatch");
            TypeAnalyzer.AnalysisResult result = TypeAnalyzer.Analyze(type!);

            Assert.IsTrue(result.Diagnostics.Any(d => d.Id == "AJSON003"));
        }

        [TestMethod]
        public void AJSON003_DoesNotFire_WhenEnumDefaultMatchesEnumProperty ()
        {
            const string src = @"
using AJut.Text.AJson;
namespace TestNs
{
    public enum eAnchor { Left, Center, Right }
    [OptimizeAJson]
    public class Match { [JsonOmitIfDefault(eAnchor.Center)] public eAnchor Anchor { get; set; } }
}";
            CSharpCompilation compilation = TestCompilation.Build(src);
            INamedTypeSymbol? type = TestCompilation.GetType(compilation, "TestNs.Match");
            TypeAnalyzer.AnalysisResult result = TypeAnalyzer.Analyze(type!);

            Assert.IsFalse(result.Diagnostics.Any(d => d.Id == "AJSON003"));
        }
    }
}
