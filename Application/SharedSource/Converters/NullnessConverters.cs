namespace AJut.Application.Converters
{
#if WINDOWS_UWP
    using Windows.UI.Xaml;
#else
    using System.Windows;
#endif

    // Nullness
    public class NullnessConverter<T> : SimpleValueConverter<T>
    {
        public T WhenNull { get; set; }
        public T WhenNotNull { get; set; }

        protected override T Convert(object value)
        {
            return value == null ? WhenNull : WhenNotNull;
        }
    }
    public class NullnessChecker : NullnessConverter<bool>
    {
        public NullnessChecker()
        {
            WhenNull = true;
            WhenNotNull = false;
        }
        public NullnessChecker(bool valueWhenNull, bool valueWhenNotNull)
        {
            WhenNull = valueWhenNull;
            WhenNotNull = valueWhenNotNull;
        }
    }
    public class NullnessToVisibilityConverter : NullnessConverter<Visibility>
    {
        public NullnessToVisibilityConverter()
        {
            WhenNull = Visibility.Collapsed;
            WhenNotNull = Visibility.Visible;
        }
        public NullnessToVisibilityConverter(Visibility valueWhenNull, Visibility valueWhenNotNull)
        {
            WhenNull = valueWhenNull;
            WhenNotNull = valueWhenNotNull;
        }
    }
}
