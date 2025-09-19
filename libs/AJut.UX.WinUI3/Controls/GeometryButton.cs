namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using DPUtils = AJut.UX.DPUtils<GeometryButton>;

    public class GeometryButton : Button
    {
        public GeometryButton()
        {
            this.DefaultStyleKey = typeof(GeometryButton);
        }

        public static readonly DependencyProperty TraceThicknessProperty = DPUtils.Register(_ => _.TraceThickness);
        public double TraceThickness
        {
            get => (double)this.GetValue(TraceThicknessProperty);
            set => this.SetValue(TraceThicknessProperty, value);
        }


        public static readonly DependencyProperty BackgroundGeometryProperty = DPUtils.Register(_ => _.BackgroundGeometry);
        public Geometry BackgroundGeometry
        {
            get => (Geometry)this.GetValue(BackgroundGeometryProperty);
            set => this.SetValue(BackgroundGeometryProperty, value);
        }
    }
}
