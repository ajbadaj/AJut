namespace AJut.UX.PropertyInteraction
{
    using System;

    /// <summary>
    /// PropertyGrid attr: positions a property row or a <see cref="PGButtonAttribute"/> method row
    /// within the grid. Lower values appear first. Tagged members sort ahead of untagged ones, which
    /// keep their natural declaration order. Because it targets both properties and methods, this is
    /// what lets buttons interleave with properties rather than always trailing at the bottom.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class PGMemberOrderAttribute : Attribute
    {
        public PGMemberOrderAttribute (int order)
        {
            this.Order = order;
        }

        public int Order { get; }
    }
}
