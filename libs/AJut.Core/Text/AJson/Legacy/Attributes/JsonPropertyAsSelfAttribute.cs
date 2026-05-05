namespace AJut.Text.AJson.Legacy
{
    using System;

    /// <summary>
    /// Use this attribute to indicate that your class has a property that should be get/set in place of the class
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple=false, Inherited=true)]
    [Obsolete("AJson V1 is moved to AJut.Text.AJson.Legacy and will be removed in a future release. Migrate to AJut.Text.AJson (V2). See AJut README for migration notes.")]
    public class JsonPropertyAsSelfAttribute : Attribute
    {
        public JsonPropertyAsSelfAttribute (string propertyName)
        {
            this.PropertyName = propertyName;
        }

        public string PropertyName { get; set; }
    }
}
