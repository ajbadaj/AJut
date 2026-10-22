namespace AJut.Core.UnitTests
{
    using System;
    using AJut.TypeManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class NumericConversionRangeTests
    {
        // ===[ short and sbyte are signed ]===

        [DataTestMethod]
        [DataRow(-5, typeof(short), (short)-5)]
        [DataRow(-5, typeof(sbyte), (sbyte)-5)]
        [DataRow(-5.0, typeof(short), (short)-5)]
        [DataRow(-120L, typeof(sbyte), (sbyte)-120)]
        public void NCR_SignedSmallTypes_KeepANegativeValue (object value, Type target, object expected)
        {
            object result = NumericConversion.PerformSafeNumericCastToTarget(value, target, out bool capMin, out bool capMax);

            Assert.AreEqual(expected, result);
            Assert.IsFalse(capMin, "A negative value inside a signed type's range is not capped");
            Assert.IsFalse(capMax);
        }

        [DataTestMethod]
        [DataRow(-40000, typeof(short), short.MinValue)]
        [DataRow(-200, typeof(sbyte), sbyte.MinValue)]
        public void NCR_SignedSmallTypes_CapAtTheirOwnMinimum (int value, Type target, object expected)
        {
            object result = NumericConversion.PerformSafeNumericCastToTarget(value, target, out bool capMin, out _);

            Assert.AreEqual(Convert.ChangeType(expected, target), result);
            Assert.IsTrue(capMin);
        }

        [DataTestMethod]
        [DataRow(typeof(short), (short)-12)]
        [DataRow(typeof(sbyte), (sbyte)-12)]
        public void NCR_SignedSmallTypes_ParseANegativeFromText (Type target, object expected)
        {
            // The type's own parse fails on the surrounding text, so this goes through the digits fallback
            Assert.IsTrue(NumericConversion.TryParseString("value -12 here", target, out object parsed));
            Assert.AreEqual(expected, parsed);
        }

        // ===[ Values beyond the largest container cap instead of overflowing ]===

        [DataTestMethod]
        [DataRow(typeof(long))]
        [DataRow(typeof(int))]
        [DataRow(typeof(short))]
        [DataRow(typeof(sbyte))]
        [DataRow(typeof(ulong))]
        [DataRow(typeof(uint))]
        [DataRow(typeof(ushort))]
        [DataRow(typeof(byte))]
        public void NCR_HugeDouble_CapsAtEveryIntegerTypesMax (Type target)
        {
            object result = NumericConversion.PerformSafeNumericCastToTarget(1e30, target, out bool capMin, out bool capMax);

            Assert.AreEqual(NumericConversion.MaxFor(target), result);
            Assert.IsFalse(capMin);
            Assert.IsTrue(capMax);
        }

        [DataTestMethod]
        [DataRow(typeof(long))]
        [DataRow(typeof(int))]
        [DataRow(typeof(short))]
        [DataRow(typeof(sbyte))]
        public void NCR_HugeNegativeDouble_CapsAtEverySignedIntegerTypesMin (Type target)
        {
            object result = NumericConversion.PerformSafeNumericCastToTarget(-1e30, target, out bool capMin, out bool capMax);

            Assert.AreEqual(NumericConversion.MinFor(target), result);
            Assert.IsTrue(capMin);
            Assert.IsFalse(capMax);
        }

        [TestMethod]
        public void NCR_HugeFloatAndDecimal_CapAtLongMax ()
        {
            Assert.AreEqual((object)long.MaxValue, NumericConversion.PerformSafeNumericCastToTarget(1e30f, typeof(long), out _, out bool floatCapMax));
            Assert.IsTrue(floatCapMax);

            Assert.AreEqual((object)long.MaxValue, NumericConversion.PerformSafeNumericCastToTarget(decimal.MaxValue, typeof(long), out _, out bool decimalCapMax));
            Assert.IsTrue(decimalCapMax);

            Assert.AreEqual((object)ulong.MaxValue, NumericConversion.PerformSafeNumericCastToTarget(decimal.MaxValue, typeof(ulong), out _, out bool unsignedCapMax));
            Assert.IsTrue(unsignedCapMax);
        }

        [TestMethod]
        public void NCR_UlongBeyondLong_CapsOnTheSignedPath ()
        {
            Assert.AreEqual((object)long.MaxValue, NumericConversion.PerformSafeNumericCastToTarget(ulong.MaxValue, typeof(long), out _, out bool longCapMax));
            Assert.IsTrue(longCapMax);

            Assert.AreEqual((object)int.MaxValue, NumericConversion.PerformSafeNumericCastToTarget(ulong.MaxValue, typeof(int), out _, out bool intCapMax));
            Assert.IsTrue(intCapMax);
        }
    }
}
