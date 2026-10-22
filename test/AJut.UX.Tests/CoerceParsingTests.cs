namespace AJut.UX.Tests
{
    using System;
    using System.Globalization;
    using AJut.UX;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Covers <see cref="CoerceParsing"/>, the toolkit-free half of every UX package's CoerceUtils. The WPF CoerceUtils built
    /// on it is covered in AJut.UX.Wpf.Tests.
    /// </summary>
    [TestClass]
    public class CoerceParsingTests
    {
        private enum eSample { First, Second }

        // ===========[ Corners and sides ]===================================

        [TestMethod]
        public void CoerceParsing_Corners_OneIsUniform_TwoAreTopThenBottom_FourAsGiven ()
        {
            Assert.AreEqual(new CoerceParsing.Corners(3, 3, 3, 3), CoerceParsing.ParseCorners("3"));
            Assert.AreEqual(new CoerceParsing.Corners(1, 1, 2, 2), CoerceParsing.ParseCorners("1,2"));
            Assert.AreEqual(new CoerceParsing.Corners(1, 2, 3, 4), CoerceParsing.ParseCorners("1 2 3 4"));
        }

        [TestMethod]
        public void CoerceParsing_Sides_OneIsUniform_TwoAreHorizontalThenVertical_FourAsGiven ()
        {
            Assert.AreEqual(new CoerceParsing.Sides(3, 3, 3, 3), CoerceParsing.ParseSides("3"));
            Assert.AreEqual(new CoerceParsing.Sides(1, 2, 1, 2), CoerceParsing.ParseSides("1, 2"));
            Assert.AreEqual(new CoerceParsing.Sides(1, 2, 3, 4), CoerceParsing.ParseSides("1,2,3,4"));
        }

        [TestMethod]
        public void CoerceParsing_ThreeValues_IsAFormatErrorExplainingTheCounts ()
        {
            var error = Assert.ThrowsException<FormatException>(() => CoerceParsing.ParseSides("1,2,3"));
            Assert.IsInstanceOfType(error.InnerException, typeof(InvalidOperationException));
        }

        [TestMethod]
        public void CoerceParsing_NotANumber_IsAFormatError ()
        {
            Assert.ThrowsException<FormatException>(() => CoerceParsing.ParseCorners("1,x"));
        }

        [TestMethod]
        public void CoerceParsing_Numbers_ReadTheSameInEveryCulture ()
        {
            // XAML and code write these with a period, whatever culture the app happens to run in
            CultureInfo before = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                Assert.AreEqual(new CoerceParsing.Sides(1.5, 2.5, 1.5, 2.5), CoerceParsing.ParseSides("1.5,2.5"));
            }
            finally
            {
                CultureInfo.CurrentCulture = before;
            }
        }

        // ===========[ Enums ]===================================
        [TestMethod]
        public void CoerceParsing_Enum_TypedOrNamed_ElseDefault ()
        {
            Assert.AreEqual(eSample.Second, CoerceParsing.CoerceEnumFrom<eSample>(eSample.Second));
            Assert.AreEqual(eSample.Second, CoerceParsing.CoerceEnumFrom<eSample>("Second"));
            Assert.AreEqual(eSample.First, CoerceParsing.CoerceEnumFrom<eSample>(42));
        }

        // ===========[ Uris ]===================================

        [TestMethod]
        public void CoerceParsing_AbsoluteUri_IsTaken_ARelativeOneHandsBackItsText ()
        {
            var absolute = new Uri("https://example.com/a.png");
            Assert.AreSame(absolute, CoerceParsing.CoerceAbsoluteUri(absolute, out _));
            Assert.AreEqual(absolute, CoerceParsing.CoerceAbsoluteUri("https://example.com/a.png", out _));

            Assert.IsNull(CoerceParsing.CoerceAbsoluteUri("images/a.png", out string text));
            Assert.AreEqual("images/a.png", text);

            Assert.IsNull(CoerceParsing.CoerceAbsoluteUri(null, out string noText));
            Assert.IsNull(noText);
        }
    }
}
