namespace AJut.Storage
{
    using System;

    /// <summary>
    /// Indicates that a readonly property should be used in <see cref="Stratabase.SetBaselineFromPropertiesOf"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class StrataIncludeReadonlyAttribute : Attribute { }
}
