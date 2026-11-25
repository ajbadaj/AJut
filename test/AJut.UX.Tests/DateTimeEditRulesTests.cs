namespace AJut.UX.Tests
{
    using System;
    using System.Globalization;
    using AJut.UX.Controls;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// What every stack's DateTimeEditIngressControl decides through <see cref="DateTimeEditRules"/> and draws its
    /// calendar from with <see cref="CalendarMonthGrid"/>
    /// </summary>
    [TestClass]
    public class DateTimeEditRulesTests
    {
        // Wednesday, November 25th 2026, 10:30 in the morning
        private static readonly DateTime kNow = new DateTime(2026, 11, 25, 10, 30, 0);
        private static readonly CultureInfo kCulture = CultureInfo.GetCultureInfo("en-US");

        // ===========[ Evaluate ]==============================================
        [TestMethod]
        public void Evaluate_EmptyText_IsValidWithNoValue ()
        {
            DateTimeEditRules.Evaluation evaluation = DateTimeEditRules.Evaluate("   ", null, Context(), treatEmptyAsInvalid: false);

            Assert.IsTrue(evaluation.IsTextValid);
            Assert.IsNull(evaluation.Value);
            Assert.IsNull(evaluation.InvalidTextReason);
        }

        [TestMethod]
        public void Evaluate_EmptyTextTreatedAsInvalid_SaysNothingWasGiven ()
        {
            DateTimeEditRules.Evaluation evaluation = DateTimeEditRules.Evaluate(null, null, Context(), treatEmptyAsInvalid: true);

            Assert.IsFalse(evaluation.IsTextValid);
            Assert.AreEqual("No date or time given", evaluation.InvalidTextReason);
        }

        [TestMethod]
        public void Evaluate_TextTheResolverCannotRead_IsInvalidWithItsReason ()
        {
            DateTimeEditRules.Evaluation evaluation = DateTimeEditRules.Evaluate("soonish", null, Context(), treatEmptyAsInvalid: false);

            Assert.IsFalse(evaluation.IsTextValid);
            Assert.IsNull(evaluation.Value);
            Assert.AreEqual("'soonish' isn't a date", evaluation.InvalidTextReason);
        }

        [TestMethod]
        public void Evaluate_DateOnly_DropsTheTimeOfDay ()
        {
            DateTimeEditRules.Evaluation evaluation = DateTimeEditRules.Evaluate("now", null, Context(), treatEmptyAsInvalid: false);

            Assert.AreEqual(kNow.Date, evaluation.Value);
        }

        [TestMethod]
        public void Evaluate_TimeOnly_KeepsTheDateAlreadyHeld ()
        {
            var current = new DateTime(2026, 3, 4, 8, 0, 0);
            DateTimeTextContext context = Context(includeDate: false, includeTime: true, current: current);

            DateTimeEditRules.Evaluation evaluation = DateTimeEditRules.Evaluate("3pm", null, context, treatEmptyAsInvalid: false);

            Assert.AreEqual(new DateTime(2026, 3, 4, 15, 0, 0), evaluation.Value);
        }

        [TestMethod]
        public void Evaluate_TimeOnlyWithNoValueYet_IsOnToday ()
        {
            DateTimeTextContext context = Context(includeDate: false, includeTime: true);

            DateTimeEditRules.Evaluation evaluation = DateTimeEditRules.Evaluate("3pm", null, context, treatEmptyAsInvalid: false);

            Assert.AreEqual(new DateTime(2026, 11, 25, 15, 0, 0), evaluation.Value);
        }

        [TestMethod]
        public void Evaluate_NeitherDateNorTimeIncluded_EditsBoth ()
        {
            DateTimeTextContext context = Context(includeDate: false, includeTime: false);

            DateTimeEditRules.Evaluation evaluation = DateTimeEditRules.Evaluate("tomorrow 3pm", null, context, treatEmptyAsInvalid: false);

            Assert.AreEqual(new DateTime(2026, 11, 26, 15, 0, 0), evaluation.Value);
        }

        [TestMethod]
        public void Evaluate_WithAResolverOfItsOwn_UsesIt ()
        {
            var resolver = new QuarterResolver();

            DateTimeEditRules.Evaluation mine = DateTimeEditRules.Evaluate("q2", resolver, Context(), treatEmptyAsInvalid: false);
            DateTimeEditRules.Evaluation fallenBack = DateTimeEditRules.Evaluate("tomorrow", resolver, Context(), treatEmptyAsInvalid: false);

            Assert.AreEqual(new DateTime(2026, 4, 1), mine.Value);
            Assert.AreEqual(new DateTime(2026, 11, 26), fallenBack.Value);
        }

        [TestMethod]
        public void Evaluate_ResolverReadsTextAsNoValue_IsEmptyAllTheSame ()
        {
            var resolver = new QuarterResolver();

            DateTimeEditRules.Evaluation allowed = DateTimeEditRules.Evaluate("none", resolver, Context(), treatEmptyAsInvalid: false);
            DateTimeEditRules.Evaluation refused = DateTimeEditRules.Evaluate("none", resolver, Context(), treatEmptyAsInvalid: true);

            Assert.IsTrue(allowed.IsTextValid);
            Assert.IsNull(allowed.Value);
            Assert.IsFalse(refused.IsTextValid);
        }

        [TestMethod]
        public void Format_WithAResolverOfItsOwn_ShowsItsText ()
        {
            Assert.AreEqual("Q4 2026", DateTimeEditRules.Format(kNow, new QuarterResolver(), Context()));
            Assert.AreEqual("11/25/2026", DateTimeEditRules.Format(kNow, null, Context()));
        }

        // ===========[ Picker choices ]========================================
        [TestMethod]
        public void WithPickedDate_KeepsTheTimeOfDayHeld ()
        {
            var current = new DateTime(2026, 11, 25, 17, 45, 0);

            Assert.AreEqual(new DateTime(2026, 12, 1, 17, 45, 0), DateTimeEditRules.WithPickedDate(current, new DateTime(2026, 12, 1, 3, 0, 0)));
            Assert.AreEqual(new DateTime(2026, 12, 1), DateTimeEditRules.WithPickedDate(null, new DateTime(2026, 12, 1)));
        }

        [TestMethod]
        public void WithPickedTimeOfDay_KeepsTheDateHeld ()
        {
            var current = new DateTime(2026, 3, 4, 8, 0, 0);
            var quarterPastNine = new TimeSpan(9, 15, 0);

            Assert.AreEqual(new DateTime(2026, 3, 4, 9, 15, 0), DateTimeEditRules.WithPickedTimeOfDay(current, quarterPastNine, kNow));
            Assert.AreEqual(new DateTime(2026, 11, 25, 9, 15, 0), DateTimeEditRules.WithPickedTimeOfDay(null, quarterPastNine, kNow));
        }

        // ===========[ CalendarMonthGrid ]=====================================
        [TestMethod]
        public void GetDays_AMonth_ShowsSixWeeksStartingOnTheWeeksFirstDay ()
        {
            // November 2026 starts on a Sunday, so a Sunday-first calendar starts right on the 1st
            DateTime[] sundayFirst = CalendarMonthGrid.GetDays(new DateTime(2026, 11, 25), DayOfWeek.Sunday);
            DateTime[] mondayFirst = CalendarMonthGrid.GetDays(new DateTime(2026, 11, 25), DayOfWeek.Monday);

            Assert.AreEqual(CalendarMonthGrid.kDaysShown, sundayFirst.Length);
            Assert.AreEqual(new DateTime(2026, 11, 1), sundayFirst[0]);
            Assert.AreEqual(new DateTime(2026, 12, 12), sundayFirst[CalendarMonthGrid.kDaysShown - 1]);
            Assert.AreEqual(new DateTime(2026, 10, 26), mondayFirst[0]);
            Assert.AreEqual(DayOfWeek.Monday, mondayFirst[0].DayOfWeek);
        }

        [TestMethod]
        public void GetDays_TheLastMonthThereIs_StaysInsideWhatADateHolds ()
        {
            DateTime[] days = CalendarMonthGrid.GetDays(DateTime.MaxValue, DayOfWeek.Sunday);

            Assert.AreEqual(new DateTime(9999, 11, 1), CalendarMonthGrid.ClampDisplayMonth(DateTime.MaxValue));
            Assert.IsTrue(days[CalendarMonthGrid.kDaysShown - 1] <= DateTime.MaxValue.Date);
        }

        [TestMethod]
        public void GetDayNames_StartOnTheWeeksFirstDay ()
        {
            string[] names = CalendarMonthGrid.GetDayNames(kCulture, DayOfWeek.Monday);

            string[] shortestNames = kCulture.DateTimeFormat.ShortestDayNames;
            Assert.AreEqual(shortestNames[(int)DayOfWeek.Monday], names[0]);
            Assert.AreEqual(shortestNames[(int)DayOfWeek.Sunday], names[CalendarMonthGrid.kDaysPerWeek - 1]);
        }

        [TestMethod]
        public void MoveDisplayMonth_AcrossAYearsEnd_ChangesTheYear ()
        {
            Assert.AreEqual(new DateTime(2027, 1, 1), CalendarMonthGrid.MoveDisplayMonth(new DateTime(2026, 12, 15), 1));
            Assert.AreEqual(new DateTime(2025, 12, 1), CalendarMonthGrid.MoveDisplayMonth(new DateTime(2026, 1, 15), -1));
        }

        [TestMethod]
        public void MoveDisplayMonth_PastEitherEnd_StopsAtTheLastMonthShown ()
        {
            Assert.AreEqual(new DateTime(1, 2, 1), CalendarMonthGrid.MoveDisplayMonth(new DateTime(1, 2, 10), -3));
            Assert.AreEqual(new DateTime(9999, 11, 1), CalendarMonthGrid.MoveDisplayMonth(new DateTime(9999, 11, 10), 3));
        }

        [TestMethod]
        public void GetDisplayMonth_YearOrMonthPastWhatThereIs_IsTheNearestThatCanShow ()
        {
            Assert.AreEqual(new DateTime(2031, 4, 1), CalendarMonthGrid.GetDisplayMonth(2031, 4));
            Assert.AreEqual(new DateTime(9999, 11, 1), CalendarMonthGrid.GetDisplayMonth(10000, 12), "The last month shown leaves a month after it");
            Assert.AreEqual(new DateTime(1, 2, 1), CalendarMonthGrid.GetDisplayMonth(0, 1), "The first month shown leaves a month before it");
            Assert.AreEqual(new DateTime(2031, 12, 1), CalendarMonthGrid.GetDisplayMonth(2031, 13));
        }

        [TestMethod]
        public void GetMonthNames_AreTheTwelveOnTheirOwn ()
        {
            string[] names = CalendarMonthGrid.GetMonthNames(CultureInfo.GetCultureInfo("en-US"));

            Assert.AreEqual(12, names.Length);
            Assert.AreEqual("January", names[0]);
            Assert.AreEqual("December", names[11]);
        }

        // ===========[ TimeOfDayRules ]========================================
        [TestMethod]
        public void GetClockHour_OnATwelveHourClock_ReadsTwelveAtMidnightAndNoon ()
        {
            Assert.AreEqual(12, TimeOfDayRules.GetClockHour(new TimeSpan(0, 5, 0), usesTwelveHourClock: true));
            Assert.AreEqual(12, TimeOfDayRules.GetClockHour(new TimeSpan(12, 5, 0), usesTwelveHourClock: true));
            Assert.AreEqual(3, TimeOfDayRules.GetClockHour(new TimeSpan(15, 5, 0), usesTwelveHourClock: true));
            Assert.AreEqual(15, TimeOfDayRules.GetClockHour(new TimeSpan(15, 5, 0), usesTwelveHourClock: false));
        }

        [TestMethod]
        public void UsesTwelveHourClock_FollowsTheCulturesShortTime ()
        {
            Assert.IsTrue(TimeOfDayRules.UsesTwelveHourClock(kCulture));
            Assert.IsFalse(TimeOfDayRules.UsesTwelveHourClock(CultureInfo.GetCultureInfo("de-DE")));
        }

        [TestMethod]
        public void UsesTwelveHourClock_AChosenClockWinsOverTheCultures ()
        {
            CultureInfo german = CultureInfo.GetCultureInfo("de-DE");
            Assert.IsTrue(TimeOfDayRules.UsesTwelveHourClock(eTimeFormat.CurrentCulture, kCulture));
            Assert.IsFalse(TimeOfDayRules.UsesTwelveHourClock(eTimeFormat.CurrentCulture, german));
            Assert.IsFalse(TimeOfDayRules.UsesTwelveHourClock(eTimeFormat.TwentyFourHour, kCulture));
            Assert.IsTrue(TimeOfDayRules.UsesTwelveHourClock(eTimeFormat.TwelveHour, german));
        }

        [TestMethod]
        public void GetShortTimePattern_OnTheCulturesOwnClock_IsTheCulturesPattern ()
        {
            Assert.AreEqual(kCulture.DateTimeFormat.ShortTimePattern, TimeOfDayRules.GetShortTimePattern(eTimeFormat.CurrentCulture, kCulture));
            Assert.AreEqual(kCulture.DateTimeFormat.ShortTimePattern, TimeOfDayRules.GetShortTimePattern(eTimeFormat.TwelveHour, kCulture));
        }

        [TestMethod]
        public void GetShortTimePattern_TwentyFourHourForATwelveHourCulture_DropsTheAmOrPm ()
        {
            Assert.AreEqual("HH:mm", TimeOfDayRules.GetShortTimePattern(eTimeFormat.TwentyFourHour, CultureWithShortTime("h:mm tt")));
            Assert.AreEqual("HH:mm", TimeOfDayRules.GetShortTimePattern(eTimeFormat.TwentyFourHour, CultureWithShortTime("tt h:mm")));
        }

        [TestMethod]
        public void GetShortTimePattern_TwelveHourForATwentyFourHourCulture_AddsTheAmOrPm ()
        {
            Assert.AreEqual("h:mm tt", TimeOfDayRules.GetShortTimePattern(eTimeFormat.TwelveHour, CultureWithShortTime("HH:mm")));
            Assert.AreEqual("h.mm tt", TimeOfDayRules.GetShortTimePattern(eTimeFormat.TwelveHour, CultureWithShortTime("H.mm")));
        }

        [TestMethod]
        public void GetShortTimePattern_QuotedOrEscapedText_IsLeftAsWritten ()
        {
            // The 'h' and the 't' inside the literals are text, not an hour or a designator
            Assert.AreEqual(@"HH:mm 'heute' \t", TimeOfDayRules.GetShortTimePattern(eTimeFormat.TwentyFourHour, CultureWithShortTime(@"h:mm 'heute' \t tt")));
        }

        [TestMethod]
        public void StepHour_PastEitherEndOfTheDay_GoesRoundInsideIt ()
        {
            Assert.AreEqual(new TimeSpan(0, 15, 0), TimeOfDayRules.StepHour(new TimeSpan(23, 15, 0), 1));
            Assert.AreEqual(new TimeSpan(23, 15, 0), TimeOfDayRules.StepHour(new TimeSpan(0, 15, 0), -1));
        }

        [TestMethod]
        public void StepMinute_PastEitherEndOfTheHour_GoesRoundInsideIt ()
        {
            Assert.AreEqual(new TimeSpan(10, 0, 0), TimeOfDayRules.StepMinute(new TimeSpan(10, 59, 0), 1, 1));
            Assert.AreEqual(new TimeSpan(10, 59, 0), TimeOfDayRules.StepMinute(new TimeSpan(10, 0, 0), -1, 1));
        }

        [TestMethod]
        public void StepMinute_CarryingIntoTheHour_MovesTheHourAndGoesRoundTheDay ()
        {
            const int kFiveMinutes = 5;
            Assert.AreEqual(new TimeSpan(11, 0, 0), TimeOfDayRules.StepMinute(new TimeSpan(10, 59, 0), 1, 1, carriesIntoHour: true));
            Assert.AreEqual(new TimeSpan(9, 55, 0), TimeOfDayRules.StepMinute(new TimeSpan(10, 0, 0), -1, kFiveMinutes, carriesIntoHour: true));
            Assert.AreEqual(new TimeSpan(0, 0, 0), TimeOfDayRules.StepMinute(new TimeSpan(23, 59, 0), 1, 1, carriesIntoHour: true));
            Assert.AreEqual(new TimeSpan(23, 59, 0), TimeOfDayRules.StepMinute(new TimeSpan(0, 0, 0), -1, 1, carriesIntoHour: true));
        }

        [TestMethod]
        public void StepMinute_FromBetweenTwoSteps_LandsOnTheNextOneThatWay ()
        {
            const int kFiveMinutes = 5;

            Assert.AreEqual(new TimeSpan(10, 10, 0), TimeOfDayRules.StepMinute(new TimeSpan(10, 7, 0), 1, kFiveMinutes));
            Assert.AreEqual(new TimeSpan(10, 5, 0), TimeOfDayRules.StepMinute(new TimeSpan(10, 7, 0), -1, kFiveMinutes));
        }

        [TestMethod]
        public void ToggleHalfOfDay_MovesTwelveHoursAndBack ()
        {
            var fiveThirtyInTheAfternoon = new TimeSpan(17, 30, 0);
            TimeSpan morning = TimeOfDayRules.ToggleHalfOfDay(fiveThirtyInTheAfternoon);

            Assert.AreEqual(new TimeSpan(5, 30, 0), morning);
            Assert.IsFalse(TimeOfDayRules.IsAfternoon(morning));
            Assert.AreEqual(fiveThirtyInTheAfternoon, TimeOfDayRules.ToggleHalfOfDay(morning));
        }

        [TestMethod]
        public void TryReadHour_OnATwelveHourClock_StaysInTheHalfOfTheDayHeld ()
        {
            var afternoon = new TimeSpan(15, 20, 10);
            var morning = new TimeSpan(9, 20, 10);

            Assert.IsTrue(TimeOfDayRules.TryReadHour("4", afternoon, usesTwelveHourClock: true, out TimeSpan read));
            Assert.AreEqual(new TimeSpan(16, 20, 10), read, "4 in the afternoon is 16:00, and the minute and second stay");
            Assert.IsTrue(TimeOfDayRules.TryReadHour("12", afternoon, usesTwelveHourClock: true, out read));
            Assert.AreEqual(new TimeSpan(12, 20, 10), read, "12 in the afternoon is noon");
            Assert.IsTrue(TimeOfDayRules.TryReadHour("12", morning, usesTwelveHourClock: true, out read));
            Assert.AreEqual(new TimeSpan(0, 20, 10), read, "12 in the morning is midnight");
        }

        [TestMethod]
        public void TryReadHour_PastTwelveOrZero_ReadsAsTheTwentyFourHourClockOnEither ()
        {
            var morning = new TimeSpan(9, 20, 0);

            Assert.IsTrue(TimeOfDayRules.TryReadHour("17", morning, usesTwelveHourClock: true, out TimeSpan read));
            Assert.AreEqual(new TimeSpan(17, 20, 0), read);
            Assert.IsTrue(TimeOfDayRules.TryReadHour("0", new TimeSpan(15, 20, 0), usesTwelveHourClock: true, out read));
            Assert.AreEqual(new TimeSpan(0, 20, 0), read);
            Assert.IsTrue(TimeOfDayRules.TryReadHour("4", new TimeSpan(15, 20, 0), usesTwelveHourClock: false, out read));
            Assert.AreEqual(new TimeSpan(4, 20, 0), read, "On a twenty four hour clock, 4 is 4 in the morning");
        }

        [TestMethod]
        public void TryReadHourAndMinute_TextThatIsNoHourOrMinute_LeavesTheTime ()
        {
            var held = new TimeSpan(9, 20, 0);
            foreach (string text in new[] { "", "24", "-1", "nine", "9.5" })
            {
                Assert.IsFalse(TimeOfDayRules.TryReadHour(text, held, usesTwelveHourClock: true, out TimeSpan read), $"Hour '{text}'");
                Assert.AreEqual(held, read);
            }

            foreach (string text in new[] { "", "60", "-5", "half" })
            {
                Assert.IsFalse(TimeOfDayRules.TryReadMinute(text, held, out TimeSpan read), $"Minute '{text}'");
                Assert.AreEqual(held, read);
            }
        }

        [TestMethod]
        public void TryReadMinute_KeepsTheHourAndSecond ()
        {
            Assert.IsTrue(TimeOfDayRules.TryReadMinute(" 05 ", new TimeSpan(15, 20, 10), out TimeSpan read));
            Assert.AreEqual(new TimeSpan(15, 5, 10), read);
        }

        // ===========[ Helper Methods ]========================================
        // en-US with its short time written as given, for a culture whose clock or layout no installed culture has
        private static CultureInfo CultureWithShortTime (string shortTimePattern)
        {
            var culture = (CultureInfo)kCulture.Clone();
            culture.DateTimeFormat.ShortTimePattern = shortTimePattern;
            return culture;
        }

        private static DateTimeTextContext Context (bool includeDate = true, bool includeTime = false, DateTime? current = null)
        {
            return new DateTimeTextContext(kNow, current, includeDate, includeTime, null, kCulture);
        }

        // ===========[ Subclasses/structs ]====================================
        // A consumer's own resolver: it reads quarters ("q2") and "none", shows a value as its quarter, and leaves
        //  everything else it is asked to read to the default
        private class QuarterResolver : IDateTimeTextResolver
        {
            private const int kMonthsPerQuarter = 3;

            public DateTimeTextResolution Resolve (string text, DateTimeTextContext context)
            {
                string trimmed = text.Trim().ToLowerInvariant();
                if (trimmed == "none")
                {
                    return DateTimeTextResolution.Resolved(null);
                }

                if ((trimmed.Length == 2) && (trimmed[0] == 'q') && Char.IsDigit(trimmed[1]))
                {
                    int quarter = trimmed[1] - '0';
                    return DateTimeTextResolution.Resolved(new DateTime(context.Now.Year, ((quarter - 1) * kMonthsPerQuarter) + 1, 1));
                }

                return DateTimeTextResolver.Default.Resolve(text, context);
            }

            public string Format (DateTime? value, DateTimeTextContext context)
            {
                return (value == null) ? String.Empty : $"Q{((value.Value.Month - 1) / kMonthsPerQuarter) + 1} {value.Value.Year}";
            }
        }
    }
}
