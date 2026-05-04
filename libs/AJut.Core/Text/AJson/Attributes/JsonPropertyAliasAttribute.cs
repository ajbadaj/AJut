namespace AJut.Text.AJson
{
    using System;

    /// <summary>
    /// Serialize / deserialize a property under a different json key than its CLR name.
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
