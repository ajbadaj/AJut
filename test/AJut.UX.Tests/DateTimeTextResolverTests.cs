namespace AJut.UX.Tests
{
    using System;
    using System.Globalization;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// What <see cref="DateTimeTextResolver"/> reads typed text as. Every test counts from the same moment, a Wednesday
    /// morning, in a culture whose week starts on Sunday, so each phrase has one right answer.
    /// </summary>
    [TestClass]
    public class DateTimeTextResolverTests
    {
        // Wednesday, November 25th 2026, 10:30 in the morning
        private static readonly DateTime kNow = new DateTime(2026, 11, 25, 10, 30, 0);
        private static readonly DateTime kToday = kNow.Date;
        private static readonly CultureInfo kCulture = CultureInfo.GetCultureInfo("en-US");

        // ===========[ Named days ]============================================
        [TestMethod]
        public void Resolve_Today_IsTodayAtMidnight ()
        {
            Assert.AreEqual(kToday, Resolve("today"));
        }

        [TestMethod]
        public void Resolve_Now_KeepsTheTimeOfDay ()
        {
            Assert.AreEqual(kNow, Resolve("now"));
        }

        [TestMethod]
        public void Resolve_TomorrowAndYesterday_AreADayEitherSide ()
        {
            Assert.AreEqual(kToday.AddDays(1), Resolve("tomorrow"));
            Assert.AreEqual(kToday.AddDays(-1), Resolve("yesterday"));
        }

        [TestMethod]
        public void Resolve_TextInAnyCaseWithSpaceAround_ReadsTheSame ()
        {
            Assert.AreEqual(kToday.AddDays(1), Resolve("  ToMorrow "));
        }

        // ===========[ Weekdays ]==============================================
        [TestMethod]
        public void Resolve_Weekday_IsTheFirstOneAfterToday ()
        {
            Assert.AreEqual(new DateTime(2026, 11, 27), Resolve("friday"));
            Assert.AreEqual(new DateTime(2026, 11, 30), Resolve("mon"));
        }

        [TestMethod]
        public void Resolve_TodaysOwnWeekday_IsAWeekOut ()
        {
            Assert.AreEqual(new DateTime(2026, 12, 2), Resolve("wednesday"));
            Assert.AreEqual(new DateTime(2026, 12, 2), Resolve("this wednesday"));
        }

        [TestMethod]
        public void Resolve_NextWeekday_IsTheOneInNextWeek ()
        {
            // The week after this one runs Sunday the 29th to Saturday December 5th
            Assert.AreEqual(new DateTime(2026, 12, 4), Resolve("next friday"));
            Assert.AreEqual(new DateTime(2026, 11, 30), Resolve("next monday"));
        }

        [TestMethod]
        public void Resolve_NextWeekdayFromTheWeeksLastDay_IsStillNextWeek ()
        {
            // On Saturday the 28th, next week's Friday is six days out, not thirteen
            var saturday = new DateTime(2026, 11, 28, 9, 0, 0);

            Assert.AreEqual(new DateTime(2026, 12, 4), Resolve("next friday", now: saturday));
        }

        [TestMethod]
        public void Resolve_NextWeekdayWhereTheWeekStartsOnMonday_CountsFromMonday ()
        {
            // On Sunday the 29th in a Monday-first culture, next week starts the day after
            var sunday = new DateTime(2026, 11, 29, 9, 0, 0);

            Assert.AreEqual(new DateTime(2026, 11, 30), Resolve("next monday", now: sunday, culture: CultureInfo.GetCultureInfo("en-GB")));
        }

        [TestMethod]
        public void Resolve_LastWeekday_IsTheLatestOneBeforeToday ()
        {
            Assert.AreEqual(new DateTime(2026, 11, 20), Resolve("last friday"));
            Assert.AreEqual(new DateTime(2026, 11, 18), Resolve("last wednesday"));
        }

        // ===========[ Offsets ]===============================================
        [TestMethod]
        public void Resolve_InTwoWeeks_IsFourteenDaysOn ()
        {
            Assert.AreEqual(new DateTime(2026, 12, 9), Resolve("in two weeks"));
            Assert.AreEqual(new DateTime(2026, 12, 9), Resolve("in 2 weeks"));
            Assert.AreEqual(new DateTime(2026, 12, 9), Resolve("2 weeks from now"));
        }

        [TestMethod]
        public void Resolve_SignedShorthand_MovesThatWay ()
        {
            Assert.AreEqual(new DateTime(2026, 11, 28), Resolve("+3d"));
            Assert.AreEqual(new DateTime(2026, 11, 11), Resolve("-2w"));
            Assert.AreEqual(new DateTime(2026, 11, 28), Resolve("+3 days"));
            Assert.AreEqual(new DateTime(2027, 11, 25), Resolve("+1y"));
        }

        [TestMethod]
        public void Resolve_DaysAgo_GoesBack ()
        {
            Assert.AreEqual(new DateTime(2026, 11, 22), Resolve("3 days ago"));
            Assert.AreEqual(new DateTime(2026, 11, 18), Resolve("a week ago"));
        }

        [TestMethod]
        public void Resolve_NextAndLastOfAUnit_MoveOneOfIt ()
        {
            Assert.AreEqual(new DateTime(2026, 12, 2), Resolve("next week"));
            Assert.AreEqual(new DateTime(2026, 12, 25), Resolve("next month"));
            Assert.AreEqual(new DateTime(2025, 11, 25), Resolve("last year"));
        }

        [TestMethod]
        public void Resolve_AMonthOnFromADayTheNextMonthLacks_IsThatMonthsLastDay ()
        {
            var endOfJanuary = new DateTime(2027, 1, 31, 8, 0, 0);

            Assert.AreEqual(new DateTime(2027, 2, 28), Resolve("+1mo", now: endOfJanuary));
        }

        [TestMethod]
        public void Resolve_HoursAndMinutes_CountFromThisMoment ()
        {
            Assert.AreEqual(kNow.AddHours(4), Resolve("+4h", includeTime: true));
            Assert.AreEqual(kNow.AddMinutes(30), Resolve("in 30 minutes", includeTime: true));
        }

        [TestMethod]
        public void Resolve_LoneM_IsMonthsForADateAndMinutesForATimeAlone ()
        {
            Assert.AreEqual(new DateTime(2026, 12, 25), Resolve("+1m"));
            Assert.AreEqual(kNow.AddMinutes(1), Resolve("+1m", includeDate: false, includeTime: true));
        }

        [TestMethod]
        public void Resolve_AmountWithNothingSayingWhichWay_IsNotAPhrase ()
        {
            DateTimeTextResolution resolution = DateTimeTextResolver.Default.Resolve("3 days", Context());

            Assert.IsFalse(resolution.IsResolved);
        }

        [TestMethod]
        public void Resolve_OffsetPastWhatADateHolds_Fails ()
        {
            DateTimeTextResolution resolution = DateTimeTextResolver.Default.Resolve("+900000y", Context());

            Assert.IsFalse(resolution.IsResolved);
        }

        // ===========[ Times of day ]==========================================
        [TestMethod]
        public void Resolve_PhraseEndingInATime_IsThatDayAtThatTime ()
        {
            Assert.AreEqual(new DateTime(2026, 11, 27, 15, 0, 0), Resolve("friday 3pm", includeTime: true));
            Assert.AreEqual(new DateTime(2026, 11, 26, 15, 30, 0), Resolve("tomorrow at 15:30", includeTime: true));
            Assert.AreEqual(new DateTime(2026, 12, 9, 12, 0, 0), Resolve("in two weeks at noon", includeTime: true));
            Assert.AreEqual(new DateTime(2026, 11, 26, 9, 15, 0), Resolve("tomorrow 9:15 am", includeTime: true));
        }

        [TestMethod]
        public void Resolve_TimeAlone_IsTodayAtThatTime ()
        {
            Assert.AreEqual(new DateTime(2026, 11, 25, 15, 0, 0), Resolve("3pm", includeTime: true));
            Assert.AreEqual(kToday, Resolve("midnight", includeTime: true));
        }

        [TestMethod]
        public void Resolve_PhraseWithNoTime_KeepsTheTimeAlreadyHeld ()
        {
            var current = new DateTime(2026, 11, 20, 17, 45, 0);

            Assert.AreEqual(new DateTime(2026, 11, 26, 17, 45, 0), Resolve("tomorrow", includeTime: true, current: current));
        }

        // ===========[ Written dates ]=========================================
        [TestMethod]
        public void Resolve_IsoDate_ReadsInAnyCulture ()
        {
            Assert.AreEqual(new DateTime(2026, 12, 24), Resolve("2026-12-24"));
            Assert.AreEqual(new DateTime(2026, 12, 24), Resolve("2026-12-24", culture: CultureInfo.GetCultureInfo("de-DE")));
        }

        [TestMethod]
        public void Resolve_DateInTheCulturesOrder_ReadsThatWay ()
        {
            Assert.AreEqual(new DateTime(2026, 3, 4), Resolve("3/4/2026"));
            Assert.AreEqual(new DateTime(2026, 4, 3), Resolve("3/4/2026", culture: CultureInfo.GetCultureInfo("en-GB")));
        }

        [TestMethod]
        public void Resolve_TextInTheDisplayFormat_ReadsBackExactly ()
        {
            const string kFormat = "dd|MM|yyyy";
            var value = new DateTime(2026, 4, 3);
            string shown = DateTimeTextResolver.Default.Format(value, Context(displayFormat: kFormat));

            Assert.AreEqual("03|04|2026", shown);
            Assert.AreEqual(value, Resolve(shown, displayFormat: kFormat));
        }

        [TestMethod]
        public void Resolve_TextThatIsNoDate_FailsAndSaysSo ()
        {
            const string kUnreadableText = "soonish";
            DateTimeTextResolution resolution = DateTimeTextResolver.Default.Resolve(kUnreadableText, Context());

            Assert.IsFalse(resolution.IsResolved);
            StringAssert.Contains(resolution.FailureReason, kUnreadableText, "The reason names the text it could not read");
        }

        // ===========[ Format ]================================================
        [TestMethod]
        public void Format_NoFormatGiven_UsesTheCulturesShortForms ()
        {
            var value = new DateTime(2026, 11, 25, 15, 5, 0);

            Assert.AreEqual("11/25/2026", DateTimeTextResolver.Default.Format(value, Context()));
            Assert.AreEqual(value.ToString("t", kCulture), DateTimeTextResolver.Default.Format(value, Context(includeDate: false, includeTime: true)));
            Assert.AreEqual(value.ToString("g", kCulture), DateTimeTextResolver.Default.Format(value, Context(includeTime: true)));
        }

        [TestMethod]
        public void Format_FormatDotNetCantUse_ShowsTheCulturesShortForm ()
        {
            // Each is what a format box holds part way through typing a good one
            var value = new DateTime(2026, 11, 25);
            foreach (string halfTyped in new[] { "h", "%", "yyyy-MM-dd\\" })
            {
                Assert.AreEqual("11/25/2026", DateTimeTextResolver.Default.Format(value, Context(displayFormat: halfTyped)), $"Format '{halfTyped}'");
            }
        }

        [TestMethod]
        public void Resolve_WithAFormatDotNetCantUse_StillReadsEverythingElse ()
        {
            Assert.AreEqual(kToday.AddDays(1), Resolve("tomorrow", displayFormat: "%"));
            Assert.AreEqual(new DateTime(2026, 12, 24), Resolve("2026-12-24", displayFormat: "h"));
        }

        [TestMethod]
        public void Format_TwentyFourHour_ShowsNoAmOrPm ()
        {
            var value = new DateTime(2026, 11, 25, 15, 5, 0);
            Assert.AreEqual("15:05", DateTimeTextResolver.Default.Format(value, Context(includeDate: false, includeTime: true, timeFormat: eTimeFormat.TwentyFourHour)));
            Assert.AreEqual("11/25/2026 15:05", DateTimeTextResolver.Default.Format(value, Context(includeTime: true, timeFormat: eTimeFormat.TwentyFourHour)));
        }

        [TestMethod]
        public void Format_TwelveHourInATwentyFourHourCulture_ShowsTheAmOrPm ()
        {
            var value = new DateTime(2026, 11, 25, 15, 5, 0);
            CultureInfo german = CultureInfo.GetCultureInfo("de-DE");
            string shown = DateTimeTextResolver.Default.Format(value, Context(includeDate: false, includeTime: true, culture: german, timeFormat: eTimeFormat.TwelveHour));
            Assert.AreEqual(value.ToString("h:mm tt", german), shown);
        }

        [TestMethod]
        public void Format_ADisplayFormatGiven_IsUsedWhateverTheTimeFormat ()
        {
            var value = new DateTime(2026, 11, 25, 15, 5, 0);
            Assert.AreEqual("3:05 PM", DateTimeTextResolver.Default.Format(value, Context(includeTime: true, displayFormat: "h:mm tt", timeFormat: eTimeFormat.TwentyFourHour)));
        }

        [TestMethod]
        public void Resolve_WhatATwentyFourHourClockShows_ReadsBack ()
        {
            var value = new DateTime(2026, 11, 25, 15, 5, 0);
            DateTimeTextContext context = Context(includeTime: true, timeFormat: eTimeFormat.TwentyFourHour);
            string shown = DateTimeTextResolver.Default.Format(value, context);
            Assert.AreEqual(value, DateTimeTextResolver.Default.Resolve(shown, context).Value);
        }

        [TestMethod]
        public void Format_NoValue_IsEmpty ()
        {
            Assert.AreEqual(String.Empty, DateTimeTextResolver.Default.Format(null, Context()));
        }

        // ===========[ Helper Methods ]========================================
        private static DateTimeTextContext Context (bool includeDate = true, bool includeTime = false, DateTime? current = null, string displayFormat = null, DateTime? now = null, CultureInfo culture = null, eTimeFormat timeFormat = eTimeFormat.CurrentCulture)
        {
            return new DateTimeTextContext(now ?? kNow, current, includeDate, includeTime, displayFormat, culture ?? kCulture, timeFormat);
        }

        // What the text resolves to, failing the test when it doesn't resolve at all
        private static DateTime? Resolve (string text, bool includeDate = true, bool includeTime = false, DateTime? current = null, string displayFormat = null, DateTime? now = null, CultureInfo culture = null)
        {
            DateTimeTextResolution resolution = DateTimeTextResolver.Default.Resolve(text, Context(includeDate, includeTime, current, displayFormat, now, culture));
            Assert.IsTrue(resolution.IsResolved, $"'{text}' did not resolve: {resolution.FailureReason}");
            return resolution.Value;
        }
    }
}
