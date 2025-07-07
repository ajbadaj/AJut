namespace AJut.UX.Converters
{
    using Microsoft.UI.Xaml;

    public class LeftMostToggleItemCornerRadiusConverter : SimpleValueConverter<CornerRadius, CornerRadius>
    {
        protected override CornerRadius Convert (CornerRadius value) => new CornerRadius(value.TopLeft, 0.0, 0.0, value.BottomLeft);
    }

    public class RightMostToggleItemCornerRadiusConverter : SimpleValueConverter<CornerRadius, CornerRadius>
    {
        protected override CornerRadius Convert (CornerRadius value) => new CornerRadius(0.0, value.TopRight, value.BottomRight, 0.0);
    }
}
