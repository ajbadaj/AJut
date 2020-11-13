namespace AJut.Storage
{
    using System;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class StratabaseIdAttribute : Attribute
    {
        public StratabaseIdAttribute (string propertyName = "")
        {
            this.PropertyName = propertyName;
        }

        public string PropertyName { get; }
        public bool IsClassDefault => this.PropertyName == String.Empty;
    }
}
