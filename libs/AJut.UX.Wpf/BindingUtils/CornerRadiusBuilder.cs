namespace AJut.UX
{
    using System;
    using System.Windows;
    using System.Windows.Data;
    using AJut.UX.Converters;

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

        private class BuildCornerRadiusConverter : SimpleMultiValueConverter<double, CornerRadius>
        {
            protected override CornerRadius Convert (double[] values, object parameter)
            {
                var src = (CornerRadiusBuilder)parameter;

                double[] final = new[] { src.Baseline.TopLeft, src.Baseline.TopRight, src.Baseline.BottomRight, src.Baseline.BottomLeft };
                int valueIndex = 0;
                if (src.BindingParts.HasFlag(eCornerPart.TopLeft))
                {
                    final[0] = values[valueIndex++];
                }

                if (src.BindingParts.HasFlag(eCornerPart.TopRight))
                {
                    final[1] = values[valueIndex++];
                }

                if (src.BindingParts.HasFlag(eCornerPart.BottomRight))
                {
                    final[2] = values[valueIndex++];
                }

                if (src.BindingParts.HasFlag(eCornerPart.BottomLeft))
                {
                    final[3] = values[valueIndex++];
                }

                return new CornerRadius(final[0], final[1], final[2], final[3]);
            }
        }
    }
}
