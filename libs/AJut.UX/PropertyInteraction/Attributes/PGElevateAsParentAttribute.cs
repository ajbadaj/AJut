namespace AJut.UX.PropertyInteraction
{
    using System;

    /// <summary>
    /// Placed on a property of a sub-object class. When PropertyGrid reflects the containing class,
    /// the first property tagged with this attribute supplies its editor for the parent row inline.
    /// No expand/collapse toggle is shown.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class PGElevateAsParentAttribute : Attribute 
    {
        /// <summary>
        /// Elevate this property as if it were the parent
        /// </summary>
        /// <param name="deferPGAttributesToParent">
        /// When elevated to represent the parent, it may be where the parent is used that has it's 
        /// PGEditor declared. For example, the containing type is a typed wrapper, and this property that is representing
        /// the parent is the value - that would cause it to not know how to set it's editor. In this case you would
        /// want to defer to the property that is utilizing the containing type.
        /// </param>
        public PGElevateAsParentAttribute(bool deferPGAttributesToParent = false)
        {
            this.DeferPGAttributesToParent = deferPGAttributesToParent;
        }

        /// <summary>
        /// When elevated to represent the parent, it may be where the parent is used that has it's 
        /// PGEditor declared. For example, the containing type is a typed wrapper, and this property that is representing
        /// the parent is the value - that would cause it to not know how to set it's editor. In this case you would
        /// want to defer to the property that is utilizing the containing type.
        /// </summary>
        /// <example>
        /// class ToDisplay
        /// {
        ///   public int Count { get; set; }
        ///   
        ///   [PGEditor("SpecialStringEditor")]
        ///   public CoolProp<string> Name { get; set; }
        /// }
        /// 
        /// class CoolProp<T>
        /// {
        ///    [PGElevateAsParent(deferPGAttributesToParent: true)]
        ///    public T Value { get; set; }
        /// }
        /// </example>
        public bool DeferPGAttributesToParent { get; set; }
    }
}
