namespace AJut.Text.AJson.SourceGenerators
{
    using Microsoft.CodeAnalysis;

    /// <summary>
    /// The full set of diagnostics the AJson source generator reports. IDs are AJSON001 onward;
    /// the original Phase D plan reserved AJSON001 for "type is not partial" but the emit shape
    /// shifted to external static helpers, so the partial constraint went away and the IDs were
    /// renumbered to start clean.
    /// </summary>
    internal static class Diagnostics
    {
        private const string kCategory = "AJson";

        public static readonly DiagnosticDescriptor MissingParameterlessConstructor = new DiagnosticDescriptor(
            id: "AJSON001",
            title: "AJson optimized type has no usable constructor",
            messageFormat: "Type '{0}' is opted into AJson optimization but has no usable constructor: {1}",
            category: kCategory,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnsupportedPropertyType = new DiagnosticDescriptor(
            id: "AJSON002",
            title: "AJson optimized type has property of unsupported type",
            messageFormat: "Property '{0}.{1}' has unsupported type '{2}' for source generation - apply [JsonRuntimeTypeEval] for polymorphic interfaces or [JsonIgnore] to skip",
            category: kCategory,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor OmitIfDefaultTypeMismatch = new DiagnosticDescriptor(
            id: "AJSON003",
            title: "JsonOmitIfDefault explicit value type mismatch",
            messageFormat: "Property '{0}.{1}' has [JsonOmitIfDefault] with explicit value of type '{2}' but the property's type is '{3}'",
            category: kCategory,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        // AJSON004 was an init-only property the generated reader could not set. It never shipped, and the reader now sets every
        //  init-only property, so the id is left unused rather than given a new meaning.

        public static readonly DiagnosticDescriptor MultipleAJsonConstructors = new DiagnosticDescriptor(
            id: "AJSON005",
            title: "AJson optimized type marks more than one constructor with [AJsonConstructor]",
            messageFormat: "Type '{0}' has {1} constructors marked [AJsonConstructor] - mark exactly one",
            category: kCategory,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnmatchedConstructorParameter = new DiagnosticDescriptor(
            id: "AJSON006",
            title: "Constructor parameter matches no property",
            messageFormat: "Parameter '{1}' of the constructor AJson builds '{0}' with matches no property by name, so json never sets it and it always gets its declared default, or the type's default when it has none",
            category: kCategory,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ConstructorDefaultDiffersFromOmitDefault = new DiagnosticDescriptor(
            id: "AJSON007",
            title: "Constructor parameter default differs from the property's JsonOmitIfDefault value",
            messageFormat: "Parameter '{1}' of the constructor AJson builds '{0}' with defaults to {2}, but property '{3}' is [JsonOmitIfDefault{4}] - the writer leaves it out at {5}, so a missing key passes {5}",
            category: kCategory,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }
}
