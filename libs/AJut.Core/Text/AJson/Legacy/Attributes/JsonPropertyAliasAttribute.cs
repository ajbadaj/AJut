namespace AJut.Text.AJson.Legacy
{
    using System;

    /// <summary>
    /// Use this attribute to indicate a property should be set into json, and read from json by a different name
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    [Obsolete("AJson V1 is moved to AJut.Text.AJson.Legacy and will be removed in a future release. Migrate to AJut.Text.AJson (V2). See AJut README for migration notes.")]
    public class JsonPropertyAliasAttribute : Attribute
    {
        public JsonPropertyAliasAttribute (string propertyName)
        {
            this.PropertyName = propertyName;
        }

        public string PropertyName { get; set; }
    }
}
