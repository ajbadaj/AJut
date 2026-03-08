namespace AJut.UX.PropertyInteraction
{
    using System;

    /// <summary>
    /// PropertyGrid attr: Conditionally hides a property or button based on a boolean member.
    /// The target member can be a property or a method that returns bool. Methods may optionally
    /// accept a single <see cref="PropertyEditTarget"/> parameter for context.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class PGHideIfAttribute : Attribute
    {
        public PGHideIfAttribute (string targetMember)
        {
            this.TargetMember = targetMember;
        }

        /// <summary>The name of the boolean property or method to evaluate.</summary>
        public string TargetMember { get; }

        /// <summary>
        /// The boolean value that, when matched by the target member, hides the property.
        /// Default is false - meaning the property is hidden when the target member evaluates
        /// to false (visible when true). Set to true for inverse logic (hidden when true).
        /// </summary>
        public bool HideWhen { get; set; } = false;
    }
}
