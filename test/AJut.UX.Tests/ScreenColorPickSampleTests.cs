namespace AJut.UX.Tests
{
    using System;
    using AJut.UX.Helpers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ScreenColorPickSampleTests
    {
        private static readonly ScreenPixelRect kMonitor = new ScreenPixelRect(0, 0, 1920, 1040);

        // ===[ Placing the magnifier ]===

        [TestMethod]
        public void SCPS_Magnifier_SitsAboveTheCursor_Centered ()
        {
            ScreenColorPickSample sample = SampleAt(new ScreenPixelPoint(960, 600), kMonitor, 1.0);

            ScreenPixelPoint at = sample.PlaceMagnifier(200, 100);

            Assert.AreEqual(860, at.X);
            Assert.IsTrue(at.Y + 100 < 600, "The magnifier should be wholly above the cursor");
            Assert.IsTrue(at.Y >= kMonitor.Top);
        }

        [TestMethod]
        public void SCPS_Magnifier_GoesBelow_WhenTheCursorIsHigh ()
        {
            ScreenColorPickSample sample = SampleAt(new ScreenPixelPoint(960, 30), kMonitor, 1.0);

            ScreenPixelPoint at = sample.PlaceMagnifier(200, 100);

            Assert.IsTrue(at.Y > 30, "The magnifier should be wholly below the cursor");
            Assert.IsTrue(at.Y + 100 <= kMonitor.Bottom);
        }

        [TestMethod]
        public void SCPS_Magnifier_IsPushedSideways_ToStayOnTheMonitor ()
        {
            ScreenColorPickSample nearRight = SampleAt(new ScreenPixelPoint(1910, 600), kMonitor, 1.0);
            ScreenColorPickSample nearLeft = SampleAt(new ScreenPixelPoint(5, 600), kMonitor, 1.0);

            Assert.AreEqual(1720, nearRight.PlaceMagnifier(200, 100).X);
            Assert.AreEqual(0, nearLeft.PlaceMagnifier(200, 100).X);
        }

        [TestMethod]
        public void SCPS_Magnifier_StaysOnAMonitorLeftOfThePrimary ()
        {
            // A second monitor to the left of the primary has negative coordinates
            var leftMonitor = new ScreenPixelRect(-2560, -200, 2560, 1400);
            ScreenColorPickSample sample = SampleAt(new ScreenPixelPoint(-5, 500), leftMonitor, 1.5);

            ScreenPixelPoint at = sample.PlaceMagnifier(300, 150);

            Assert.AreEqual(-300, at.X);
            Assert.IsTrue(at.Y + 150 < 500);
        }

        [TestMethod]
        public void SCPS_Magnifier_GapGrowsWithTheMonitorScale ()
        {
            var cursor = new ScreenPixelPoint(960, 600);

            int gapAtOne = 600 - (SampleAt(cursor, kMonitor, 1.0).PlaceMagnifier(200, 100).Y + 100);
            int gapAtTwo = 600 - (SampleAt(cursor, kMonitor, 2.0).PlaceMagnifier(200, 100).Y + 100);

            Assert.AreEqual(gapAtOne * 2, gapAtTwo);
        }

        // ===[ The pixels ]===

        [TestMethod]
        public void SCPS_EvenPixelCounts_RoundUpToOdd ()
        {
            Assert.AreEqual(25, new ScreenColorPickOptions { MagnifierPixelCount = 24 }.OddMagnifierPixelCount);
            Assert.AreEqual(25, new ScreenColorPickOptions { MagnifierPixelCount = 25 }.OddMagnifierPixelCount);
            Assert.AreEqual(1, new ScreenColorPickOptions { MagnifierPixelCount = 0 }.OddMagnifierPixelCount);
        }

        [TestMethod]
        public void SCPS_TheColor_IsTheCenterPixel ()
        {
            var pixels = new ColorArgb[9];
            pixels[4] = ColorArgb.FromRgb(0x00, 0xFF, 0x21);

            var sample = new ScreenColorPickSample(pixels, 3, new ScreenPixelPoint(10, 10), kMonitor, 1.0, isPressing: false);

            Assert.AreEqual(ColorArgb.FromRgb(0x00, 0xFF, 0x21), sample.Color);
            Assert.AreEqual("#00FF21", sample.Hex);
        }

        [TestMethod]
        public void SCPS_WriteBgra_BlowsEachPixelUpIntoABlock ()
        {
            var pixels = new ColorArgb[9];
            pixels[5] = ColorArgb.FromRgb(0x30, 0x20, 0x10); // row 1, column 2
            var sample = new ScreenColorPickSample(pixels, 3, new ScreenPixelPoint(10, 10), kMonitor, 1.0, isPressing: false);

            const int kCell = 2;
            const int kSide = 3 * kCell;
            var bgra = new byte[kSide * kSide * BgraPixels.kBytesPerPixel];
            sample.WriteBgra(bgra, kCell);

            // Every pixel of the block at row 1, column 2 (x 4 to 5, y 2 to 3) is that color, and its neighbor is not
            foreach (ScreenPixelPoint point in new[] { new ScreenPixelPoint(4, 2), new ScreenPixelPoint(5, 2), new ScreenPixelPoint(4, 3), new ScreenPixelPoint(5, 3) })
            {
                int index = ((point.Y * kSide) + point.X) * BgraPixels.kBytesPerPixel;
                CollectionAssert.AreEqual(new byte[] { 0x10, 0x20, 0x30, 0xFF }, bgra[index..(index + 4)], $"Pixel {point.X}, {point.Y}");
            }

            int neighbor = ((2 * kSide) + 3) * BgraPixels.kBytesPerPixel;
            CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0 }, bgra[neighbor..(neighbor + 4)]);
        }

        [TestMethod]
        public void SCPS_AnEvenSquare_IsRefused ()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ScreenColorPickSample(new ColorArgb[16], 4, default, kMonitor, 1.0, isPressing: false));
        }

        private static ScreenColorPickSample SampleAt (ScreenPixelPoint cursor, ScreenPixelRect monitor, double scale)
        {
            return new ScreenColorPickSample(new ColorArgb[1], 1, cursor, monitor, scale, isPressing: false);
        }
    }
}
