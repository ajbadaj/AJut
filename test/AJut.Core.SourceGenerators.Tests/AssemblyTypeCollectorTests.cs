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
    }
}
