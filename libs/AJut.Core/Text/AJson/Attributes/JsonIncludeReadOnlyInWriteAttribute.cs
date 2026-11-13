namespace AJut.Text.AJson
{
    using System;

    /// <summary>
    /// Where the member names given to a <see cref="JsonIncludeReadOnlyInWriteAttribute"/> are expected to be found
    /// </summary>
    public enum eAllowedProperties
    {
        /// <summary>
        /// On the declared type of the member the attribute is on (or of its items, for a collection). The source generator
        /// warns about a name it can't find there (AJSON009), and a value without one is logged when it is written.
        /// </summary>
        DeclaredType,

        /// <summary>
        /// On a type derived from the declared type, such as the Radius of a Circle held in a member declared as Shape. A value
        /// without the name is expected (a plain Shape has no Radius), so neither the source generator's warning nor the log
        /// when it is written reports it.
        /// </summary>
        DerivedTypes,
    }

    /// <summary>
    /// Writes get-only members that AJson otherwise leaves out of what it writes. By default a get-only property or readonly
    /// field is written only when AJson's reader can get it back (see <see cref="JsonBuilderSettings.UseReadonlyObjectProperties"/>),
    /// so a computed or constant value, a type discriminator, or a Rect's Right, is not. This puts chosen ones in, for
    /// something other than AJson's reader to see. AJson's reader passes over the keys it can't set, so the text still reads
    /// back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With no names, <c>[JsonIncludeReadOnlyInWrite]</c> writes the member it is on. On a member that is written anyway it
    /// changes nothing.
    /// </para>
    /// <para>
    /// With names, <c>[JsonIncludeReadOnlyInWrite("Left", "Right")]</c> also writes those members of the member's value, on top
    /// of everything the value writes by default, which is how to reach a type that can't carry the attribute itself. A name
    /// is the member's C# name, and the value is written under the member's own key, its <see cref="JsonPropertyAliasAttribute"/>
    /// included. A dotted name reaches further down ("Bounds.Right"). On a collection the names apply to each item, a
    /// dictionary's values included, and a dotted name that crosses a collection applies to each of its items. A name that
    /// matches a member written anyway adds nothing. The names are looked up on the runtime type of each value, so a member
    /// declared as Shape writes a Circle's Radius; pass <see cref="eAllowedProperties.DerivedTypes"/> to say a name is only on
    /// a subtype, which quiets both the source generator's warning and the log below for it.
    /// </para>
    /// <para>
    /// A name that matches no public member of the value it is looked up on is logged as an error once per type and name,
    /// unless it was given with <see cref="eAllowedProperties.DerivedTypes"/>, and the rest is still written. A value written as one value (a string, a number, anything a converter writes) has no members
    /// to add, and is logged the same way.
    /// </para>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class JsonIncludeReadOnlyInWriteAttribute : Attribute
    {
        /// <summary>
        /// Writes the member this is on, and the named members of its value
        /// </summary>
        /// <param name="memberNames">Members of the value to write as well, by C# name, dotted to reach further down</param>
        public JsonIncludeReadOnlyInWriteAttribute (params string[] memberNames)
            : this(eAllowedProperties.DeclaredType, memberNames)
        {
        }

        /// <summary>
        /// Writes the member this is on, and the named members of its value, saying where the names are expected to be found
        /// </summary>
        /// <param name="allowedProperties">Whether the names may be on a subtype of the declared type</param>
        /// <param name="memberNames">Members of the value to write as well, by C# name, dotted to reach further down</param>
        public JsonIncludeReadOnlyInWriteAttribute (eAllowedProperties allowedProperties, params string[] memberNames)
        {
            this.AllowedProperties = allowedProperties;
            this.MemberNames = memberNames ?? Array.Empty<string>();
        }

        /// <summary>
        /// Whether the names may be on a subtype of the declared type
        /// </summary>
        public eAllowedProperties AllowedProperties { get; }

        /// <summary>
        /// Members of the value to write as well, by C# name, dotted to reach further down. Empty when only the member itself is
        /// written.
        /// </summary>
        public string[] MemberNames { get; }
    }
}
