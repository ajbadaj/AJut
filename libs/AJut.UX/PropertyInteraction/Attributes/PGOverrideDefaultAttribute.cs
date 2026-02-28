namespace AJut.UX.PropertyInteraction
{
    using System;

    /// <summary>
    /// PropertyGrid attr: Specifies the default value for a property.
    /// Used by the "Set to default" context menu and IsAtDefaultValue tracking.
    ///
    /// Use typed constructors for literal values:
    ///   [PGOverrideDefault(3.14f)]
    ///   [PGOverrideDefault(42)]
    ///
    /// Use the string constructor with nameof() for method-based defaults.
    /// The method must be a zero-parameter instance or static method on the source object's type:
    ///   [PGOverrideDefault(nameof(GetOverrideWidthDefault))]
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class PGOverrideDefaultAttribute : Attribute
    {
        public PGOverrideDefaultAttribute(bool value)
        {
            this.FixedDefaultValue = value;
            this.IsMethodBased = false;
        }

        public PGOverrideDefaultAttribute(byte value)
        {
            this.FixedDefaultValue = value;
            this.IsMethodBased = false;
        }

        public PGOverrideDefaultAttribute(short value)
        {
            this.FixedDefaultValue = value;
            this.IsMethodBased = false;
        }

        public PGOverrideDefaultAttribute(int value)
        {
            this.FixedDefaultValue = value;
            this.IsMethodBased = false;
        }

        public PGOverrideDefaultAttribute(long value)
        {
            this.FixedDefaultValue = value;
            this.IsMethodBased = false;
        }

        public PGOverrideDefaultAttribute(float value)
        {
            this.FixedDefaultValue = value;
            this.IsMethodBased = false;
        }

        public PGOverrideDefaultAttribute(double value)
        {
            this.FixedDefaultValue = value;
            this.IsMethodBased = false;
        }

        public PGOverrideDefaultAttribute(decimal value)
        {
            this.FixedDefaultValue = value;
            this.IsMethodBased = false;
        }

        public PGOverrideDefaultAttribute(char value)
        {
            this.FixedDefaultValue = value;
            this.IsMethodBased = false;
        }

        /// <summary>
        /// Method-based default. Pass nameof(YourMethod) where YourMethod is a
        /// zero-parameter public or private instance (or static) method on the source object.
        /// The method is called once when the PropertyEditTarget is created.
        /// </summary>
        public PGOverrideDefaultAttribute(string methodName)
        {
            this.MethodName = methodName;
            this.IsMethodBased = true;
        }

        /// <summary>The literal default value (for non-method-based defaults).</summary>
        public object FixedDefaultValue { get; }

        /// <summary>The method name to call for a dynamic default (for method-based defaults).</summary>
        public string MethodName { get; }

        /// <summary>True if this uses a method call rather than a literal value.</summary>
        public bool IsMethodBased { get; }
    }
}
