namespace AJut.Application.Converters
{
#if WINDOWS_UWP
    using Windows.UI.Xaml;
#else
    using System.Windows;
    using System.Windows.Media;
#endif

    public class ColorTextConverter : SimpleValueConverter<string, Brush>
    {
        protected override Brush Convert (string value) => CoerceUtils.CoerceBrushFrom(value);
    }
}