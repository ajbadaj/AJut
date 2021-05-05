namespace AJut.Application.Controls
{
    using System;

    /// <summary>
    /// <see cref="PropertyGrid"/> attr: Hide from the property grid
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class PGHiddenAttribute : Attribute { }
}
