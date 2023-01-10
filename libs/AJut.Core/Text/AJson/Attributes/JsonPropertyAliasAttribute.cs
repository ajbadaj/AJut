namespace AJut.Text.AJson.Attributes
{
    using System;

    /// <summary>
    /// Use this attribute to indicate a property should be set into json, and read from json by a different name
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class JsonPropertyAliasAttribute : Attribute
    {
        public JsonPropertyAliasAttribute (string propertyName)
        {
            this.PropertyName = propertyName;
        }

        public string PropertyName { get; set; }
    }
}
