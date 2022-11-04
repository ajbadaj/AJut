namespace AJut.UX.Converters
{
    using System;
    using System.Globalization;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Markup;

    public class ParameterizedBorderGapMaskConverter : MarkupExtension, IMultiValueConverter
    {
        private readonly BorderGapMaskConverter m_borderGapMaskConverter = new BorderGapMaskConverter();
        public double GapStart { get; set; }

        public override object ProvideValue (IServiceProvider serviceProvider) => this;

        public object Convert (object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return m_borderGapMaskConverter.Convert(values, targetType, this.GapStart, culture);
        }

        public object[] ConvertBack (object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
