namespace AJut.Text.AJson.SourceGenerators
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using AJut.Text.AJson.SourceGenerators.Analysis;
    using AJut.Text.AJson.SourceGenerators.Emit;
    using AJut.Text.AJson.SourceGenerators.Model;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    /// <summary>
    /// Roslyn incremental generator for AJson. Picks up [OptimizeAJson]-marked types and
    /// [assembly: OptimizeAJson(typeof(...))] markers, runs each through the analysis layer,
    /// and emits a serializer helper class per type.
    /// </summary>
    [Generator(LanguageNames.CSharp)]
    public sealed class AJsonSourceGenerator : IIncrementalGenerator
    {
        public void Initialize (IncrementalGeneratorInitializationContext context)
        {
            // ---- Per-type opt-in pipeline ----
            // Pick up class/struct declarations carrying any attribute, narrow to ones with
            // [OptimizeAJson] using semantic info, hand the symbol to the analyzer.
            IncrementalValuesProvider<INamedTypeSymbol> perTypeSymbols = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => IsCandidateTypeDecl(node),
                    transform: static (gsc, _) => GetTypeSymbolIfAnnotated(gsc))
                .Where(static t => t != null);

            // ---- Assembly-level opt-in pipeline ----
            // [assembly: OptimizeAJson(typeof(SomeMarker))] declarations are picked up via the
            // CompilationProvider so we have access to the full IAssemblySymbol for the marker
            // type's containing assembly.
            IncrementalValueProvider<ImmutableArray<INamedTypeSymbol>> assemblyExpansions = context.CompilationProvider
                .Select(static (compilation, _) => CollectAssemblyMarkedTypes(compilation));

            // ---- Combine + dedupe + analyze + emit ----
            IncrementalValueProvider<ImmutableArray<INamedTypeSymbol>> allCandidates = perTypeSymbols.Collect()
                .Combine(assemblyExpansions)
                .Select(static (pair, _) => Dedupe(pair.Left, pair.Right));

            context.RegisterSourceOutput(allCandidates, static (spc, candidates) =>
            {
                foreach (INamedTypeSymbol type in candidates)
                {
                    TypeAnalyzer.AnalysisResult analysis = TypeAnalyzer.Analyze(type);

                    foreach (Diagnostic diag in analysis.Diagnostics)
                    {
                        spc.ReportDiagnostic(diag);
                    }

                    string source = SerializerEmitter.Emit(analysis.Model);
                    string hint = $"AJsonSerializer_{analysis.Model.MangledName}.g.cs";
                    spc.AddSource(hint, source);
                }
            });
        }

        // ===========================[ Pipeline helpers ]===========================
        private static bool IsCandidateTypeDecl (SyntaxNode node)
        {
            // Class or struct declaration that carries at least one attribute.
            if (node is ClassDeclarationSyntax cls)
            {
                return cls.AttributeLists.Count > 0;
            }
            if (node is StructDeclarationSyntax str)
            {
                return str.AttributeLists.Count > 0;
            }
            if (node is RecordDeclarationSyntax rec)
            {
                return rec.AttributeLists.Count > 0;
            }
            return false;
        }

        private static INamedTypeSymbol GetTypeSymbolIfAnnotated (GeneratorSyntaxContext gsc)
        {
            INamedTypeSymbol type = gsc.SemanticModel.GetDeclaredSymbol(gsc.Node) as INamedTypeSymbol;
            if (type == null)
            {
                return null;
            }

            foreach (AttributeData attr in type.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() == AttributeNames.kOptimizeAJson)
                {
                    return type;
                }
            }
            return null;
        }

        private static ImmutableArray<INamedTypeSymbol> CollectAssemblyMarkedTypes (Compilation compilation)
        {
            ImmutableArray<INamedTypeSymbol>.Builder collected = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

            foreach (AttributeData attr in compilation.Assembly.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() != AttributeNames.kOptimizeAJson)
                {
                    continue;
                }
                if (attr.ConstructorArguments.Length == 0)
                {
                    continue;
                }
                if (!(attr.ConstructorArguments[0].Value is INamedTypeSymbol marker))
                {
                    continue;
                }

                IAssemblySymbol targetAssembly = marker.ContainingAssembly;
                if (targetAssembly == null)
                {
                    continue;
                }

                foreach (INamedTypeSymbol candidate in AssemblyTypeCollector.CollectPublicTypes(targetAssembly))
                {
                    collected.Add(candidate);
                }
            }

            return collected.ToImmutable();
        }

        private static ImmutableArray<INamedTypeSymbol> Dedupe (
            ImmutableArray<INamedTypeSymbol> perType,
            ImmutableArray<INamedTypeSymbol> assemblyExpanded)
        {
            HashSet<INamedTypeSymbol> seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            ImmutableArray<INamedTypeSymbol>.Builder result = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

            foreach (INamedTypeSymbol t in perType)
            {
                if (t != null && seen.Add(t))
                {
                    result.Add(t);
                }
            }
            foreach (INamedTypeSymbol t in assemblyExpanded)
            {
                if (t != null && seen.Add(t))
                {
                    result.Add(t);
                }
            }

            return result.ToImmutable();
        }
    }
}
