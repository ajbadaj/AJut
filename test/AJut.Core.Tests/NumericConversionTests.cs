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
        public void NumericConversion_InterestingStringParsing_IntTypes(string toParse, object expectedValue, Type expectedType)
        {
            Assert.IsTrue(NumericConversion.TryParseString(toParse, expectedType, out object castedValue));
            Assert.AreEqual(Convert.ChangeType(expectedValue, expectedType), castedValue);
        }

        [DataTestMethod]
        [DataRow("43.5", typeof(int))]
        [DataRow("-43.5", typeof(int))]
        [DataRow("-43.5", typeof(long))]
        [DataRow("7.25", typeof(uint))]
        [DataRow("value: 12.5 units", typeof(byte))]
        [DataRow("sdfasdf-3.332a.0", typeof(long))]
        public void NumericConversion_IntegerTypes_RefuseAFraction (string toParse, Type integerType)
        {
            // An integer type cannot hold the number the text says, so the parse fails rather than quietly dropping
            // the fraction and handing back a different number
            Assert.IsFalse(NumericConversion.TryParseString(toParse, integerType, out object _));
        }

        [DataTestMethod]
        [DataRow("sdfasdf-5a.0", -5f, typeof(float))]
        [DataRow("sdfasdf-3.332a.0", -3.332, typeof(double))]
        public void NumericConversion_InterestingStringParsing_FloatTypes (string toParse, object expectedValue, Type expectedType)
        {
            Assert.IsTrue(NumericConversion.TryParseString(toParse, expectedType, out object castedValue));
            Assert.AreEqual(expectedType, castedValue.GetType());
            Assert.IsTrue(MathExtensions.IsApproximatelyEqualTo(Convert.ToDouble(expectedValue), Convert.ToDouble(castedValue)));
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
