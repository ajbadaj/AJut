namespace AJut.UX.Converters
{
    using Microsoft.UI.Xaml.Media;
    using Microsoft.UI.Xaml.Media.Imaging;

    public class StringToImageSourceConverter : SimpleValueConverter<string, ImageSource>
    {
        protected override ImageSource Convert (string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            var uri = CoerceUtils.CoerceUriSourceFrom(value);
            if (uri == null)
            {
                return null;
            }

            return new BitmapImage(uri);
        }
    }
}
