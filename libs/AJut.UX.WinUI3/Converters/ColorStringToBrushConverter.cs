namespace AJut.UX.Converters
{
    using Microsoft.UI.Xaml.Media;

    public class ColorStringToBrushConverter : SimpleValueConverter<string, Brush>
    {
        protected override Brush Convert (string value) => CoerceUtils.CoerceBrushFrom(value);
    }
}