namespace AJut.TypeManagement
{
    using System;

    /// <summary>
    /// Sets the ordering priority for all members declared directly on this class or struct,
    /// relative to members from other tiers in the inheritance hierarchy. Lower values appear first.
    /// When absent, inheritance depth is used (most-derived type = 0, each base type increments).
    /// Works in conjunction with <see cref="TypeMetadataExtensionRegistrar"/> registration, which
    /// takes precedence over this attribute for types you control registration for externally.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class TypeTierOrderAttribute : Attribute
    {
        public TypeTierOrderAttribute (int order)
        {
            this.Order = order;
        }

        public int Order { get; }
    }
}
