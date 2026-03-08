namespace AJut.UX.PropertyInteraction
{
    using System;

    /// <summary>
    /// PropertyGrid attr: Conditionally shows a property or button based on a boolean member.
    /// The target member can be a property or a method that returns bool. Methods may optionally
    /// accept a single <see cref="PropertyEditTarget"/> parameter for context.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class PGShowIfAttribute : Attribute
    {
        public PGShowIfAttribute (string targetMember)
        {
            this.TargetMember = targetMember;
        }

        /// <summary>The name of the boolean property or method to evaluate.</summary>
        public string TargetMember { get; }

        /// <summary>
        /// The boolean value that, when matched by the target member, makes the property visible.
        /// Default is true (show when the target member evaluates to true).
        /// Set to false for inverse logic (show when the target member evaluates to false).
        /// </summary>
        public bool ShowWhen { get; set; } = true;
    }
}
