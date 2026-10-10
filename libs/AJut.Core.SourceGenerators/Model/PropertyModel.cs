namespace AJut.Text.AJson.SourceGenerators.Model
{
    /// <summary>
    /// Per-member analysis result, for a public property or a public field. Frozen record - the
    /// emitter consumes these without going back to the symbol model. Generated code reads and
    /// assigns a field with the same syntax as a property, so the two share one model: a field
    /// always has a getter, has a setter unless it is readonly, and is never init-only.
    /// </summary>
    internal sealed record PropertyModel
    {
        /// <summary>
        /// CLR property or field name (from the source).
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// JSON key the property serializes / deserializes under. Equals Name unless [JsonPropertyAlias] is present.
        /// </summary>
        public string JsonKey { get; init; } = string.Empty;

        /// <summary>
        /// Fully qualified type name including namespace, suitable for emission ("global::Foo.Bar.Baz").
        /// </summary>
        public string TypeFullName { get; init; } = string.Empty;

        /// <summary>
        /// Underlying type name without nullable wrapper, for the StringParser conversion call. Equal to TypeFullName for non-Nullable&lt;T&gt; types.
        /// </summary>
        public string UnderlyingTypeFullName { get; init; } = string.Empty;

        public ePropertyKind Kind { get; init; }

        /// <summary>
        /// True if the declared property type is `Nullable&lt;T&gt;`. Generated read code unwraps via the nullable ctor.
        /// </summary>
        public bool IsNullable { get; init; }

        /// <summary>
        /// True if the property type is a value type (after Nullable unwrap). Drives whether the writer needs a null check before access.
        /// </summary>
        public bool IsValueType { get; init; }

        /// <summary>
        /// True if the property has an accessible setter. Read code skips properties without one (matches reflection path).
        /// </summary>
        public bool HasSetter { get; init; }

        /// <summary>
        /// True if the setter is init-only, so the generated reader cannot assign it after construction and calls the setter through an UnsafeAccessor instead.
        /// </summary>
        public bool IsInitOnly { get; init; }

        /// <summary>
        /// Fully qualified name of the type that declares the setter. For a property inherited from a base class that is the base, which is what an UnsafeAccessor for the setter has to target.
        /// </summary>
        public string DeclaringTypeFullName { get; init; } = string.Empty;

        /// <summary>
        /// Metadata name of the setter ("set_Name"), which an UnsafeAccessor looks the setter up by. Empty when there is no setter.
        /// </summary>
        public string SetterName { get; init; } = string.Empty;

        /// <summary>
        /// True if the property has an accessible getter. Write code skips properties without one.
        /// </summary>
        public bool HasGetter { get; init; }

        /// <summary>
        /// True for a get-only property or readonly field whose type is a collection with Add (any ICollection&lt;T&gt; but an
        /// array). The reader fills the collection already there, through AJsonGenerationSupport.FillGetOnlyCollection.
        /// </summary>
        public bool IsGetOnlyCollection { get; init; }

        /// <summary>
        /// `true` for typical quoted-on-write types (string, char, enum, GUID, DateTime). False for numerics / bool. Drives the AddProperty isUsuallyQuoted flag.
        /// </summary>
        public bool IsUsuallyQuoted { get; init; }

        /// <summary>
        /// True when [JsonOmitIfDefault] is present. Combined with the other Omit* fields to drive the comparison branch.
        /// </summary>
        public bool HasOmitIfDefault { get; init; }

        /// <summary>
        /// True when [JsonOmitIfDefault(value)] passed an explicit value. The literal text used in the comparison expression.
        /// </summary>
        public bool HasExplicitOmitDefault { get; init; }

        /// <summary>
        /// Literal C# expression for the explicit-default comparison (e.g. "eFoo.Center", "42", "\"none\""). Empty unless HasExplicitOmitDefault is true.
        /// </summary>
        public string ExplicitOmitDefaultLiteral { get; init; } = string.Empty;

        /// <summary>
        /// Element type's full name for collection/dictionary kinds. Empty otherwise.
        /// </summary>
        public string ElementTypeFullName { get; init; } = string.Empty;

        /// <summary>
        /// Dictionary key type name. Empty for non-dictionary kinds.
        /// </summary>
        public string DictionaryKeyTypeFullName { get; init; } = string.Empty;

        /// <summary>
        /// Dictionary value type name. Empty for non-dictionary kinds.
        /// </summary>
        public string DictionaryValueTypeFullName { get; init; } = string.Empty;

        /// <summary>
        /// For [JsonRuntimeTypeEval] - the eTypeIdInfo flag literal to emit ("AJut.Text.AJson.eTypeIdInfo.Any" by default).
        /// </summary>
        public string RuntimeTypeEvalFlagLiteral { get; init; } = string.Empty;
    }
}
