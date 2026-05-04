namespace AJut.Text.AJson.SourceGenerators.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using AJut.Text.AJson;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;

    /// <summary>
    /// Spins up a CSharpCompilation from a source string with references to AJut.Core. The
    /// returned compilation lets the analyzer pull symbols by metadata name without going
    /// through the full source-generator driver.
    /// </summary>
    internal static class TestCompilation
    {
        private static readonly IReadOnlyList<MetadataReference> g_baseRefs = BuildBaseRefs();

        public static CSharpCompilation Build (string source, string assemblyName = "TestAssembly")
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
            return CSharpCompilation.Create(
                assemblyName: assemblyName,
                syntaxTrees: new[] { tree },
                references: g_baseRefs,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }

        public static INamedTypeSymbol? GetType (CSharpCompilation compilation, string fullyQualifiedMetadataName)
        {
            return compilation.GetTypeByMetadataName(fullyQualifiedMetadataName);
        }

        // ---- Base reference set ----
        // Force-touch types from each assembly we need so the runtime loads them, then walk the
        // resulting reference graph. AppDomain.CurrentDomain.GetAssemblies() alone is unreliable -
        // it only contains things already JITted, which would not include AJut.Core unless a test
        // had already touched it.
        private static IReadOnlyList<MetadataReference> BuildBaseRefs ()
        {
            HashSet<string> seenLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<MetadataReference> result = new List<MetadataReference>();

            // Anchors: types whose containing assembly we definitely need referenced.
            Type[] anchors =
            {
                typeof(object),                      // System.Private.CoreLib / System.Runtime
                typeof(System.Linq.Enumerable),
                typeof(System.Collections.Generic.List<>),
                typeof(System.ComponentModel.Component),
                typeof(System.Runtime.CompilerServices.ModuleInitializerAttribute),
                typeof(OptimizeAJsonAttribute),      // AJut.Core
                typeof(JsonValue),                   // also AJut.Core, double-anchor in case the first is somehow elided
            };
            foreach (Type anchor in anchors)
            {
                AddAssemblyAndReferences(anchor.Assembly, seenLocations, result);
            }

            // Plus the netstandard reference assembly (the trimmed compile-time reference set
            // System.Object 'forwards' through). Without this, code that uses BCL types defined
            // outside System.Private.CoreLib's own export list can fail to resolve.
            string runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
            string netstandardPath = Path.Combine(runtimeDir, "netstandard.dll");
            if (File.Exists(netstandardPath) && seenLocations.Add(netstandardPath))
            {
                result.Add(MetadataReference.CreateFromFile(netstandardPath));
            }

            return result;
        }

        private static void AddAssemblyAndReferences (Assembly asm, HashSet<string> seen, List<MetadataReference> output)
        {
            if (asm.IsDynamic || string.IsNullOrEmpty(asm.Location))
            {
                return;
            }
            if (!seen.Add(asm.Location))
            {
                return;
            }
            output.Add(MetadataReference.CreateFromFile(asm.Location));

            foreach (AssemblyName referenced in asm.GetReferencedAssemblies())
            {
                Assembly loaded;
                try
                {
                    loaded = Assembly.Load(referenced);
                }
                catch
                {
                    continue;
                }
                AddAssemblyAndReferences(loaded, seen, output);
            }
        }
    }
}
