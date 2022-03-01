namespace AJut.Text.AJson
{
    using System;

    /// <summary>
    /// Read/write runtime type info
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public class JsonRuntimeTypeEvalAttribute : Attribute
    {
        public JsonRuntimeTypeEvalAttribute(eTypeIdInfo typeWriteTarget = eTypeIdInfo.Any)
        {
            this.TypeWriteTarget = typeWriteTarget;
        }

        public eTypeIdInfo TypeWriteTarget { get; }
    }
}
