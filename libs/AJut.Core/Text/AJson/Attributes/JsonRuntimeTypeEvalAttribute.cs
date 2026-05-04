namespace AJut.Text.AJson
{
    using System;

    /// <summary>
    /// Property-level: serialize using the runtime type of the value (rather than the
    /// declared property type) so polymorphic graphs round-trip through an object/interface
    /// property. The written shape wraps the value in a small document carrying a
    /// "__type" key plus the actual payload under "__value"; on read the type id is used
    /// to resolve a concrete type via the AJut TypeIdRegistrar before deserializing.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public class JsonRuntimeTypeEvalAttribute : Attribute
    {
        public JsonRuntimeTypeEvalAttribute (eTypeIdInfo typeWriteTarget = eTypeIdInfo.Any)
        {
            this.TypeWriteTarget = typeWriteTarget;
        }

        public eTypeIdInfo TypeWriteTarget { get; }
    }
}
