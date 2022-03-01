namespace AJut.UX.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using AJut.UX.AttachedProperties;
    using DPUtils = DPUtils<ColorEditIngressControl>;

    public class ColorEditIngressControl : Control
    {
        static ColorEditIngressControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorEditIngressControl), new FrameworkPropertyMetadata(typeof(ColorEditIngressControl)));
        }

        public ColorEditIngressControl()
        {
            ClickableXTA.SetIsTrackingClick(this, true);
            ClickableXTA.AddClickHandler(this, (e, a) => this.ShowEditDisplay = !this.ShowEditDisplay);
        }

        public static readonly DependencyProperty EditColorProperty = DPUtils.Register(_ => _.EditColor);
        public Color EditColor
        {
            get => (Color)this.GetValue(EditColorProperty);
            set => this.SetValue(EditColorProperty, value);
        }

        public static readonly DependencyProperty HighlightBorderBrushProperty = DPUtils.Register(_ => _.HighlightBorderBrush);
        public Brush HighlightBorderBrush
        {
            get => (Brush)this.GetValue(HighlightBorderBrushProperty);
            set => this.SetValue(HighlightBorderBrushProperty, value);
        }

        public static readonly DependencyProperty PreferShortStringHexProperty = DPUtils.Register(_ => _.PreferShortStringHex);
        public bool PreferShortStringHex
        {
            get => (bool)this.GetValue(PreferShortStringHexProperty);
            set => this.SetValue(PreferShortStringHexProperty, value);
        }

        public static readonly DependencyProperty IsReadOnlyProperty = DPUtils.Register(_ => _.IsReadOnly);
        public bool IsReadOnly
        {
            get => (bool)this.GetValue(IsReadOnlyProperty);
            set => this.SetValue(IsReadOnlyProperty, value);
        }

        public static readonly DependencyProperty CornerBannerDimensionsProperty = DPUtils.Register(_ => _.CornerBannerDimensions, 32.0);
        public double CornerBannerDimensions
        {
            get => (double)this.GetValue(CornerBannerDimensionsProperty);
            set => this.SetValue(CornerBannerDimensionsProperty, value);
        }

        public static readonly DependencyProperty CornerBannerIconFontSizeProperty = DPUtils.Register(_ => _.CornerBannerIconFontSize, 12.0);
        public double CornerBannerIconFontSize
        {
            get => (double)this.GetValue(CornerBannerIconFontSizeProperty);
            set => this.SetValue(CornerBannerIconFontSizeProperty, value);
        }

        public static readonly DependencyProperty PopupWidthProperty = DPUtils.Register(_ => _.PopupWidth, 235.0);
        public double PopupWidth
        {
            get => (double)this.GetValue(PopupWidthProperty);
            set => this.SetValue(PopupWidthProperty, value);
        }

        public static readonly DependencyProperty ShowEditDisplayProperty = DPUtils.Register(_ => _.ShowEditDisplay, false);
        public bool ShowEditDisplay
        {
            get => (bool)this.GetValue(ShowEditDisplayProperty);
            set => this.SetValue(ShowEditDisplayProperty, value);
        }
    }
}
