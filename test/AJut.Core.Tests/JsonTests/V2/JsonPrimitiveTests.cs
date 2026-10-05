namespace AJut.Core.UnitTests.AJsonV2
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Numerics;
    using System.Reflection;
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Round-trip coverage for the primitives AJson claims to handle, through both the reflection
    /// path and the source-generated path. A primitive with no registered parser reads back as its
    /// default with no error, so every one of them needs a round-trip here to be trusted.
    /// </summary>
    [TestClass]
    public class JsonPrimitiveTests
    {
        // de-DE uses a comma for the decimal separator and a period for grouping, so it is the
        //  culture that tells culture-invariant number text apart from current-culture text.
        private const string kCommaDecimalCulture = "de-DE";
        private const string kPeriodDecimalCulture = "en-US";
        private static readonly Vector2 kFractionalVector = new Vector2(0.5f, 1.25f);

        // ===========================[ Test Models ]===================================
        public class NumericMatrix
        {
            public byte Byte { get; set; }
            public sbyte SByte { get; set; }
            public short Short { get; set; }
            public ushort UShort { get; set; }
            public int Int { get; set; }
            public uint UInt { get; set; }
            public long Long { get; set; }
            public ulong ULong { get; set; }
            public float Float { get; set; }
            public double Double { get; set; }
            public decimal Decimal { get; set; }
            public decimal? NullableDecimal { get; set; }
        }

        [OptimizeAJson]
        public class NumericMatrixGen
        {
            public byte Byte { get; set; }
            public sbyte SByte { get; set; }
            public short Short { get; set; }
            public ushort UShort { get; set; }
            public int Int { get; set; }
            public uint UInt { get; set; }
            public long Long { get; set; }
            public ulong ULong { get; set; }
            public float Float { get; set; }
            public double Double { get; set; }
            public decimal Decimal { get; set; }
            public decimal? NullableDecimal { get; set; }
        }

        public class PriceHolder
        {
            public decimal Price { get; set; }
        }

        public class VectorHolder
        {
            public Vector2 Position { get; set; }
        }

        // ===========================[ Numeric Round-Trips ]===================================
        [TestMethod]
        public void Primitives_NumericMatrix_RoundTrip ()
        {
            NumericMatrix source = new NumericMatrix
            {
                Byte = 200,
                SByte = -100,
                Short = -30000,
                UShort = 65000,
                Int = -2000000000,
                UInt = 4000000000,
                Long = -9000000000000,
                ULong = 18000000000000000000,
                Float = 0.5f,
                Double = 1.25,
                Decimal = 12345.6789m,
                NullableDecimal = -0.001m,
            };

            AssertAllPropertiesEqual(source, RoundTrip(source));
        }

        [TestMethod]
        public void Primitives_NumericMatrix_SourceGen_RoundTrip ()
        {
            Assert.IsTrue(AJsonGeneratedDispatch.IsRegistered(typeof(NumericMatrixGen)), "Source generator did not register NumericMatrixGen");

            NumericMatrixGen source = new NumericMatrixGen
            {
                Byte = 200,
                SByte = -100,
                Short = -30000,
                UShort = 65000,
                Int = -2000000000,
                UInt = 4000000000,
                Long = -9000000000000,
                ULong = 18000000000000000000,
                Float = 0.5f,
                Double = 1.25,
                Decimal = 12345.6789m,
                NullableDecimal = -0.001m,
            };

            AssertAllPropertiesEqual(source, RoundTrip(source));
        }

        [TestMethod]
        public void Primitives_Decimal_ReadsFromText ()
        {
            Json json = JsonHelper.ParseText("{\"Price\": 1.5}");
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            PriceHolder holder = JsonHelper.BuildObjectForJson<PriceHolder>(json);
            Assert.AreEqual(1.5m, holder.Price);
        }

        // ===========================[ Culture ]===================================
        [TestMethod]
        public void Primitives_FloatingPoint_ReadsJsonNumbers_UnderCommaDecimalCulture ()
        {
            RunUnderCulture(kCommaDecimalCulture, () =>
            {
                Json json = JsonHelper.ParseText("{\"Float\": 0.5, \"Double\": 1.25, \"Decimal\": 12345.6789}");
                Assert.IsFalse(json.HasErrors, json.GetErrorReport());

                NumericMatrix read = JsonHelper.BuildObjectForJson<NumericMatrix>(json);
                Assert.AreEqual(0.5f, read.Float, "Float");
                Assert.AreEqual(1.25, read.Double, "Double");
                Assert.AreEqual(12345.6789m, read.Decimal, "Decimal");
            });
        }

        [TestMethod]
        public void Primitives_NumericMatrix_RoundTrip_UnderCommaDecimalCulture ()
        {
            RunUnderCulture(kCommaDecimalCulture, () =>
            {
                NumericMatrix source = new NumericMatrix
                {
                    Float = 0.5f,
                    Double = 1.25,
                    Decimal = 12345.6789m,
                    NullableDecimal = -0.001m,
                };

                AssertAllPropertiesEqual(source, RoundTrip(source));
            });
        }

        [TestMethod]
        public void Primitives_FloatingPoint_TextWrittenUnderOneCulture_ReadsUnderAnother ()
        {
            NumericMatrix source = new NumericMatrix
            {
                Float = 0.5f,
                Double = 1.25,
                Decimal = 12345.6789m,
            };

            string written = null;
            RunUnderCulture(kCommaDecimalCulture, () => written = JsonHelper.BuildJsonForObject(source).ToString());

            RunUnderCulture(kPeriodDecimalCulture, () =>
            {
                Json json = JsonHelper.ParseText(written);
                Assert.IsFalse(json.HasErrors, json.GetErrorReport());
                AssertAllPropertiesEqual(source, JsonHelper.BuildObjectForJson<NumericMatrix>(json));
            });
        }

        [TestMethod]
        public void Primitives_Infinity_WrittenByCurrentCulture_StillReads ()
        {
            // Guards older text: before numbers were written culture-invariant, infinity was written
            //  with the current culture's symbol, which is not always the invariant "Infinity".
            RunUnderCulture(kPeriodDecimalCulture, () =>
            {
                string oldText = "{\"Double\": " + double.PositiveInfinity.ToString() + "}";
                Json json = JsonHelper.ParseText(oldText);
                Assert.IsFalse(json.HasErrors, json.GetErrorReport());
                Assert.AreEqual(double.PositiveInfinity, JsonHelper.BuildObjectForJson<NumericMatrix>(json).Double, oldText);
            });
        }

        // ===========================[ Vector2 ]===================================
        [TestMethod]
        public void Vector2_RoundTrips ()
        {
            Assert.AreEqual(kFractionalVector, RoundTrip(new VectorHolder { Position = kFractionalVector }).Position);
        }

        [TestMethod]
        public void Vector2_RoundTrips_UnderCommaDecimalCulture ()
        {
            RunUnderCulture(kCommaDecimalCulture, () =>
            {
                VectorHolder round = RoundTrip(new VectorHolder { Position = kFractionalVector });
                Assert.AreEqual(kFractionalVector, round.Position);
            });
        }

        [TestMethod]
        public void Vector2_ReadsInvariantText_UnderCommaDecimalCulture ()
        {
            RunUnderCulture(kCommaDecimalCulture, () =>
            {
                Json json = JsonHelper.ParseText("{ \"Position\": \"<0.5,1.25>\" }");
                Assert.IsFalse(json.HasErrors, json.GetErrorReport());
                Assert.AreEqual(kFractionalVector, JsonHelper.BuildObjectForJson<VectorHolder>(json).Position);
            });
        }

        [TestMethod]
        public void Vector2_TextWrittenUnderOneCulture_ReadsUnderAnother ()
        {
            string written = null;
            RunUnderCulture(kCommaDecimalCulture, () => written = JsonHelper.BuildJsonForObject(new VectorHolder { Position = kFractionalVector }).ToString());

            RunUnderCulture(kPeriodDecimalCulture, () =>
            {
                Json json = JsonHelper.ParseText(written);
                Assert.IsFalse(json.HasErrors, json.GetErrorReport());
                Assert.AreEqual(kFractionalVector, JsonHelper.BuildObjectForJson<VectorHolder>(json).Position, written);
            });
        }

        [TestMethod]
        public void Vector2_UnreadableText_ReportsError ()
        {
            Json json = JsonHelper.ParseText("{ \"Position\": \"<not,a,vector>\" }");
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            VectorHolder read = JsonHelper.BuildObjectForJson<VectorHolder>(json);
            Assert.AreEqual(Vector2.Zero, read.Position);
            Assert.IsTrue(json.HasErrors, "An unreadable Vector2 must be reported, not silently zeroed");
        }

        // ===========================[ Helpers ]===================================
        private static void RunUnderCulture (string cultureName, Action action)
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                action();
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        private static T RoundTrip<T> (T source)
        {
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            string serialized = json.ToString();
            Json reparsed = JsonHelper.ParseText(serialized);
            Assert.IsFalse(reparsed.HasErrors, "Reparse errors:\n  " + String.Join("\n  ", reparsed.Errors) + "\nText:\n" + serialized);

            return JsonHelper.BuildObjectForJson<T>(reparsed);
        }

        // Compares every public property and reports all mismatches at once, so one run shows every
        //  primitive that fails to round-trip rather than only the first.
        private static void AssertAllPropertiesEqual<T> (T expected, T actual)
        {
            Assert.IsNotNull(actual, "Round-trip produced null");

            List<string> mismatches = new List<string>();
            foreach (PropertyInfo prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                object expectedValue = prop.GetValue(expected);
                object actualValue = prop.GetValue(actual);
                if (!Equals(expectedValue, actualValue))
                {
                    mismatches.Add($"{prop.Name}: expected <{expectedValue ?? "null"}> got <{actualValue ?? "null"}>");
                }
            }

            Assert.AreEqual(0, mismatches.Count, "Primitives that did not round-trip: " + String.Join("; ", mismatches));
        }
    }
}
