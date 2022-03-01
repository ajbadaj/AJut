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

            double result = System.Convert.ToDouble(value);
            
            switch (this.Operation)
            {
                case eArithmeticOperation.Add: result += this.FixedOperand; break;
                case eArithmeticOperation.Subtract: result -= this.FixedOperand; break;
                case eArithmeticOperation.Multiply: result *= this.FixedOperand; break;
                case eArithmeticOperation.Divide:
                    if (this.FixedOperand != 0.0)
                    {
                        result /= this.FixedOperand;
                    }
                    break;
                default: break;
            }

            return System.Convert.ChangeType(result, targetType);
        }
    }
}
