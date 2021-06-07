namespace AJut.Application.Controls
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using AJut.Application.AttachedProperties;
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
            ClickableXTA.AddClickHandler(this, (e, a) => this.ShowEditDisplay = true);
        }

        public static readonly DependencyProperty EditColorProperty = DPUtils.Register(_ => _.EditColor);
        public Color EditColor
        {
            get => (Color)this.GetValue(EditColorProperty);
            set => this.SetValue(EditColorProperty, value);
        }

        public static readonly DependencyProperty IsReadOnlyProperty = DPUtils.Register(_ => _.IsReadOnly);
        public bool IsReadOnly
        {
            get => (bool)this.GetValue(IsReadOnlyProperty);
            set => this.SetValue(IsReadOnlyProperty, value);
        }

        public static readonly DependencyProperty ShowEditDisplayProperty = DPUtils.Register(_ => _.ShowEditDisplay, false);
        public bool ShowEditDisplay
        {
            get => (bool)this.GetValue(ShowEditDisplayProperty);
            set => this.SetValue(ShowEditDisplayProperty, value);
        }
    }
}
