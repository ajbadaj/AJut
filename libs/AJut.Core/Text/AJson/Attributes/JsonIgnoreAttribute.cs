namespace AJut.Text.AJson
{
    using System;

    /// <summary>
    /// Marks a property or public field to be skipped entirely by AJson - not written on serialize, not consumed on deserialize.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class JsonIgnoreAttribute : Attribute { }
}
