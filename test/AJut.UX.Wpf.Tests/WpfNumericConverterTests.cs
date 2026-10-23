namespace AJut.UX.Tests
{
    using System.Globalization;
    using System.Windows;
    using AJut.UX.Converters;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>Covers the WPF numeric converters, built on AJut.UX's <see cref="NumericConverterMath"/></summary>
    [TestClass]
    public class WpfNumericConverterTests
    {
        [TestMethod]
        public void NumericConverterMath_WpfComparisonConverter_GivesEachComparison ()
        {
            Assert.AreEqual(true, CompareWithWpf(NumericComparisonToBoolConverter.eComparison.Equal, 5, 5.0));
            Assert.AreEqual(false, CompareWithWpf(NumericComparisonToBoolConverter.eComparison.NotEqual, 5, 5.0));
            Assert.AreEqual(true, CompareWithWpf(NumericComparisonToBoolConverter.eComparison.GreaterThan, 6, 5.0));
            Assert.AreEqual(false, CompareWithWpf(NumericComparisonToBoolConverter.eComparison.LessThan, 6, 5.0));
            Assert.AreEqual(true, CompareWithWpf(NumericComparisonToBoolConverter.eComparison.GreaterThanOrEqual, 5, 5.0));
            Assert.AreEqual(false, CompareWithWpf(NumericComparisonToBoolConverter.eComparison.LessThanOrEqual, 6, 5.0));
        }

        [TestMethod]
        public void NumericConverterMath_WpfComparisonConverter_NaNIsOnlyNotEqual ()
        {
            Assert.AreEqual(true, CompareWithWpf(NumericComparisonToBoolConverter.eComparison.NotEqual, double.NaN, 5.0));
            Assert.AreEqual(false, CompareWithWpf(NumericComparisonToBoolConverter.eComparison.Equal, double.NaN, 5.0));
            Assert.AreEqual(false, CompareWithWpf(NumericComparisonToBoolConverter.eComparison.GreaterThanOrEqual, double.NaN, 5.0));
            Assert.AreEqual(false, CompareWithWpf(NumericComparisonToBoolConverter.eComparison.LessThanOrEqual, double.NaN, 5.0));
        }

        [TestMethod]
        public void NumericConverterMath_WpfComparisonConverter_AnIntegerAgainstAFractionKeepsTheFraction ()
        {
            Assert.AreEqual(true, CompareWithWpf(NumericComparisonToBoolConverter.eComparison.GreaterThan, 2, 1.5));
        }

        // A null value gives the false value for every comparison, "not equal" included
        [TestMethod]
        public void NumericConverterMath_WpfComparisonConverter_NullGivesTheFalseValue ()
        {
            Assert.AreEqual(false, CompareWithWpf(NumericComparisonToBoolConverter.eComparison.NotEqual, null, 1.0));
            Assert.AreEqual(false, CompareWithWpf(NumericComparisonToBoolConverter.eComparison.Equal, null, 1.0));
        }

        [TestMethod]
        public void NumericConverterMath_WpfVisibilityConverter_GivesItsVisibilities ()
        {
            var converter = new NumericComparisonToVisibilityConverter { Comp = NumericComparisonToVisibilityConverter.eComparison.GreaterThan, To = 0 };

            Assert.AreEqual(Visibility.Visible, converter.Convert(3, typeof(Visibility), null, CultureInfo.InvariantCulture));
            Assert.AreEqual(Visibility.Collapsed, converter.Convert(0, typeof(Visibility), null, CultureInfo.InvariantCulture));
        }

        [TestMethod]
        public void NumericConverterMath_WpfFixedArithmeticConverter_AppliesItsOperandInTheTargetType ()
        {
            var converter = new FixedArithmeticConverter { Operation = eArithmeticOperation.Multiply, FixedOperand = 2.5 };

            Assert.AreEqual(10.0, converter.Convert(4, typeof(double), null, CultureInfo.InvariantCulture));
            Assert.AreEqual(4, new FixedArithmeticConverter { Operation = eArithmeticOperation.Divide, FixedOperand = 0 }.Convert(4, typeof(int), null, CultureInfo.InvariantCulture));
        }

        [TestMethod]
        public void NumericConverterMath_WpfArithmeticConverter_AppliesInOrder ()
        {
            var converter = new ArithmeticConverter { Operation = eArithmeticOperation.Subtract };

            Assert.AreEqual(5.0, converter.Convert(new object[] { 10, 3, 2 }, typeof(double), null, CultureInfo.InvariantCulture));
        }

        // ===========[ Helpers ]===============================================
        private static object CompareWithWpf (NumericComparisonToBoolConverter.eComparison comparison, object value, double to)
        {
            var converter = new NumericComparisonToBoolConverter { Comp = comparison, To = to };
            return converter.Convert(value, typeof(bool), null, CultureInfo.InvariantCulture);
        }
    }
}
