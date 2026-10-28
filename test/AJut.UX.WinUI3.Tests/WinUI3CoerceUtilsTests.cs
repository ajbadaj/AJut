namespace AJut.UX.WinUI3.Tests
{
    using AJut.UX;
    using Microsoft.UI.Xaml;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>Covers the WinUI3 <see cref="CoerceUtils"/> on top of the shared CoerceParsing</summary>
    [TestClass]
    public class WinUI3CoerceUtilsTests
    {
        [TestMethod]
        public void CoerceUtils_WinUI3_NoCornerRadius_IsNull ()
        {
            Assert.IsNull(CoerceUtils.CoerceCornerRadiusFrom(null), "No value is no corner radius, the same as no thickness");
        }
    }
}
