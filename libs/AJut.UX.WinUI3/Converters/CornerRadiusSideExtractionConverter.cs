namespace AJut.UX.Converters
{
    using Microsoft.UI.Xaml;

    public enum eDockSide
    {
        Left,
        Top,
        Right,
        Bottom
    }
    public class CornerRadiusSideExtractionConverter : SimpleValueConverter<CornerRadius, CornerRadius>
    {
        public eDockSide Side { get; set; }
        public CornerRadius Fallback { get; set; } = new CornerRadius(0.0);
        public double Reduction { get; set; }
        protected override CornerRadius Convert (CornerRadius value)
        {
            switch (this.Side)
            {
                case eDockSide.Left:
                    return new CornerRadius(
                        value.TopLeft - this.Reduction, this.Fallback.TopRight, 
                                       this.Fallback.BottomRight,
                        value.BottomLeft - this.Reduction
                    );

                case eDockSide.Right:
                    return new CornerRadius(
                        this.Fallback.TopLeft, value.TopRight - this.Reduction,
                                       value.BottomRight - this.Reduction,
                        this.Fallback.BottomLeft
                    );

                case eDockSide.Top:
                    return new CornerRadius(
                        value.TopLeft - this.Reduction, value.TopRight - this.Reduction,
                                       this.Fallback.BottomRight,
                        this.Fallback.BottomLeft
                    );

                case eDockSide.Bottom:
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
