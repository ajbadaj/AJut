namespace AJut.Core.UnitTests.AJsonV2
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// JsonValueConverter: registering one for a type, what wins over one, and the default builder settings
    /// </summary>
    [TestClass]
    public class JsonConverterTests
    {
        private static readonly Guid kKnownId = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");

        private JsonValueConverter m_originalGuidConverter;
        private JsonBuilderSettings m_originalDefaultBuilderSettings;

        // ===========================[ Test Models ]===========================
        public readonly struct Celsius
        {
            public Celsius (double degrees)
            {
                this.Degrees = degrees;
            }

            public double Degrees { get; }
        }

        public sealed class CelsiusConverter : JsonScalarConverter
        {
            public CelsiusConverter () : base(typeof(Celsius), isUsuallyQuoted: true)
            {
            }

            public override string ToJsonText (object instance)
            {
                return ((Celsius)instance).Degrees.ToString(CultureInfo.InvariantCulture) + "C";
            }

            public override bool TryReadJsonText (string text, Type fullTarget, JsonInterpreterSettings settings, out object value)
            {
                value = null;
                if (text.EndsWith("C", StringComparison.Ordinal)
                    && double.TryParse(text.AsSpan(0, text.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out double degrees))
                {
                    value = new Celsius(degrees);
                    return true;
                }

                return false;
            }
        }

        public class Room
        {
            public Celsius Temperature { get; set; }
            public Celsius[] Readings { get; set; }
        }

        public readonly struct Money
        {
            public Money (decimal amount, string currency)
            {
                this.Amount = amount;
                this.Currency = currency;
            }

            public decimal Amount { get; }
            public string Currency { get; }
        }

        /// <summary>
        /// Writes Money as { "amount": 1.25, "currency": "CAD" }
        /// </summary>
        public sealed class MoneyConverter : JsonValueConverter
        {
            public MoneyConverter () : base(typeof(Money), eJsonValueShape.Document)
            {
            }

            public override void Write (object instance, JsonBuilder target)
            {
                Money money = (Money)instance;
                JsonBuilder document = StartDocument(target);
                document.AddProperty("amount", money.Amount, isUsuallyQuoted: false);
                document.AddProperty("currency", money.Currency);
            }

            public override bool CanRead (JsonValue json) => json.IsDocument;

            public override object Read (Type fullTarget, JsonValue json, JsonInterpreterSettings settings, Json owner)
            {
                JsonDocument document = (JsonDocument)json;
                decimal amount = decimal.Parse(document.ValueFor("amount").StringValue, CultureInfo.InvariantCulture);
                return new Money(amount, document.ValueFor("currency").StringValue);
            }
        }

        public class Wallet
        {
            public Money Balance { get; set; }
            public List<Money> History { get; set; }
        }

        [OptimizeAJson]
        public class WalletGen
        {
            public Money Balance { get; set; }
            public Celsius Temperature { get; set; }
        }

        public class Box<T>
        {
            public T Value { get; set; }
        }

        /// <summary>
        /// Writes any Box of a number as the number alone
        /// </summary>
        public sealed class BoxConverter : JsonScalarConverter
        {
            public BoxConverter () : base(typeof(Box<>), isUsuallyQuoted: false)
            {
            }

            public override string ToJsonText (object instance)
            {
                object value = instance.GetType().GetProperty(nameof(Box<int>.Value)).GetValue(instance);
                return ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture);
            }

            public override bool TryReadJsonText (string text, Type fullTarget, JsonInterpreterSettings settings, out object value)
            {
                value = Activator.CreateInstance(fullTarget);
                object inner = Convert.ChangeType(text, fullTarget.GenericTypeArguments[0], CultureInfo.InvariantCulture);
                fullTarget.GetProperty(nameof(Box<int>.Value)).SetValue(value, inner);
                return true;
            }
        }

        public class BoxHolder
        {
            public Box<int> Count { get; set; }
            public Box<double> Ratio { get; set; }
        }

        public class Event
        {
            public Guid Id { get; set; }
            public DateTime At { get; set; }
        }

        /// <summary>
        /// Formats and parses itself as text, and also has settable members, so text from before it was written as text is a
        /// document of those members
        /// </summary>
        public class Setting : IFormattable, IParsable<Setting>
        {
            public string Name { get; set; }
            public int Level { get; set; }

            public string ToString (string format, IFormatProvider formatProvider)
            {
                return this.Name + ":" + this.Level.ToString(formatProvider);
            }

            public static Setting Parse (string s, IFormatProvider provider)
            {
                return TryParse(s, provider, out Setting result) ? result : throw new FormatException($"'{s}' is not a setting");
            }

            public static bool TryParse (string s, IFormatProvider provider, out Setting result)
            {
                int split = s?.LastIndexOf(':') ?? -1;
                if (split > 0 && int.TryParse(s.AsSpan(split + 1), NumberStyles.Integer, provider, out int level))
                {
                    result = new Setting { Name = s.Substring(0, split), Level = level };
                    return true;
                }

                result = null;
                return false;
            }
        }

        public class SettingHolder
        {
            public Setting Setting { get; set; }
        }

        public class ZoneHolder
        {
            public TimeZoneInfo Zone { get; set; }
        }

        // ===========================[ Setup/Construction/Teardown ]===========================
        [TestInitialize]
        public void Setup ()
        {
            JsonHelper.TryGetConverterFor(typeof(Guid), out m_originalGuidConverter);
            m_originalDefaultBuilderSettings = JsonBuilderSettings.Default;
        }

        [TestCleanup]
        public void Cleanup ()
        {
            JsonHelper.UnregisterConverter(typeof(Celsius));
            JsonHelper.UnregisterConverter(typeof(Money));
            JsonHelper.UnregisterConverter(typeof(Box<>));
            JsonHelper.RegisterConverter(m_originalGuidConverter);
            JsonBuilderSettings.Default = m_originalDefaultBuilderSettings;
        }

        // ===========================[ Registered Converters ]===========================
        [TestMethod]
        public void ScalarConverter_WritesAndReadsItsText_InAPropertyAndInAnArray ()
        {
            JsonHelper.RegisterConverter(new CelsiusConverter());
            Room source = new Room { Temperature = new Celsius(21.5), Readings = new[] { new Celsius(-3), new Celsius(0.25) } };

            JsonDocument written = WriteAndReparse(source);
            Assert.AreEqual("21.5C", written.ValueFor(nameof(Room.Temperature)).StringValue);
            CollectionAssert.AreEqual(
                new[] { "-3C", "0.25C" },
                ((JsonArray)written.ValueFor(nameof(Room.Readings))).Select(item => item.StringValue).ToArray()
            );

            Room round = RoundTrip(source, out string text);
            Assert.AreEqual(21.5, round.Temperature.Degrees, text);
            CollectionAssert.AreEqual(new[] { -3.0, 0.25 }, round.Readings.Select(r => r.Degrees).ToArray(), text);
        }

        [TestMethod]
        public void DocumentConverter_WritesItsDocument_InAPropertyAndInAList ()
        {
            JsonHelper.RegisterConverter(new MoneyConverter());
            Wallet source = new Wallet
            {
                Balance = new Money(12.5m, "CAD"),
                History = new List<Money> { new Money(1m, "USD"), new Money(-2.25m, "CAD") },
            };

            JsonDocument written = WriteAndReparse(source);
            JsonDocument balance = written.ValueFor(nameof(Wallet.Balance)) as JsonDocument;
            Assert.IsNotNull(balance, "Money should be written as a document");
            CollectionAssert.AreEqual(new[] { "amount", "currency" }, balance.AllKeys().ToArray());

            Wallet round = RoundTrip(source, out string text);
            Assert.AreEqual(source.Balance, round.Balance, text);
            CollectionAssert.AreEqual(source.History, round.History, text);
        }

        [TestMethod]
        public void Converters_AreUsedByAGeneratedSerializer ()
        {
            Assert.IsTrue(AJsonGeneratedDispatch.IsRegistered(typeof(WalletGen)), "The source generator did not register WalletGen");
            JsonHelper.RegisterConverter(new MoneyConverter());
            JsonHelper.RegisterConverter(new CelsiusConverter());

            WalletGen source = new WalletGen { Balance = new Money(3m, "EUR"), Temperature = new Celsius(18) };
            JsonDocument written = WriteAndReparse(source);
            Assert.IsInstanceOfType(written.ValueFor(nameof(WalletGen.Balance)), typeof(JsonDocument));
            Assert.AreEqual("18C", written.ValueFor(nameof(WalletGen.Temperature)).StringValue);

            WalletGen round = RoundTrip(source, out string text);
            Assert.AreEqual(source.Balance, round.Balance, text);
            Assert.AreEqual(18.0, round.Temperature.Degrees, text);
        }

        [TestMethod]
        public void ConverterForAnOpenGenericType_CoversEveryTypeMadeFromIt ()
        {
            JsonHelper.RegisterConverter(new BoxConverter());
            BoxHolder source = new BoxHolder { Count = new Box<int> { Value = 4 }, Ratio = new Box<double> { Value = 0.5 } };

            JsonDocument written = WriteAndReparse(source);
            Assert.AreEqual("4", written.ValueFor(nameof(BoxHolder.Count)).StringValue);
            Assert.IsFalse(written.ValueFor(nameof(BoxHolder.Count)).IsQuoted);

            BoxHolder round = RoundTrip(source, out string text);
            Assert.AreEqual(4, round.Count.Value, text);
            Assert.AreEqual(0.5, round.Ratio.Value, text);
        }

        [TestMethod]
        public void ConverterRegisteredForABuiltInType_ReplacesIt ()
        {
            JsonHelper.RegisterConverter(new GuidWithoutDashesConverter());

            JsonDocument written = WriteAndReparse(new Event { Id = kKnownId });
            Assert.AreEqual(kKnownId.ToString("N"), written.ValueFor(nameof(Event.Id)).StringValue);
        }

        [TestMethod]
        public void DataClassification_FollowsTheConverters ()
        {
            JsonHelper.RegisterConverter(new CelsiusConverter());
            JsonHelper.RegisterConverter(new MoneyConverter());

            Assert.IsTrue(JsonHelper.IsValueData(new Celsius(1)));
            Assert.IsTrue(JsonHelper.IsDocumentData(new Money(1m, "CAD")));
            Assert.IsTrue(JsonHelper.IsValueData(new byte[] { 1, 2 }), "A byte array is written as one base64 value");
            Assert.IsTrue(JsonHelper.IsValueData(DateTimeOffset.Now));
        }

        // ===========================[ What Wins Over a Converter ]===========================
        [TestMethod]
        public void StringMakerOnTheSettings_WinsOverAConverter_ForThoseSettingsOnly ()
        {
            JsonBuilderSettings settings = new JsonBuilderSettings();
            settings.SetCustomJsonManager(typeof(Guid), instance => ((Guid)instance).ToString("B"));

            JsonDocument withMaker = WriteAndReparse(new Event { Id = kKnownId }, settings);
            Assert.AreEqual(kKnownId.ToString("B"), withMaker.ValueFor(nameof(Event.Id)).StringValue);

            JsonDocument withoutMaker = WriteAndReparse(new Event { Id = kKnownId });
            Assert.AreEqual(kKnownId.ToString(), withoutMaker.ValueFor(nameof(Event.Id)).StringValue);
        }

        [TestMethod]
        public void ConstructorOnTheSettings_WinsOverAConverter ()
        {
            JsonInterpreterSettings settings = new JsonInterpreterSettings();
            settings.RegisterCustomConstructor(json => kKnownId);

            Json json = JsonHelper.ParseText("{ \"Id\": \"00000000-0000-0000-0000-000000000001\" }");
            Event read = JsonHelper.BuildObjectForJson<Event>(json, settings);
            Assert.AreEqual(kKnownId, read.Id);
        }

        // ===========================[ Default Builder Settings ]===========================
        [TestMethod]
        public void DefaultBuilderSettings_ApplyToBuildsWithNoSettings_AndToJsonDocumentSet ()
        {
            JsonBuilderSettings.Default = new JsonBuilderSettings();
            JsonBuilderSettings.Default.SetCustomJsonManager(typeof(Guid), instance => ((Guid)instance).ToString("N"));

            JsonDocument built = WriteAndReparse(new Event { Id = kKnownId });
            Assert.AreEqual(kKnownId.ToString("N"), built.ValueFor(nameof(Event.Id)).StringValue);

            JsonDocument document = new JsonDocument();
            document.Set("Id", kKnownId);
            Assert.AreEqual(kKnownId.ToString("N"), document.ValueFor("Id").StringValue);
        }

        // ===========================[ Text Written Before ]===========================
        [TestMethod]
        public void ParsableTypeWithSettableMembers_IsWrittenAsText_AndStillReadsItsOldDocument ()
        {
            JsonDocument written = WriteAndReparse(new SettingHolder { Setting = new Setting { Name = "volume", Level = 7 } });
            Assert.AreEqual("volume:7", written.ValueFor(nameof(SettingHolder.Setting)).StringValue);

            Json json = JsonHelper.ParseText("{ \"Setting\": { \"Name\": \"volume\", \"Level\": 7 } }");
            SettingHolder read = JsonHelper.BuildObjectForJson<SettingHolder>(json);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            Assert.AreEqual("volume", read.Setting.Name);
            Assert.AreEqual(7, read.Setting.Level);
        }

        [TestMethod]
        public void TimeZoneThatCannotBeFound_ReadsAsUtc_AndIsReported ()
        {
            Json json = JsonHelper.ParseText("{ \"Zone\": \"Not A Real Zone\" }");
            ZoneHolder read = JsonHelper.BuildObjectForJson<ZoneHolder>(json);

            Assert.AreEqual(TimeZoneInfo.Utc, read.Zone);
            Assert.IsTrue(json.HasErrors, "An unknown time zone should be reported");
        }

        // ===========================[ Helpers ]===========================
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

        // ===========================[ Subclasses/structs ]===========================
        private sealed class GuidWithoutDashesConverter : JsonScalarConverter
        {
            public GuidWithoutDashesConverter () : base(typeof(Guid), isUsuallyQuoted: true)
            {
            }

            public override string ToJsonText (object instance) => ((Guid)instance).ToString("N");

            public override bool TryReadJsonText (string text, Type fullTarget, JsonInterpreterSettings settings, out object value)
            {
                bool isRead = Guid.TryParseExact(text, "N", out Guid read);
                value = read;
                return isRead;
            }
        }
    }
}
