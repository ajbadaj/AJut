namespace AJut.UX.Converters
{
    using System;

    /// <summary>
    /// Convert numeric-ish values to target numeric-ish values. Support for min/max.
    /// </summary>
    public class RelativeNumericValueConverter : SimpleValueConverter
    {
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public RelativeNumericValueConverter()
        {
            this.MinValue = double.MinValue;
            this.MaxValue = double.MaxValue;
        }
        public RelativeNumericValueConverter(double minValue, double maxValue)
        {
            this.MinValue = minValue;
            this.MaxValue = maxValue;
        }

        protected override object Convert(object value, Type targetType, object parameter)
        {
            double valueCasted = System.Convert.ToDouble(value);
            if (parameter != null)
            {
                valueCasted += System.Convert.ToDouble(parameter);
            }

            if (valueCasted < this.MinValue)
            {
                valueCasted = this.MinValue;
            }
            if (valueCasted > this.MaxValue)
            {
                valueCasted = this.MaxValue;
            }
            return System.Convert.ChangeType(valueCasted, targetType);
        }


        protected override object ConvertBack(object convertedValue, Type sourceType, object parameter)
        {
            double valueCasted = System.Convert.ToDouble(convertedValue);
            if (parameter != null)
            {
                valueCasted -= System.Convert.ToDouble(parameter);
            }
           
            return System.Convert.ChangeType(valueCasted, sourceType);
        }
    }
}
