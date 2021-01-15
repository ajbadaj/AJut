namespace AJut.Application.Converters
{
    using System.Windows.Media;

    public class BrushColorExtractionConverter : SimpleValueConverter<Brush, Color>
    {
        public Color Default { get; set; } = Colors.Black;
        protected override Color Convert (Brush value)
        {
            if (value is SolidColorBrush scb)
            {
                return scb.Color;
            }

            if (value is GradientBrush gb && gb.GradientStops.Count > 0)
            {
                return gb.GradientStops[0].Color;
            }

            return this.Default;
        }
    }
}
