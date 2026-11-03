namespace AJut.UX.Tests
{
    using System;
    using System.Linq;
    using AJut.UX;
    using AJut.UX.Helpers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The toolkit-free svg read: what <see cref="SvgParsing"/> makes of a document, and the <see cref="SvgMatrix"/> math
    /// it places paths with. WPF's own Svg tests cover the reading rules through SvgSerialization, which reads with this.
    /// </summary>
    [TestClass]
    public class SvgParsingTests
    {
        private const double kTolerance = 1e-9;
        private const string kSvgNamespace = "xmlns=\"http://www.w3.org/2000/svg\"";

        // ===========[ SvgMatrix ]===================================

        [TestMethod]
        public void SvgMatrix_Then_AppliesThisFirst ()
        {
            SvgMatrix scaleThenMove = SvgMatrix.Scale(2, 2).Then(SvgMatrix.Translate(10, 0));
            AssertPoint(new PathPoint(12, 2), scaleThenMove.Apply(new PathPoint(1, 1)));

            SvgMatrix moveThenScale = SvgMatrix.Translate(10, 0).Then(SvgMatrix.Scale(2, 2));
            AssertPoint(new PathPoint(22, 2), moveThenScale.Apply(new PathPoint(1, 1)));
        }

        [TestMethod]
        public void SvgMatrix_RotateSkewAndIdentity_PlacePointsAsXamlDoes ()
        {
            AssertPoint(new PathPoint(0, 1), SvgMatrix.Rotate(90).Apply(new PathPoint(1, 0)));
            AssertPoint(new PathPoint(20, 10), SvgMatrix.Rotate(90, 10, 10).Apply(new PathPoint(10, 0)));
            AssertPoint(new PathPoint(1, 1), SvgMatrix.Skew(45, 0).Apply(new PathPoint(0, 1)));
            AssertPoint(new PathPoint(1, 1), SvgMatrix.Skew(0, 45).Apply(new PathPoint(1, 0)));
            Assert.IsTrue(SvgMatrix.Identity.IsIdentity);
            Assert.IsTrue(SvgMatrix.Translate(0, 0).IsIdentity);
            Assert.IsFalse(SvgMatrix.Translate(1, 0).IsIdentity);
        }

        [TestMethod]
        public void SvgMatrix_ApplyToAPath_MovesEveryPointAndItsBounds ()
        {
            PathMarkupData path = PathMarkup.Parse("M0,0 L10,0 C10,5 5,10 0,10 Z");
            PathMarkupData moved = SvgMatrix.Translate(100, 50).Apply(path);

            Assert.AreEqual(path.Commands.Count, moved.Commands.Count);
            Assert.AreEqual(path.FillRule, moved.FillRule);
            AssertPoint(new PathPoint(110, 50), moved.Commands[1].End);
            AssertPoint(new PathPoint(110, 55), moved.Commands[2].Control1);
            Assert.AreEqual(path.Bounds.X + 100, moved.Bounds.X, kTolerance);
            Assert.AreEqual(path.Bounds.Y + 50, moved.Bounds.Y, kTolerance);
            Assert.AreEqual(path.Bounds.Width, moved.Bounds.Width, kTolerance);
            Assert.AreSame(path, SvgMatrix.Identity.Apply(path), "No transform leaves the path as it is");
        }

        // ===========[ Documents ]===================================

        [TestMethod]
        public void ParseText_Document_IsTheSvgElementAsAGroupOfNodes ()
        {
            SvgDocument document = SvgParsing.ParseText($@"<svg id=""doc"" width=""20"" height=""10"" {kSvgNamespace}>
                <g id=""layer""><path id=""a"" d=""M0,0 L1,1""/><g id=""inner""><path id=""b"" d=""M2,2 L3,3""/></g></g>
                <path id=""c"" d=""M4,4 L5,5""/>
            </svg>");

            Assert.IsNotNull(document);
            Assert.AreEqual("doc", document.Root.Id);
            Assert.IsFalse(document.Root.IsDrawn);
            Assert.AreEqual(20.0, document.Metadata.Width, kTolerance);
            CollectionAssert.AreEqual(new[] { "layer", "c" }, document.Root.Children.Select(c => c.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, document.DrawnNodes.Select(n => n.Id).ToArray(), "Drawn in document order, depth first");
        }

        [TestMethod]
        public void ParseText_TwoGroupsWithOneId_LoadAndTheFirstKeepsTheMetadata ()
        {
            SvgDocument document = SvgParsing.ParseText($@"<svg id=""doc"" {kSvgNamespace} xmlns:inkscape=""http://www.inkscape.org/namespaces/inkscape"">
                <g id=""same"" class=""first"" inkscape:label=""first""><path id=""a"" d=""M0,0 L1,1""/></g>
                <g id=""same"" class=""second"" inkscape:label=""second""><path id=""b"" d=""M2,2 L3,3""/></g>
            </svg>");

            Assert.IsNotNull(document);
            CollectionAssert.AreEqual(new[] { "a", "b" }, document.DrawnNodes.Select(n => n.Id).ToArray(), "Both groups are read");
            Assert.IsTrue(document.Metadata.GetOrGenerateElementMetadata("same").TryGetArbitraryData("class", out string groupClass));
            Assert.AreEqual("first", groupClass, "An id already used keeps the metadata of the element that used it first");
            Assert.IsTrue(document.Metadata.GetOrGenerateNamespace("inkscape").TryGetArbitraryData("same.label", out string label));
            Assert.AreEqual("first", label);
        }

        [TestMethod]
        public void ParseText_TwoOtherElementsOfOneName_LoadAndTheFirstIsKept ()
        {
            SvgDocument document = SvgParsing.ParseText($@"<svg id=""doc"" {kSvgNamespace} xmlns:sodipodi=""http://sodipodi.sourceforge.net/DTD/sodipodi-0.dtd"">
                <sodipodi:namedview id=""first""/>
                <sodipodi:namedview id=""second""/>
                <path id=""a"" d=""M0,0 L1,1""/>
            </svg>");

            Assert.IsNotNull(document);
            Assert.IsTrue(document.Metadata.GetOrGenerateNamespace("sodipodi").TryGetArbitraryData("namedview", out string kept));
            StringAssert.Contains(kept, "first");
        }

        [TestMethod]
        public void ParseText_NotSvg_IsNull ()
        {
            Assert.IsNull(SvgParsing.ParseText(@"<html id=""page""/>"));
            Assert.IsNull(SvgParsing.ParseText(@"<svg id=""broken"""));
        }

        [TestMethod]
        public void ParseText_Path_IsInTheDocumentsCoordinatesWithItsTextKept ()
        {
            SvgDocument document = SvgParsing.ParseText($@"<svg id=""doc"" {kSvgNamespace}>
                <g id=""moved"" transform=""translate(10,20)""><path id=""a"" transform=""scale(2)"" d=""M0,0 L5,5""/></g>
            </svg>");

            SvgNode path = document.DrawnNodes.Single();
            Assert.AreEqual("M0,0 L5,5", path.PathText, "The text is the d as written");
            AssertPoint(new PathPoint(10, 20), path.Path.Commands[0].End);
            AssertPoint(new PathPoint(20, 30), path.Path.Commands[1].End, "Scaled by its own transform, then moved by its group's");
            AssertPoint(new PathPoint(20, 30), path.Transform.Apply(new PathPoint(5, 5)));
        }

        [TestMethod]
        public void ParseText_PathDataThatDoesNotParse_KeepsItsTextWithNoPath ()
        {
            SvgDocument document = SvgParsing.ParseText($@"<svg id=""doc"" {kSvgNamespace}><path id=""a"" d=""L1,1 nonsense""/></svg>");
            SvgNode path = document.DrawnNodes.Single();
            Assert.IsTrue(path.IsDrawn);
            Assert.AreEqual("L1,1 nonsense", path.PathText);
            Assert.IsNull(path.Path);
        }

        [TestMethod]
        public void ParseText_Paint_IsAColorAGradientOrNone ()
        {
            SvgDocument document = SvgParsing.ParseText($@"<svg id=""doc"" {kSvgNamespace}>
                <path id=""plain"" d=""M0,0 L1,1""/>
                <path id=""colored"" style=""fill:rgba(0,128,255,0.5);stroke:linear-gradient(90deg, #FF0000 0%, #0000FF 100%)"" d=""M0,0 L1,1""/>
                <path id=""bare"" fill=""none"" d=""M0,0 L1,1""/>
            </svg>");

            SvgNode plain = Node(document, "plain");
            Assert.AreEqual(eSvgPaintKind.Color, plain.Fill.Kind);
            Assert.AreEqual(new ColorArgb(255, 0, 0, 0), plain.Fill.Color, "A path that names no fill is black");
            Assert.IsNull(plain.Stroke);

            SvgNode colored = Node(document, "colored");
            Assert.AreEqual(new ColorArgb(127, 0, 128, 255), colored.Fill.Color);
            Assert.AreEqual(eSvgPaintKind.Gradient, colored.Stroke.Kind);
            Assert.AreEqual(2, colored.Stroke.Gradient.Stops.Length);
            Assert.AreEqual("linear-gradient(90deg, #FF0000 0%, #0000FF 100%)", colored.Stroke.Text);

            Assert.IsNull(Node(document, "bare").Fill);
        }

        // ===========[ Shapes ]===================================

        [TestMethod]
        public void ParseText_Rect_IsADrawnPathOfItsOutline ()
        {
            SvgNode rect = Single($@"<rect id=""box"" x=""10"" y=""20"" width=""30"" height=""40"" fill=""#FF0000""/>");
            Assert.AreEqual("box", rect.Id);
            AssertBounds(new PathBounds(10, 20, 30, 40), rect.Path.Bounds);
            Assert.AreEqual(new ColorArgb(255, 255, 0, 0), rect.Fill.Color);
            Assert.AreEqual(ePathCommand.Close, rect.Path.Commands.Last().Kind);
        }

        [TestMethod]
        public void ParseText_RoundedRect_RoundsItsCornersInsideItsBounds ()
        {
            SvgNode rounded = Single(@"<rect x=""10"" y=""20"" width=""30"" height=""40"" rx=""5""/>");
            AssertBounds(new PathBounds(10, 20, 30, 40), rounded.Path.Bounds);
            Assert.IsTrue(rounded.Path.Commands.Any(c => c.Kind == ePathCommand.CubicTo), "The corners are curves");
            Assert.IsFalse(rounded.Path.Commands.Any(c => (c.Kind != ePathCommand.Close) && (c.End.X == 10) && (c.End.Y == 20)), "The square corner itself is cut off");
        }

        [TestMethod]
        public void ParseText_CircleAndEllipse_AreDrawnWithinTheirBounds ()
        {
            AssertBounds(new PathBounds(40, 40, 20, 20), Single(@"<circle cx=""50"" cy=""50"" r=""10""/>").Path.Bounds);
            AssertBounds(new PathBounds(30, 40, 40, 20), Single(@"<ellipse cx=""50"" cy=""50"" rx=""20"" ry=""10""/>").Path.Bounds);
        }

        [TestMethod]
        public void ParseText_LinePolylineAndPolygon_FollowTheirPoints ()
        {
            SvgNode line = Single(@"<line x1=""1"" y1=""2"" x2=""30"" y2=""40"" stroke=""#000000""/>");
            CollectionAssert.AreEqual(new[] { ePathCommand.MoveTo, ePathCommand.LineTo }, line.Path.Commands.Select(c => c.Kind).ToArray());
            AssertPoint(new PathPoint(30, 40), line.Path.Commands[1].End);

            SvgNode polyline = Single(@"<polyline points=""0,0 10,0 10,10"" stroke=""#000000""/>");
            CollectionAssert.AreEqual(new[] { ePathCommand.MoveTo, ePathCommand.LineTo, ePathCommand.LineTo }, polyline.Path.Commands.Select(c => c.Kind).ToArray(), "A polyline stays open");

            SvgNode polygon = Single(@"<polygon points=""0 0, 10 0, 10 10""/>");
            Assert.AreEqual(ePathCommand.Close, polygon.Path.Commands.Last().Kind, "A polygon closes");
            AssertBounds(new PathBounds(0, 0, 10, 10), polygon.Path.Bounds);
        }

        [TestMethod]
        public void ParseText_ShapeOfNoSize_DrawsNothing ()
        {
            SvgDocument document = SvgParsing.ParseText($@"<svg id=""doc"" {kSvgNamespace}>
                <rect width=""0"" height=""10""/><circle r=""0""/><ellipse rx=""5"" ry=""0""/><polyline points=""5,5""/>
            </svg>");
            Assert.AreEqual(0, document.DrawnNodes.Count());
        }

        [TestMethod]
        public void ParseText_ShapesUnderTheSvg_AreDrawnRatherThanKeptAsMetadata ()
        {
            SvgDocument document = SvgParsing.ParseText($@"<svg id=""doc"" {kSvgNamespace}><rect width=""1"" height=""1""/><defs id=""d""/></svg>");
            Assert.IsFalse(document.Metadata.TryGetArbitraryData("rect", out _));
            Assert.IsTrue(document.Metadata.TryGetArbitraryData("defs", out _), "Elements it does not draw are still kept");
        }

        // ===========[ viewBox ]===================================

        [TestMethod]
        public void ParseText_ViewBoxAndSize_ScaleTheDrawingToTheSize ()
        {
            SvgNode path = Single(@"<path d=""M0,0 L20,10""/>", @"width=""200"" height=""100"" viewBox=""0 0 20 10""");
            AssertPoint(new PathPoint(200, 100), path.Path.Commands[1].End);
        }

        [TestMethod]
        public void ParseText_ViewBoxOfAnotherShape_ScalesEvenlyAndCenters ()
        {
            // SVG's default is xMidYMid meet: the largest even scale that fits, centered on the axis with room left
            SvgNode path = Single(@"<path d=""M0,0 L20,10""/>", @"width=""200"" height=""200"" viewBox=""0 0 20 10""");
            AssertPoint(new PathPoint(0, 50), path.Path.Commands[0].End);
            AssertPoint(new PathPoint(200, 150), path.Path.Commands[1].End);
        }

        [TestMethod]
        public void ParseText_ViewBoxAspectRatioChoices_PlaceTheDrawingAsAsked ()
        {
            SvgNode stretched = Single(@"<path d=""M0,0 L20,10""/>", @"width=""200"" height=""200"" viewBox=""0 0 20 10"" preserveAspectRatio=""none""");
            AssertPoint(new PathPoint(200, 200), stretched.Path.Commands[1].End);

            SvgNode topLeft = Single(@"<path d=""M0,0 L20,10""/>", @"width=""200"" height=""200"" viewBox=""0 0 20 10"" preserveAspectRatio=""xMinYMin meet""");
            AssertPoint(new PathPoint(0, 0), topLeft.Path.Commands[0].End);

            SvgNode sliced = Single(@"<path d=""M0,0 L20,10""/>", @"width=""200"" height=""200"" viewBox=""0 0 20 10"" preserveAspectRatio=""xMidYMid slice""");
            AssertPoint(new PathPoint(-100, 0), sliced.Path.Commands[0].End);
            AssertPoint(new PathPoint(300, 200), sliced.Path.Commands[1].End);
        }

        [TestMethod]
        public void ParseText_ViewBoxWithNoSize_StartsItsDrawingAtTheOrigin ()
        {
            SvgNode path = Single(@"<path d=""M10,20 L40,60""/>", @"viewBox=""10 20 30 40""");
            AssertPoint(new PathPoint(0, 0), path.Path.Commands[0].End);
            AssertPoint(new PathPoint(30, 40), path.Path.Commands[1].End);
        }

        [TestMethod]
        public void ParseText_StrokeWidth_ScalesWithTheTransformsOverIt ()
        {
            // SVG strokes in the element's own units, so the width scales with the viewBox and every transform over it
            SvgNode underViewBox = Single(@"<path stroke=""#000000"" stroke-width=""2"" d=""M0,0 L20,10""/>", @"width=""200"" height=""100"" viewBox=""0 0 20 10""");
            Assert.AreEqual(20.0, underViewBox.StrokeWidth, 1e-9);

            SvgNode underGroup = Single(@"<g transform=""scale(3)""><path stroke=""#000000"" stroke-width=""2"" d=""M0,0 L1,1""/></g>");
            Assert.AreEqual(6.0, underGroup.StrokeWidth, 1e-9);

            SvgNode unscaled = Single(@"<g transform=""translate(5,5) rotate(30)""><path stroke=""#000000"" stroke-width=""2"" d=""M0,0 L1,1""/></g>");
            Assert.AreEqual(2.0, unscaled.StrokeWidth, 1e-9, "Moving and turning leave the width alone");
        }

        // ===========[ Group paint ]===================================

        [TestMethod]
        public void ParseText_GroupPaint_ReachesWhatIsInsideIt ()
        {
            SvgDocument document = SvgParsing.ParseText($@"<svg id=""doc"" {kSvgNamespace}>
                <g fill=""#FF0000"" stroke=""#0000FF"" stroke-width=""3"">
                    <path id=""plain"" d=""M0,0 L1,1""/>
                    <path id=""own"" fill=""#00FF00"" d=""M0,0 L1,1""/>
                    <g style=""fill:#FFFF00""><rect id=""nested"" width=""1"" height=""1""/></g>
                </g>
            </svg>");

            SvgNode plain = Node(document, "plain");
            Assert.AreEqual(new ColorArgb(255, 255, 0, 0), plain.Fill.Color);
            Assert.AreEqual(new ColorArgb(255, 0, 0, 255), plain.Stroke.Color);
            Assert.AreEqual(3.0, plain.StrokeWidth, kTolerance);
            Assert.AreEqual(new ColorArgb(255, 0, 255, 0), Node(document, "own").Fill.Color, "An element's own paint wins");
            Assert.AreEqual(new ColorArgb(255, 255, 255, 0), Node(document, "nested").Fill.Color, "The nearest group's paint wins");
            Assert.AreEqual(new ColorArgb(255, 0, 0, 255), Node(document, "nested").Stroke.Color, "What a nearer group leaves alone comes from further out");
        }

        [TestMethod]
        public void ParseText_GroupFillNone_LeavesWhatIsInsideItUnfilled ()
        {
            SvgDocument document = SvgParsing.ParseText($@"<svg id=""doc"" {kSvgNamespace}><g fill=""none""><path id=""a"" stroke=""#000000"" d=""M0,0 L1,1""/></g></svg>");
            Assert.IsNull(Node(document, "a").Fill);
        }

        [TestMethod]
        public void ParseText_GroupOpacity_MultipliesIntoWhatIsInsideIt ()
        {
            SvgDocument document = SvgParsing.ParseText($@"<svg id=""doc"" {kSvgNamespace}><g opacity=""0.5""><g opacity=""0.5""><path id=""a"" opacity=""0.5"" d=""M0,0 L1,1""/></g></g></svg>");
            Assert.AreEqual(0.125, Node(document, "a").Opacity, kTolerance);
        }

        [TestMethod]
        public void DrawnBounds_CoverEveryPathWhereItIsDrawn ()
        {
            SvgDocument document = SvgParsing.ParseText($@"<svg id=""doc"" {kSvgNamespace}>
                <path id=""a"" d=""M0,0 L10,10""/>
                <g id=""moved"" transform=""translate(100,50)""><path id=""b"" d=""M0,0 L5,20""/></g>
            </svg>");

            Assert.AreEqual(new PathBounds(0, 0, 105, 70), document.DrawnBounds);
            Assert.IsTrue(SvgParsing.ParseText($@"<svg id=""doc"" {kSvgNamespace}><g id=""empty""/></svg>").DrawnBounds.IsEmpty);
        }

        // ===========[ Painted bounds ]===================================
        // A stroke straddles its outline and joins its corners with miters, as SVG joins them by default, so what is
        // painted reaches past the drawn bounds by half the stroke and by each miter's tip

        [TestMethod]
        public void PaintedBounds_CountHalfOfEachStrokeOutsideItsOutline ()
        {
            SvgDocument document = SvgParsing.ParseText($@"<svg id=""doc"" {kSvgNamespace}>
                <rect x=""10"" y=""10"" width=""20"" height=""20"" fill=""none"" stroke=""#000000"" stroke-width=""2""/>
            </svg>");

            AssertBounds(new PathBounds(9, 9, 22, 22), document.PaintedBounds);
        }

        [TestMethod]
        public void PaintedBounds_CountTheTipOfEveryMiterWithinTheLimit ()
        {
            // The apex turns through 53.13 degrees, so its miter reaches half the stroke over sin(26.57) past it, sqrt(5)/2.
            //  The base corners, the closing one included, reach out half the golden ratio and half the stroke down.
            SvgDocument document = SvgParsing.ParseText($@"<svg id=""doc"" {kSvgNamespace}>
                <polygon points=""0,10 5,0 10,10"" stroke=""#000000"" stroke-width=""1""/>
            </svg>");

            double baseTip = (1.0 + Math.Sqrt(5.0)) / 4.0;
            double apexTip = Math.Sqrt(5.0) / 2.0;
            AssertBounds(new PathBounds(-baseTip, -apexTip, 10.0 + (2.0 * baseTip), 10.5 + apexTip), document.PaintedBounds);
        }

        [TestMethod]
        public void PaintedBounds_BevelAMiterPastTheLimitAndCapOpenEndsSquare ()
        {
            // The spike's miter would reach 100 past its point, far past SVG's limit of 4, so it is beveled. The open ends
            //  are butt capped, so they reach past their points only across the line.
            SvgDocument document = SvgParsing.ParseText($@"<svg id=""doc"" {kSvgNamespace}>
                <polyline points=""0,0 100,1 0,2"" fill=""none"" stroke=""#000000"" stroke-width=""2""/>
            </svg>");

            double across = 100.0 / Math.Sqrt(10001.0);
            double along = 1.0 / Math.Sqrt(10001.0);
            AssertBounds(new PathBounds(-along, -across, 100.0 + (2.0 * along), 2.0 + (2.0 * across)), document.PaintedBounds);
        }

        [TestMethod]
        public void PaintedBounds_CountCurvedStrokesAtTheirTurnsInTheDocumentsCoordinates ()
        {
            SvgDocument document = SvgParsing.ParseText($@"<svg id=""doc"" {kSvgNamespace}>
                <circle cx=""50"" cy=""50"" r=""10"" fill=""none"" stroke=""#000000"" stroke-width=""4""/>
                <g transform=""scale(2)""><circle cx=""100"" cy=""100"" r=""10"" fill=""none"" stroke=""#000000"" stroke-width=""2""/></g>
            </svg>");

            // The first circle reaches 2 past its 40 to 60, the second, scaled with its stroke, 2 past its 180 to 220
            AssertBounds(new PathBounds(38, 38, 184, 184), document.PaintedBounds);
        }

        [TestMethod]
        public void PaintedBounds_OfPathsWithNoStroke_AreTheirDrawnBounds ()
        {
            SvgDocument document = SvgParsing.ParseText($@"<svg id=""doc"" {kSvgNamespace}>
                <rect x=""10"" y=""10"" width=""20"" height=""20""/>
                <rect x=""40"" y=""40"" width=""5"" height=""5"" stroke=""#000000"" stroke-width=""0""/>
            </svg>");

            AssertBounds(document.DrawnBounds, document.PaintedBounds);
            Assert.IsTrue(SvgParsing.ParseText($@"<svg id=""doc"" {kSvgNamespace}><g id=""empty""/></svg>").PaintedBounds.IsEmpty);
        }

        [TestMethod]
        public void ParseText_MillimeterLengths_ReadAtTheDpiGiven ()
        {
            string svg = $@"<svg id=""doc"" width=""25.4mm"" height=""2.54cm"" {kSvgNamespace}><path id=""a"" d=""M0,0 L1,1""/></svg>";
            Assert.AreEqual(96.0, SvgParsing.ParseText(svg).Metadata.Width, kTolerance, "CSS's 96 to the inch unless told otherwise");
            Assert.AreEqual(144.0, SvgParsing.ParseText(svg, 144, 144).Metadata.Height, kTolerance);
        }

        // ===========[ Helpers ]===================================

        private static SvgNode Node (SvgDocument document, string id) => document.DrawnNodes.Single(n => n.Id == id);

        private static SvgNode Single (string element, string svgAttributes = "")
        {
            SvgDocument document = SvgParsing.ParseText($@"<svg id=""doc"" {svgAttributes} {kSvgNamespace}>{element}</svg>");
            return document.DrawnNodes.Single();
        }

        private static void AssertBounds (PathBounds expected, PathBounds actual)
        {
            Assert.AreEqual(expected.X, actual.X, 1e-6, $"x of {actual}");
            Assert.AreEqual(expected.Y, actual.Y, 1e-6, $"y of {actual}");
            Assert.AreEqual(expected.Width, actual.Width, 1e-6, $"width of {actual}");
            Assert.AreEqual(expected.Height, actual.Height, 1e-6, $"height of {actual}");
        }

        private static void AssertPoint (PathPoint expected, PathPoint actual, string message = null)
        {
            Assert.AreEqual(expected.X, actual.X, 1e-6, $"x of {actual}. {message}");
            Assert.AreEqual(expected.Y, actual.Y, 1e-6, $"y of {actual}. {message}");
        }
    }
}
