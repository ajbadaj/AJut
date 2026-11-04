namespace AJutShowRoomUIKit.UI.Controls
{
    using System.IO;
    using AJut.UX;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using Foundation;
    using UIKit;

    public sealed class SvgPage : ShowroomPageController
    {
        // A 24 unit star on a viewBox: a polygon and a circle, with the star's paint set on its group
        private const string kStarIcon = @"<svg width=""24"" height=""24"" viewBox=""0 0 24 24"" xmlns=""http://www.w3.org/2000/svg"">
  <g fill=""#FFC107"" stroke=""#E65100"" stroke-width=""1"">
    <polygon points=""12,2 15,9 22,9 16.5,13.5 18.5,21 12,16.5 5.5,21 7.5,13.5 2,9 9,9""/>
  </g>
  <circle cx=""12"" cy=""12"" r=""2"" fill=""#E65100""/>
</svg>";

        public SvgPage () : base("Svg")
        {
        }

        protected override void BuildPage ()
        {
            this.AddHeading("Svg");
            this.AddText("Draws an svg file through AJut.UX's SvgParsing, the same read WPF's Svg draws from. This is the WPF showroom's penguin, from its own file, fitted with Stretch = Uniform.");

            var penguin = new Svg { Stretch = ePathStretch.Uniform, TranslatesAutoresizingMaskIntoConstraints = false };
            using (Stream file = File.OpenRead(NSBundle.MainBundle.PathForResource("PenguinExample", "svg")))
            {
                penguin.Source = SvgParsing.Parse(file);
            }

            penguin.WidthAnchor.ConstraintEqualTo(200).Active = true;
            penguin.HeightAnchor.ConstraintEqualTo(200).Active = true;
            this.AddDemo(penguin);
            this.AddCode(@"
using Stream file = File.OpenRead(NSBundle.MainBundle.PathForResource(""PenguinExample"", ""svg""));
var penguin = new Svg { Source = SvgParsing.Parse(file), Stretch = ePathStretch.Uniform };");

            this.AddHeading("A small icon");
            this.AddText("A 24 unit icon on a viewBox, made of a polygon and a circle with its paint set on a group, read from text: at its own size, then fitted into 96 points.");
            SvgDocument star = SvgParsing.ParseText(kStarIcon);
            var small = new Svg { Source = star };
            var large = new Svg { Source = star, Stretch = ePathStretch.Uniform, TranslatesAutoresizingMaskIntoConstraints = false };
            large.WidthAnchor.ConstraintEqualTo(96).Active = true;
            large.HeightAnchor.ConstraintEqualTo(96).Active = true;
            this.AddDemoRow(small, large);
        }
    }
}
