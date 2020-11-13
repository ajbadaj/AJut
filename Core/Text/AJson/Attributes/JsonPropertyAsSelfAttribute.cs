namespace AJut.Text.AJson
{
    using System;

    /// <summary>
    /// Use this attribute to indicate that your class has a property that should be get/set in place of the class
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple=false, Inherited=true)]
    public class JsonPropertyAsSelfAttribute : Attribute
    {
        public JsonPropertyAsSelfAttribute (string propertyName)
        {
            this.PropertyName = propertyName;
        }

        public string PropertyName { get; set; }
    }
}
