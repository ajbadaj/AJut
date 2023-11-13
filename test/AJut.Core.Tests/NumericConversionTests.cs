namespace AJut.Core.UnitTests
{
    using System;
    using System.Linq;
    using AJut.TypeManagement;
    using AJut.MathUtilities;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class NumericConversionTests
    {
        [DataTestMethod]
        [DataRow("sdfasdf-5a.0", -5, typeof(int))]
        [DataRow("sdfasdf-3.332a.0", -3, typeof(long))]
        public void NumericConversion_InterestingStringParsing_IntTypes(string toParse, dynamic expectedValue, Type expectedType)
        {
            Assert.IsTrue(NumericConversion.TryParseString(toParse, expectedType, out dynamic castedValue));
            Assert.AreEqual(expectedValue, castedValue);
        }

        [DataTestMethod]
        [DataRow("sdfasdf-5a.0", -5f, typeof(float))]
        [DataRow("sdfasdf-3.332a.0", -3.332, typeof(double))]
        public void NumericConversion_InterestingStringParsing_FloatTypes (string toParse, dynamic expectedValue, Type expectedType)
        {
            Assert.IsTrue(NumericConversion.TryParseString(toParse, expectedType, out dynamic castedValue));
            Assert.IsTrue(MathExtensions.IsApproximatelyEqualTo(expectedValue, castedValue));
        }

        [TestMethod]
        public void NumericConversion_NumericCast_BasicOverflowCast()
        {
            Assert.AreEqual((byte)255, NumericConversion.PerformSafeNumericCastToTarget(55665.000234f, typeof(byte), out bool didCapMin, out bool didCapMax));
            Assert.IsFalse(didCapMin);
            Assert.IsTrue(didCapMax);
        }

        [TestMethod]
        public void NumericConversion_NumericCast_NoOverflowCast ()
        {
            Assert.AreEqual((byte)21, NumericConversion.PerformSafeNumericCastToTarget(21.000234f, typeof(byte), out bool didCapMin, out bool didCapMax));
            Assert.IsFalse(didCapMin);
            Assert.IsFalse(didCapMax);
        }


        [TestMethod]
        public void NumericConversion_NumericCap_ByteOverflow ()
        {
            Assert.AreEqual((byte)0, NumericConversion.PerformNumericTypeSafeCapping<byte>(-55665.000234f, 0f, 200f, out bool didCapMin, out bool didCapMax));
            Assert.IsTrue(didCapMin);
            Assert.IsFalse(didCapMax);
        }

        [TestMethod]
        public void NumericConversion_NumericCap_RangeOverflow ()
        {
            Assert.AreEqual((byte)200, NumericConversion.PerformNumericTypeSafeCapping<byte>(230f, -20f, 200f, out bool didCapMin, out bool didCapMax));
            Assert.IsFalse(didCapMin);
            Assert.IsTrue(didCapMax);
        }

        [TestMethod]
        public void NumericConversion_NumericCap_No ()
        {
            Assert.AreEqual((byte)150, NumericConversion.PerformNumericTypeSafeCapping<byte>(150f, -20f, 200f, out bool didCapMin, out bool didCapMax));
            Assert.IsFalse(didCapMin);
            Assert.IsFalse(didCapMax);
        }
    }
}
