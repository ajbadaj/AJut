namespace AJut.UX.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Media;
    using DPUtils = AJut.UX.DPUtils<DockZone>;

    public sealed partial class DockZone // Style
    {
        public static readonly DependencyProperty PanelBackgroundProperty = DPUtils.RegisterFP(_ => _.PanelBackground, null, null, CoerceUtils.CallbackForBrush);
        public Brush PanelBackground
        {
            get => (Brush)this.GetValue(PanelBackgroundProperty);
            set => this.SetValue(PanelBackgroundProperty, value);
        }

        public static readonly DependencyProperty PanelForegroundProperty = DPUtils.RegisterFP(_ => _.PanelForeground, null, null, CoerceUtils.CallbackForBrush);
        public Brush PanelForeground
        {
            get => (Brush)this.GetValue(PanelForegroundProperty);
            set => this.SetValue(PanelForegroundProperty, value);
        }

        public static readonly DependencyProperty PanelBorderThicknessProperty = DPUtils.Register(_ => _.PanelBorderThickness);
        public Thickness PanelBorderThickness
        {
            get => (Thickness)this.GetValue(PanelBorderThicknessProperty);
            set => this.SetValue(PanelBorderThicknessProperty, value);
        }

        public static readonly DependencyProperty PanelBorderBrushProperty = DPUtils.RegisterFP(_ => _.PanelBorderBrush, null, null, CoerceUtils.CallbackForBrush);
        public Brush PanelBorderBrush
        {
            get => (Brush)this.GetValue(PanelBorderBrushProperty);
            set => this.SetValue(PanelBorderBrushProperty, value);
        }

        public static readonly DependencyProperty PanelCornerRadiusProperty = DPUtils.Register(_ => _.PanelCornerRadius);
        public CornerRadius PanelCornerRadius
        {
            get => (CornerRadius)this.GetValue(PanelCornerRadiusProperty);
            set => this.SetValue(PanelCornerRadiusProperty, value);
        }

        public static readonly DependencyProperty EmptyPanelCornerRadiusProperty = DPUtils.Register(_ => _.EmptyPanelCornerRadius);
        public CornerRadius EmptyPanelCornerRadius
        {
            get => (CornerRadius)this.GetValue(EmptyPanelCornerRadiusProperty);
            set => this.SetValue(EmptyPanelCornerRadiusProperty, value);
        }

        public static readonly DependencyProperty SeparationSizeProperty = DPUtils.Register(_ => _.SeparationSize, (d, e) => d.HalfSeparationSize = d.SeparationSize / 2);
        public double SeparationSize
        {
            get => (double)this.GetValue(SeparationSizeProperty);
            set => this.SetValue(SeparationSizeProperty, value);
        }

        private static readonly DependencyPropertyKey HalfSeparationSizePropertyKey = DPUtils.RegisterReadOnly(_ => _.HalfSeparationSize);
        public static readonly DependencyProperty HalfSeparationSizeProperty = HalfSeparationSizePropertyKey.DependencyProperty;
        public double HalfSeparationSize
        {
            get => (double)this.GetValue(HalfSeparationSizeProperty);
            private set => this.SetValue(HalfSeparationSizePropertyKey, value);
        }

        public static readonly DependencyProperty SeparatorBrushProperty = DPUtils.RegisterFP(_ => _.SeparatorBrush, null, null, CoerceUtils.CallbackForBrush);
        public Brush SeparatorBrush
        {
            get => (Brush)this.GetValue(SeparatorBrushProperty);
            set => this.SetValue(SeparatorBrushProperty, value);
        }
    }
}
