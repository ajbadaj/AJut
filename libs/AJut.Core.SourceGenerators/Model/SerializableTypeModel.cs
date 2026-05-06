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

        public bool HasParameterlessConstructor { get; init; }

        public bool HasAJsonConstructor { get; init; }

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
