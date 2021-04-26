namespace AJut.Text.AJson
{
    using System;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public class JsonTypeIdAttribute : Attribute
    {
        public JsonTypeIdAttribute (string id)
        {
            this.Id = id;
        }

        public string Id { get; }
    }
}
