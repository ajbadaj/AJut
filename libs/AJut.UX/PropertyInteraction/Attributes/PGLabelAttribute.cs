namespace AJut.UX.PropertyInteraction
{
    using System;

    /// <summary>
    /// <see cref="PropertyGrid"/> attr: Override the display label and optionally add a subtitle
    /// to this property's row in the property grid.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class PGLabelAttribute : Attribute
    {
        public PGLabelAttribute (string label)
        {
            this.Label = label;
        }

        public PGLabelAttribute (string label, string subtitle)
        {
            this.Label = label;
            this.Subtitle = subtitle;
        }

        public string Label { get; set; }
        public string Subtitle { get; set; }
    }
}
