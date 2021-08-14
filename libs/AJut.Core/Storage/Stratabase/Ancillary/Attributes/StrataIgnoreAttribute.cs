namespace AJut.Storage
{
    using System;

    /// <summary>
    /// Do not include the indicated property in serialization into <see cref="Stratabase"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class StrataIgnoreAttribute : Attribute 
    {
        public StrataIgnoreAttribute (bool ignoreWhenInput = true, bool ignoreWhenOutput = false)
        {
            this.WhenInput = ignoreWhenInput;
            this.WhenOutput = ignoreWhenOutput;
        }

        /// <summary>
        /// Indicates that a property should be used as input in <see cref="Stratabase.SetBaselineFromPropertiesOf"/>
        /// </summary>
        public bool WhenInput { get; set; }

        /// <summary>
        /// Indicates that a property should be used as output in <see cref="Stratabase.SetObjectWithProperties"/>
        /// </summary>
        public bool WhenOutput { get; set; }
    }
}
