namespace AJut.TypeManagement
{
    using System;
    using System.Collections.Generic;

    public static class AJutActivator
    {
        private static readonly Dictionary<Type, Func<object>> g_typeFactories = new Dictionary<Type, Func<object>>();

        /// <summary>
        /// Create an instance of the given type, avoiding common <see cref="Activator.CreateInstance"/> issues, and
        /// including the custom type factories registered.
        /// </summary>
        public static object CreateInstanceOf (Type type)
        {
            if (g_typeFactories.TryGetValue(type, out var factoryFunc))
            {
                return factoryFunc();
            }

            if (type == typeof(string))
            {
                return String.Empty;
            }

            if (type.IsArray)
            {
                return Activator.CreateInstance(type, new object[] { 0 });
            }

            return Activator.CreateInstance(type);
        }

        /// <summary>
        /// Create an instance of the type associated to the given type identifier
        /// </summary>
        public static object CreateInstanceOf (string typeIdentifier)
        {
            if (TypeIdRegistrar.TryGetType(typeIdentifier, out Type type))
            {
                return CreateInstanceOf(type);
            }

            type = Type.GetType(typeIdentifier);
            if (type != null)
            {
                return CreateInstanceOf(type);
            }

            return null;
        }

        /// <summary>
        /// Register a type factory for custom instance generation
        /// </summary>
        public static void RegisterTypeFactory<T>(Func<object> factory)
        {
            RegisterTypeFactory(typeof(T), factory);
        }

        /// <summary>
        /// Register a type factory for custom instance generation
        /// </summary>
        public static void RegisterTypeFactory (Type type, Func<object> factory)
        {
            if (g_typeFactories.ContainsKey(type))
            {
                throw new Exception($"AJutActivator: Tried to register a type factory with existing type '{type.FullName}'");
            }

            g_typeFactories.Add(type, factory);
        }
    }
}
