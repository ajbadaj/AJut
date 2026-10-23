namespace AJut.Core.UnitTests
{
    using System;
    using AJut.MathUtilities;
    using AJut.TypeManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    // These pin what NumericConversion, Calculate, Lerp and Cap return today, results and result types both, so their
    // move from dynamic to typed math can be checked to change nothing. Each result is taken as var, object or the
    // type it already comes back as, so the same tests compile against either form of the API.
    [TestClass]
    public class NumericMathPinningTests
    {
        // ===[ NumericConversion: supported types and bounds ]===

        [TestMethod]
        public void NMP_IsSupportedNumericType_TheElevenNumericTypesOnly ()
        {
            foreach (Type type in kNumericTypes)
            {
                Assert.IsTrue(NumericConversion.IsSupportedNumericType(type), type.Name);
            }

            Assert.IsFalse(NumericConversion.IsSupportedNumericType(typeof(string)));
            Assert.IsFalse(NumericConversion.IsSupportedNumericType(typeof(char)));
            Assert.IsFalse(NumericConversion.IsSupportedNumericType(typeof(bool)));
        }

        [TestMethod]
        public void NMP_MinFor_MaxFor_Generic_AreTheTypesOwnBounds ()
        {
            Assert.AreEqual(byte.MinValue, NumericConversion.MinFor<byte>());
            Assert.AreEqual(byte.MaxValue, NumericConversion.MaxFor<byte>());
            Assert.AreEqual(int.MinValue, NumericConversion.MinFor<int>());
            Assert.AreEqual(int.MaxValue, NumericConversion.MaxFor<int>());
            Assert.AreEqual(ulong.MaxValue, NumericConversion.MaxFor<ulong>());
            Assert.AreEqual(float.MinValue, NumericConversion.MinFor<float>());
            Assert.AreEqual(decimal.MaxValue, NumericConversion.MaxFor<decimal>());
        }

        [TestMethod]
        public void NMP_MinFor_MaxFor_ByType_AreBoxedInTheirOwnType ()
        {
            foreach (Type type in kNumericTypes)
            {
                object min = NumericConversion.MinFor(type);
                object max = NumericConversion.MaxFor(type);
                Assert.AreEqual(type, min.GetType(), type.Name);
                Assert.AreEqual(type, max.GetType(), type.Name);
            }

            Assert.AreEqual((object)short.MinValue, (object)NumericConversion.MinFor(typeof(short)));
            Assert.AreEqual((object)double.MaxValue, (object)NumericConversion.MaxFor(typeof(double)));
        }

        // ===[ NumericConversion: PerformSafeNumericCastToTarget ]===

        [TestMethod]
        public void NMP_SafeCast_SameType_ComesBackUnchanged ()
        {
            object result = NumericConversion.PerformSafeNumericCastToTarget(42, typeof(int), out bool capMin, out bool capMax);
            Assert.AreEqual((object)42, result);
            Assert.IsFalse(capMin);
            Assert.IsFalse(capMax);
        }

        [DataTestMethod]
        [DataRow(300, typeof(byte), (byte)255, false, true)]
        [DataRow(-5, typeof(byte), (byte)0, true, false)]
        [DataRow(-1, typeof(uint), 0u, true, false)]
        [DataRow(-1, typeof(ushort), (ushort)0, true, false)]
        [DataRow(70000, typeof(ushort), (ushort)65535, false, true)]
        [DataRow(5, typeof(long), 5L, false, false)]
        [DataRow(-5, typeof(long), -5L, false, false)]
        public void NMP_SafeCast_IntSource_CapsToTheTargetsBounds (int value, Type target, object expected, bool expectCapMin, bool expectCapMax)
        {
            object result = NumericConversion.PerformSafeNumericCastToTarget(value, target, out bool capMin, out bool capMax);
            Assert.AreEqual(expected, result);
            Assert.AreEqual(target, result.GetType());
            Assert.AreEqual(expectCapMin, capMin);
            Assert.AreEqual(expectCapMax, capMax);
        }

        [DataTestMethod]
        [DataRow(2.5, typeof(int), 2)]
        [DataRow(3.5, typeof(int), 4)]
        [DataRow(-2.5, typeof(int), -2)]
        [DataRow(2.4, typeof(long), 2L)]
        [DataRow(0.5, typeof(byte), (byte)0)]
        [DataRow(1.5, typeof(byte), (byte)2)]
        public void NMP_SafeCast_DoubleToInteger_RoundsHalfToEven (double value, Type target, object expected)
        {
            object result = NumericConversion.PerformSafeNumericCastToTarget(value, target, out _, out _);
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void NMP_SafeCast_DoubleBeyondLong_CapsAtLongMax ()
        {
            // This pin used to record a throw, a defect: the value was converted to the largest integer container before
            // it was capped, and a double past long's range does not fit that container. NumericConversionRangeTests
            // covers every integer type.
            Assert.AreEqual((object)long.MaxValue, NumericConversion.PerformSafeNumericCastToTarget(1e30, typeof(long), out _, out bool capMax));
            Assert.IsTrue(capMax);
        }

        [TestMethod]
        public void NMP_SafeCast_HugeDouble_CapsAtFloatMax ()
        {
            Assert.AreEqual((object)float.MaxValue, (object)NumericConversion.PerformSafeNumericCastToTarget(1e300, typeof(float), out bool floatCapMin, out bool floatCapMax));
            Assert.IsFalse(floatCapMin);
            Assert.IsTrue(floatCapMax);

            Assert.AreEqual((object)float.MinValue, (object)NumericConversion.PerformSafeNumericCastToTarget(-1e300, typeof(float), out bool lowCapMin, out _));
            Assert.IsTrue(lowCapMin);
        }

        [TestMethod]
        public void NMP_SafeCast_LargeUnsignedSource_CapsAtIntMax ()
        {
            object result = NumericConversion.PerformSafeNumericCastToTarget(10_000_000_000ul, typeof(int), out bool capMin, out bool capMax);
            Assert.AreEqual((object)int.MaxValue, result);
            Assert.IsFalse(capMin);
            Assert.IsTrue(capMax);
        }

        [TestMethod]
        public void NMP_SafeCast_BetweenFloatingPointTypes ()
        {
            Assert.AreEqual((object)1.25m, (object)NumericConversion.PerformSafeNumericCastToTarget(1.25, typeof(decimal), out _, out _));
            Assert.AreEqual((object)2.5, (object)NumericConversion.PerformSafeNumericCastToTarget(2.5m, typeof(double), out _, out _));
            Assert.AreEqual((object)0.5f, (object)NumericConversion.PerformSafeNumericCastToTarget(0.5, typeof(float), out _, out _));
            Assert.AreEqual((object)7.0, (object)NumericConversion.PerformSafeNumericCastToTarget(7, typeof(double), out _, out _));
        }

        // ===[ NumericConversion: PerformNumericTypeSafeCapping ]===

        [TestMethod]
        public void NMP_Capping_NullBounds_AreTheTypesOwnBounds ()
        {
            Assert.AreEqual(255, NumericConversion.PerformNumericTypeSafeCapping<byte>(1000, null, null, out bool capMin, out bool capMax));
            Assert.IsFalse(capMin);
            Assert.IsTrue(capMax);

            Assert.AreEqual(-7, NumericConversion.PerformNumericTypeSafeCapping<int>(-7, null, null, out capMin, out capMax));
            Assert.IsFalse(capMin);
            Assert.IsFalse(capMax);
        }

        [TestMethod]
        public void NMP_Capping_MixedTypeBounds_CapInTheTargetType ()
        {
            Assert.AreEqual(10f, NumericConversion.PerformNumericTypeSafeCapping<float>(12.5, 0, 10.0, out bool capMin, out bool capMax));
            Assert.IsFalse(capMin);
            Assert.IsTrue(capMax);

            Assert.AreEqual(3, NumericConversion.PerformNumericTypeSafeCapping<int>(1, 3.0, 9L, out capMin, out capMax));
            Assert.IsTrue(capMin);
            Assert.IsFalse(capMax);

            Assert.AreEqual(4.5m, NumericConversion.PerformNumericTypeSafeCapping<decimal>(4.5m, 0, 10, out capMin, out capMax));
            Assert.IsFalse(capMin);
            Assert.IsFalse(capMax);
        }

        // ===[ NumericConversion: TryParseString ]===

        [TestMethod]
        public void NMP_TryParse_Generic ()
        {
            Assert.IsTrue(NumericConversion.TryParseString("42", out int parsedInt));
            Assert.AreEqual(42, parsedInt);
            Assert.IsTrue(NumericConversion.TryParseString("1.5", out float parsedFloat));
            Assert.AreEqual(1.5f, parsedFloat);
            Assert.IsTrue(NumericConversion.TryParseString("x12y", out byte parsedByte));
            Assert.AreEqual((byte)12, parsedByte);
            Assert.IsFalse(NumericConversion.TryParseString("abc", out int _));
            Assert.IsFalse(NumericConversion.TryParseString("", out double _));
        }

        [TestMethod]
        public void NMP_TryParse_ByType_ComesBackInThatType ()
        {
            Assert.IsTrue(NumericConversion.TryParseString("42", typeof(long), out var parsedLong));
            Assert.AreEqual((object)42L, (object)parsedLong);

            Assert.IsTrue(NumericConversion.TryParseString("2.25", typeof(decimal), out var parsedDecimal));
            Assert.AreEqual((object)2.25m, (object)parsedDecimal);

            Assert.IsTrue(NumericConversion.TryParseString("value: -3.5 units", typeof(double), out var parsedDouble));
            Assert.AreEqual((object)(-3.5), (object)parsedDouble);

            Assert.IsFalse(NumericConversion.TryParseString("nothing", typeof(int), out var notParsed));
            Assert.IsNull((object)notParsed);
        }

        // ===[ Calculate ]===

        [TestMethod]
        public void NMP_Calculate_Mean_KeepsTheElementType ()
        {
            int intMean = Calculate.Mean(new[] { 1, 2 });
            Assert.AreEqual(1, intMean, "Integer elements divide as integers");

            double doubleMean = Calculate.Mean(new[] { 1.0, 2.0 });
            Assert.AreEqual(1.5, doubleMean);

            decimal decimalMean = Calculate.Mean(new[] { 1m, 2m, 4m });
            Assert.AreEqual(7m / 3m, decimalMean);

            Assert.AreEqual(20, Calculate.Mean(10, 20, 30));
            Assert.AreEqual(2.5, Calculate.Mean(2.0, 3.0));
        }

        [TestMethod]
        public void NMP_Calculate_SumAndCount ()
        {
            int intSum = Calculate.SumAndCount(new[] { 4, 5, 6 }, out int intCount);
            Assert.AreEqual(15, intSum);
            Assert.AreEqual(3, intCount);

            double doubleSum = Calculate.SumAndCount(new[] { 0.5, 0.25 }, out int doubleCount);
            Assert.AreEqual(0.75, doubleSum);
            Assert.AreEqual(2, doubleCount);
        }

        [TestMethod]
        public void NMP_Calculate_MinAndMax_OfDoubles ()
        {
            Calculate.MinAndMaxValuesIn(new[] { 2.5, -1.0, 8.25, 3.0 }, out double min, out double max);
            Assert.AreEqual(-1.0, min);
            Assert.AreEqual(8.25, max);
        }

        [TestMethod]
        public void NMP_Calculate_GcdAndLcm_OfLongs ()
        {
            long gcd = Calculate.GreatestCommonDenominator(12L, 18L, 30L);
            Assert.AreEqual(6L, gcd);

            long lcm = Calculate.LeastCommonMultiple(4L, 6L, 10L);
            Assert.AreEqual(60L, lcm);
        }

        [TestMethod]
        public void NMP_Calculate_QuickEvaluate_OfDoubles ()
        {
            Assert.IsTrue(Calculate.QuickEvaluateIfSumIsLessThan(new[] { 0.5, 0.25 }, 1.0));
            Assert.IsFalse(Calculate.QuickEvaluateIfSumIsLessThan(new[] { 0.5, 0.5 }, 1.0));
            Assert.IsTrue(Calculate.QuickEvaluateIfSumIsLessThanOrEqualTo(new[] { 0.5, 0.5 }, 1.0));
            Assert.IsFalse(Calculate.QuickEvaluateIfSumIsLessThanOrEqualTo(new[] { 0.5, 0.75 }, 1.0));
        }

        // ===[ Lerp ]===

        [TestMethod]
        public void NMP_Lerp_IntegerEnds_ComeBackAsDouble ()
        {
            var value = Lerp.Value(1, 5, 0.25);
            Assert.AreEqual(2.0, (object)value);
            Assert.AreEqual(typeof(double), ((object)value).GetType());

            var start = Lerp.Start(5, 4, 0.5);
            Assert.AreEqual(3.0, (object)start);

            var end = Lerp.End(1, 2, 0.25);
            Assert.AreEqual(5.0, (object)end);

            Assert.AreEqual(0.25, Lerp.Percent(0, 8, 2));
        }

        [TestMethod]
        public void NMP_Lerp_DoubleEnds ()
        {
            Assert.AreEqual(1.25, (object)Lerp.Value(1.0, 2.0, 0.25));
            Assert.AreEqual(0.75, Lerp.Percent(0.0, 2.0, 1.5));
        }

        // ===[ Cap ]===

        [TestMethod]
        public void NMP_Cap_Within ()
        {
            int capped = Cap.Within(0, 10, 15);
            Assert.AreEqual(10, capped);
            Assert.AreEqual(0, Cap.Within(0, 10, -3));
            Assert.AreEqual(4, Cap.Within(0, 10, 4));

            double cappedDouble = Cap.Within(0.0, 1.0, 1.5);
            Assert.AreEqual(1.0, cappedDouble);
        }

        // ===========[ Subclasses/structs ]===================================
        private static readonly Type[] kNumericTypes =
        {
            typeof(byte), typeof(short), typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal),
            typeof(sbyte), typeof(uint), typeof(ushort), typeof(ulong),
        };
    }
}
