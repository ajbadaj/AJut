namespace AJut.UX.Converters
{
    using System;
    using Microsoft.UI.Xaml;

    public class UniformCornerRadiusModifierConverter : SimpleValueConverter<CornerRadius, CornerRadius>
    {
        public double Modifier { get; set; } = -1;

        protected override CornerRadius Convert (CornerRadius value)
        {
            return new CornerRadius
            {
                TopLeft = Math.Max(0, value.TopLeft + this.Modifier),
                TopRight = Math.Max(0, value.TopRight + this.Modifier),
                BottomLeft = Math.Max(0, value.BottomLeft + this.Modifier),
                BottomRight = Math.Max(0, value.BottomRight + this.Modifier),
            };
        }
    }
}
