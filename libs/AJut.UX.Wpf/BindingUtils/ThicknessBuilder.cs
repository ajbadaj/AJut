namespace AJut.UX
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;

    [Flags]
    public enum eThicknessPart 
    {
        None   = 0,
        Left   = 0b0001,
        Top    = 0b0010,
        Right  = 0b0100,
        Bottom = 0b1000,
    }

    public class ThicknessBuilder : MultiBinding
    {
        public ThicknessBuilder()
        {
            this.Converter = new BuildThicknessConverter();
            this.ConverterParameter = this;
        }

        public Thickness Baseline { get; set; } = new Thickness(0);
        public eThicknessPart BindingParts { get; set; }

        // Each bound part reads its own binding's slot, in the order eThicknessPart lists the parts. A binding that has not
        //  resolved (an ancestor it looks for that is not there yet) hands over UnsetValue rather than a number, and that
        //  part keeps the Baseline. The slots are read as they are rather than filtered down to the numbers, so one
        //  unresolved part never shifts another part's value into its place.
        private class BuildThicknessConverter : IMultiValueConverter
        {
            public object Convert (object[] values, Type targetType, object parameter, CultureInfo culture)
            {
                var src = (ThicknessBuilder)parameter;

                double[] final = new[] { src.Baseline.Left, src.Baseline.Top, src.Baseline.Right, src.Baseline.Bottom };
                int valueIndex = 0;
                if (src.BindingParts.HasFlag(eThicknessPart.Left))
                {
                    final[0] = PartValueOr(values, valueIndex++, final[0]);
                }

                if (src.BindingParts.HasFlag(eThicknessPart.Top))
                {
                    final[1] = PartValueOr(values, valueIndex++, final[1]);
                }

                if (src.BindingParts.HasFlag(eThicknessPart.Right))
                {
                    final[2] = PartValueOr(values, valueIndex++, final[2]);
                }

                if (src.BindingParts.HasFlag(eThicknessPart.Bottom))
                {
                    final[3] = PartValueOr(values, valueIndex++, final[3]);
                }

                return new Thickness(final[0], final[1], final[2], final[3]);
            }

            public object[] ConvertBack (object value, Type[] targetTypes, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }

            private static double PartValueOr (object[] values, int index, double baseline)
                => ((index < values.Length) && (values[index] is double value)) ? value : baseline;
        }
    }
}
