namespace AJut.UX.Tests
{
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Covers <see cref="CornerRadiusBuilder"/> and <see cref="ThicknessBuilder"/>, the multi bindings that bind some parts
    /// of a corner radius or thickness and take the rest from a baseline. A binding that has not resolved (an ancestor it
    /// looks for that is not there yet) hands its part UnsetValue rather than a number.
    /// </summary>
    [TestClass]
    public class WpfBindingBuilderTests
    {
        [TestMethod]
        public void CornerRadiusBuilder_AnUnresolvedPart_KeepsTheBaseline_AndTheOthersTheirOwnValues ()
        {
            var builder = new CornerRadiusBuilder { BindingParts = eCornerPart.TopLeft | eCornerPart.TopRight, Baseline = new CornerRadius(2) };
            object result = Convert(builder, DependencyProperty.UnsetValue, 5.0);
            Assert.AreEqual(new CornerRadius(2, 5, 2, 2), result, "Top left has no value of its own, and top right takes the one bound to it");
        }

        [TestMethod]
        public void CornerRadiusBuilder_NoPartResolved_IsTheBaseline ()
        {
            var builder = new CornerRadiusBuilder { BindingParts = eCornerPart.TopLeft | eCornerPart.TopRight, Baseline = new CornerRadius(2) };
            Assert.AreEqual(new CornerRadius(2), Convert(builder, DependencyProperty.UnsetValue, DependencyProperty.UnsetValue));
        }

        [TestMethod]
        public void CornerRadiusBuilder_EveryPartResolved_EachTakesItsOwnValue ()
        {
            var builder = new CornerRadiusBuilder { BindingParts = eCornerPart.TopLeft | eCornerPart.BottomLeft, Baseline = new CornerRadius(2) };
            Assert.AreEqual(new CornerRadius(3, 2, 2, 4), Convert(builder, 3.0, 4.0));
        }

        [TestMethod]
        public void ThicknessBuilder_EveryPartResolved_EachTakesItsOwnValue ()
        {
            var builder = new ThicknessBuilder { BindingParts = eThicknessPart.Top | eThicknessPart.Right, Baseline = new Thickness(1) };
            Assert.AreEqual(new Thickness(1, 3, 4, 1), Convert(builder, 3.0, 4.0));
        }

        [TestMethod]
        public void ThicknessBuilder_AnUnresolvedPart_KeepsTheBaseline_AndTheOthersTheirOwnValues ()
        {
            var builder = new ThicknessBuilder { BindingParts = eThicknessPart.Left | eThicknessPart.Bottom, Baseline = new Thickness(1) };
            object result = Convert(builder, DependencyProperty.UnsetValue, 4.0);
            Assert.AreEqual(new Thickness(1, 1, 1, 4), result, "Left has no value of its own, and bottom takes the one bound to it");
        }

        // ===========[ Helper Methods ]===================================
        private static object Convert (MultiBinding builder, params object[] values)
        {
            return builder.Converter.Convert(values, typeof(object), builder.ConverterParameter, CultureInfo.InvariantCulture);
        }
    }
}
