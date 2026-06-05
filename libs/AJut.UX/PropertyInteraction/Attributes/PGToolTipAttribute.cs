namespace AJut.UX.PropertyInteraction
{
    using System;

    /// <summary>
    /// <see cref="PropertyGrid"/> attr: The tooltip text for this property's row. By default the
    /// tooltip is prefixed with the property's display name (rendered as "$Name: $ToolTip"); set
    /// <see cref="ShowName"/> to false to show only the tooltip text. With no attribute the tooltip
    /// falls back to the display name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class PGToolTipAttribute : Attribute
    {
        public PGToolTipAttribute (string toolTip, bool showName = true)
        {
            this.ToolTip = toolTip;
            this.ShowName = showName;
        }

        public string ToolTip { get; set; }
        public bool ShowName { get; set; } = true;
    }
}
