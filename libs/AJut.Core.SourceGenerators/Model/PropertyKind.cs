namespace AJut.Text.AJson.SourceGenerators.Model
{
    /// <summary>
    /// How the emitter should treat a property's underlying type. Decided at analysis time so
    /// the emitter is just an unrolling step - no symbol queries needed during emit.
    /// </summary>
    internal enum ePropertyKind
    {
        /// <summary>
        /// String, primitive number, bool, char - direct AddProperty + StringParser.Convert.
        /// </summary>
        SimpleValue,

        /// <summary>
        /// Enum - written as quoted string, read via StringParser.
        /// </summary>
        Enum,

        /// <summary>
        /// DateTime, TimeSpan, Guid, Vector2, TimeZoneInfo - the curated list of types that have
        /// custom string makers / constructors registered in JsonBuilderSettings /
        /// JsonInterpreterSettings by default. Treated as simple values for write/read but the
        /// parser side has to route through the settings hook.
        /// </summary>
        BuiltInCustom,

        /// <summary>
        /// Reference type that is itself a candidate for nested object serialization. Generated
        /// code recurses via JsonHelper for these.
        /// </summary>
        ComplexReference,

        /// <summary>
        /// Array / List / IEnumerable. Generated code emits explicit StartArray + per-element
        /// recursion.
        /// </summary>
        Collection,

        /// <summary>
        /// Dictionary. Same shape as Collection but with KVP element type and the dictionary-add
        /// path on read.
        /// </summary>
        Dictionary,

        /// <summary>
        /// Property carries [JsonRuntimeTypeEval] - emit the wrapper-doc shape on write, unwrap
        /// on read. Underlying property type is usually an interface or abstract base.
        /// </summary>
        RuntimeTypeEval,

        /// <summary>
        /// Property type is something the generator does not know how to handle. The analysis
        /// layer reports AJSON002 for these and the emitter skips them so the generated code
        /// still compiles even if the consumer ignores the diagnostic.
        /// </summary>
        Unsupported,
    }
}
