﻿namespace AJut.TypeManagement
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    public static class TypeIdRegistrar
    {
        private static readonly Dictionary<string, Type> g_typeAliases = new Dictionary<string, Type>();
        private static readonly HashSet<string> g_alreadySearchedAemblies = new HashSet<string>();

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
            if (g_typeAliases.TryGetValue(id, out Type existing) && existing != type)
            {
                return false;
            }

            g_typeAliases.Add(id, type);
            return true;
        }

        /// <summary>
        /// Register all <see cref="TypeIdAttribute"/> type ids from the given assembly
        /// </summary>
        /// <param name="assembly">The assembly to search</param>
        /// <param name="forceSearch">Whether or not to search again if the assembly has already been searched (cached by name)</param>
        public static void RegisterAllTypeIds (Assembly assembly, bool forceSearch = false)
        {
            if (!forceSearch && g_alreadySearchedAemblies.Contains(assembly.FullName))
            {
                return;
            }

            g_alreadySearchedAemblies.Add(assembly.FullName);

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
