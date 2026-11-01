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
    public class GeometryButtonTests
    {
        private const int kWidth = 200;
        private const int kHeight = 100;

        // A right triangle with its right angle at the top left. Fitted with Fill into 200 x 100 it covers the top
        // left half of the view, split by the diagonal from (200, 0) to (0, 100).
        private const string kTopLeftTriangle = "M 0,0 L 10,0 0,10 Z";

        [TestMethod]
        public void Geometry_FillsInsideTheShapeAndNothingOutside ()
        {
            var button = new GeometryButton(TestHost.RequiredContext)
            {
                BackgroundGeometry = kTopLeftTriangle,
                GeometryStretch = ePathStretch.Fill,
            };

            using Bitmap bitmap = _Render(button);
            Assert.AreNotEqual(0, (int)_Pixel(bitmap, 20, 10).A, "Inside the triangle, near the right angle, is painted");
            Assert.AreEqual(0, (int)_Pixel(bitmap, 190, 90).A, "Outside the triangle, near the far corner, is clear");
        }

        [TestMethod]
        public void Geometry_UnreadableMarkupDrawsNothing ()
        {
            var button = new GeometryButton(TestHost.RequiredContext) { BackgroundGeometry = "M 0,0 L 10" };
            using Bitmap bitmap = _Render(button);
            Assert.AreEqual(0, (int)_Pixel(bitmap, 20, 10).A);
            Assert.AreEqual("M 0,0 L 10", button.BackgroundGeometry, "The markup is kept as given");
        }

        [TestMethod]
        public void Geometry_WrapContentMeasuresToTheGeometryInDp ()
        {
            // An unsized GeometryButton is the size of its geometry, as on WinUI3, where geometry units are device
            // independent, the same as dp
            var button = new GeometryButton(TestHost.RequiredContext) { BackgroundGeometry = "M 0,0 L 150,0 150,100 0,100 Z" };
            float density = TestHost.RequiredContext.Resources!.DisplayMetrics!.Density;
            int unspecified = View.MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified);

            button.Measure(unspecified, unspecified);
            Assert.AreEqual(150 * density, button.MeasuredWidth, 1.0, "Width is the geometry's");
            Assert.AreEqual(100 * density, button.MeasuredHeight, 1.0, "Height is the geometry's");

            button.Measure(
                View.MeasureSpec.MakeMeasureSpec(kWidth, MeasureSpecMode.Exactly),
                View.MeasureSpec.MakeMeasureSpec(kHeight, MeasureSpecMode.Exactly)
            );
            Assert.AreEqual(kWidth, button.MeasuredWidth, "An exact size is kept");
            Assert.AreEqual(kHeight, button.MeasuredHeight, "An exact size is kept");
        }

        [TestMethod]
        public void Geometry_StretchNoneDrawsTheGeometryInDp ()
        {
            // A 10 x 10 square, unstretched, covers 10dp, not 10 pixels
            var button = new GeometryButton(TestHost.RequiredContext)
            {
                BackgroundGeometry = "M 0,0 L 10,0 10,10 0,10 Z",
                GeometryStretch = ePathStretch.None,
            };
            float density = TestHost.RequiredContext.Resources!.DisplayMetrics!.Density;
            int insideInDpOnly = (int)(10 * density) - 2;
            Assert.IsTrue(insideInDpOnly > 10, "The device's density is high enough to tell dp from pixels");

            using Bitmap bitmap = _Render(button);
            Assert.AreNotEqual(0, (int)_Pixel(bitmap, insideInDpOnly, insideInDpOnly).A, "Inside the square, measured in dp, is painted");
            Assert.AreEqual(0, (int)_Pixel(bitmap, insideInDpOnly + 4, insideInDpOnly + 4).A, "Past the square is clear");
        }

        [TestMethod]
        public void Geometry_InflatesFromLayoutWithItsAttributes ()
        {
            View root = LayoutInflater.From(TestHost.RequiredContext)!.Inflate(Resource.Layout.geometry_button_test, null)!;
            var button = root.FindViewById<GeometryButton>(Resource.Id.geometry_button)!;
            Assert.AreEqual(kTopLeftTriangle, button.BackgroundGeometry);
            Assert.AreEqual(ePathStretch.Uniform, button.GeometryStretch);
            Assert.AreEqual(4f, button.TraceThickness);
        }

        // ===========[ Helper Methods ]===================================
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
