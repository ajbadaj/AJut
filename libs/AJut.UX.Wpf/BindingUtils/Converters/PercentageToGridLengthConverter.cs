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
}