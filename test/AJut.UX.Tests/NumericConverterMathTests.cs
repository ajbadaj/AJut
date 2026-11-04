namespace AJut.UX.Tests
{
    using AJut.UX.Converters;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public enum eIntBackedFixture { Zero, One, Two }
    public enum eByteBackedFixture : byte { Zero, One, Two }
    public enum eLongBackedFixture : long { Zero, One, Two }

    [TestClass]
    public class NumericConverterMathTests
    {
        // ===========[ Arithmetic ]============================================
        [TestMethod]
        public void NumericConverterMath_Apply_DoesEachOperation ()
        {
            Assert.AreEqual(7.0, NumericConverterMath.Apply(eArithmeticOperation.Add, 5.0, 2.0));
            Assert.AreEqual(3.0, NumericConverterMath.Apply(eArithmeticOperation.Subtract, 5.0, 2.0));
            Assert.AreEqual(10.0, NumericConverterMath.Apply(eArithmeticOperation.Multiply, 5.0, 2.0));
            Assert.AreEqual(2.5, NumericConverterMath.Apply(eArithmeticOperation.Divide, 5.0, 2.0));
        }

        [TestMethod]
        public void NumericConverterMath_Apply_DividingByZeroLeavesTheValue ()
        {
            Assert.AreEqual(5.0, NumericConverterMath.Apply(eArithmeticOperation.Divide, 5.0, 0.0));
        }

        // ===========[ Comparison ]============================================
        [TestMethod]
        public void NumericConverterMath_TryRelate_ComparesEachNumericType ()
        {
            AssertRelation(3, 5.0, isLess: true, isEqual: false, isGreater: false);
            AssertRelation(5, 5.0, isLess: false, isEqual: true, isGreater: false);
            AssertRelation(7L, 5.0, isLess: false, isEqual: false, isGreater: true);
            AssertRelation((short)5, 5.0, isLess: false, isEqual: true, isGreater: false);
            AssertRelation((byte)4, 5.0, isLess: true, isEqual: false, isGreater: false);
            AssertRelation(4.5, 5.0, isLess: true, isEqual: false, isGreater: false);
            AssertRelation(5.5f, 5.5, isLess: false, isEqual: true, isGreater: false);
            AssertRelation(6.25m, 5.0, isLess: false, isEqual: false, isGreater: true);
        }

        [TestMethod]
        public void NumericConverterMath_TryRelate_NaNIsNeitherLessEqualNorGreater ()
        {
            AssertRelation(double.NaN, 5.0, isLess: false, isEqual: false, isGreater: false);
            AssertRelation(5.0, double.NaN, isLess: false, isEqual: false, isGreater: false);
            AssertRelation(float.NaN, 5.0, isLess: false, isEqual: false, isGreater: false);
        }

        [TestMethod]
        public void NumericConverterMath_TryRelate_AnEnumComparesByItsNumber ()
        {
            AssertRelation(eIntBackedFixture.One, 1.0, isLess: false, isEqual: true, isGreater: false);
            AssertRelation(eIntBackedFixture.Two, 1.0, isLess: false, isEqual: false, isGreater: true);
        }

        // An integer compared with a fractional number compares as the fraction it is, not rounded to an integer first
        [TestMethod]
        public void NumericConverterMath_TryRelate_AnIntegerAgainstAFractionKeepsTheFraction ()
        {
            AssertRelation(2, 1.5, isLess: false, isEqual: false, isGreater: true);
            AssertRelation(2, 2.5, isLess: true, isEqual: false, isGreater: false);
            AssertRelation(2L, 2.5, isLess: true, isEqual: false, isGreater: false);
        }

        [TestMethod]
        public void NumericConverterMath_TryRelate_NullHasNothingToCompare ()
        {
            Assert.IsFalse(NumericConverterMath.TryRelate(null, 1.0, out _));
        }

        [TestMethod]
        public void NumericConverterMath_TryRelate_AnEnumOfAnyBackingTypeComparesByItsNumber ()
        {
            AssertRelation(eByteBackedFixture.Two, 2.0, isLess: false, isEqual: true, isGreater: false);
            AssertRelation(eLongBackedFixture.One, 2.0, isLess: true, isEqual: false, isGreater: false);
        }

        // ===========[ Helpers ]===============================================
        private static void AssertRelation (object value, double to, bool isLess, bool isEqual, bool isGreater)
        {
            Assert.IsTrue(NumericConverterMath.TryRelate(value, to, out NumericConverterMath.Relation relation), $"{value} against {to} has nothing to compare");
            Assert.AreEqual(new NumericConverterMath.Relation(isLess, isEqual, isGreater), relation, $"{value} against {to}");
        }
    }
}
