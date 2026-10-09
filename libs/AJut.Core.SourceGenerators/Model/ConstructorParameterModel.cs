namespace AJut.Text.AJson.SourceGenerators.Model
{
    /// <summary>
    /// One parameter of the constructor a generated reader builds through. Frozen record, like the other models, so the emitter
    /// needs nothing from the symbol model.
    /// </summary>
    internal sealed record ConstructorParameterModel
    {
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Fully qualified type name of the parameter. A matched property's json value is read as this type, since this is what
        /// the constructor takes.
        /// </summary>
        public string TypeFullName { get; init; } = string.Empty;

        /// <summary>
        /// Name of the property the parameter matches (same name, case-insensitive), whose json key it reads. Empty when no
        /// property matches, in which case the argument is always <see cref="MissingValueExpression"/>.
        /// </summary>
        public string PropertyName { get; init; } = string.Empty;

        /// <summary>
        /// The C# expression passed when the json has no key for the parameter: the matched property's [JsonOmitIfDefault(x)]
        /// value, else the parameter's declared default, else the type's default
        /// </summary>
        public string MissingValueExpression { get; init; } = string.Empty;
    }
}
