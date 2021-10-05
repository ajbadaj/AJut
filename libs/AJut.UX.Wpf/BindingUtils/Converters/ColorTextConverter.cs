namespace AJut.UX.Converters
{
    using System.Windows.Media;

    public class ColorTextConverter : SimpleValueConverter<string, Brush>
    {
        protected override Brush Convert (string value) => CoerceUtils.CoerceBrushFrom(value);
    }
}