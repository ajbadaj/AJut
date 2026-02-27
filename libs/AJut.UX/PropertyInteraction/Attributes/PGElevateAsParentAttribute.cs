namespace AJut.UX.PropertyInteraction
{
    using System;

    /// <summary>
    /// Placed on a property of a sub-object class. When PropertyGrid reflects the containing class,
    /// the first property tagged with this attribute supplies its editor for the parent row inline.
    /// No expand/collapse toggle is shown.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class PGElevateAsParentAttribute : Attribute { }
}
