namespace AJut.UX.PropertyInteraction
{
    using System;

    /// <summary>
    /// <see cref="PropertyGrid"/> attr: Marks a list/array/collection property for list editing.
    /// Produces an expandable parent row with an inline element count and add button,
    /// and child rows for each element with editors and delete buttons.
    /// Supports drag/drop reordering for indexed collection types (IList, T[]).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class PGListAttribute : Attribute
    {
        /// <summary>
        /// Optional method name on the source object to call when adding an element.
        /// Signature: void Method() or object Method() (returning the new element).
        /// When null, uses AJutActivator.CreateInstanceOf to create a default instance
        /// and adds it to the collection.
        /// </summary>
        public string AddMethodName { get; set; }

        /// <summary>
        /// Optional method name on the source object to call when removing an element.
        /// Signature: void Method(int index) or void Method(object element).
        /// When null, uses IList.RemoveAt or creates a new array without the element.
        /// </summary>
        public string RemoveMethodName { get; set; }

        /// <summary>
        /// Optional method name on the source object to validate whether a drag/drop
        /// reorder should be accepted. Signature: bool Method(int fromIndex, int toIndex).
        /// When null, all reorders within the same list are accepted.
        /// </summary>
        public string AcceptReorderMethodName { get; set; }

        /// <summary>
        /// Whether drag/drop reordering is enabled. Defaults to true for IList and T[],
        /// false for non-indexed collection types.
        /// Set explicitly to override the auto-detection.
        /// </summary>
        public bool CanReorder { get; set; } = true;

        /// <summary>
        /// Whether elements can be removed. Defaults to true.
        /// </summary>
        public bool CanRemove { get; set; } = true;

        /// <summary>
        /// Whether new elements can be added. Defaults to true.
        /// </summary>
        public bool CanAdd { get; set; } = true;
    }
}
