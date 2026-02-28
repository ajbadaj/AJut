namespace AJut.TypeManagement
{
    /// <summary>
    /// Controls the default tier ordering used by <see cref="TypeMetadataExtensionRegistrar.GetOrderedProperties"/>
    /// when no explicit tier order is registered via <see cref="TypeMetadataExtension.SetTierOrder"/> or
    /// <see cref="TypeTierOrderAttribute"/>. Only affects tiers without explicit ordering; explicit
    /// registrations and attributes are always honored regardless of this setting.
    /// </summary>
    public enum eMemberInheritanceOrdering
    {
        /// <summary>Base class properties appear first, most-derived class properties appear last.</summary>
        BaseFirst,

        /// <summary>Most-derived class properties appear first, base class properties appear last.</summary>
        DerivedFirst,
    }
}
