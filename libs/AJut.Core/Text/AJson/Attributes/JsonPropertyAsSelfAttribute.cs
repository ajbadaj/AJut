namespace AJut.Text.AJson
{
    using System;

    /// <summary>
    /// Class-level: the named property's content is serialized in place of the class itself,
    /// flattening one wrapper layer out of the json. On read, the same flattened shape is
    /// unwrapped back into the property; the older non-elevated shape (the property written
    /// as a normal entry) is also tolerated for backward compatibility.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class JsonPropertyAsSelfAttribute : Attribute
    {
        public JsonPropertyAsSelfAttribute (string propertyName)
        {
            this.PropertyName = propertyName;
        }

        public string PropertyName { get; set; }
    }
}
