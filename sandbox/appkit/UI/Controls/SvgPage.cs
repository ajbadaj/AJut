namespace AJutShowRoomAppKit.UI.Controls
{
    using System.IO;
    using AJut.UX;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using AppKit;
    using Foundation;

    public sealed class SvgPage : ShowroomPage
    {
        public SvgPage ()
            : base("Svg", "Draws an svg file through AJut.UX's SvgParsing, the same read WPF's Svg and the phones' Svg views draw from. It draws the file's own colors, so it looks the same with the AJut theme on or off.")
        {
            const double kPenguinSize = 200.0;
            const double kLargeIconSize = 96.0;

            // A 24 unit star on a viewBox: a polygon and a circle, with the star's paint set on its group
            const string kStarIcon = @"<svg width=""24"" height=""24"" viewBox=""0 0 24 24"" xmlns=""http://www.w3.org/2000/svg"">
  <g fill=""#FFC107"" stroke=""#E65100"" stroke-width=""1"">
    <polygon points=""12,2 15,9 22,9 16.5,13.5 18.5,21 12,16.5 5.5,21 7.5,13.5 2,9 9,9""/>
  </g>
  <circle cx=""12"" cy=""12"" r=""2"" fill=""#E65100""/>
</svg>";

            this.AddHeading("A file");
            this.AddText("The WPF showroom's penguin, from its own file, fitted into 200 points with Stretch = Uniform.");
            string penguinPath = NSBundle.MainBundle.PathForResource("PenguinExample", "svg");
            if (penguinPath == null)
            {
                this.AddText("PenguinExample.svg is missing from the app bundle's resources.");
            }
            else
            {
                var penguin = new Svg { Stretch = ePathStretch.Uniform };
                using (Stream file = File.OpenRead(penguinPath))
                {
                    penguin.Source = SvgParsing.Parse(file);
                }

                this.AddDemo(MakeSized(penguin, kPenguinSize), eFillStackCrossAlignment.Start);
            }

            this.AddHeading("A small icon");
            this.AddText("A 24 unit icon on a viewBox, made of a polygon and a circle with its paint set on a group, read from text: at its own size, then fitted into 96 points, where its outline thickens in step.");
            SvgDocument star = SvgParsing.ParseText(kStarIcon);
            this.AddLabeledDemo("At its own size", new Svg { Source = star });
            this.AddLabeledDemo("Fitted into 96 points", MakeSized(new Svg { Source = star, Stretch = ePathStretch.Uniform }, kLargeIconSize));

            this.AddCode(
@"using Stream file = File.OpenRead(NSBundle.MainBundle.PathForResource(""PenguinExample"", ""svg""));
var penguin = new Svg { Source = SvgParsing.Parse(file), Stretch = ePathStretch.Uniform };

// Unstretched, an Svg is the size its drawing is; stretched, it fills the size it is given
var icon = new Svg { Source = SvgParsing.ParseText(iconText) };"
            );
        }

        private static Svg MakeSized (Svg svg, double size)
        {
            svg.TranslatesAutoresizingMaskIntoConstraints = false;
            NSLayoutConstraint.ActivateConstraints(new[]
            {
                svg.WidthAnchor.ConstraintEqualTo((nfloat)size),
                svg.HeightAnchor.ConstraintEqualTo((nfloat)size),
            });
            return svg;
        }
    }
}
