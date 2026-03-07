namespace AJut.UX.PropertyInteraction
{
    using System;

    /// <summary>
    /// PropertyGrid attr: Specifies a custom coercion method for a property's value.
    /// The method replaces the default type coercion (<c>Convert.ChangeType</c>).
    ///
    /// Supported method signatures (instance or static):
    ///   <c>object CoerceMethod(object value)</c>
    ///   <c>object CoerceMethod(object value, PropertyEditTarget target)</c>
    ///
    /// The method receives the raw value from the editor and must return
    /// the coerced value to set on the source property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class PGCoerceAttribute : Attribute
    {
        public PGCoerceAttribute (string memberName)
        {
            this.MemberName = memberName;
        }

        /// <summary>The name of the coercion method (instance or static) on the source type.</summary>
        public string MemberName { get; }
    }
}
