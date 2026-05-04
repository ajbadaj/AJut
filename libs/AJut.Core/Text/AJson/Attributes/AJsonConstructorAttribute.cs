namespace AJut.Text.AJson
{
    using System;

    /// <summary>
    /// Marks a constructor as the one the AJson source generator should call when constructing
    /// an instance of an [OptimizeAJson]-annotated type that has no parameterless constructor.
    /// The generator matches each constructor parameter to a json property by name (case
    /// insensitive) and emits a direct constructor call with the resolved values.
    /// </summary>
    /// <remarks>
    /// Only applicable to types opted into the source generator. The reflection path uses
    /// JsonInterpreterSettings.RegisterCustomConstructor for the same purpose.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    public sealed class AJsonConstructorAttribute : Attribute
    {
    }
}
