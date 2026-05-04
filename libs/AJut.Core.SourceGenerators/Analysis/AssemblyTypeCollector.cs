namespace AJut.Text.AJson.SourceGenerators.Analysis
{
    using System.Collections.Generic;
    using Microsoft.CodeAnalysis;

    /// <summary>
    /// Walks an IAssemblySymbol and yields every public, non-abstract, non-interface type that
    /// is a serialization candidate. Used to expand the assembly-level [assembly: OptimizeAJson(typeof(...))]
    /// form into a per-type list the analyzer can process.
    /// </summary>
    internal static class AssemblyTypeCollector
    {
        public static IEnumerable<INamedTypeSymbol> CollectPublicTypes (IAssemblySymbol assembly)
        {
            return WalkNamespace(assembly.GlobalNamespace);
        }

        private static IEnumerable<INamedTypeSymbol> WalkNamespace (INamespaceSymbol ns)
        {
            foreach (INamespaceOrTypeSymbol member in ns.GetMembers())
            {
                if (member is INamespaceSymbol nested)
                {
                    foreach (INamedTypeSymbol type in WalkNamespace(nested))
                    {
                        yield return type;
                    }
                }
                else if (member is INamedTypeSymbol type)
                {
                    foreach (INamedTypeSymbol candidate in WalkType(type))
                    {
                        yield return candidate;
                    }
                }
            }
        }

        private static IEnumerable<INamedTypeSymbol> WalkType (INamedTypeSymbol type)
        {
            if (IsCandidate(type))
            {
                yield return type;
            }

            // Nested types - public-nested only.
            foreach (INamedTypeSymbol nested in type.GetTypeMembers())
            {
                if (nested.DeclaredAccessibility == Accessibility.Public)
                {
                    foreach (INamedTypeSymbol candidate in WalkType(nested))
                    {
                        yield return candidate;
                    }
                }
            }
        }

        private static bool IsCandidate (INamedTypeSymbol type)
        {
            if (type.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
            if (type.IsAbstract)
            {
                return false;
            }
            if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
            {
                return false;
            }
            // Skip generic type definitions - we cannot emit a serializer for an unconstructed generic.
            if (type.IsGenericType && type.IsUnboundGenericType)
            {
                return false;
            }
            if (type.IsGenericType && type.TypeArguments.Length > 0 && type.TypeArguments[0].TypeKind == TypeKind.TypeParameter)
            {
                return false;
            }
            return true;
        }
    }
}
