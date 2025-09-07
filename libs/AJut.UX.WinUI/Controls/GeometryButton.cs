namespace AJut.UX.Controls
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using Microsoft.UI.Xaml.Shapes;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using DPUtils = AJut.UX.DPUtils<GeometryButton>;

    public class GeometryButton : Button
    {
        public GeometryButton()
        {
            this.DefaultStyleKey = typeof(GeometryButton);
            //Path p;
            //p.Data
            //Geometry geometry = Geometry.Parse(pathDataString);
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
