namespace AJut.Text.AJson
{
    using System;

    /// <summary>
    /// Skip writing a property when its value matches a default. The zero-arg form treats
    /// default(T) (the type's zero value) as the omit marker; the explicit-default form
    /// takes a value to compare against, used when a class's initializer default differs
    /// from default(T) - the canonical case is enums whose intended default is not the
    /// underlying-zero member.
    /// </summary>
    /// <remarks>
    /// Read-side is unaffected - older files that include the default value still round-trip
    /// to the same value.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class JsonOmitIfDefaultAttribute : Attribute
    {
        public JsonOmitIfDefaultAttribute ()
        {
        }

        public JsonOmitIfDefaultAttribute (object explicitDefault)
        {
            this.HasExplicitDefault = true;
            this.ExplicitDefault = explicitDefault;
        }

        public bool HasExplicitDefault { get; }

        /// <summary>
        /// Stored as object - C# attribute argument rules force this. The omit-check runtime
        /// coerces enums through the property type before comparing so an enum-valued
        /// argument (compiled as the underlying int) still equates to the boxed enum value.
        /// </summary>
        public object ExplicitDefault { get; }
    }
}
