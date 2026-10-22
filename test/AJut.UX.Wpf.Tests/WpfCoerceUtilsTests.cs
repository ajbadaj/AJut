namespace AJut.UX.Tests
{
    using System.Windows;
    using AJut.UX;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>Covers the WPF CoerceUtils, built on AJut.UX's <see cref="CoerceParsing"/></summary>
    [TestClass]
    public class WpfCoerceUtilsTests
    {
        [TestMethod]
        public void CoerceUtils_Wpf_NoCornerRadius_IsNull ()
        {
            Assert.IsNull(CoerceUtils.CoerceCornerRadiusFrom(null), "No value is no corner radius, the same as no thickness");
        }

        [TestMethod]
        public void CoerceUtils_Wpf_CornerRadiusAndThickness_ComeFromTheSharedParse ()
        {
            Assert.AreEqual(new CornerRadius(1, 1, 2, 2), CoerceUtils.CoerceCornerRadiusFrom("1,2"));
            Assert.AreEqual(new Thickness(1, 2, 1, 2), CoerceUtils.CoerceThicknessFrom("1,2"));
            Assert.AreEqual(new CornerRadius(5), CoerceUtils.CoerceCornerRadiusFrom(new CornerRadius(5)));
            Assert.IsNull(CoerceUtils.CoerceThicknessFrom(null));
        }
    }
}
