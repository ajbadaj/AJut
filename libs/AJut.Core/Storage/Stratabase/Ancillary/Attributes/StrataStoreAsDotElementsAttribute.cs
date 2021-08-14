namespace AJut.Storage
{
    using System;

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
    public class StrataStoreAsDotElementsAttribute : Attribute
    {
    }
}
