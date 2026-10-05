namespace AJut.Text.AJson.SourceGenerators.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Runtime.Loader;
    using AJut.TypeManagement;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Compiles fixture sources and loads them into the test process, with or without the generator's output, so a test can run
    /// generated serializers (or the reflection path, for comparison) for real
    /// </summary>
    internal static class FixtureAssemblies
    {
        /// <summary>
        /// Runs the generator over the compilation and compiles the result, failing the test with the generator's diagnostics or
        /// the compiler's errors (and the emitted source) if either reports a problem
        /// </summary>
        public static byte[] CompileWithGeneratedSerializers (CSharpCompilation compilation)
        {
            GeneratorDriver driver = CSharpGeneratorDriver.Create(new AJsonSourceGenerator()).RunGenerators(compilation);
            GeneratorDriverRunResult result = driver.GetRunResult();

            Diagnostic[] generatorErrors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
            if (generatorErrors.Length > 0)
            {
                Assert.Fail($"Generator reported errors for '{compilation.AssemblyName}':\n{_Report(generatorErrors)}");
            }

            using MemoryStream image = new MemoryStream();
            Microsoft.CodeAnalysis.Emit.EmitResult emit = compilation.AddSyntaxTrees(result.GeneratedTrees).Emit(image);
            if (!emit.Success)
            {
                string emitted = string.Join("\n---\n", result.GeneratedTrees.Select(t => t.ToString()));
                Diagnostic[] compileErrors = emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
                Assert.Fail($"Fixture '{compilation.AssemblyName}' did not compile with its generated serializers. Errors:\n{_Report(compileErrors)}\n\nEmitted source:\n{emitted}");
            }

            return image.ToArray();

            static string _Report (Diagnostic[] _diagnostics) => string.Join("\n", _diagnostics.Select(d => $"  {d.Id}: {d.GetMessage()} @ {d.Location}"));
        }

        /// <summary>
        /// Loads a compiled image and runs its module constructor, which is where the generated [ModuleInitializer] registers each
        /// serializer with AJsonGeneratedDispatch. Pass a shared <paramref name="context"/> when one fixture references another.
        /// </summary>
        public static Assembly Load (byte[] image, FixtureLoadContext? context = null)
        {
            Assembly loaded = (context ?? new FixtureLoadContext()).LoadImage(image);
            RuntimeHelpers.RunModuleConstructor(loaded.ManifestModule.ModuleHandle);
            TypeIdRegistrar.RegisterAllTypeIds(loaded);
            return loaded;
        }

        public static Assembly LoadWithGeneratedSerializers (string source, string assemblyName)
        {
            return Load(CompileWithGeneratedSerializers(TestCompilation.Build(source, assemblyName)));
        }

        /// <summary>
        /// Loads the fixture with no generator run over it, so its types go through the reflection path
        /// </summary>
        public static Assembly LoadWithoutGenerator (string source, string assemblyName)
        {
            return Load(TestCompilation.EmitToImage(TestCompilation.Build(source, assemblyName)));
        }
    }

    /// <summary>
    /// Holds fixture assemblies that reference each other. Anything it was not handed (AJut.Core, the runtime) falls through to
    /// the default context, so fixtures share the AJut.Core the tests themselves run against.
    /// </summary>
    internal sealed class FixtureLoadContext : AssemblyLoadContext
    {
        private readonly Dictionary<string, Assembly> m_loaded = new Dictionary<string, Assembly>();

        public Assembly LoadImage (byte[] image)
        {
            Assembly loaded = this.LoadFromStream(new MemoryStream(image));
            m_loaded[loaded.GetName().Name!] = loaded;
            return loaded;
        }

        protected override Assembly? Load (AssemblyName assemblyName)
        {
            return assemblyName.Name != null && m_loaded.TryGetValue(assemblyName.Name, out Assembly? found) ? found : null;
        }
    }
}
