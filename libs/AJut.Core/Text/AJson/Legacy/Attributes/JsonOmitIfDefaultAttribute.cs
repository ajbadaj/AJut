namespace AJut.Text.AJson.Legacy
{
    using System;

    /// <summary>
    /// Use this attribute to indicate that a property should not be written to JSON
    /// when its value equals the default for its declared type. Reading JSON is not
    /// affected, so files that still include the property value will populate it
    /// correctly -- the attribute only trims noise from the write side.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class JsonOmitIfDefaultAttribute : Attribute { }
}
