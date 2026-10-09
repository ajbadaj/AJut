namespace AJut.Text.AJson
{
    using System;

    /// <summary>
    /// Marks the constructor AJson builds a type with when reading it from json. Each parameter is passed the value of the
    /// property with the same name (matched case-insensitively), read from that property's json key, which honors
    /// <see cref="JsonPropertyAliasAttribute"/>. Properties the constructor does not take are set afterwards from their own keys,
    /// init-only ones included.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both paths honor it the same way: the reflection path (<see cref="JsonInterpreterSettings.ConstructInstanceFor"/>), and
    /// the reader the source generator writes for an [OptimizeAJson] type, which calls the constructor directly.
    /// </para>
    /// <para>
    /// Which constructor is used, in order:
    /// <list type="number">
    /// <item><description>A constructor registered with <see cref="JsonInterpreterSettings.RegisterCustomConstructor{T}"/> wins over
    /// everything else. A registered constructor on a type that also marks one here logs a warning the first time it is used.</description></item>
    /// <item><description>A non-private parameterless constructor. A value type uses its default constructor unless it marks one
    /// here.</description></item>
    /// <item><description>The one constructor marked with this attribute. Marking more than one is a compile error (AJSON005) for an
    /// [OptimizeAJson] type, and on the reflection path an error on the owning <see cref="Json"/> when the type gets this far.</description></item>
    /// <item><description>A record's positional constructor, which needs no attribute.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// A key the json does not have is never an error, since nulls are never written. The parameter is passed, in order: the
    /// matched property's <see cref="JsonOmitIfDefaultAttribute"/> explicit value (the writer leaves the property out when it
    /// equals that), the parameter's own declared default, or the type's default. A parameter that matches no property always
    /// gets that value (warning AJSON006 for an [OptimizeAJson] type), and a declared default that differs from the omit value is
    /// warning AJSON007.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    public sealed class AJsonConstructorAttribute : Attribute
    {
    }
}
