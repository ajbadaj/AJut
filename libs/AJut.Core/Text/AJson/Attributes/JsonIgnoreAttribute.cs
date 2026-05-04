namespace AJut.Text.AJson
{
    using System;

    /// <summary>
    /// Marks a property to be skipped entirely by AJson - not written on serialize, not consumed on deserialize.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class JsonIgnoreAttribute : Attribute { }
}
