namespace AJut.Storage
{
    using System;

    /// <summary>
    /// Indicates that this object, (which must be properly marked up here, or in the class 
    /// definition with <see cref="StratabaseIdAttribute"/>) should be stored by reference.
    /// For example, if a property Foo had a value of some StratabaseId marked up class, you
    /// could mark it as a StratabaseReference, then "Foo" would contain a Guid of another
    /// Stratabase item.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class StratabaseReferenceAttribute : Attribute
    {
    }
}
