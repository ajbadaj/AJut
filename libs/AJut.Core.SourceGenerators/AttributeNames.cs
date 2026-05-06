namespace AJut.Text.AJson.SourceGenerators
{
    /// <summary>
    /// String constants for the attribute / type lookups the generator does. Centralized so a
    /// rename in AJut.Core only needs to update one spot.
    /// </summary>
    internal static class AttributeNames
    {
        public const string kNamespace = "AJut.Text.AJson";

        public const string kOptimizeAJson = "AJut.Text.AJson.OptimizeAJsonAttribute";
        public const string kAJsonConstructor = "AJut.Text.AJson.AJsonConstructorAttribute";

        public const string kPropertyAlias = "AJut.Text.AJson.JsonPropertyAliasAttribute";
        public const string kPropertyAsSelf = "AJut.Text.AJson.JsonPropertyAsSelfAttribute";
        public const string kOmitIfDefault = "AJut.Text.AJson.JsonOmitIfDefaultAttribute";
        public const string kRuntimeTypeEval = "AJut.Text.AJson.JsonRuntimeTypeEvalAttribute";
        public const string kIgnore = "AJut.Text.AJson.JsonIgnoreAttribute";

        public const string kETypeIdInfo = "AJut.Text.AJson.eTypeIdInfo";
    }
}
