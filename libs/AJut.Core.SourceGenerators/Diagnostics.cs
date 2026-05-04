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
            messageFormat: "Type '{0}' is opted into AJson optimization but has no parameterless constructor and no constructor marked [AJsonConstructor]",
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
    }
}
