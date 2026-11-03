namespace AJut.UX.Tests
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Windows;
    using System.Windows.Media;
    using AJut.Tree;
    using AJut.UX;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// What WPF's <see cref="SvgSerialization.LoadSvg(Stream)"/> makes of a document: the tree, each path's paint and
    /// transform, and the metadata kept beside them.
    /// </summary>
    [TestClass]
    public class WpfSvgLoadingTests
    {
        private const double kTolerance = 1e-9;
        private const string kSvgNamespace = "xmlns=\"http://www.w3.org/2000/svg\"";

        // ===========[ The tree ]===================================

        [TestMethod]
        public void LoadSvg_GroupsAndPaths_BecomeATreeUnderTheSvgElement ()
        {
            StaTestThread.Run(() =>
            {
                SvgSource source = Load($@"<svg id=""doc"" {kSvgNamespace}>
                    <g id=""layer"">
                        <path id=""first"" d=""M0,0 L10,0 L10,10 Z""/>
                        <g id=""inner""><path id=""second"" d=""M1,1 L2,2""/></g>
                    </g>
                    <path id=""third"" d=""M5,5 L6,6""/>
                </svg>");

                Assert.IsNotNull(source);
                Assert.AreEqual("doc", source.Root.Id, "The svg element itself is the root");
                Assert.IsNull(source.Root.Data, "A group draws nothing of its own");
                CollectionAssert.AreEqual(new[] { "layer", "third" }, source.Root.Children.Select(c => c.Id).ToArray(), "Children keep document order");

                SvgTreeElement layer = source.Root.Children[0];
                Assert.IsNull(layer.Data);
                CollectionAssert.AreEqual(new[] { "first", "inner" }, layer.Children.Select(c => c.Id).ToArray());
                Assert.IsNotNull(Find(source, "first").Data, "A path carries its geometry");
                Assert.IsNotNull(Find(source, "second").Data, "A path in a nested group is found");
                Assert.IsTrue(Find(source, "first").Data.Bounds.Width > 0.0, "The geometry is the path's d");
            });
        }

        [TestMethod]
        public void LoadSvg_PathWithoutData_IsLeftOut ()
        {
            StaTestThread.Run(() =>
            {
                SvgSource source = Load($@"<svg id=""doc"" {kSvgNamespace}><g id=""layer""><path id=""empty""/><path id=""drawn"" d=""M0,0 L1,1""/></g></svg>");
                CollectionAssert.AreEqual(new[] { "drawn" }, source.Root.Children[0].Children.Select(c => c.Id).ToArray());
            });
        }

        [TestMethod]
        public void LoadSvg_NotAnSvgOrNotXml_IsNull ()
        {
            StaTestThread.Run(() =>
            {
                Assert.IsNull(Load(@"<html id=""page""><body/></html>"), "A document that is not an svg loads nothing");
                Assert.IsNull(Load(@"<svg id=""broken"""), "Text that is not xml loads nothing");
            });
        }

        [TestMethod]
        public void LoadSvg_SvgWithoutAnId_LoadsWithAMadeUpId ()
        {
            StaTestThread.Run(() =>
            {
                SvgSource source = Load($@"<svg {kSvgNamespace}><path id=""a"" d=""M0,0 L1,1""/></svg>");
                Assert.IsNotNull(source);
                Assert.IsFalse(string.IsNullOrEmpty(source.Root.Id));
                Assert.AreEqual("a", source.Root.Children.Single().Id);
            });
        }

        [TestMethod]
        public void LoadSvg_GroupWithoutAnId_LoadsWithAMadeUpId ()
        {
            StaTestThread.Run(() =>
            {
                SvgSource source = Load($@"<svg id=""doc"" {kSvgNamespace}><g><path id=""a"" d=""M0,0 L1,1""/></g></svg>");
                Assert.IsNotNull(source);
                Assert.IsFalse(string.IsNullOrEmpty(source.Root.Children.Single().Id));
                Assert.IsNotNull(Find(source, "a"));
            });
        }

        [TestMethod]
        public void LoadSvg_CommentInTheSvg_IsSkipped ()
        {
            StaTestThread.Run(() =>
            {
                SvgSource source = Load($@"<svg id=""doc"" {kSvgNamespace}><!-- made by hand --><path id=""a"" d=""M0,0 L1,1""/></svg>");
                Assert.IsNotNull(source);
                Assert.IsNotNull(Find(source, "a"));
            });
        }

        // ===========[ Metadata ]===================================

        [TestMethod]
        public void LoadSvg_Metadata_KeepsTheIdSizeAttributesAndOtherElements ()
        {
            StaTestThread.Run(() =>
            {
                SvgSource source = Load($@"<svg id=""doc"" width=""100px"" height=""50"" viewBox=""0 0 10 5"" version=""1.1"" {kSvgNamespace}>
                    <defs id=""shared""><linearGradient id=""fade""/></defs>
                    <g id=""layer"" class=""art""><path id=""a"" d=""M0,0 L1,1""/></g>
                </svg>");

                Assert.AreEqual("doc", source.Metadata.Id);
                Assert.AreEqual(100.0, source.Metadata.Width, kTolerance, "px is read as pixels");
                Assert.AreEqual(50.0, source.Metadata.Height, kTolerance, "A number with no unit is pixels");
                Assert.AreEqual("0 0 10 5", source.Metadata["viewBox"]);
                Assert.AreEqual("1.1", source.Metadata["version"]);
                Assert.IsTrue(source.Metadata["defs"].Contains("linearGradient"), "An element that is not a group or a path is kept whole");
                Assert.IsTrue(source.Metadata.GetOrGenerateElementMetadata("layer").TryGetArbitraryData("class", out string groupClass), "A group's attributes are kept by its id");
                Assert.AreEqual("art", groupClass);
            });
        }

        [TestMethod]
        public void LoadSvg_NoSize_IsNaN ()
        {
            StaTestThread.Run(() =>
            {
                SvgSource source = Load($@"<svg id=""doc"" {kSvgNamespace}><path id=""a"" d=""M0,0 L1,1""/></svg>");
                Assert.IsTrue(double.IsNaN(source.Metadata.Width));
                Assert.IsTrue(double.IsNaN(source.Metadata.Height));
            });
        }

        [TestMethod]
        public void LoadSvg_OneCharacterNumber_Reads ()
        {
            StaTestThread.Run(() =>
            {
                SvgSource source = Load($@"<svg id=""doc"" width=""5"" height=""8"" {kSvgNamespace}><g id=""moved"" transform=""translate(5,0)""><path id=""a"" d=""M0,0 L1,1""/></g></svg>");
                Assert.AreEqual(5.0, source.Metadata.Width, kTolerance);
                Assert.AreEqual(8.0, source.Metadata.Height, kTolerance);
                AssertPoint(new Point(5, 0), Find(source, "a").Data.Transform.Value.Transform(new Point(0, 0)));
            });
        }

        // ===========[ Paint ]===================================

        [TestMethod]
        public void LoadSvg_PathStyle_ReadsFillStrokeStrokeWidthAndOpacity ()
        {
            StaTestThread.Run(() =>
            {
                SvgTreeElement path = LoadPath("fill:rgb(255,0,0);stroke:#0000FF;stroke-width:2.5;opacity:0.5");
                Assert.AreEqual(Color.FromArgb(255, 255, 0, 0), SolidColorOf(path.FillBrush));
                Assert.AreEqual(Color.FromArgb(255, 0, 0, 255), SolidColorOf(path.StrokeBrush));
                Assert.AreEqual(2.5, path.StrokeWidth, kTolerance);
                Assert.AreEqual(0.5, path.Opacity, kTolerance);
            });
        }

        [TestMethod]
        public void LoadSvg_NoStyle_HasNoStrokeAndTheDefaultWidthAndOpacity ()
        {
            StaTestThread.Run(() =>
            {
                SvgTreeElement path = LoadPath(null);
                Assert.IsNull(path.StrokeBrush);
                Assert.AreEqual(1.0, path.StrokeWidth, kTolerance);
                Assert.AreEqual(1.0, path.Opacity, kTolerance);
            });
        }

        [TestMethod]
        public void LoadSvg_NoFill_IsBlack ()
        {
            StaTestThread.Run(() =>
            {
                Assert.AreEqual(Colors.Black, SolidColorOf(LoadPath(null).FillBrush), "SVG fills a path black when it names no fill");
                Assert.AreEqual(Colors.Black, SolidColorOf(LoadPath("stroke:#0000FF").FillBrush));
            });
        }

        [TestMethod]
        public void LoadSvg_FillNone_IsNoFill ()
        {
            StaTestThread.Run(() =>
            {
                Assert.IsNull(LoadPath("fill:none").FillBrush);
            });
        }

        [TestMethod]
        public void WriteSvgTo_PathWithNoFill_ReadsBackWithNoFill ()
        {
            StaTestThread.Run(() =>
            {
                SvgTreeElement unfilled = LoadPath("fill:none;stroke:#0000FF;stroke-width:2");
                SvgTreeElement readBack = RoundTrip(new SvgSource(unfilled.Parent, new SvgMetadata("doc", 10, 10))).Root.Children.Single();
                Assert.IsNull(readBack.FillBrush, "A path written with no fill reads back with none, not black");
                Assert.AreEqual(Color.FromArgb(255, 0, 0, 255), SolidColorOf(readBack.StrokeBrush));
            });
        }

        [TestMethod]
        public void LoadSvg_FillColor_ReadsRgbHexAndNames ()
        {
            StaTestThread.Run(() =>
            {
                Assert.AreEqual(Color.FromArgb(255, 1, 2, 3), SolidColorOf(LoadPath("fill:rgb(1, 2, 3)").FillBrush));
                Assert.AreEqual(Color.FromArgb(255, 0, 128, 255), SolidColorOf(LoadPath("fill:rgba(0,128,255,1)").FillBrush), "rgba's alpha runs from 0 to 1");
                Assert.AreEqual(Color.FromArgb(255, 0, 255, 0), SolidColorOf(LoadPath("fill:#00FF00").FillBrush));
                Assert.AreEqual(Color.FromArgb(255, 0, 255, 0), SolidColorOf(LoadPath("fill:#0F0").FillBrush));
                Assert.AreEqual(Color.FromArgb(0x80, 0xFF, 0, 0), SolidColorOf(LoadPath("fill:#80FF0000").FillBrush), "Eight digits are alpha first, as XAML writes them");
                Assert.AreEqual(Colors.Green, SolidColorOf(LoadPath("fill:Green").FillBrush), "A color name in XAML's casing");
            });
        }

        [TestMethod]
        public void LoadSvg_RgbaFill_ReadsAFractionalAlpha ()
        {
            StaTestThread.Run(() =>
            {
                Assert.AreEqual(Color.FromArgb(127, 0, 128, 255), SolidColorOf(LoadPath("fill:rgba(0,128,255,0.5)").FillBrush));
            });
        }

        [TestMethod]
        public void LoadSvg_ColorName_ReadsInAnyCaseAndLength ()
        {
            StaTestThread.Run(() =>
            {
                Assert.AreEqual(Colors.Red, SolidColorOf(LoadPath("fill:red").FillBrush));
                Assert.AreEqual(Colors.Red, SolidColorOf(LoadPath("fill:Red").FillBrush));
                Assert.AreEqual(Colors.Blue, SolidColorOf(LoadPath("fill:blue").FillBrush));
                Assert.AreEqual(Colors.Orange, SolidColorOf(LoadPath("fill:orange").FillBrush));
                Assert.AreEqual(Colors.White, SolidColorOf(LoadPath("fill:white").FillBrush));
                Assert.AreEqual(Colors.DarkBlue, SolidColorOf(LoadPath("fill:darkblue").FillBrush));
            });
        }

        [TestMethod]
        public void LoadSvg_StrokeGradient_ReadsAsAGradientBrush ()
        {
            StaTestThread.Run(() =>
            {
                SvgTreeElement path = LoadPath("stroke:linear-gradient(90deg, #FF0000 0%, #0000FF 100%)");
                Assert.IsInstanceOfType(path.StrokeBrush, typeof(LinearGradientBrush));
                Assert.AreEqual(2, ((LinearGradientBrush)path.StrokeBrush).GradientStops.Count);
            });
        }

        [TestMethod]
        public void LoadSvg_StrokeNoneOrRgb_IsNoStrokeOrThatColor ()
        {
            StaTestThread.Run(() =>
            {
                Assert.IsNull(LoadPath("stroke:none").StrokeBrush, "none asks for no stroke");
                Assert.AreEqual(Color.FromArgb(255, 0, 0, 255), SolidColorOf(LoadPath("stroke:rgb(0,0,255)").StrokeBrush));
            });
        }

        [TestMethod]
        public void LoadSvg_StrokeWidthInPixels_ReadsTheNumber ()
        {
            StaTestThread.Run(() =>
            {
                Assert.AreEqual(2.0, LoadPath("stroke:#000000;stroke-width:2px").StrokeWidth, kTolerance, "The px unit WPF's own writer puts on a stroke width");
            });
        }

        [TestMethod]
        public void LoadSvg_StyleWithSpaces_ReadsEveryPart ()
        {
            StaTestThread.Run(() =>
            {
                SvgTreeElement path = LoadPath("fill: #FF0000; stroke: #0000FF; stroke-width: 3; opacity: 0.25");
                Assert.AreEqual(Color.FromArgb(255, 255, 0, 0), SolidColorOf(path.FillBrush));
                Assert.AreEqual(Color.FromArgb(255, 0, 0, 255), SolidColorOf(path.StrokeBrush));
                Assert.AreEqual(3.0, path.StrokeWidth, kTolerance);
                Assert.AreEqual(0.25, path.Opacity, kTolerance);
            });
        }

        [TestMethod]
        public void LoadSvg_PaintAttributes_ReadLikeStyle ()
        {
            StaTestThread.Run(() =>
            {
                SvgSource source = Load($@"<svg id=""doc"" {kSvgNamespace}><path id=""a"" fill=""#FF0000"" stroke=""#0000FF"" stroke-width=""2"" opacity=""0.5"" d=""M0,0 L1,1""/></svg>");
                SvgTreeElement path = Find(source, "a");
                Assert.AreEqual(Color.FromArgb(255, 255, 0, 0), SolidColorOf(path.FillBrush));
                Assert.AreEqual(Color.FromArgb(255, 0, 0, 255), SolidColorOf(path.StrokeBrush));
                Assert.AreEqual(2.0, path.StrokeWidth, kTolerance);
                Assert.AreEqual(0.5, path.Opacity, kTolerance);
            });
        }

        [TestMethod]
        public void LoadSvg_CommaDecimalCulture_ReadsNumbersTheSame ()
        {
            StaTestThread.Run(() =>
            {
                CultureInfo previous = Thread.CurrentThread.CurrentCulture;
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                try
                {
                    SvgTreeElement path = LoadPath("stroke:#000000;stroke-width:1.5;opacity:0.25");
                    Assert.AreEqual(1.5, path.StrokeWidth, kTolerance);
                    Assert.AreEqual(0.25, path.Opacity, kTolerance);
                }
                finally
                {
                    Thread.CurrentThread.CurrentCulture = previous;
                }
            });
        }

        // ===========[ Shapes, viewBox and group paint ]===================================

        [TestMethod]
        public void LoadSvg_Shapes_AreDrawnAsPaths ()
        {
            StaTestThread.Run(() =>
            {
                SvgSource source = Load($@"<svg id=""doc"" {kSvgNamespace}><rect id=""box"" x=""10"" y=""20"" width=""30"" height=""40"" fill=""#FF0000""/><circle id=""dot"" cx=""50"" cy=""50"" r=""10""/></svg>");
                SvgTreeElement box = Find(source, "box");
                Assert.IsNotNull(box?.Data, "A rect is drawn");
                Assert.AreEqual(new Rect(10, 20, 30, 40), box.Data.Bounds);
                Assert.AreEqual(Color.FromArgb(255, 255, 0, 0), SolidColorOf(box.FillBrush));
                Assert.IsNotNull(Find(source, "dot")?.Data, "A circle is drawn");
            });
        }

        [TestMethod]
        public void LoadSvg_ViewBox_ScalesTheDrawingToTheSvgsSize ()
        {
            StaTestThread.Run(() =>
            {
                SvgSource source = Load($@"<svg id=""doc"" width=""200"" height=""100"" viewBox=""0 0 20 10"" {kSvgNamespace}><path id=""a"" d=""M0,0 L20,10""/></svg>");
                AssertPoint(new Point(200, 100), Find(source, "a").Data.Transform.Value.Transform(new Point(20, 10)));
            });
        }

        [TestMethod]
        public void LoadSvg_StrokeUnderAScaledGroup_ScalesItsWidth ()
        {
            StaTestThread.Run(() =>
            {
                SvgSource source = Load($@"<svg id=""doc"" {kSvgNamespace}><g transform=""scale(3)""><path id=""a"" stroke=""#000000"" stroke-width=""2"" d=""M0,0 L1,1""/></g></svg>");
                Assert.AreEqual(6.0, Find(source, "a").StrokeWidth, kTolerance, "The geometry's transform leaves a WPF Path's stroke alone, so the width carries the scale");
            });
        }

        [TestMethod]
        public void LoadSvg_GroupPaint_ReachesItsPaths ()
        {
            StaTestThread.Run(() =>
            {
                SvgSource source = Load($@"<svg id=""doc"" {kSvgNamespace}><g fill=""#FF0000""><path id=""a"" d=""M0,0 L1,1""/></g></svg>");
                Assert.AreEqual(Color.FromArgb(255, 255, 0, 0), SolidColorOf(Find(source, "a").FillBrush));
            });
        }

        // ===========[ Writing ]===================================

        [TestMethod]
        public void WriteSvgTo_CommaDecimalCulture_WritesNumbersThatReadBack ()
        {
            StaTestThread.Run(() =>
            {
                // The drawing fills the svg's size exactly, so the viewBox WriteSvgTo writes (the drawing's bounds, in
                //  that size) reads back as no scale at all
                SvgTreeElement path = Find(Load($@"<svg id=""doc"" {kSvgNamespace}><path id=""target"" style=""fill:#FF0000;stroke:#0000FF;stroke-width:1.5;opacity:0.5"" d=""M0,0 L10.5,20.25""/></svg>"), "target");
                var source = new SvgSource(path.Parent, new SvgMetadata("doc", 10.5, 20.25));
                CultureInfo previous = Thread.CurrentThread.CurrentCulture;
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                SvgSource readBack;
                try
                {
                    readBack = RoundTrip(source);
                }
                finally
                {
                    Thread.CurrentThread.CurrentCulture = previous;
                }

                SvgTreeElement readPath = readBack.Root.Children.Single();
                Assert.AreEqual(1.5, readPath.StrokeWidth, kTolerance);
                Assert.AreEqual(0.5, readPath.Opacity, 1e-4);
                Assert.AreEqual(10.5, readBack.Metadata.Width, kTolerance);
                Assert.AreEqual(20.25, readBack.Metadata.Height, kTolerance);
            });
        }

        [TestMethod]
        public void WriteSvgTo_PathWithNoId_WritesItWithoutOne ()
        {
            StaTestThread.Run(() =>
            {
                SvgSource source = Load($@"<svg id=""doc"" {kSvgNamespace}><path fill=""#FF0000"" d=""M0,0 L10,10""/></svg>");
                SvgTreeElement readPath = RoundTrip(source).Root.Children.Single();
                Assert.IsNull(readPath.Id, "A path the document gave no id still has none");
                Assert.AreEqual(Color.FromArgb(255, 255, 0, 0), SolidColorOf(readPath.FillBrush));
            });
        }

        [TestMethod]
        public void WriteSvgTo_PathUnderATransform_KeepsItsTransform ()
        {
            StaTestThread.Run(() =>
            {
                // The svg is the size the scaled square draws at, so the viewBox WriteSvgTo writes reads back as no scale
                SvgSource source = Load($@"<svg id=""doc"" width=""20"" height=""10"" {kSvgNamespace}><g id=""wide"" transform=""scale(2,1)""><path id=""a"" d=""M0,0 L10,0 L10,10 L0,10 Z""/></g></svg>");
                SvgTreeElement readBack = TreeTraversal<SvgTreeElement>.All(RoundTrip(source).Root).Single(e => e.Data != null);
                Assert.AreEqual(new Rect(0, 0, 20, 10), readBack.Data.Bounds, "The square comes back stretched twice as wide, as it was drawn");
            });
        }

        [TestMethod]
        public void WriteSvgTo_TranslucentFill_KeepsItsAlpha ()
        {
            StaTestThread.Run(() =>
            {
                foreach (byte alpha in new byte[] { 0x80, 0x01, 0xFE, 0x00 })
                {
                    SvgTreeElement path = LoadPath($"fill:#{alpha:X2}FF0000");
                    SvgTreeElement readPath = RoundTrip(new SvgSource(path.Parent, new SvgMetadata("doc", 10, 10))).Root.Children.Single();
                    Assert.AreEqual(Color.FromArgb(alpha, 0xFF, 0, 0), SolidColorOf(readPath.FillBrush), $"Alpha {alpha} survives being written");
                }
            });
        }

        [TestMethod]
        public void WriteSvgTo_GradientStroke_KeepsItsStopOffsets ()
        {
            StaTestThread.Run(() =>
            {
                SvgTreeElement path = LoadPath("stroke:linear-gradient(90deg, #FF0000 0%, #00FF00 25%, #0000FF 100%)");
                SvgTreeElement readPath = RoundTrip(new SvgSource(path.Parent, new SvgMetadata("doc", 10, 10))).Root.Children.Single();
                Assert.IsInstanceOfType(readPath.StrokeBrush, typeof(LinearGradientBrush));
                CollectionAssert.AreEqual(new[] { 0.0, 0.25, 1.0 }, ((LinearGradientBrush)readPath.StrokeBrush).GradientStops.Select(s => Math.Round(s.Offset, 6)).ToArray());
            });
        }

        // ===========[ Transforms ]===================================

        [TestMethod]
        public void LoadSvg_GroupTransform_MovesTheGroupsOwnPaths ()
        {
            StaTestThread.Run(() =>
            {
                AssertPoint(new Point(11, 20), TransformOfPathIn("translate(10,20)").Transform(new Point(1, 0)));
                AssertPoint(new Point(2, 0), TransformOfPathIn("scale(2)").Transform(new Point(1, 0)));
                AssertPoint(new Point(2, 3), TransformOfPathIn("scale(2,3)").Transform(new Point(1, 1)));
                AssertPoint(new Point(0, 1), TransformOfPathIn("rotate(90)").Transform(new Point(1, 0)));
                AssertPoint(new Point(0, 1), TransformOfPathIn("rotate(90deg)").Transform(new Point(1, 0)));
            });
        }

        [TestMethod]
        public void LoadSvg_GroupTransform_LeavesTheGroupsOwnTransformEmpty ()
        {
            StaTestThread.Run(() =>
            {
                SvgSource source = Load($@"<svg id=""doc"" {kSvgNamespace}><g id=""moved"" transform=""translate(10,20)""><path id=""a"" d=""M0,0 L1,1""/></g></svg>");
                Assert.IsNull(Find(source, "moved").LocalTransform, "The group's transform reaches its paths through their geometry, so the template does not apply it twice");
            });
        }

        [TestMethod]
        public void LoadSvg_TransformList_AppliesRightToLeft ()
        {
            StaTestThread.Run(() =>
            {
                // SVG applies the last transform in a list first: translate(10,20) scale(2) scales, then moves
                AssertPoint(new Point(12, 20), TransformOfPathIn("translate(10,20) scale(2)").Transform(new Point(1, 0)));
            });
        }

        [TestMethod]
        public void LoadSvg_NestedGroupTransforms_AllApply ()
        {
            StaTestThread.Run(() =>
            {
                SvgSource source = Load($@"<svg id=""doc"" {kSvgNamespace}>
                    <g id=""outer"" transform=""translate(10,30)""><g id=""inner"" transform=""translate(20,40)""><path id=""a"" d=""M0,0 L1,1""/></g></g>
                </svg>");
                AssertPoint(new Point(30, 70), Find(source, "a").Data.Transform.Value.Transform(new Point(0, 0)));
            });
        }

        [TestMethod]
        public void LoadSvg_ScaleWithOneNumber_ScalesBothAxes ()
        {
            StaTestThread.Run(() =>
            {
                AssertPoint(new Point(2, 2), TransformOfPathIn("scale(2)").Transform(new Point(1, 1)));
            });
        }

        [TestMethod]
        public void LoadSvg_TransformArgumentsSeparatedBySpaces_Read ()
        {
            StaTestThread.Run(() =>
            {
                AssertPoint(new Point(10, 20), TransformOfPathIn("translate(10 20)").Transform(new Point(0, 0)));
                AssertPoint(new Point(2, 3), TransformOfPathIn("scale(2 3)").Transform(new Point(1, 1)));
            });
        }

        [TestMethod]
        public void LoadSvg_MatrixTransform_Applies ()
        {
            StaTestThread.Run(() =>
            {
                AssertPoint(new Point(11, 20), TransformOfPathIn("matrix(1,0,0,1,10,20)").Transform(new Point(1, 0)));
                AssertPoint(new Point(2, 3), TransformOfPathIn("matrix(2 0 0 3 0 0)").Transform(new Point(1, 1)));
            });
        }

        [TestMethod]
        public void LoadSvg_SkewXAndSkewY_Apply ()
        {
            StaTestThread.Run(() =>
            {
                AssertPoint(new Point(1, 1), TransformOfPathIn("skewX(45)").Transform(new Point(0, 1)));
                AssertPoint(new Point(1, 1), TransformOfPathIn("skewY(45)").Transform(new Point(1, 0)));
            });
        }

        [TestMethod]
        public void LoadSvg_RotateAboutAPoint_Applies ()
        {
            StaTestThread.Run(() =>
            {
                // A quarter turn about (10,10) carries (10,0), above the center, round to its right
                AssertPoint(new Point(20, 10), TransformOfPathIn("rotate(90 10 10)").Transform(new Point(10, 0)));
            });
        }

        [TestMethod]
        public void LoadSvg_PathTransform_Applies ()
        {
            StaTestThread.Run(() =>
            {
                SvgSource source = Load($@"<svg id=""doc"" {kSvgNamespace}><path id=""a"" transform=""translate(30,40)"" d=""M0,0 L1,1""/></svg>");
                AssertPoint(new Point(30, 40), Find(source, "a").Data.Transform.Value.Transform(new Point(0, 0)));
            });
        }

        // ===========[ Helpers ]===================================

        private static SvgSource Load (string svgText)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svgText));
            return SvgSerialization.LoadSvg(stream);
        }

        private static SvgSource RoundTrip (SvgSource source)
        {
            using var written = new MemoryStream();
            SvgSerialization.WriteSvgTo(source, written);
            written.Position = 0;
            return SvgSerialization.LoadSvg(written);
        }

        private static SvgTreeElement LoadPath (string style)
        {
            string styleAttribute = style == null ? string.Empty : $@" style=""{style}""";
            SvgSource source = Load($@"<svg id=""doc"" {kSvgNamespace}><path id=""target""{styleAttribute} d=""M0,0 L10,10""/></svg>");
            return Find(source, "target");
        }

        private static Matrix TransformOfPathIn (string groupTransform)
        {
            SvgSource source = Load($@"<svg id=""doc"" {kSvgNamespace}><g id=""moved"" transform=""{groupTransform}""><path id=""target"" d=""M0,0 L1,1""/></g></svg>");
            Transform transform = Find(source, "target").Data.Transform;
            Assert.IsNotNull(transform, $"'{groupTransform}' reaches the path");
            return transform.Value;
        }

        private static SvgTreeElement Find (SvgSource source, string id)
        {
            return TreeTraversal<SvgTreeElement>.All(source.Root).FirstOrDefault(e => e.Id == id);
        }

        private static Color SolidColorOf (Brush brush)
        {
            Assert.IsInstanceOfType(brush, typeof(SolidColorBrush));
            return ((SolidColorBrush)brush).Color;
        }

        private static void AssertPoint (Point expected, Point actual)
        {
            Assert.AreEqual(expected.X, actual.X, 1e-6, $"x of {actual}");
            Assert.AreEqual(expected.Y, actual.Y, 1e-6, $"y of {actual}");
        }
    }
}
