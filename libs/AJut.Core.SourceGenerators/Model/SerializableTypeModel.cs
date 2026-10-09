namespace AJut.Text.AJson.SourceGenerators.Model
{
    using System.Collections.Generic;

    /// <summary>
    /// Per-type analysis result. The emitter consumes a list of these. Equality is value-based
    /// (record) so the incremental generator can cache emit work across compilations.
    /// </summary>
    internal sealed record SerializableTypeModel
    {
        /// <summary>
        /// Fully qualified type name with namespace, suitable for emission ("global::Foo.Bar").
        /// </summary>
        public string FullyQualifiedTypeName { get; init; } = string.Empty;

        /// <summary>
        /// Type's containing namespace, or empty string if global namespace. Used for the emitted helper class's namespace placement.
        /// </summary>
        public string ContainingNamespace { get; init; } = string.Empty;

        /// <summary>
        /// Mangled identifier suitable for use in the helper class name (no dots, no generic ticks). Derived from FullyQualifiedTypeName.
        /// </summary>
        public string MangledName { get; init; } = string.Empty;

        public bool IsValueType { get; init; }

        /// <summary>
        /// True when the generated reader builds the instance with new T(): the type has a non-private parameterless constructor,
        /// or is a value type that marks no constructor with [AJsonConstructor].
        /// </summary>
        public bool HasParameterlessConstructor { get; init; }

        /// <summary>
        /// True when the generated reader builds the instance through a constructor that takes <see cref="ConstructorParameters"/>:
        /// the one marked [AJsonConstructor], or a record's positional constructor. False whenever <see cref="HasParameterlessConstructor"/>
        /// is true, and when neither is, the type has no usable constructor (AJSON001 or AJSON005).
        /// </summary>
        public bool HasConstructorRoute { get; init; }

        /// <summary>
        /// True when the constructor route's constructor is not public, so the generated reader calls it through an
        /// UnsafeAccessor rather than with new
        /// </summary>
        public bool ConstructsThroughAccessor { get; init; }

        /// <summary>
        /// The parameters of the constructor route, in order. Empty unless <see cref="HasConstructorRoute"/> is true.
        /// </summary>
        public IReadOnlyList<ConstructorParameterModel> ConstructorParameters { get; init; } = System.Array.Empty<ConstructorParameterModel>();

        /// <summary>
        /// When [JsonPropertyAsSelf] is on the type, the name of the property whose content
        /// represents the entire type on the wire. Empty otherwise.
        /// </summary>
        public string PropertyAsSelfName { get; init; } = string.Empty;

        /// <summary>
        /// Properties to serialize, in the same order the reflection path would walk them.
        /// </summary>
        public IReadOnlyList<PropertyModel> Properties { get; init; } = System.Array.Empty<PropertyModel>();
    }
}
