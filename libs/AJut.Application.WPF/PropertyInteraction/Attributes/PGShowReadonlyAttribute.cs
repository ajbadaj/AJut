namespace AJut.Application.PropertyInteraction
{
    using System;

    /// <summary>
    /// <see cref="PropertyGrid"/> attr: Should readonly property be shown? (If tagged on class, should all readonly properties be shown?)
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class PGShowReadonlyAttribute : Attribute { }
}
