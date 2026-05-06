namespace AJut.Text.AJson
{
    using System;
    using System.Collections.Concurrent;

    /// <summary>
    /// Writes a json representation of <paramref name="instance"/> into <paramref name="builder"/>.
    /// Called by JsonHelper when a generated serializer is registered for the source type.
    /// Generated code casts the boxed instance back to the concrete type internally.
    /// </summary>
    public delegate void AJsonGeneratedWriter (object instance, JsonBuilder builder);

    /// <summary>
    /// Reads a json value into a fresh instance of the registered type. Owner json (if any)
    /// receives error reports through <see cref="Json.Failure(string)"/> instead of throws -
    /// matches the rest of V2's errors-or-value contract.
    /// </summary>
    public delegate object AJsonGeneratedReader (JsonValue value, JsonInterpreterSettings settings, Json owner);

    /// <summary>
    /// Bundle for a generated serializer registration. Both delegates are required - registering
    /// without one of them is meaningless.
    /// </summary>
    public readonly struct AJsonGeneratedSerializer
    {
        public AJsonGeneratedSerializer (AJsonGeneratedWriter writer, AJsonGeneratedReader reader)
        {
            this.Writer = writer ?? throw new ArgumentNullException(nameof(writer));
            this.Reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        public AJsonGeneratedWriter Writer { get; }
        public AJsonGeneratedReader Reader { get; }
    }

    /// <summary>
    /// Registry that the AJson source generator populates at module-init time. JsonHelper
    /// consults this map before falling through to the reflection path.
    /// </summary>
    public static class AJsonGeneratedDispatch
    {
        private static readonly ConcurrentDictionary<Type, AJsonGeneratedSerializer> g_byType
            = new ConcurrentDictionary<Type, AJsonGeneratedSerializer>();

        /// <summary>
        /// Registers a generated serializer pair for <paramref name="targetType"/>. Called by the
        /// emitted [ModuleInitializer] code in consumer assemblies. Last-write-wins if the same
        /// type is registered twice (would only happen if a consumer opts in both per-type and
        /// per-assembly, which is harmless - the generated code is equivalent).
        /// </summary>
        public static void Register (Type targetType, AJsonGeneratedWriter writer, AJsonGeneratedReader reader)
        {
            if (targetType == null)
            {
                throw new ArgumentNullException(nameof(targetType));
            }

            g_byType[targetType] = new AJsonGeneratedSerializer(writer, reader);
        }

        /// <summary>
        /// Lookup hook used by JsonHelper. Returns true and populates the out parameter if a
        /// generated serializer is registered for the type.
        /// </summary>
        public static bool TryGet (Type targetType, out AJsonGeneratedSerializer serializer)
        {
            return g_byType.TryGetValue(targetType, out serializer);
        }

        /// <summary>
        /// Diagnostic hook for tests / introspection - reports whether a generated serializer
        /// has been registered for the given type.
        /// </summary>
        public static bool IsRegistered (Type targetType) => g_byType.ContainsKey(targetType);
    }
}
