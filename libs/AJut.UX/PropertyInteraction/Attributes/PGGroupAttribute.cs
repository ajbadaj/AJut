namespace AJut.UX.PropertyInteraction
{
    using System;

    /// <summary>
    /// <see cref="PropertyGrid"/> attr: Groups properties with the same group ID under a shared
    /// expandable header node. The group appears at the position of the first member.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class PGGroupAttribute : Attribute
    {
        public PGGroupAttribute (string groupId)
        {
            this.GroupId = groupId;
        }

        public string GroupId { get; }
    }
}
