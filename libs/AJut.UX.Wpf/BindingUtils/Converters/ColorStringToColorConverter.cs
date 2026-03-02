namespace AJut.UX.Converters
{
    using System.Windows.Media;

    public class ColorStringToColorConverter : SimpleValueConverter<string, Color>
    {
        protected override Color Convert (string value) => CoerceUtils.CoerceColorFrom(value);
    }
}