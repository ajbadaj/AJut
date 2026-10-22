namespace AJut.TypeManagement
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Similar to the normal <see cref="Activator"/>, but with some specialty handlers to include the <see cref="TypeIdRegistrar"/> - as well as to avoid common pitfalls
    /// </summary>
    public static class AJutActivator
    {
        private static readonly Dictionary<Type, Func<object>> g_typeFactories = new Dictionary<Type, Func<object>>();

        /// <summary>
        /// Create an instance of the given type, avoiding common <see cref="Activator.CreateInstance"/> issues, and
        /// including the custom type factories registered.
        /// </summary>
        public static object CreateInstanceOf ([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type)
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
                return CreateInstanceOfArray(type, 0);
            }

            return Activator.CreateInstance(type);
        }

        /// <summary>
        /// Create an instance of the given type, avoiding common <see cref="Activator.CreateInstance"/> issues, and
        /// including the custom type factories registered.
        /// </summary>
        public static object CreateInstanceOfArray ([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type arrayType, int arrayCount)
        {
            if (arrayType.IsArray)
            {
                return CreateArray(arrayType, arrayCount);
            }

            return Activator.CreateInstance(arrayType);

            // An array's constructors are made by the runtime for its array type rather than written in IL, so there is
            //  nothing on one for the trimmer to remove, and the trimmer's analysis can't say so itself.
            [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "An array type's constructors come from the runtime, not from IL the trimmer could remove.")]
            static object CreateArray (Type arrayType, int arrayCount) => Activator.CreateInstance(arrayType, new object[] { arrayCount });
        }

        /// <summary>
        /// Create an instance of the type associated to the given type identifier
        /// </summary>
        [RequiresUnreferencedCode("Falls back to finding the type by name when its id isn't registered, and trimming can remove a type nothing else uses. Register the type with TypeIdRegistrar to keep it.")]
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
