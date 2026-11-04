namespace AJut.UX
{
    using AJut.UX.Helpers;

    /// <summary>What an <see cref="SvgPaint"/> paints with</summary>
    public enum eSvgPaintKind
    {
        /// <summary>One color, in <see cref="SvgPaint.Color"/></summary>
        Color,

        /// <summary>A linear or radial gradient, in <see cref="SvgPaint.Gradient"/></summary>
        Gradient,
    }

    // ===========[ SvgPaint ]===================================================
    // An svg fill or stroke: one color, or a gradient written as CSS gradient text (the form WPF's
    // SvgSerialization writes). No paint at all ("none") is no SvgPaint, a null.
    public sealed class SvgPaint
    {
        // ===========[ Setup/Construction/Teardown ]===========================
        private SvgPaint (eSvgPaintKind kind, ColorArgb color, GradientBuilder gradient, string text)
        {
            this.Kind = kind;
            this.Color = color;
            this.Gradient = gradient;
            this.Text = text;
        }

        /// <summary>A single color. <paramref name="text"/> is how it was written, when it was read from a document.</summary>
        public static SvgPaint FromColor (ColorArgb color, string? text = null) => new SvgPaint(eSvgPaintKind.Color, color, default, text ?? string.Empty);

        /// <summary>A gradient, from the CSS gradient text it was read from</summary>
        public static SvgPaint FromGradient (GradientBuilder gradient, string text) => new SvgPaint(eSvgPaintKind.Gradient, default, gradient, text);

        // ===========[ Properties ]============================================
        public eSvgPaintKind Kind { get; }

        /// <summary>The color, for <see cref="eSvgPaintKind.Color"/></summary>
        public ColorArgb Color { get; }

        /// <summary>The gradient, for <see cref="eSvgPaintKind.Gradient"/></summary>
        public GradientBuilder Gradient { get; }

        /// <summary>The paint as it was written in the document, for a toolkit that builds its brushes from text</summary>
        public string Text { get; }
    }
}
