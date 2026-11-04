namespace AJut.UX
{
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;

    [Flags]
    public enum eCornerPart
    {
        None = 0,
        TopLeft = 0b0001,
        TopRight = 0b0010,
        BottomRight = 0b0100,
        BottomLeft = 0b1000
    }

    public class CornerRadiusBuilder : MultiBinding
    {
        public CornerRadiusBuilder ()
        {
            this.Converter = new BuildCornerRadiusConverter();
            this.ConverterParameter = this;
        }

        public CornerRadius Baseline { get; set; } = new CornerRadius(0);
        public eCornerPart BindingParts { get; set; }

        // Each bound part reads its own binding's slot, in the order eCornerPart lists the parts. A binding that has not
        //  resolved (an ancestor it looks for that is not there yet) hands over UnsetValue rather than a number, and that
        //  part keeps the Baseline. The slots are read as they are rather than filtered down to the numbers, so one
        //  unresolved part never shifts another part's value into its place or runs off the end.
        private class BuildCornerRadiusConverter : IMultiValueConverter
        {
            public object Convert (object[] values, Type targetType, object parameter, CultureInfo culture)
            {
                var src = (CornerRadiusBuilder)parameter;

                double[] final = new[] { src.Baseline.TopLeft, src.Baseline.TopRight, src.Baseline.BottomRight, src.Baseline.BottomLeft };
                int valueIndex = 0;
                if (src.BindingParts.HasFlag(eCornerPart.TopLeft))
                {
                    final[0] = PartValueOr(values, valueIndex++, final[0]);
                }

                if (src.BindingParts.HasFlag(eCornerPart.TopRight))
                {
                    final[1] = PartValueOr(values, valueIndex++, final[1]);
                }

                if (src.BindingParts.HasFlag(eCornerPart.BottomRight))
                {
                    final[2] = PartValueOr(values, valueIndex++, final[2]);
                }

                if (src.BindingParts.HasFlag(eCornerPart.BottomLeft))
                {
                    final[3] = PartValueOr(values, valueIndex++, final[3]);
                }

                return new CornerRadius(final[0], final[1], final[2], final[3]);
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
