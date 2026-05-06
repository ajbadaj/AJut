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
    [Obsolete("AJson V1 is moved to AJut.Text.AJson.Legacy and will be removed in a future release. Migrate to AJut.Text.AJson (V2). See AJut README for migration notes.")]
    public class JsonOmitIfDefaultAttribute : Attribute { }
}
