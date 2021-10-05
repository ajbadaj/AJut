namespace AJut.UX.Converters
{
    using System.Windows;
    using System.Windows.Controls;

    public class CornerRadiusSideExtractionConverter : SimpleValueConverter<CornerRadius, CornerRadius>
    {
        public Dock Side { get; set; }
        public CornerRadius Fallback { get; set; } = new CornerRadius(0.0);
        public double Reduction { get; set; }
        protected override CornerRadius Convert (CornerRadius value)
        {
            switch (this.Side)
            {
                case Dock.Left:
                    return new CornerRadius(
                        value.TopLeft - this.Reduction, this.Fallback.TopRight, 
                                       this.Fallback.BottomRight,
                        value.BottomLeft - this.Reduction
                    );

                case Dock.Right:
                    return new CornerRadius(
                        this.Fallback.TopLeft, value.TopRight - this.Reduction,
                                       value.BottomRight - this.Reduction,
                        this.Fallback.BottomLeft
                    );

                case Dock.Top:
                    return new CornerRadius(
                        value.TopLeft - this.Reduction, value.TopRight - this.Reduction,
                                       this.Fallback.BottomRight,
                        this.Fallback.BottomLeft
                    );

                case Dock.Bottom:
                    return new CornerRadius(
                        this.Fallback.TopLeft, this.Fallback.TopRight,
                                       value.BottomRight - this.Reduction,
                        value.BottomLeft - this.Reduction
                    );
            }

            return value;
        }
    }
}
