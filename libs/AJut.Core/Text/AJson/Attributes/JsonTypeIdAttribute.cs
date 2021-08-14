namespace AJut.Text.AJson
{
    using System;
    using AJut.TypeManagement;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    [Obsolete("JsonTypeId will be deprecated soon in favor of the more generically applicable TypeId attribute")]
    public class JsonTypeIdAttribute : TypeIdAttribute
    {
        public JsonTypeIdAttribute (string id) : base(id)
        {
        }
    }
}
