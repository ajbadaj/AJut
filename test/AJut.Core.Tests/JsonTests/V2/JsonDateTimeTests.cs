namespace AJut.Core.UnitTests.AJsonV2
{
    using System;
    using AJut.Text.AJson;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// A DateTime has to come back with the same ticks and the same Kind it went out with, whatever
    /// its Kind, and text written before DateTimes were written as round-trip ISO 8601 still has to
    /// load. The values carry sub-second ticks on purpose: the old culture text dropped them.
    /// </summary>
    [TestClass]
    public class JsonDateTimeTests
    {
        private const long kSubSecondTicks = 1234567;
        private static readonly DateTime kUtc = MakeSample(DateTimeKind.Utc);
        private static readonly DateTime kLocal = MakeSample(DateTimeKind.Local);
        private static readonly DateTime kUnspecified = MakeSample(DateTimeKind.Unspecified);

        // ===========================[ Test Models ]===================================
        public class DateHolder
        {
            public DateTime When { get; set; }
        }

        // ===========================[ Round-Trips ]===================================
        [TestMethod]
        public void DateTime_Utc_RoundTripsWithTicksAndKind ()
        {
            AssertRoundTrips(kUtc);
        }

        [TestMethod]
        public void DateTime_Local_RoundTripsWithTicksAndKind ()
        {
            AssertRoundTrips(kLocal);
        }

        [TestMethod]
        public void DateTime_Unspecified_RoundTripsWithTicksAndKind ()
        {
            AssertRoundTrips(kUnspecified);
        }

        [TestMethod]
        public void DateTime_Utc_WrittenAsRoundTripIso8601 ()
        {
            Json json = JsonHelper.BuildJsonForObject(new DateHolder { When = kUtc });
            string written = ((JsonDocument)json.Data).ValueFor(nameof(DateHolder.When)).StringValue;
            Assert.AreEqual("2026-10-05T07:30:15.1234567Z", written);
        }

        // ===========================[ Reading Text ]===================================
        [TestMethod]
        public void DateTime_ReadsIso8601WithoutFraction ()
        {
            DateHolder read = ReadFrom("{ \"When\": \"2026-10-05T07:30:15Z\" }");
            Assert.AreEqual(new DateTime(2026, 10, 5, 7, 30, 15, DateTimeKind.Utc), read.When);
            Assert.AreEqual(DateTimeKind.Utc, read.When.Kind);
        }

        [TestMethod]
        public void DateTime_ReadsOldCultureText ()
        {
            // The old writer converted to UTC and wrote the current culture's general format, which
            //  has no offset and no fraction. It has to read back as the same instant it always did.
            DateTime wholeSecondUtc = new DateTime(2026, 10, 5, 7, 30, 15, DateTimeKind.Utc);
            string oldText = "{ \"When\": \"" + wholeSecondUtc.ToString() + "\" }";

            DateHolder read = ReadFrom(oldText);
            Assert.AreEqual(wholeSecondUtc, read.When.ToUniversalTime(), oldText);
        }

        [TestMethod]
        public void DateTime_UnparseableText_ReportsError ()
        {
            Json json = JsonHelper.ParseText("{ \"When\": \"not a date\" }");
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            DateHolder read = JsonHelper.BuildObjectForJson<DateHolder>(json);
            Assert.AreEqual(default(DateTime), read.When);
            Assert.IsTrue(json.HasErrors, "An unparseable DateTime must be reported, not silently defaulted");
        }

        // ===========================[ Helpers ]===================================
        private static DateTime MakeSample (DateTimeKind kind) => new DateTime(2026, 10, 5, 7, 30, 15, kind).AddTicks(kSubSecondTicks);

        private static void AssertRoundTrips (DateTime source)
        {
            Json json = JsonHelper.BuildJsonForObject(new DateHolder { When = source });
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            string text = json.ToString();
            DateHolder read = ReadFrom(text);
            Assert.AreEqual(source.Kind, read.When.Kind, "Kind. Text: " + text);
            Assert.AreEqual(source.Ticks, read.When.Ticks, $"Ticks: expected <{source:o}> got <{read.When:o}>. Text: " + text);
        }

        private static DateHolder ReadFrom (string text)
        {
            Json json = JsonHelper.ParseText(text);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());

            DateHolder read = JsonHelper.BuildObjectForJson<DateHolder>(json);
            Assert.IsFalse(json.HasErrors, json.GetErrorReport());
            return read;
        }
    }
}
