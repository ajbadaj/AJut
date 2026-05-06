namespace AJut.Text.AJson.Legacy
{
    using System;

    /// <summary>
    /// Read/write runtime type info
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    [Obsolete("AJson V1 is moved to AJut.Text.AJson.Legacy and will be removed in a future release. Migrate to AJut.Text.AJson (V2). See AJut README for migration notes.")]
    public class JsonRuntimeTypeEvalAttribute : Attribute
    {
        public JsonRuntimeTypeEvalAttribute(eTypeIdInfo typeWriteTarget = eTypeIdInfo.Any)
        {
            this.TypeWriteTarget = typeWriteTarget;
        }

        public eTypeIdInfo TypeWriteTarget { get; }
    }
}
