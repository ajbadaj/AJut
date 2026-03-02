namespace AJut.UX.Converters
{
    using AJut.UX.Helpers;
    using Windows.UI;

    public class ColorStringToColorConverter : SimpleValueConverter<string, Color>
    {
        protected override Color Convert (string value) => CoerceUtils.CoerceColorFrom(value);
        protected override string ConvertBack(Color color)
        {
            return AJutColorHelper.GetSmallestHexString(color.A, color.R, color.G, color.B);
        }
    }
}