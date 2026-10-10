namespace AJut.TypeManagement
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;

    /// <summary>
    /// Maps type ids to types. Lookups are safe from any thread, including while something else
    /// registers: the AJson source generator's [ModuleInitializer] registers an assembly's enums
    /// when that assembly is first touched, on whatever thread touched it, so registration is not
    /// only a startup concern.
    /// </summary>
    public static class TypeIdRegistrar
    {
        // Type id lookups happen on every typed json read and never take a lock, so the map is
        //  concurrent. A whole-assembly scan is rare and takes g_assemblyScanLock, so a second
        //  caller for the same assembly waits for the first scan to finish instead of returning
        //  before its ids are registered. The tracked assembly list is copy-on-write under that
        //  lock, so the name fallback always enumerates a complete snapshot.
        private static readonly ConcurrentDictionary<string, Type> g_typeAliases = new ConcurrentDictionary<string, Type>();
        private static readonly object g_assemblyScanLock = new object();
        private static readonly HashSet<string> g_alreadySearchedAssemblies = new HashSet<string>();
        private static Assembly[] g_trackedAssemblies = Array.Empty<Assembly>();

        /// <summary>
        /// Assemblies the registrar has been asked to track via <see cref="RegisterAllTypeIds"/>. The
        /// fallback name-resolution path searches these when a type id cannot bind by identity.
        /// </summary>
        internal static IReadOnlyList<Assembly> TrackedAssemblies => Volatile.Read(ref g_trackedAssemblies);

        /// <summary>
        /// Register a type to be associated with the given type id
        /// </summary>
        public static bool RegisterTypeId<T> (string id)
        {
            return RegisterTypeId(id, typeof(T));
        }

        /// <summary>
        /// Register a type to be associated with the given type id
        /// </summary>
        public static bool RegisterTypeId (string id, Type type)
        {
            // The first registration for an id wins, as it always has
            if (g_typeAliases.TryAdd(id, type))
            {
                return true;
            }

            return g_typeAliases.TryGetValue(id, out Type existing) && existing != type;
        }

        /// <summary>
        /// Register all <see cref="TypeIdAttribute"/> type ids from the given assembly
        /// </summary>
        /// <param name="assembly">The assembly to search</param>
        /// <param name="forceSearch">Whether or not to search again if the assembly has already been searched (cached by name)</param>
        public static void RegisterAllTypeIds (Assembly assembly, bool forceSearch = false)
        {
            lock (g_assemblyScanLock)
            {
                if (Array.IndexOf(g_trackedAssemblies, assembly) == -1)
                {
                    Assembly[] grown = new Assembly[g_trackedAssemblies.Length + 1];
                    g_trackedAssemblies.CopyTo(grown, 0);
                    grown[grown.Length - 1] = assembly;
                    Volatile.Write(ref g_trackedAssemblies, grown);
                }

                if (!g_alreadySearchedAssemblies.Add(assembly.FullName) && !forceSearch)
                {
                    return;
                }

                Type[] allTypes;
                if (assembly.FullName == typeof(AJutActivator).Assembly.FullName)
                {
                    allTypes = assembly.GetTypes();
                }
                else
                {
                    allTypes = assembly.GetExportedTypes();
                }

                foreach (Type type in allTypes)
                {
                    string typeId = GetTypeIdFor(type);
                    if (typeId != null)
                    {
                        RegisterTypeId(typeId, type);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the type that cooresponds to the typeId provided (typeId could be registered typeId or <see cref="Type"/>)
        /// </summary>
        public static bool TryGetType (string typeId, out Type type)
        {
            if (g_typeAliases.TryGetValue(typeId, out type))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get the typeId for the given type <typeparamref name="T"/>
        /// </summary>
        /// <returns>The typeId for <typeparamref name="T"/> or null if none found</returns>
        public static string GetTypeIdFor<T> ()
        {
            return GetTypeIdFor(typeof(T));
        }

        /// <summary>
        /// Get the typeId for the given type
        /// </summary>
        /// <returns>The typeId for type or null if none found</returns>
        public static string GetTypeIdFor (Type type)
        {
            var idAttr = type.GetAttributes<TypeIdAttribute>()?.FirstOrDefault();
            if (idAttr != null)
            {
                return idAttr.Id;
            }

            return null;
        }
    }
}
