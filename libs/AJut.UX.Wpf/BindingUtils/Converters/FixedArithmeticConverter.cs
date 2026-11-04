namespace AJut.UX.Converters
{
    using System;
    using System.Windows;

    public class FixedArithmeticConverter : SimpleValueConverter
    {
        public eArithmeticOperation Operation { get; set; } = eArithmeticOperation.Multiply;
        public double FixedOperand { get; set; }

        protected override object Convert (object value, Type targetType, object parameter)
        {
            if (value == DependencyProperty.UnsetValue)
            {
                return value;
            }

            double result = NumericConverterMath.Apply(this.Operation, System.Convert.ToDouble(value), this.FixedOperand);
            return System.Convert.ChangeType(result, targetType);
        }
    }
}
