namespace AJut.UX.Tests
{
    using AJut.UX;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using Android.Graphics;
    using Android.Views;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using AndroidColor = Android.Graphics.Color;

    [TestClass]
    public class SvgTests
    {
        private const int kWidth = 200;
        private const int kHeight = 100;
        private const string kSvgNamespace = "xmlns=\"http://www.w3.org/2000/svg\"";

        [TestMethod]
        public void Draw_Unstretched_DrawsAtItsOwnCoordinatesInDp ()
        {
            var svg = new Svg(TestHost.RequiredContext) { Source = _RedSquare() };
            float density = TestHost.RequiredContext.Resources!.DisplayMetrics!.Density;
            int insideInDpOnly = (int)(10 * density) - 2;
            Assert.IsTrue(insideInDpOnly > 10, "The device's density is high enough to tell dp from pixels");

            using Bitmap bitmap = _Render(svg);
            Assert.AreEqual(new ColorArgb(255, 255, 0, 0), _Pixel(bitmap, insideInDpOnly, insideInDpOnly), "Inside the square, measured in dp, is its fill");
            Assert.AreEqual(0, (int)_Pixel(bitmap, insideInDpOnly + 4, insideInDpOnly + 4).A, "Past the square is clear");
        }

        [TestMethod]
        public void Draw_Uniform_FitsTheDrawingAndCentersIt ()
        {
            // A 10 by 10 square fitted evenly into 200 by 100 is 100 by 100, from x 50 to 150
            var svg = new Svg(TestHost.RequiredContext) { Source = _RedSquare(), Stretch = ePathStretch.Uniform };
            using Bitmap bitmap = _Render(svg);
            Assert.AreEqual(new ColorArgb(255, 255, 0, 0), _Pixel(bitmap, 100, 50));
            Assert.AreEqual(0, (int)_Pixel(bitmap, 25, 50).A, "Left of the centered square is clear");
            Assert.AreEqual(0, (int)_Pixel(bitmap, 175, 50).A, "Right of it is clear");
        }

        [TestMethod]
        public void Draw_StrokeOnly_PaintsTheOutlineAndNotTheInside ()
        {
            var svg = new Svg(TestHost.RequiredContext)
            {
                Source = SvgParsing.ParseText($@"<svg id=""doc"" {kSvgNamespace}><rect x=""10"" y=""10"" width=""80"" height=""80"" fill=""none"" stroke=""#0000FF"" stroke-width=""4""/></svg>"),
                Stretch = ePathStretch.Fill,
            };

            using Bitmap bitmap = _Render(svg);
            Assert.AreEqual(0, (int)_Pixel(bitmap, 100, 50).A, "The middle is unfilled");
            Assert.AreNotEqual(0, (int)_Pixel(bitmap, 100, 1).A, "The top edge is stroked");
        }

        [TestMethod]
        public void Draw_Opacity_DrawsTheElementTranslucent ()
        {
            var svg = new Svg(TestHost.RequiredContext)
            {
                Source = SvgParsing.ParseText($@"<svg id=""doc"" {kSvgNamespace}><rect width=""10"" height=""10"" fill=""#FF0000"" opacity=""0.5""/></svg>"),
                Stretch = ePathStretch.Fill,
            };

            using Bitmap bitmap = _Render(svg);
            Assert.AreEqual(128, (int)_Pixel(bitmap, 100, 50).A, 2);
        }

        [TestMethod]
        public void Measure_WrapContent_IsTheDrawingsSizeInDp ()
        {
            var svg = new Svg(TestHost.RequiredContext) { Source = SvgParsing.ParseText($@"<svg id=""doc"" {kSvgNamespace}><rect x=""5"" y=""5"" width=""10"" height=""20""/></svg>") };
            float density = TestHost.RequiredContext.Resources!.DisplayMetrics!.Density;
            int unspecified = View.MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified);

            svg.Measure(unspecified, unspecified);
            Assert.AreEqual(15 * density, svg.MeasuredWidth, 1.0, "Unstretched, from the origin out to its far edge");
            Assert.AreEqual(25 * density, svg.MeasuredHeight, 1.0);

            svg.Stretch = ePathStretch.Uniform;
            svg.Measure(unspecified, unspecified);
            Assert.AreEqual(10 * density, svg.MeasuredWidth, 1.0, "Stretched, just what it draws");
            Assert.AreEqual(20 * density, svg.MeasuredHeight, 1.0);
        }

        [TestMethod]
        public void NoSource_MeasuresAndDrawsNothing ()
        {
            var svg = new Svg(TestHost.RequiredContext);
            int unspecified = View.MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified);
            svg.Measure(unspecified, unspecified);
            Assert.AreEqual(0, svg.MeasuredWidth);
            using Bitmap bitmap = _Render(svg);
            Assert.AreEqual(0, (int)_Pixel(bitmap, 5, 5).A);
        }

        [TestMethod]
        public void Svg_InflatesFromLayoutWithItsSourceAndStretch ()
        {
            View root = LayoutInflater.From(TestHost.RequiredContext)!.Inflate(Resource.Layout.svg_test, null)!;
            var svg = root.FindViewById<Svg>(Resource.Id.svg)!;
            Assert.IsNotNull(svg.Source, "The raw resource is read");
            Assert.AreEqual("test_square", svg.Source.Root.Id);
            Assert.AreEqual(ePathStretch.Uniform, svg.Stretch);
        }

        // ===========[ Helper Methods ]===================================
        private static SvgDocument _RedSquare () => SvgParsing.ParseText($@"<svg id=""doc"" {kSvgNamespace}><rect width=""10"" height=""10"" fill=""#FF0000""/></svg>")!;

        private static Bitmap _Render (View view)
        {
            view.Measure(
                View.MeasureSpec.MakeMeasureSpec(kWidth, MeasureSpecMode.Exactly),
                View.MeasureSpec.MakeMeasureSpec(kHeight, MeasureSpecMode.Exactly)
            );
            view.Layout(0, 0, kWidth, kHeight);

            Bitmap bitmap = Bitmap.CreateBitmap(kWidth, kHeight, Bitmap.Config.Argb8888!)!;
            using (var canvas = new Canvas(bitmap))
            {
                view.Draw(canvas);
            }

            return bitmap;
        }

        private static ColorArgb _Pixel (Bitmap bitmap, int x, int y) => new AndroidColor(bitmap.GetPixel(x, y)).ToColorArgb();
    }
}
