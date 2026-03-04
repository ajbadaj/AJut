namespace AJut.UX.Converters
{
    using System;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    public class StringToImageSourceConverter : SimpleValueConverter<string, ImageSource>
    {
        protected override ImageSource Convert (string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            Uri uri = CoerceUtils.CoerceUriSourceFrom(value);
            if (uri == null)
            {
                return null;
            }

            return new BitmapImage(uri);
        }
    }
}
