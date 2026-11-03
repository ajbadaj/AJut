namespace AJut.UX.Tests
{
    using System;
    using AJut.UX;
    using AJut.UX.Controls;
    using AJut.UX.Helpers;
    using CoreGraphics;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using UIKit;

    [TestClass]
    public class SvgTests
    {
        private const string kSvgNamespace = "xmlns=\"http://www.w3.org/2000/svg\"";

        [TestMethod]
        public void IntrinsicContentSize_Unstretched_IsFromTheOriginToTheFarEdge ()
        {
            var svg = new Svg { Source = _Rect(x: 5, y: 5, width: 10, height: 20) };
            Assert.AreEqual(15.0, (double)svg.IntrinsicContentSize.Width, 0.001);
            Assert.AreEqual(25.0, (double)svg.IntrinsicContentSize.Height, 0.001);
        }

        [TestMethod]
        public void IntrinsicContentSize_Stretched_IsWhatItDraws ()
        {
            var svg = new Svg { Source = _Rect(x: 5, y: 5, width: 10, height: 20), Stretch = ePathStretch.Uniform };
            Assert.AreEqual(10.0, (double)svg.IntrinsicContentSize.Width, 0.001);
            Assert.AreEqual(20.0, (double)svg.IntrinsicContentSize.Height, 0.001);
        }

        [TestMethod]
        public void NoSource_HasNoSize ()
        {
            var svg = new Svg();
            Assert.AreEqual(UIView.NoIntrinsicMetric, svg.IntrinsicContentSize.Width);
            Assert.AreEqual(CGSize.Empty, svg.SizeThatFits(new CGSize(100, 100)));
        }

        [TestMethod]
        public void Draw_Fill_FitsTheDrawingToTheView ()
        {
            var svg = new Svg { Source = _Rect(x: 0, y: 0, width: 10, height: 10), Stretch = ePathStretch.Fill, Frame = new CGRect(0, 0, 100, 100) };
            byte[] pixels = _Render(svg, 100, 100);
            Assert.AreEqual(255, _Alpha(pixels, 100, 50, 50), "The middle of the view is the square's fill");
            Assert.AreEqual(255, _Red(pixels, 100, 50, 50));
        }

        [TestMethod]
        public void Draw_Unstretched_DrawsAtItsOwnCoordinatesInPoints ()
        {
            var svg = new Svg { Source = _Rect(x: 0, y: 0, width: 10, height: 10), Frame = new CGRect(0, 0, 100, 100) };
            byte[] pixels = _Render(svg, 100, 100);
            Assert.AreEqual(255, _Alpha(pixels, 100, 5, 5), "Inside the square");
            Assert.AreEqual(0, _Alpha(pixels, 100, 50, 50), "Past it is clear");
        }

        // ===========[ Helper Methods ]===================================
        private static SvgDocument _Rect (double x, double y, double width, double height)
        {
            return SvgParsing.ParseText(FormattableString.Invariant($@"<svg id=""doc"" {kSvgNamespace}><rect x=""{x}"" y=""{y}"" width=""{width}"" height=""{height}"" fill=""#FF0000""/></svg>"));
        }

        // Draws the view into an RGBA bitmap a point to a pixel, flipped so the bitmap's first row is the view's top, as
        //  UIKit draws
        private static byte[] _Render (UIView view, int width, int height)
        {
            var pixels = new byte[width * height * 4];
            using CGColorSpace colorSpace = CGColorSpace.CreateDeviceRGB();
            using var context = new CGBitmapContext(pixels, width, height, 8, width * 4, colorSpace, CGImageAlphaInfo.PremultipliedLast);
            context.TranslateCTM(0, height);
            context.ScaleCTM(1, -1);
            UIGraphics.PushContext(context);
            try
            {
                view.Draw(new CGRect(0, 0, width, height));
            }
            finally
            {
                UIGraphics.PopContext();
            }

            return pixels;
        }

        private static int _Red (byte[] pixels, int width, int x, int y) => pixels[((y * width) + x) * 4];
        private static int _Alpha (byte[] pixels, int width, int x, int y) => pixels[(((y * width) + x) * 4) + 3];
    }
}
