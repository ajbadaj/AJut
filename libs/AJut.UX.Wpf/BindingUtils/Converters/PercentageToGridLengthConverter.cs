namespace AJut.UX.Converters
{
    using AJut.MathUtilities;
    using System;
    using System.Windows;

    public class PercentageToGridLengthConverter : SimpleValueConverter<GridLength>
    {
        public double Multiplier { get; set; } = 1.0;
        protected override GridLength Convert (object objValue)
        {
            if (System.Convert.ChangeType(objValue, typeof(double)) is double value)
            {
                return new GridLength(value * this.Multiplier, GridUnitType.Star);
            }

            throw new InvalidCastException("PercentageToGridLengthConverter, source value must be convertable to double");
        }
    }

    public class LerpPercentageToGridLengthConverter : SimpleValueConverter<GridLength>
    {
        public double Min { get; set; } = 0.0;
        public double Max { get; set; } = 1.0;
        protected override GridLength Convert (object objValue)
        {
            if (System.Convert.ChangeType(objValue, typeof(double)) is double value)
            {
                return new GridLength(Lerp.Percent(this.Min, this.Max, value), GridUnitType.Star);
            }

            throw new InvalidCastException("PercentageToGridLengthConverter, source value must be convertable to double");
        }
    }

    /// <summary>
    /// Converts double to GridLength: NaN returns Auto; any other value returns Pixel(value).
    /// Used for LabelColumnWidth binding on the WPF PropertyGrid label ColumnDefinition.
    /// </summary>
    public class DoubleToFixedOrAutoGridLengthConverter : SimpleValueConverter<GridLength>
    {
        protected override GridLength Convert (object value)
        {
            double d = (double)value;
            return double.IsNaN(d) ? GridLength.Auto : new GridLength(d, GridUnitType.Pixel);
        }
    }

    /// <summary>
    /// Multi-value converter for PropertyGrid label column width after indent.
    /// Values: [0] double indentWidth, [1] double labelColumnWidth.
    /// Returns Auto when labelColumnWidth is NaN; otherwise Pixel(Max(0, labelColumnWidth - indentWidth - 18)).
    /// The 18px deduction reserves space for the expander toggle column.
    /// </summary>
    public class LabelWidthAfterIndentConverter : SimpleMultiValueConverter<GridLength>
    {
        private const double kExpanderColumnWidth = 18.0;

        protected override GridLength Convert (object[] values)
        {
            if (values == null || values.Length < 2)
            {
                return GridLength.Auto;
            }

            if (values[1] is not double labelColumnWidth || double.IsNaN(labelColumnWidth))
            {
                return GridLength.Auto;
            }

            double indentWidth = values[0] is double d ? d : 0.0;
            double available = labelColumnWidth - indentWidth - kExpanderColumnWidth;
            return new GridLength(available > 0 ? available : 0, GridUnitType.Pixel);
        }
    }
}