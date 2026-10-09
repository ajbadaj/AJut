namespace AJut.Text.AJson.SourceGenerators.Tests
{
    using System.Linq;
    using AJut.Text.AJson.SourceGenerators.Analysis;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class AssemblyTypeCollectorTests
    {
        private const string kWithStaticClasses = @"
namespace MarkerNs
{
    public class A { public int X { get; set; } }
    public static class Helpers { public static int Twice (int x) => x * 2; }
    public class Outer { public static class NestedHelpers { } }
}";

        [TestMethod]
        public void CollectsPublicTypesFromMarkerAssembly ()
        {
            const string src = @"
namespace MarkerNs
{
    public class A { public int X { get; set; } }
    public class B { public string S { get; set; } }
    internal class C { }
    public abstract class D { }
}";
            CSharpCompilation compilation = TestCompilation.Build(src, "MarkerAssembly");
            IAssemblySymbol asm = compilation.Assembly;

            var collected = AssemblyTypeCollector.CollectPublicTypes(asm).Select(t => t.Name).ToList();

            CollectionAssert.Contains(collected, "A");
            CollectionAssert.Contains(collected, "B");
            CollectionAssert.DoesNotContain(collected, "C");   // internal - skip
            CollectionAssert.DoesNotContain(collected, "D");   // abstract - skip
        }

        [TestMethod]
        public void SkipsStaticClasses ()
        {
            // A static class can never have a constructor, so collecting it can only ever end in AJSON001 (AJU-1)
            CSharpCompilation compilation = TestCompilation.Build(kWithStaticClasses, "MarkerAssembly");

            var collected = AssemblyTypeCollector.CollectPublicTypes(compilation.Assembly).Select(t => t.Name).ToList();

            CollectionAssert.Contains(collected, "A");
            CollectionAssert.Contains(collected, "Outer");
            CollectionAssert.DoesNotContain(collected, "Helpers", "the static class Helpers was collected");
            CollectionAssert.DoesNotContain(collected, "NestedHelpers", "the nested static class NestedHelpers was collected");
        }

        [TestMethod]
        public void SkipsStaticClasses_InAReferencedAssembly ()
        {
            // The marker can live in a referenced assembly, which the collector sees as metadata, and metadata stores a static class
            //  as abstract and sealed rather than with a static flag, so this walk is checked on its own
            MetadataReference library = TestCompilation.EmitToReference(TestCompilation.Build(kWithStaticClasses, "ReferencedMarkerAssembly"));
            CSharpCompilation consumer = TestCompilation.Build("namespace ConsumerNs { public class C { } }", "ConsumerAssembly", library);
            IAssemblySymbol referenced = consumer.GetTypeByMetadataName("MarkerNs.A")!.ContainingAssembly;

            var collected = AssemblyTypeCollector.CollectPublicTypes(referenced).Select(t => t.Name).ToList();

            CollectionAssert.Contains(collected, "A");
            CollectionAssert.Contains(collected, "Outer");
            CollectionAssert.DoesNotContain(collected, "Helpers", "the static class Helpers was collected");
            CollectionAssert.DoesNotContain(collected, "NestedHelpers", "the nested static class NestedHelpers was collected");
        }
    }
}
