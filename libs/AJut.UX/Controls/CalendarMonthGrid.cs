namespace AJut.UX.Controls
{
    using System;
    using System.Globalization;

    // ===========[ CalendarMonthGrid ]==========================================
    // The days a month calendar shows for one month: six weeks of seven days, starting on the week's first day, with the
    // end of the month before and the start of the month after filling the rows the month doesn't. Six rows whatever the
    // month, so a calendar keeps its height as the months go by. A stack with no calendar of its own (WPF) draws one
    // from this.
    /// <summary>The six weeks of days a month calendar shows for a month, for a stack that draws its own calendar</summary>
    public static class CalendarMonthGrid
    {
        /// <summary>How many days a week has, and so how many columns the calendar has</summary>
        public const int kDaysPerWeek = 7;

        /// <summary>How many weeks are shown, enough for any month from any first day of the week</summary>
        public const int kWeeksShown = 6;

        /// <summary>How many days are shown in all</summary>
        public const int kDaysShown = kDaysPerWeek * kWeeksShown;

        /// <summary>How many months a year has, and so how many the month menu lists</summary>
        public const int kMonthsPerYear = 12;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// The days shown for <paramref name="month"/>'s month, in order, a row of <see cref="kDaysPerWeek"/> at a time.
        /// The month is brought to <see cref="ClampDisplayMonth"/> first, so the days around it are always there to show.
        /// </summary>
        /// <param name="month">Any day in the month to show</param>
        /// <param name="firstDayOfWeek">The day each row starts on</param>
        public static DateTime[] GetDays (DateTime month, DayOfWeek firstDayOfWeek)
        {
            DateTime firstOfMonth = ClampDisplayMonth(month);
            int daysBeforeTheFirst = (kDaysPerWeek + (int)firstOfMonth.DayOfWeek - (int)firstDayOfWeek) % kDaysPerWeek;
            DateTime firstShown = firstOfMonth.AddDays(-daysBeforeTheFirst);

            var days = new DateTime[kDaysShown];
            for (int index = 0; index < kDaysShown; ++index)
            {
                days[index] = firstShown.AddDays(index);
            }

            return days;
        }

        /// <summary>
        /// The names over the calendar's columns, the culture's shortest, starting on <paramref name="firstDayOfWeek"/>.
        /// How short depends on the culture data the runtime uses: on .NET's ICU data, en-US gives a single letter ("M").
        /// </summary>
        public static string[] GetDayNames (CultureInfo culture, DayOfWeek firstDayOfWeek)
        {
            string[] shortestNames = culture.DateTimeFormat.ShortestDayNames;
            var names = new string[kDaysPerWeek];
            for (int index = 0; index < kDaysPerWeek; ++index)
            {
                names[index] = shortestNames[((int)firstDayOfWeek + index) % kDaysPerWeek];
            }

            return names;
        }

        /// <summary>
        /// The first day of the month <paramref name="month"/> is in, kept a month in from either end of what a DateTime
        /// holds, since a calendar shows days from the months either side
        /// </summary>
        public static DateTime ClampDisplayMonth (DateTime month)
        {
            const int kFirstMonthShown = 2;
            const int kLastMonthShown = 11;
            var earliest = new DateTime(DateTime.MinValue.Year, kFirstMonthShown, 1);
            var latest = new DateTime(DateTime.MaxValue.Year, kLastMonthShown, 1);
            var firstOfMonth = new DateTime(month.Year, month.Month, 1);
            if (firstOfMonth < earliest)
            {
                return earliest;
            }

            return (firstOfMonth > latest) ? latest : firstOfMonth;
        }

        /// <summary>
        /// The names of the months, January first, as <paramref name="culture"/> writes them on their own (not the
        /// genitive form some cultures use inside a date), for a calendar's month menu
        /// </summary>
        public static string[] GetMonthNames (CultureInfo culture)
        {
            // MonthNames holds a thirteenth, empty, name for the calendars that have a thirteenth month
            var names = new string[kMonthsPerYear];
            Array.Copy(culture.DateTimeFormat.MonthNames, names, kMonthsPerYear);
            return names;
        }

        /// <summary>
        /// The month <paramref name="monthOfYear"/> (1 to 12) of <paramref name="year"/>, as <see cref="ClampDisplayMonth"/>
        /// gives it, for a calendar told to show a month or a year picked from a list or typed. A year or month past what
        /// there is to show is brought to the nearest one there is.
        /// </summary>
        public static DateTime GetDisplayMonth (int year, int monthOfYear)
        {
            int clampedYear = Math.Clamp(year, DateTime.MinValue.Year, DateTime.MaxValue.Year);
            int clampedMonth = Math.Clamp(monthOfYear, 1, kMonthsPerYear);
            return ClampDisplayMonth(new DateTime(clampedYear, clampedMonth, 1));
        }

        /// <summary>
        /// The month <paramref name="monthCount"/> months on from <paramref name="month"/> (back, when negative), as
        /// <see cref="ClampDisplayMonth"/> gives it, for a calendar's previous and next buttons
        /// </summary>
        public static DateTime MoveDisplayMonth (DateTime month, int monthCount)
        {
            DateTime from = ClampDisplayMonth(month);
            long months = ((long)from.Year * kMonthsPerYear) + (from.Month - 1) + monthCount;
            long year = months / kMonthsPerYear;
            if ((months < 0) || (year < DateTime.MinValue.Year))
            {
                return ClampDisplayMonth(DateTime.MinValue);
            }

            if (year > DateTime.MaxValue.Year)
            {
                return ClampDisplayMonth(DateTime.MaxValue);
            }

            return ClampDisplayMonth(new DateTime((int)year, (int)(months % kMonthsPerYear) + 1, 1));
        }
    }
}
