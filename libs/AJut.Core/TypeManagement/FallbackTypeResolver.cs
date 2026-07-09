namespace AJut.TypeManagement
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;

    /// <summary>
    /// Best-effort resolution of a type from its name when identity-based resolution
    /// (<see cref="Type.GetType(string)"/>) cannot bind the assembly - the failure mode of a packaged
    /// / ReadyToRun app where an assembly-qualified name no longer resolves. Matches the type's full
    /// name against a caller-supplied set of assemblies (see
    /// <see cref="TypeIdRegistrar.TrackedAssemblies"/>).
    /// <para/>
    /// This is a fallback, not a guarantee: it relies on the type's metadata still being present in a
    /// loaded assembly, so it is not trim / NativeAOT safe and does not search anything the caller did
    /// not hand in. A type that must round-trip under trimming should carry a
    /// <see cref="TypeIdAttribute"/> or be registered up front.
    /// <para/>
    /// The resolver is type-kind agnostic; callers decide what may be resolved this way. AJson only
    /// accepts an enum resolved by name, keeping the polymorphic-deserialization surface closed for
    /// arbitrary classes. It is internal for now - promote (and, if its scope broadens past enums,
    /// rename) if a general need appears.
    /// </summary>
    internal static class FallbackTypeResolver
    {
        /// <summary>
        /// Resolve a type by its full name across the provided assemblies. Accepts either a bare full
        /// name or a full assembly-qualified name (the assembly identity is dropped and only the name
        /// is matched). Returns the single match, or null when there is no match or more than one - an
        /// ambiguous match logs and resolves to null rather than guessing.
        /// </summary>
        internal static Type ResolveByName (string typeNameOrAssemblyQualifiedName, IReadOnlyList<Assembly> assemblies)
        {
            if (String.IsNullOrEmpty(typeNameOrAssemblyQualifiedName) || assemblies == null)
            {
                return null;
            }

            // Key off the type full name; if an assembly-qualified name came through, drop the
            //  (unbindable) assembly identity and match on the name alone.
            string typeFullName = ExtractTypeFullName(typeNameOrAssemblyQualifiedName);

            List<Type> candidates = new List<Type>();
            for (int index = 0; index < assemblies.Count; ++index)
            {
                Type found = assemblies[index]?.GetType(typeFullName);
                if (found != null && !candidates.Contains(found))
                {
                    candidates.Add(found);
                }
            }

            return PickSingleCandidate(typeFullName, candidates);
        }

        /// <summary>
        /// Extract the bare type full name from a possibly assembly-qualified name - everything before
        /// the first comma. Returns the input unchanged when it carries no assembly qualifier.
        /// </summary>
        internal static string ExtractTypeFullName (string typeNameOrAssemblyQualifiedName)
        {
            if (String.IsNullOrEmpty(typeNameOrAssemblyQualifiedName))
            {
                return typeNameOrAssemblyQualifiedName;
            }

            // The type-name / assembly boundary is the first comma at bracket depth zero. A generic
            //  type carries its arguments as nested assembly-qualified names inside [[ ... ]] whose own
            //  commas sit at depth > 0 - a naive IndexOf(',') would split inside them.
            int depth = 0;
            for (int index = 0; index < typeNameOrAssemblyQualifiedName.Length; ++index)
            {
                char current = typeNameOrAssemblyQualifiedName[index];
                if (current == '[')
                {
                    ++depth;
                }
                else if (current == ']')
                {
                    --depth;
                }
                else if (current == ',' && depth == 0)
                {
                    return typeNameOrAssemblyQualifiedName.Substring(0, index).Trim();
                }
            }

            return typeNameOrAssemblyQualifiedName;
        }

        /// <summary>
        /// Apply the resolution rule to a candidate set: exactly one match resolves, zero or more than
        /// one resolves to null. Multiple matches are ambiguous - they are logged rather than guessed.
        /// </summary>
        internal static Type PickSingleCandidate (string typeFullName, IReadOnlyList<Type> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            if (candidates.Count == 1)
            {
                return candidates[0];
            }

            Logger.LogError($"FallbackTypeResolver found {candidates.Count} types matching '{typeFullName}' across the tracked assemblies; refusing to guess which was meant. Disambiguate by giving the type a [TypeId] or registering it explicitly.");
            return null;
        }
    }
}
