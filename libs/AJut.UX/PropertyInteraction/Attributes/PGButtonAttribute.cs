namespace AJut.UX.PropertyInteraction
{
    using System;

    /// <summary>
    /// PropertyGrid attr: Marks a method to appear as a clickable button in the property grid.
    /// The method must be zero-parameter. Works with <see cref="PGShowIfAttribute"/> and
    /// <see cref="PGHideIfAttribute"/> for conditional visibility.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class PGButtonAttribute : Attribute
    {
        public PGButtonAttribute () { }

        public PGButtonAttribute (string buttonName)
        {
            this.ButtonName = buttonName;
        }

        /// <summary>
        /// The display text for the button. If null or empty, the method name
        /// is converted to a friendly display string.
        /// </summary>
        public string ButtonName { get; }
    }
}
