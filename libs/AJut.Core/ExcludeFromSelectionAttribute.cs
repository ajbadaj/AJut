namespace AJut
{
    using System;

    /// <summary>
    /// Marks an enum member (or any field) as excluded from auto-built selection UI like
    /// EnumToggleStrip / EnumComboBox. Useful for hiding sentinel values (None, All, etc.)
    /// from a strip without polluting the enum itself with [Browsable(false)] (which carries
    /// some incidental designer-affecting behavior).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class ExcludeFromSelectionAttribute : Attribute { }
}
