namespace AJut.Text.AJson
{
    using System;

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class JsonTypeIdAttribute : Attribute
    {
        public JsonTypeIdAttribute (string id)
        {
            this.Id = id;
        }

        public string Id { get; }
    }
}
