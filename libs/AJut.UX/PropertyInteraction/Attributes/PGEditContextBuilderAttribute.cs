namespace AJut.UX.PropertyInteraction
{
    using System;

    /// <summary>
    /// <see cref="PropertyGrid"/> attr: Specifies an EditContext to build from a registered
    /// <see cref="AJut.TypeManagement.TypeIdAttribute"/> type and a JSON string. The type is
    /// resolved via <see cref="AJut.TypeManagement.TypeIdRegistrar"/> and the JSON is
    /// deserialized via AJson into that type, then assigned to the property's EditContext.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class PGEditContextBuilderAttribute : Attribute
    {
        public PGEditContextBuilderAttribute (string typeId, string json)
        {
            this.TypeId = typeId;
            this.Json = json;
        }

        public string TypeId { get; set; }
        public string Json { get; set; }
    }
}
