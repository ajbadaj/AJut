namespace AJut.UX.Converters
{
    using System;
    using System.Linq;
    using System.Windows;
    using System.Windows.Data;

    public enum eArithmeticOperation { Add, Subtract, Multiply, Divide }
    public class ArithmeticConverter : SimpleMultiValueConverter
    {
        public eArithmeticOperation Operation { get; set; }
        protected override object Convert (object[] values, Type targetType, object parameter)
        {
            var options = values.Where(_ => _ != DependencyProperty.UnsetValue).Select(_ => System.Convert.ToDouble(_));
            if (!options.Any())
            {
                return null;
            }

            double result = options.First();

            foreach (double other in options.Skip(1))
            {
                switch (this.Operation)
                {
                    case eArithmeticOperation.Add: result += other; break;
                    case eArithmeticOperation.Subtract: result -= other; break;
                    case eArithmeticOperation.Multiply: result *= other; break;
                    case eArithmeticOperation.Divide:
                        if (other != 0.0)
                        {
                            result /= other;
                        }
                        break;
                    default: break;
                }
            }

            return System.Convert.ChangeType(result, targetType);
        }
    }

    public class ArithmeticMultiBinding : MultiBinding
    {
        ArithmeticConverter m_converter = new ArithmeticConverter();
        public ArithmeticMultiBinding ()
        {
            this.Converter = m_converter;
        }

        public eArithmeticOperation Operation
        {
            get => m_converter.Operation;
            set => m_converter.Operation = value;
        }
    }
}