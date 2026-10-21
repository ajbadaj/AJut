namespace AJutShowRoomWinUI.UI.Controls
{
    using Microsoft.UI.Xaml.Controls;

    /// <summary>
    /// The GeometryButton demo: a button drawn from a path geometry.
    /// </summary>
    public sealed partial class GeometryButtonPage : Page
    {
        public GeometryButtonPage ()
        {
            this.InitializeComponent();
        }

        public string DemoCode { get; } = @"<ajut:GeometryButton Content=""Geometry Button""
                     BackgroundGeometry=""M 0,0 20,20 40,0 150,0 150,100 100,90 0,100 Z""
                     TraceThickness=""1""/>";
    }
}
