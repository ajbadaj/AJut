namespace AJut.Application.Converters
{
    using System.Windows;
    using System.Windows.Controls;

    public class CornerRadiusSideExtractionConverter : SimpleValueConverter<CornerRadius, CornerRadius>
    {
        public Dock Side { get; set; }
        public CornerRadius Fallback { get; set; } = new CornerRadius(0.0);
        protected override CornerRadius Convert (CornerRadius value)
        {
            switch (this.Side)
            {
                case Dock.Left:
                    return new CornerRadius(
                        value.TopLeft, this.Fallback.TopRight, 
                                       this.Fallback.BottomRight,
                        value.BottomLeft
                    );

                case Dock.Right:
                    return new CornerRadius(
                        this.Fallback.TopLeft, value.TopRight,
                                       value.BottomRight,
                        this.Fallback.BottomLeft
                    );

                case Dock.Top:
                    return new CornerRadius(
                        value.TopLeft, value.TopRight,
                                       this.Fallback.BottomRight,
                        this.Fallback.BottomLeft
                    );

                case Dock.Bottom:
                    return new CornerRadius(
                        this.Fallback.TopLeft, this.Fallback.TopRight,
                                       value.BottomRight,
                        value.BottomLeft
                    );
            }

            return value;
        }
    }
}
