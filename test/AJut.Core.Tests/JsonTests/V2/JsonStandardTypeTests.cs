namespace AJut.Core.UnitTests.AJsonV2
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Numerics;
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The .NET types AJson has to write in a form it can read back: dates and times, versions, addresses, big numbers,
    /// Complex, tuples and byte arrays, the collection interfaces, dictionaries, how numbers are quoted, and what happens to
    /// a type AJson cannot read back. Each case runs on the reflection path and, where it applies, through a generated
    /// serializer.
    /// </summary>
    [TestClass]
    public class JsonStandardTypeTests
    {
        private readonly List<string> m_capturedLog = new List<string>();

        // ===========================[ Test Models ]===========================
        public class TimeHolder
        {
            public DateTimeOffset When { get; set; }
            public DateTimeOffset? MaybeWhen { get; set; }
            public DateOnly Day { get; set; }
            public TimeOnly At { get; set; }
        }

        public class IdentityHolder
        {
            public Version Version { get; set; }
            public Uri Link { get; set; }
            public Uri RelativeLink { get; set; }
            public IPAddress Address { get; set; }
            public IPEndPoint EndPoint { get; set; }
        }

        public class BigNumberHolder
        {
            public Int128 Big { get; set; }
            public UInt128 BigUnsigned { get; set; }
            public BigInteger Huge { get; set; }
            public Half Small { get; set; }
        }

        public class ComplexHolder
        {
            public Complex Value { get; set; }
        }

        public class TupleHolder
        {
            public Tuple<int, string> Pair { get; set; }
        }

        public class BytesHolder
        {
            public byte[] Data { get; set; }
        }

        [OptimizeAJson]
        public class StandardTypesGen
        {
            public DateTimeOffset When { get; set; }
            public DateOnly Day { get; set; }
            public TimeOnly At { get; set; }
            public Version Version { get; set; }
            public Uri Link { get; set; }
            public BigInteger Huge { get; set; }
            public Complex Value { get; set; }
            public byte[] Data { get; set; }
            public Temperature Target { get; set; }
        }

        /// <summary>
        /// A value type of the shape .NET's own scalars have: get-only state, written and read as text through
        /// IFormattable and IParsable
        /// </summary>
        public readonly struct Temperature : IFormattable, IParsable<Temperature>
        {
            public Temperature (double degrees)
            {
                this.Degrees = degrees;
            }

            public double Degrees { get; }

            public string ToString (string format, IFormatProvider formatProvider)
            {
                return this.Degrees.ToString(format, formatProvider) + "C";
            }

            public static Temperature Parse (string s, IFormatProvider provider)
            {
                if (TryParse(s, provider, out Temperature result))
                {
                    return result;
                }

                throw new FormatException($"'{s}' is not a temperature");
            }

            public static bool TryParse (string s, IFormatProvider provider, out Temperature result)
            {
                if (s != null
                    && s.EndsWith("C", StringComparison.Ordinal)
                    && double.TryParse(s.AsSpan(0, s.Length - 1), NumberStyles.Float, provider, out double degrees))
                {
                    result = new Temperature(degrees);
                    return true;
                }

                result = default;
                return false;
            }
        }

        public class ThermostatHolder
        {
            public Temperature Target { get; set; }
        }

        public class InterfaceCollections
        {
            public IEnumerable<int> Enumerable { get; set; }
            public IReadOnlyList<int> ReadOnlyList { get; set; }
            public IReadOnlyCollection<int> ReadOnlyCollection { get; set; }
            public ICollection<int> Collection { get; set; }
            public IList<int> List { get; set; }
            public ISet<string> Set { get; set; }
            public IReadOnlySet<string> ReadOnlySet { get; set; }
            public IDictionary<string, int> Dictionary { get; set; }
            public IReadOnlyDictionary<string, int> ReadOnlyDictionary { get; set; }
        }

        [OptimizeAJson]
        public class InterfaceCollectionsGen
        {
            public IReadOnlyList<int> ReadOnlyList { get; set; }
            public ISet<string> Set { get; set; }
            public IReadOnlyDictionary<string, int> ReadOnlyDictionary { get; set; }
        }

        public interface IShape
        {
        }

        public class ShapeHolder
        {
            public string Name { get; set; }
            public IShape Shape { get; set; }
        }

        public record PointKey
        {
            public int X { get; init; }
            public int Y { get; init; }
        }

        public class DictionaryHolder
        {
            public Dictionary<string, int> ByName { get; set; }
            public Dictionary<DayOfWeek, string> ByDay { get; set; }
            public Dictionary<int, string> ById { get; set; }
            public Dictionary<Guid, int> ByGuid { get; set; }
            public Dictionary<PointKey, string> ByPoint { get; set; }
        }

        public class GetOnlyDictionaryHolder
        {
            public Dictionary<string, int> Counts { get; } = new Dictionary<string, int>();
        }

        [OptimizeAJson]
        public class DictionaryHolderGen
        {
            public Dictionary<string, int> ByName { get; set; }
            public Dictionary<DayOfWeek, string> ByDay { get; set; }
        }

        public class NumberArrays
        {
            public int[] Ints { get; set; }
            public bool[] Flags { get; set; }
            public List<double> Weights { get; set; }
        }

        public class UnsignedHolder
        {
            public byte Byte { get; set; }
            public sbyte SByte { get; set; }
            public ushort UShort { get; set; }
            public uint UInt { get; set; }
            public ulong ULong { get; set; }
        }

        [OptimizeAJson]
        public class UnsignedHolderGen
        {
            public byte Byte { get; set; }
            public sbyte SByte { get; set; }
            public ushort UShort { get; set; }
            public uint UInt { get; set; }
            public ulong ULong { get; set; }
        }

        /// <summary>
        /// Only a constructor nobody marked can set its value, so AJson has no way to read it back
        /// </summary>
        public readonly struct Unreadable
        {
            public Unreadable (int amount)
            {
                this.Amount = amount;
            }

            public int Amount { get; }
        }

        public class UnreadableHolder
        {
            public Unreadable Value { get; set; }
        }

        /// <summary>
        /// Keeps its state where AJson cannot see it
        /// </summary>
        public class Opaque
        {
            private int m_value;

            public void Bump () => ++m_value;
        }

        public class OpaqueHolder
        {
            public string Name { get; set; }
            public Opaque Hidden { get; set; }
        }

        // ===========================[ Setup/Construction/Teardown ]===========================
        [TestInitialize]
        public void Setup ()
        {
            m_capturedLog.Clear();
            Logger.SetSingleOverrideLogTarget(this.CaptureLog);
        }

        [TestCleanup]
        public void Cleanup ()
        {
            Logger.SetSingleOverrideLogTarget(null);
        }

        // ===========================[ Dates and Times ]===========================
        [TestMethod]
        public void DateTimeOffset_IsWrittenAsRoundTripIso8601_AndReadsBack ()
        {
            DateTimeOffset when = new DateTimeOffset(2026, 10, 9, 14, 30, 15, 123, TimeSpan.FromHours(-4)).AddTicks(4567);
            TimeHolder round = RoundTrip(new TimeHolder { When = when, MaybeWhen = when }, out string text);

            Assert.AreEqual(when, round.When, text);
            Assert.AreEqual(when.Offset, round.When.Offset, text);
            Assert.AreEqual(when, round.MaybeWhen, text);

            JsonValue written = WriteAndReparse(new TimeHolder { When = when }).ValueFor(nameof(TimeHolder.When));
            Assert.AreEqual(when.ToString("o", CultureInfo.InvariantCulture), written.StringValue);
        }

        [TestMethod]
        public void DateOnly_AndTimeOnly_AreWrittenAsIso8601_AndReadBack ()
        {
            TimeHolder source = new TimeHolder { Day = new DateOnly(2026, 10, 9), At = new TimeOnly(14, 30, 15, 250) };
            TimeHolder round = RoundTrip(source, out string text);
            Assert.AreEqual(source.Day, round.Day, text);
            Assert.AreEqual(source.At, round.At, text);

            JsonDocument written = WriteAndReparse(source);
            Assert.AreEqual("2026-10-09", written.ValueFor(nameof(TimeHolder.Day)).StringValue);
            Assert.AreEqual("14:30:15.2500000", written.ValueFor(nameof(TimeHolder.At)).StringValue);
        }

        // ===========================[ Versions, Addresses and Links ]===========================
        [TestMethod]
        public void Version_Uri_AndAddresses_AreWrittenAsText_AndReadBack ()
        {
            IdentityHolder source = new IdentityHolder
            {
                Version = new Version(1, 6, 2, 144),
                Link = new Uri("https://example.com/a?b=c"),
                RelativeLink = new Uri("docs/readme.md", UriKind.Relative),
                Address = IPAddress.Parse("10.0.0.1"),
                EndPoint = IPEndPoint.Parse("[::1]:8080"),
            };

            IdentityHolder round = RoundTrip(source, out string text);
            Assert.AreEqual(source.Version, round.Version, text);
            Assert.AreEqual(source.Link, round.Link, text);
            Assert.AreEqual(source.RelativeLink, round.RelativeLink, text);
            Assert.AreEqual(source.Address, round.Address, text);
            Assert.AreEqual(source.EndPoint, round.EndPoint, text);

            JsonDocument written = WriteAndReparse(source);
            Assert.AreEqual("1.6.2.144", written.ValueFor(nameof(IdentityHolder.Version)).StringValue);
            Assert.AreEqual("https://example.com/a?b=c", written.ValueFor(nameof(IdentityHolder.Link)).StringValue);
            Assert.AreEqual("10.0.0.1", written.ValueFor(nameof(IdentityHolder.Address)).StringValue);
        }

        // ===========================[ Big and Small Numbers ]===========================
        [TestMethod]
        public void Int128_UInt128_BigInteger_AndHalf_AreWrittenAsNumbers_AndReadBack ()
        {
            BigNumberHolder source = new BigNumberHolder
            {
                Big = Int128.MinValue,
                BigUnsigned = UInt128.MaxValue,
                Huge = BigInteger.Parse("-123456789012345678901234567890", CultureInfo.InvariantCulture),
                Small = (Half)1.5f,
            };

            BigNumberHolder round = RoundTrip(source, out string text);
            Assert.AreEqual(source.Big, round.Big, text);
            Assert.AreEqual(source.BigUnsigned, round.BigUnsigned, text);
            Assert.AreEqual(source.Huge, round.Huge, text);
            Assert.AreEqual(source.Small, round.Small, text);

            JsonDocument written = WriteAndReparse(source);
            JsonValue huge = written.ValueFor(nameof(BigNumberHolder.Huge));
            Assert.AreEqual("-123456789012345678901234567890", huge.StringValue);
            Assert.IsFalse(huge.IsQuoted, "A number is written unquoted");
        }

        [TestMethod]
        public void Complex_IsWrittenAsItsRealAndImaginaryParts_AndReadsBack ()
        {
            ComplexHolder source = new ComplexHolder { Value = new Complex(1.5, -2.25) };
            ComplexHolder round = RoundTrip(source, out string text);
            Assert.AreEqual(source.Value, round.Value, text);

            JsonDocument value = WriteAndReparse(source).ValueFor(nameof(ComplexHolder.Value)) as JsonDocument;
            Assert.IsNotNull(value, "Complex should be written as a document");
            CollectionAssert.AreEqual(new[] { "Real", "Imaginary" }, KeysOf(value));
        }

        // ===========================[ Tuples and Bytes ]===========================
        [TestMethod]
        public void Tuple_ReadsBackFromTheItemsItIsWrittenWith ()
        {
            TupleHolder round = RoundTrip(new TupleHolder { Pair = Tuple.Create(3, "three") }, out string text);
            Assert.AreEqual(Tuple.Create(3, "three"), round.Pair, text);
        }

        [TestMethod]
        public void ByteArray_IsWrittenAsBase64_AndReadsBack ()
        {
            byte[] data = { 0, 1, 2, 127, 128, 255 };
            BytesHolder round = RoundTrip(new BytesHolder { Data = data }, out string text);
            CollectionAssert.AreEqual(data, round.Data, text);

            JsonValue written = WriteAndReparse(new BytesHolder { Data = data }).ValueFor(nameof(BytesHolder.Data));
            Assert.IsTrue(written.IsValue, "A byte array is written as one value");
            Assert.AreEqual(Convert.ToBase64String(data), written.StringValue);
        }

        // ===========================[ IFormattable and IParsable ]===========================
        [TestMethod]
        public void ParsableType_IsWrittenAsItsText_AndReadsBack ()
        {
            ThermostatHolder source = new ThermostatHolder { Target = new Temperature(21.5) };
            ThermostatHolder round = RoundTrip(source, out string text);
            Assert.AreEqual(21.5, round.Target.Degrees, text);

            JsonValue written = WriteAndReparse(source).ValueFor(nameof(ThermostatHolder.Target));
            Assert.AreEqual("21.5C", written.StringValue);
        }

        // ===========================[ Generated Path ]===========================
        [TestMethod]
        public void StandardTypes_RoundTrip_ThroughAGeneratedSerializer ()
        {
            AssertIsGenerated(typeof(StandardTypesGen));

            StandardTypesGen source = new StandardTypesGen
            {
                When = new DateTimeOffset(2026, 10, 9, 14, 30, 15, TimeSpan.FromHours(5.5)),
                Day = new DateOnly(2026, 10, 9),
                At = new TimeOnly(23, 59, 59),
                Version = new Version(2, 0),
                Link = new Uri("https://example.com/"),
                Huge = BigInteger.Pow(2, 100),
                Value = new Complex(-1, 0.5),
                Data = new byte[] { 9, 8, 7 },
                Target = new Temperature(-3.25),
            };

            StandardTypesGen round = RoundTrip(source, out string text);
            Assert.AreEqual(source.When, round.When, text);
            Assert.AreEqual(source.Day, round.Day, text);
            Assert.AreEqual(source.At, round.At, text);
            Assert.AreEqual(source.Version, round.Version, text);
            Assert.AreEqual(source.Link, round.Link, text);
            Assert.AreEqual(source.Huge, round.Huge, text);
            Assert.AreEqual(source.Value, round.Value, text);
            CollectionAssert.AreEqual(source.Data, round.Data, text);
            Assert.AreEqual(source.Target.Degrees, round.Target.Degrees, text);
        }

        // ===========================[ Collection Interfaces ]===========================
        [TestMethod]
        public void InterfaceTypedCollections_RoundTrip ()
        {
            InterfaceCollections source = new InterfaceCollections
            {
                Enumerable = new List<int> { 1, 2 },
                ReadOnlyList = new List<int> { 3, 4 },
                ReadOnlyCollection = new List<int> { 5 },
                Collection = new List<int> { 6, 7 },
                List = new List<int> { 8 },
                Set = new HashSet<string> { "a", "b" },
                ReadOnlySet = new HashSet<string> { "c" },
                Dictionary = new Dictionary<string, int> { ["d"] = 9 },
                ReadOnlyDictionary = new Dictionary<string, int> { ["e"] = 10 },
            };

            InterfaceCollections round = RoundTrip(source, out string text);
            CollectionAssert.AreEqual(new[] { 1, 2 }, round.Enumerable?.ToArray(), text);
            CollectionAssert.AreEqual(new[] { 3, 4 }, round.ReadOnlyList?.ToArray(), text);
            CollectionAssert.AreEqual(new[] { 5 }, round.ReadOnlyCollection?.ToArray(), text);
            CollectionAssert.AreEqual(new[] { 6, 7 }, round.Collection?.ToArray(), text);
            CollectionAssert.AreEqual(new[] { 8 }, round.List?.ToArray(), text);
            Assert.IsTrue(round.Set?.SetEquals(new[] { "a", "b" }) ?? false, text);
            Assert.IsTrue(round.ReadOnlySet?.SetEquals(new[] { "c" }) ?? false, text);
            Assert.AreEqual(9, round.Dictionary?["d"], text);
            Assert.AreEqual(10, round.ReadOnlyDictionary?["e"], text);
        }

        [TestMethod]
        public void InterfaceTypedCollections_RoundTrip_ThroughAGeneratedSerializer ()
        {
            AssertIsGenerated(typeof(InterfaceCollectionsGen));

            InterfaceCollectionsGen source = new InterfaceCollectionsGen
            {
                ReadOnlyList = new List<int> { 3, 4 },
                Set = new HashSet<string> { "a" },
                ReadOnlyDictionary = new Dictionary<string, int> { ["e"] = 10 },
            };

            InterfaceCollectionsGen round = RoundTrip(source, out string text);
            CollectionAssert.AreEqual(new[] { 3, 4 }, round.ReadOnlyList?.ToArray(), text);
            Assert.IsTrue(round.Set?.SetEquals(new[] { "a" }) ?? false, text);
            Assert.AreEqual(10, round.ReadOnlyDictionary?["e"], text);
        }

        [TestMethod]
        public void InterfaceTypedProperty_WithNoTypeId_IsReportedAndLeftNull_RatherThanThrowing ()
        {
            Json json = JsonHelper.ParseText("{ \"Name\": \"square\", \"Shape\": { \"Size\": 2 } }");
            ShapeHolder read = JsonHelper.BuildObjectForJson<ShapeHolder>(json);

            Assert.IsNotNull(read);
            Assert.AreEqual("square", read.Name);
            Assert.IsNull(read.Shape);
            Assert.IsTrue(json.HasErrors, "Not knowing what to build for an interface should be reported");
            StringAssert.Contains(json.GetErrorReport(), nameof(IShape));
        }

        // ===========================[ Dictionaries ]===========================
        [TestMethod]
        public void Dictionary_WithStringKeys_IsWrittenAsAnObject_AndReadsBack ()
        {
            DictionaryHolder source = new DictionaryHolder { ByName = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 } };

            JsonDocument byName = WriteAndReparse(source).ValueFor(nameof(DictionaryHolder.ByName)) as JsonDocument;
            Assert.IsNotNull(byName, "A dictionary with string keys should be written as an object");
            CollectionAssert.AreEqual(new[] { "a", "b" }, KeysOf(byName));
            Assert.AreEqual("1", byName.ValueFor("a").StringValue);

            DictionaryHolder round = RoundTrip(source, out string text);
            CollectionAssert.AreEquivalent(source.ByName, round.ByName, text);
        }

        [TestMethod]
        public void Dictionary_WithEnumIntAndGuidKeys_IsWrittenAsAnObject_AndReadsBack ()
        {
            Guid id = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");
            DictionaryHolder source = new DictionaryHolder
            {
                ByDay = new Dictionary<DayOfWeek, string> { [DayOfWeek.Monday] = "start", [DayOfWeek.Friday] = "end" },
                ById = new Dictionary<int, string> { [7] = "seven", [-1] = "none" },
                ByGuid = new Dictionary<Guid, int> { [id] = 4 },
            };

            JsonDocument written = WriteAndReparse(source);
            Assert.IsInstanceOfType(written.ValueFor(nameof(DictionaryHolder.ByDay)), typeof(JsonDocument));
            Assert.IsInstanceOfType(written.ValueFor(nameof(DictionaryHolder.ById)), typeof(JsonDocument));
            Assert.IsInstanceOfType(written.ValueFor(nameof(DictionaryHolder.ByGuid)), typeof(JsonDocument));
            JsonDocument byDay = (JsonDocument)written.ValueFor(nameof(DictionaryHolder.ByDay));
            CollectionAssert.AreEqual(new[] { "Monday", "Friday" }, KeysOf(byDay));

            DictionaryHolder round = RoundTrip(source, out string text);
            CollectionAssert.AreEquivalent(source.ByDay, round.ByDay, text);
            CollectionAssert.AreEquivalent(source.ById, round.ById, text);
            CollectionAssert.AreEquivalent(source.ByGuid, round.ByGuid, text);
        }

        [TestMethod]
        public void Dictionary_WithAComplexKey_IsWrittenAsKeysAndValues_AndReadsBack ()
        {
            DictionaryHolder source = new DictionaryHolder
            {
                ByPoint = new Dictionary<PointKey, string> { [new PointKey { X = 1, Y = 2 }] = "corner" },
            };

            Assert.IsInstanceOfType(WriteAndReparse(source).ValueFor(nameof(DictionaryHolder.ByPoint)), typeof(JsonArray));

            DictionaryHolder round = RoundTrip(source, out string text);
            CollectionAssert.AreEquivalent(source.ByPoint, round.ByPoint, text);
        }

        [TestMethod]
        public void Dictionary_WrittenAsKeysAndValues_StillReads ()
        {
            DictionaryHolder read = Read<DictionaryHolder>(
                "{ \"ByName\": [ { \"Key\": \"a\", \"Value\": 1 }, { \"Key\": \"b\", \"Value\": 2 } ] }"
            );

            CollectionAssert.AreEquivalent(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }, read.ByName);
        }

        [TestMethod]
        public void Dictionary_IsWrittenAsKeysAndValues_WhenKeyValuePairTypeIdsAreAskedFor ()
        {
            JsonBuilderSettings settings = new JsonBuilderSettings { KeyValuePairValueTypeIdToWrite = eTypeIdInfo.Any };
            DictionaryHolder source = new DictionaryHolder { ByName = new Dictionary<string, int> { ["a"] = 1 } };

            JsonValue written = WriteAndReparse(source, settings).ValueFor(nameof(DictionaryHolder.ByName));
            Assert.IsInstanceOfType(written, typeof(JsonArray));
        }

        [TestMethod]
        public void Dictionary_WithAKeyThatIsAnAJsonMarker_IsWrittenAsKeysAndValues_AndReadsBack ()
        {
            DictionaryHolder source = new DictionaryHolder
            {
                ByName = new Dictionary<string, int> { [JsonDocument.kTypeIndicator] = 1, ["b"] = 2 },
            };

            Assert.IsInstanceOfType(WriteAndReparse(source).ValueFor(nameof(DictionaryHolder.ByName)), typeof(JsonArray));

            DictionaryHolder round = RoundTrip(source, out string text);
            CollectionAssert.AreEquivalent(source.ByName, round.ByName, text);
        }

        [TestMethod]
        public void Dictionaries_InAList_AtTheRoot_AndEmpty_RoundTrip ()
        {
            List<Dictionary<string, int>> list = new List<Dictionary<string, int>>
            {
                new Dictionary<string, int> { ["a"] = 1 },
                new Dictionary<string, int>(),
            };

            List<Dictionary<string, int>> roundList = RoundTrip(list, out string listText);
            Assert.AreEqual(2, roundList.Count, listText);
            CollectionAssert.AreEquivalent(list[0], roundList[0], listText);
            Assert.AreEqual(0, roundList[1].Count, listText);

            Dictionary<string, int> root = new Dictionary<string, int> { ["x"] = 9 };
            Dictionary<string, int> roundRoot = RoundTrip(root, out string rootText);
            CollectionAssert.AreEquivalent(root, roundRoot, rootText);
        }

        [TestMethod]
        public void NumbersWrittenQuoted_StillRead ()
        {
            NumberArrays read = Read<NumberArrays>("{ \"Ints\": [ \"1\", \"2\" ], \"Flags\": [ \"true\" ] }");
            CollectionAssert.AreEqual(new[] { 1, 2 }, read.Ints);
            CollectionAssert.AreEqual(new[] { true }, read.Flags);
        }

        [TestMethod]
        public void GetOnlyDictionary_IsWrittenAsAnObject_AndFilledBack ()
        {
            GetOnlyDictionaryHolder source = new GetOnlyDictionaryHolder();
            source.Counts["x"] = 3;

            JsonValue written = WriteAndReparse(source).ValueFor(nameof(GetOnlyDictionaryHolder.Counts));
            Assert.IsInstanceOfType(written, typeof(JsonDocument));

            GetOnlyDictionaryHolder round = RoundTrip(source, out string text);
            CollectionAssert.AreEquivalent(source.Counts, round.Counts, text);
        }

        [TestMethod]
        public void Dictionary_IsWrittenAsAnObject_AndReadsBack_ThroughAGeneratedSerializer ()
        {
            AssertIsGenerated(typeof(DictionaryHolderGen));

            DictionaryHolderGen source = new DictionaryHolderGen
            {
                ByName = new Dictionary<string, int> { ["a"] = 1 },
                ByDay = new Dictionary<DayOfWeek, string> { [DayOfWeek.Sunday] = "rest" },
            };

            JsonValue written = WriteAndReparse(source).ValueFor(nameof(DictionaryHolderGen.ByName));
            Assert.IsInstanceOfType(written, typeof(JsonDocument));

            DictionaryHolderGen round = RoundTrip(source, out string text);
            CollectionAssert.AreEquivalent(source.ByName, round.ByName, text);
            CollectionAssert.AreEquivalent(source.ByDay, round.ByDay, text);
        }

        // ===========================[ Quoting ]===========================
        [TestMethod]
        public void NumbersAndBools_InAnArray_AreWrittenUnquoted ()
        {
            JsonDocument written = WriteAndReparse(new NumberArrays
            {
                Ints = new[] { 1, 2 },
                Flags = new[] { true, false },
                Weights = new List<double> { 0.5 },
            });

            string[] arrayKeys = { nameof(NumberArrays.Ints), nameof(NumberArrays.Flags), nameof(NumberArrays.Weights) };
            foreach (string key in arrayKeys)
            {
                foreach (JsonValue item in (JsonArray)written.ValueFor(key))
                {
                    Assert.IsFalse(item.IsQuoted, $"{key} item '{item.StringValue}' should be unquoted");
                }
            }
        }

        [TestMethod]
        public void UnsignedAndByteProperties_AreWrittenUnquoted_OnBothPaths ()
        {
            UnsignedHolder reflected = new UnsignedHolder
            {
                Byte = 255,
                SByte = -8,
                UShort = 65535,
                UInt = 4000000000,
                ULong = ulong.MaxValue,
            };

            UnsignedHolderGen generated = new UnsignedHolderGen
            {
                Byte = reflected.Byte,
                SByte = reflected.SByte,
                UShort = reflected.UShort,
                UInt = reflected.UInt,
                ULong = reflected.ULong,
            };

            foreach (JsonDocument written in new[] { WriteAndReparse(reflected), WriteAndReparse(generated) })
            {
                foreach (string key in KeysOf(written))
                {
                    Assert.IsFalse(written.ValueFor(key).IsQuoted, $"{key} should be unquoted");
                }
            }
        }

        // ===========================[ What AJson Cannot Read Back ]===========================
        [TestMethod]
        public void ReadingATypeNothingCanSet_IsReported ()
        {
            Json json = JsonHelper.ParseText("{ \"Value\": { \"Amount\": 5 } }");
            JsonHelper.BuildObjectForJson<UnreadableHolder>(json);

            Assert.IsTrue(json.HasErrors, "Json that nothing can read into should be reported");
            StringAssert.Contains(json.GetErrorReport(), nameof(Unreadable));
        }

        [TestMethod]
        public void WritingATypeWhoseStateIsHidden_IsLogged ()
        {
            OpaqueHolder source = new OpaqueHolder { Name = "hidden", Hidden = new Opaque() };
            source.Hidden.Bump();

            JsonHelper.BuildJsonForObject(source).ToString();

            string log = String.Join("\n", m_capturedLog);
            Assert.IsTrue(
                m_capturedLog.Any(line => line.Contains(nameof(Opaque), StringComparison.Ordinal)),
                "Writing nothing for a type with state AJson cannot see should be logged. Log:\n" + log
            );
        }

        // ===========================[ Helpers ]===========================
        private void CaptureLog (string line) => m_capturedLog.Add(line);

        private static void AssertIsGenerated (Type type)
        {
            Assert.IsTrue(AJsonGeneratedDispatch.IsRegistered(type), $"The source generator did not register {type.Name}");
        }

        private static T Read<T> (string text)
        {
            Json json = JsonHelper.ParseText(text);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            T read = JsonHelper.BuildObjectForJson<T>(json);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            return read;
        }

        private static JsonDocument WriteAndReparse (object source, JsonBuilderSettings settings = null)
        {
            Json json = JsonHelper.BuildJsonForObject(source, settings);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            Json reparsed = JsonHelper.ParseText(json.ToString());
            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport());
            return (JsonDocument)reparsed.Data;
        }

        private static T RoundTrip<T> (T source, out string text)
        {
            Json json = JsonHelper.BuildJsonForObject(source);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            text = json.ToString();
            Json reparsed = JsonHelper.ParseText(text);
            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport() + "\nText:\n" + text);

            T round = JsonHelper.BuildObjectForJson<T>(reparsed);
            Assert.IsFalse(reparsed.HasErrors, reparsed.GetErrorReport() + "\nText:\n" + text);
            return round;
        }

        /// <summary>
        /// The document's keys in order, leaving out AJson's own markers (the version, a type id)
        /// </summary>
        private static string[] KeysOf (JsonDocument document)
        {
            return document.Select(kvp => kvp.Key).Where(key => !key.StartsWith("__", StringComparison.Ordinal)).ToArray();
        }
    }
}
