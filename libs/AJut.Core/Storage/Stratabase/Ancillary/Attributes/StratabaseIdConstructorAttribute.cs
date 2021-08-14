namespace AJut.Storage
{
    using System;

    /// <summary>
    /// Denotes a constructor, which only takes in a single Guid, whose purpose is to construct an element represented in the database in a way that the Id may be passed in (allowing the class to utilize a readonly Id).
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = true)]
    public class StratabaseIdConstructorAttribute : StrataIncludeReadonlyAttribute
    {
        public StratabaseIdConstructorAttribute ()
        {
        }
    }
}
