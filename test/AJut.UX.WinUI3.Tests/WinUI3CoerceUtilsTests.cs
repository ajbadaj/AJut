namespace AJut.UX.WinUI3.Tests
{
    using AJut.UX;
    using Microsoft.UI.Xaml;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Windows.UI;

    /// <summary>Covers the WinUI3 <see cref="CoerceUtils"/> on top of the shared CoerceParsing</summary>
    [TestClass]
    public class WinUI3CoerceUtilsTests
    {
        [TestMethod]
        public void CoerceUtils_WinUI3_NoCornerRadius_IsNull ()
        {
            Assert.IsNull(CoerceUtils.CoerceCornerRadiusFrom(null), "No value is no corner radius, the same as no thickness");
        }

        [TestMethod]
        public void CoerceUtils_WinUI3_ColorName_ReadsInAnyCase ()
        {
            Assert.IsTrue(CoerceUtils.TryGetColorFromString("Red", out Color red));
            Assert.AreEqual(new Color { A = 0xFF, R = 0xFF, G = 0x00, B = 0x00 }, red);
            Assert.IsTrue(CoerceUtils.TryGetColorFromString("red", out Color lowerRed));
            Assert.AreEqual(new Color { A = 0xFF, R = 0xFF, G = 0x00, B = 0x00 }, lowerRed);
            Assert.IsTrue(CoerceUtils.TryGetColorFromString("DARKBLUE", out Color darkBlue));
            Assert.AreEqual(new Color { A = 0xFF, R = 0x00, G = 0x00, B = 0x8B }, darkBlue);
            Assert.IsFalse(CoerceUtils.TryGetColorFromString("notacolor", out _));
        }
    }
}
