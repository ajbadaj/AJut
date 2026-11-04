namespace AJutShowRoomUIKit.UI.Controls
{
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using UIKit;

    public sealed class GeometryButtonPage : ShowroomPageController
    {
        public GeometryButtonPage () : base("GeometryButton")
        {
        }

        protected override void BuildPage ()
        {
            this.AddHeading("GeometryButton");
            this.AddText("A button whose background is a path instead of a rectangle. BackgroundGeometry takes the XAML path mini-language through PathMarkup.Parse, TraceThickness outlines it, and GeometryStretch fits it to the button.");
            this.AddDemo(
                _Make("Geometry Button", "M 0,0 20,20 40,0 150,0 150,100 100,90 0,100 Z", ePathStretch.Fill, traceThickness: 1, width: 180f, height: 110f)
            );
            this.AddCode(@"
var button = new GeometryButton
{
    BackgroundGeometry = PathMarkup.Parse(""M 0,0 20,20 40,0 150,0 150,100 100,90 0,100 Z""),
    TraceThickness = 1,
    GeometryStretch = ePathStretch.Fill,
};
button.SetTitle(""Geometry Button"", UIControlState.Normal);");

            this.AddHeading("Curves, arcs and stretch");
            this.AddText("Uniform keeps an arc's circle round inside a square button. Fill stretches a wave of cubic curves to the button's shape.");
            this.AddDemoRow(
                _Make("Uniform", "M 50,0 A 50,50 0 1 1 49.99,0 Z", ePathStretch.Uniform, traceThickness: 2, width: 110f, height: 110f),
                _Make("Fill", "M 0,20 C 30,-10 70,50 100,20 L 100,80 C 70,110 30,50 0,80 Z", ePathStretch.Fill, traceThickness: 2, width: 160f, height: 110f)
            );

            this.AddText("Disabled, with a clipped corner shape drawn with H, V and L.");
            GeometryButton disabled = _Make(
                "Disabled",
                "M 10,0 H 90 L 100,10 V 40 L 90,50 H 10 L 0,40 V 10 Z",
                ePathStretch.Fill,
                traceThickness: 1,
                width: 220f,
                height: 60f
            );
            disabled.Enabled = false;
            this.AddDemo(disabled);

            static GeometryButton _Make (string title, string geometry, ePathStretch stretch, float traceThickness, float width, float height)
            {
                var button = new GeometryButton
                {
                    BackgroundGeometry = PathMarkup.Parse(geometry),
                    TraceThickness = traceThickness,
                    GeometryStretch = stretch,
                };
                button.SetTitle(title, UIControlState.Normal);
                button.TranslatesAutoresizingMaskIntoConstraints = false;
                button.WidthAnchor.ConstraintEqualTo(width).Active = true;
                button.HeightAnchor.ConstraintEqualTo(height).Active = true;
                return button;
            }
        }
    }
}
