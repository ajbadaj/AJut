namespace AJut.TypeManagement
{
    using System;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public class TypeIdAttribute : Attribute
    {
        public TypeIdAttribute (string id)
        {
            this.Id = id;
        }

        public string Id { get; }
    }
}
