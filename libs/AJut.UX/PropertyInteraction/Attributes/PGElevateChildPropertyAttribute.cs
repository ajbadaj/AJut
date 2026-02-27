namespace AJut.UX.PropertyInteraction
{
    using System;

    /// <summary>
    /// Placed on a complex-type property. Specifies which named child property to elevate
    /// into this row's editor slot inline (no expand/collapse toggle shown).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class PGElevateChildPropertyAttribute : Attribute
    {
        public PGElevateChildPropertyAttribute (string childPropertyName)
        {
            this.ChildPropertyName = childPropertyName;
        }

        public string ChildPropertyName { get; }
    }
}
