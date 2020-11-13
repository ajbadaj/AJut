namespace AJut.Text.AJson
{
    using System;

    /// <summary>
    /// Use this attribute to indicate you do not want a property to be serialized
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple=false, Inherited=true)]
    public class JsonIgnoreAttribute : Attribute { }
}
