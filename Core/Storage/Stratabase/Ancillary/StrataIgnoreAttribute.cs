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

        public bool WhenInput { get; set; }
        public bool WhenOutput { get; set; }
    }
}
