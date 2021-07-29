namespace AJut.Application
{
    using System;
    using System.Windows;
    using System.Windows.Data;
    using AJut.Application.Converters;

    [Flags]
    public enum eThicknessPart 
    {
        None   = 0,
        Left   = 0b0001, 
        Top    = 0b0010, 
        Right  = 0b0100,
        Bottom = 0b1000
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

        private class BuildThicknessConverter : SimpleMultiValueConverter<double,Thickness>
        {
            protected override Thickness Convert (double[] values, object parameter)
            {
                var src = (ThicknessBuilder)parameter;

                double[] final = new[] { src.Baseline.Left, src.Baseline.Top, src.Baseline.Right, src.Baseline.Bottom };
                int valueIndex = 0;
                if (src.BindingParts.HasFlag(eThicknessPart.Left))
                {
                    final[0] = values[valueIndex++];
                }

                if (src.BindingParts.HasFlag(eThicknessPart.Top))
                {
                    final[1] = values[valueIndex++];
                }

                if (src.BindingParts.HasFlag(eThicknessPart.Right))
                {
                    final[2] = values[valueIndex++];
                }

                if (src.BindingParts.HasFlag(eThicknessPart.Bottom))
                {
                    final[3] = values[valueIndex++];
                }

                return new Thickness(final[0], final[1], final[2], final[3]);
            }
        }
    }
}
