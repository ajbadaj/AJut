namespace AJut.UX.PropertyInteraction
{
    /// <summary>
    /// Controls when a list element row in the <see cref="PropertyGrid"/> shows its
    /// element header (the value resolved from <see cref="Attributes.PGListAttribute.ElementDisplayMemberName"/>).
    /// </summary>
    public enum eElementHeaderDisplay
    {
        /// <summary>Show the header whether the element row is expanded or collapsed.</summary>
        Always,

        /// <summary>Show the header only while the element row is collapsed.</summary>
        WhenCollapsed,
    }
}
