namespace AJut.TypeManagement
{
    using System;

    /// <summary>
    /// Sets the display/serialization order for a specific property or field within its declaring
    /// type's tier. Lower values appear first. When absent, declaration order (MetadataToken) is used.
    /// Works in conjunction with <see cref="TypeMetadataExtensionRegistrar"/> ordering, which takes
    /// precedence over this attribute for types you control registration for externally.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    public sealed class MemberOrderAttribute : Attribute
    {
        public MemberOrderAttribute (int order)
        {
            this.Order = order;
        }

        public int Order { get; }
    }
}
