// AJut.Core leaves nullable reference types off, and this code is written with them on
#nullable enable

namespace AJut.DateAndTime
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    // ===========[ DateTimeTextResolver ]=======================================
    // The resolver typed text is read with when no other is given, as it is by every DateTimeEditIngressControl given none.
    // Text is tried three ways, and the first that reads it wins:
    //  1. The display format, exactly, so text shown in it always reads back as the same value
    //  2. Phrases, in English whatever the culture, all counted from the context's Now:
    //       now, today, tomorrow, yesterday
    //       friday, this friday       the first Friday after today
    //       next friday               Friday of next week
    //       last friday               the latest Friday before today
    //       next week, last month     a week, a month or a year either way
    //       in two weeks, in 3 days   also "3 days from now" and "3 days ago"
    //       +3d, -2w, +1mo, +1y, +4h, +30min
    //     Any of them can end in a time of day: "friday 3pm", "tomorrow at 15:30", "in two weeks at noon". A time of day
    //     alone ("3pm", "noon") is today at that time.
    //  3. Whatever the culture, and then the invariant culture, reads as a date and time (2026-11-25, 11/25/2026, Nov 25)
    //
    // A phrase with no time of day in it keeps the time of the value already held, so "tomorrow" moves a date and time a
    // day on rather than to midnight.
    /// <summary>
    /// The resolver used when no other is given: it reads the display format, plain phrases such as "in two weeks",
    /// "friday", and "+3d", and anything the culture reads as a date and time
    /// </summary>
    public class DateTimeTextResolver : IDateTimeTextResolver
    {
        // The times of day a phrase can end in, or be: 3pm, 3 pm, 3:30pm, 3:30 pm, 15:30 and the same with seconds
        private static readonly string[] kTimeOfDayFormats = { "htt", "h tt", "h:mmtt", "h:mm tt", "H:mm", "h:mm:sstt", "h:mm:ss tt", "H:mm:ss" };
        private static readonly char[] kWordSeparators = { ' ', '\t', ',' };

        // The most of any unit a phrase can move by, which keeps the arithmetic far inside what a long of ticks holds
        private const int kLargestOffsetAmount = 1000000;
        private const int kDaysPerWeek = 7;
        private const int kMonthsPerYear = 12;

        private static readonly Dictionary<string, int> kNumberWords = new Dictionary<string, int>
        {
            { "a", 1 }, { "an", 1 }, { "one", 1 }, { "two", 2 }, { "three", 3 }, { "four", 4 }, { "five", 5 },
            { "six", 6 }, { "seven", 7 }, { "eight", 8 }, { "nine", 9 }, { "ten", 10 }, { "eleven", 11 }, { "twelve", 12 },
            { "thirteen", 13 }, { "fourteen", 14 }, { "fifteen", 15 }, { "sixteen", 16 }, { "seventeen", 17 },
            { "eighteen", 18 }, { "nineteen", 19 }, { "twenty", 20 },
        };

        private static readonly Dictionary<string, DayOfWeek> kWeekdays = new Dictionary<string, DayOfWeek>
        {
            { "sunday", DayOfWeek.Sunday }, { "sun", DayOfWeek.Sunday },
            { "monday", DayOfWeek.Monday }, { "mon", DayOfWeek.Monday },
            { "tuesday", DayOfWeek.Tuesday }, { "tue", DayOfWeek.Tuesday }, { "tues", DayOfWeek.Tuesday },
            { "wednesday", DayOfWeek.Wednesday }, { "wed", DayOfWeek.Wednesday },
            { "thursday", DayOfWeek.Thursday }, { "thu", DayOfWeek.Thursday }, { "thur", DayOfWeek.Thursday }, { "thurs", DayOfWeek.Thursday },
            { "friday", DayOfWeek.Friday }, { "fri", DayOfWeek.Friday },
            { "saturday", DayOfWeek.Saturday }, { "sat", DayOfWeek.Saturday },
        };

        // "m" is not here: it is months where a date is edited and minutes where only a time is, see TryGetUnit
        private static readonly Dictionary<string, eOffsetUnit> kUnits = new Dictionary<string, eOffsetUnit>
        {
            { "min", eOffsetUnit.Minute }, { "mins", eOffsetUnit.Minute }, { "minute", eOffsetUnit.Minute }, { "minutes", eOffsetUnit.Minute },
            { "h", eOffsetUnit.Hour }, { "hr", eOffsetUnit.Hour }, { "hrs", eOffsetUnit.Hour }, { "hour", eOffsetUnit.Hour }, { "hours", eOffsetUnit.Hour },
            { "d", eOffsetUnit.Day }, { "day", eOffsetUnit.Day }, { "days", eOffsetUnit.Day },
            { "w", eOffsetUnit.Week }, { "wk", eOffsetUnit.Week }, { "wks", eOffsetUnit.Week }, { "week", eOffsetUnit.Week }, { "weeks", eOffsetUnit.Week },
            { "mo", eOffsetUnit.Month }, { "mos", eOffsetUnit.Month }, { "month", eOffsetUnit.Month }, { "months", eOffsetUnit.Month },
            { "y", eOffsetUnit.Year }, { "yr", eOffsetUnit.Year }, { "yrs", eOffsetUnit.Year }, { "year", eOffsetUnit.Year }, { "years", eOffsetUnit.Year },
        };

        // ===========[ Properties ]============================================
        /// <summary>The one used when no other resolver is given, and the one to fall back to from another</summary>
        public static DateTimeTextResolver Default { get; } = new DateTimeTextResolver();

        // ===========[ IDateTimeTextResolver Implementations ]=================
        /// <inheritdoc/>
        public virtual DateTimeTextResolution Resolve (string text, DateTimeTextContext context)
        {
            string trimmed = (text ?? String.Empty).Trim();
            if (trimmed.Length == 0)
            {
                return DateTimeTextResolution.Resolved(null);
            }

            // 1. The display format, so what was shown reads back as what it was
            if (TryParseDisplayFormat(trimmed, context, out DateTime exact))
            {
                return DateTimeTextResolution.Resolved(exact);
            }

            // 2. Phrases
            if (TryResolvePhrase(trimmed, context, out DateTime phrased))
            {
                return DateTimeTextResolution.Resolved(phrased);
            }

            // 3. Whatever reads as a date and time
            if (DateTime.TryParse(trimmed, context.Culture, DateTimeStyles.AllowWhiteSpaces, out DateTime parsed)
                || DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
            {
                return DateTimeTextResolution.Resolved(parsed);
            }

            return DateTimeTextResolution.Failed($"'{trimmed}' isn't {DescribeWhatIsEdited(context)}");
        }

        /// <inheritdoc/>
        public virtual string Format (DateTime? value, DateTimeTextContext context)
        {
            if (value == null)
            {
                return String.Empty;
            }

            if (String.IsNullOrEmpty(context.DisplayFormat))
            {
                return value.Value.ToString(GetDefaultFormat(context), context.Culture);
            }

            // A display format can be anything a consumer hands over, including one typed in a box as it is being typed:
            //  "h" alone, a lone "%" or a trailing "\" are not formats, and DateTime.ToString throws FormatException for
            //  them. .NET has no way to ask whether a date and time format is good short of using it (TryFormat throws for
            //  a bad format too, it only reports a short buffer), so a bad one is caught here and the value is shown in the
            //  culture's own form until the format is fixed.
            try
            {
                return value.Value.ToString(context.DisplayFormat, context.Culture);
            }
            catch (FormatException)
            {
                return value.Value.ToString(GetDefaultFormat(context), context.Culture);
            }
        }

        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// The format a value is shown in when no display format is given: the culture's short date, its short time, or both.
        /// A time on a clock other than the culture's (<see cref="DateTimeTextContext.TimeFormat"/>) takes the culture's
        /// short time moved to that clock, as <see cref="TimeOfDayRules.GetShortTimePattern"/> gives it.
        /// </summary>
        public static string GetDefaultFormat (DateTimeTextContext context)
        {
            const string kShortDate = "d";
            const string kShortTime = "t";
            const string kShortDateAndTime = "g";
            if (!context.ShowsTime)
            {
                return kShortDate;
            }

            if (TimeOfDayRules.UsesTwelveHourClock(context.TimeFormat, context.Culture) != TimeOfDayRules.UsesTwelveHourClock(context.Culture))
            {
                // "g" is the short date and the short time with a space between, so this is "g" on the other clock
                string shortTime = TimeOfDayRules.GetShortTimePattern(context.TimeFormat, context.Culture);
                return context.ShowsDate ? $"{context.Culture.DateTimeFormat.ShortDatePattern} {shortTime}" : shortTime;
            }

            return context.ShowsDate ? kShortDateAndTime : kShortTime;
        }

        // ===========[ Helper Methods ]========================================
        // Reads text in the display format, when there is one. DateTime.TryParseExact returns false for text that doesn't
        //  match, but throws FormatException for a format .NET can't use ("h" alone, a lone "%"), as ToString does in
        //  Format, so a bad format is caught here and the text is read every other way instead.
        private static bool TryParseDisplayFormat (string text, DateTimeTextContext context, out DateTime parsed)
        {
            parsed = default;
            if (String.IsNullOrEmpty(context.DisplayFormat))
            {
                return false;
            }

            try
            {
                return DateTime.TryParseExact(text, context.DisplayFormat, context.Culture, DateTimeStyles.AllowWhiteSpaces, out parsed);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static string DescribeWhatIsEdited (DateTimeTextContext context)
        {
            if (!context.ShowsTime)
            {
                return "a date";
            }

            return context.ShowsDate ? "a date or time" : "a time";
        }

        private static bool TryResolvePhrase (string text, DateTimeTextContext context, out DateTime resolved)
        {
            resolved = default;
            var words = new List<string>(text.ToLowerInvariant().Split(kWordSeparators, StringSplitOptions.RemoveEmptyEntries));

            // 1. Take a time of day off the end, with the "at" in front of it
            TimeSpan? timeOfDay = TakeTimeOfDayOffTheEnd(words);
            if (timeOfDay != null
                && (words.Count > 0)
                && ((words[words.Count - 1] == "at") || (words[words.Count - 1] == "@")))
            {
                words.RemoveAt(words.Count - 1);
            }

            // 2. What is left is the day. Nothing left is a time of day alone, which is today's.
            DateTime day = context.Now.Date;
            bool carriesTimeOfDay = false;
            if (words.Count > 0 && !TryResolveDayPhrase(words, context, out day, out carriesTimeOfDay))
            {
                return false;
            }

            if ((words.Count == 0) && (timeOfDay == null))
            {
                return false;
            }

            // 3. The time of day: the one given, the one the phrase works out to ("now", "+4h"), or else the time of the
            //    value already held
            if (timeOfDay != null)
            {
                resolved = day.Date + timeOfDay.Value;
            }
            else if (carriesTimeOfDay)
            {
                resolved = day;
            }
            else
            {
                resolved = day.Date + (context.Current?.TimeOfDay ?? TimeSpan.Zero);
            }

            return true;
        }

        // The time of day the words end in, removed from them, or null when they end in none. Two words ("3 pm") are
        //  tried before one, so the "3" isn't left behind as part of the day.
        private static TimeSpan? TakeTimeOfDayOffTheEnd (List<string> words)
        {
            const int kMostWordsInATimeOfDay = 2;
            for (int wordCount = Math.Min(kMostWordsInATimeOfDay, words.Count); wordCount >= 1; --wordCount)
            {
                string candidate = String.Join(" ", words.GetRange(words.Count - wordCount, wordCount));
                if (TryReadTimeOfDay(candidate, out TimeSpan timeOfDay))
                {
                    words.RemoveRange(words.Count - wordCount, wordCount);
                    return timeOfDay;
                }
            }

            return null;
        }

        private static bool TryReadTimeOfDay (string text, out TimeSpan timeOfDay)
        {
            const int kNoonHour = 12;
            switch (text)
            {
                case "noon":
                    timeOfDay = TimeSpan.FromHours(kNoonHour);
                    return true;

                case "midnight":
                    timeOfDay = TimeSpan.Zero;
                    return true;
            }

            if (DateTime.TryParseExact(text, kTimeOfDayFormats, CultureInfo.InvariantCulture, DateTimeStyles.NoCurrentDateDefault, out DateTime parsed))
            {
                timeOfDay = parsed.TimeOfDay;
                return true;
            }

            timeOfDay = default;
            return false;
        }

        private static bool TryResolveDayPhrase (List<string> words, DateTimeTextContext context, out DateTime day, out bool carriesTimeOfDay)
        {
            DateTime today = context.Now.Date;
            day = today;
            carriesTimeOfDay = false;

            if (words.Count == 1)
            {
                switch (words[0])
                {
                    case "now":
                        day = context.Now;
                        carriesTimeOfDay = true;
                        return true;

                    case "today":
                    case "tod":
                        return true;

                    case "tomorrow":
                    case "tmrw":
                    case "tom":
                        return TryOffset(today, 1, eOffsetUnit.Day, out day);

                    case "yesterday":
                        return TryOffset(today, -1, eOffsetUnit.Day, out day);
                }

                if (kWeekdays.TryGetValue(words[0], out DayOfWeek weekday))
                {
                    return TryOffset(today, CountDaysToComing(weekday, today), eOffsetUnit.Day, out day);
                }
            }

            if (words.Count == 2)
            {
                switch (words[0])
                {
                    case "this":
                        return kWeekdays.TryGetValue(words[1], out DayOfWeek thisWeekday)
                            && TryOffset(today, CountDaysToComing(thisWeekday, today), eOffsetUnit.Day, out day);

                    case "next":
                        return TryResolveNextOrLast(words[1], 1, today, context.Culture, out day);

                    case "last":
                        return TryResolveNextOrLast(words[1], -1, today, context.Culture, out day);
                }
            }

            return TryResolveOffsetPhrase(words, context, out day, out carriesTimeOfDay);
        }

        // "next friday", "next week", "last month": direction is 1 for next and -1 for last
        private static bool TryResolveNextOrLast (string what, int direction, DateTime today, CultureInfo culture, out DateTime day)
        {
            day = today;
            if (kWeekdays.TryGetValue(what, out DayOfWeek weekday))
            {
                if (direction < 0)
                {
                    // The latest one before today, so a week back when today is that day
                    int daysBack = CountDaysBetween(weekday, today.DayOfWeek);
                    return TryOffset(today, -((daysBack == 0) ? kDaysPerWeek : daysBack), eOffsetUnit.Day, out day);
                }

                // The one in next week, where a week starts on the culture's first day: on a Saturday, "next friday" is
                //  six days out, and on a Wednesday nine
                DayOfWeek firstDayOfWeek = culture.DateTimeFormat.FirstDayOfWeek;
                int daysIntoThisWeek = CountDaysBetween(firstDayOfWeek, today.DayOfWeek);
                int daysIntoThatWeek = CountDaysBetween(firstDayOfWeek, weekday);
                return TryOffset(today, kDaysPerWeek - daysIntoThisWeek + daysIntoThatWeek, eOffsetUnit.Day, out day);
            }

            switch (what)
            {
                case "week": return TryOffset(today, direction, eOffsetUnit.Week, out day);
                case "month": return TryOffset(today, direction, eOffsetUnit.Month, out day);
                case "year": return TryOffset(today, direction, eOffsetUnit.Year, out day);
            }

            return false;
        }

        // "in two weeks", "3 days from now", "3 days ago", "+3d", "-2 weeks". A number and unit with nothing saying which
        //  way they go ("3 days") is not one of these.
        private static bool TryResolveOffsetPhrase (List<string> words, DateTimeTextContext context, out DateTime day, out bool carriesTimeOfDay)
        {
            day = context.Now.Date;
            carriesTimeOfDay = false;

            // 1. Which way it goes, from the words around the amount
            int first = 0;
            int count = words.Count;
            int sign = 1;
            bool saysWhichWay = false;
            if (words[first] == "in")
            {
                ++first;
                --count;
                saysWhichWay = true;
            }

            if ((count >= 1) && (words[first + count - 1] == "ago"))
            {
                --count;
                sign = -1;
                saysWhichWay = true;
            }
            else if ((count >= 2) && (words[first + count - 2] == "from") && (words[first + count - 1] == "now"))
            {
                count -= 2;
                saysWhichWay = true;
            }

            // 2. The amount and its unit, as two words ("3 days") or run together ("3d")
            string amountText;
            string unitText;
            if (count == 2)
            {
                amountText = words[first];
                unitText = words[first + 1];
            }
            else if (count == 1)
            {
                string joined = words[first];
                int unitStart = 0;
                while ((unitStart < joined.Length) && !Char.IsLetter(joined[unitStart]))
                {
                    ++unitStart;
                }

                amountText = joined.Substring(0, unitStart);
                unitText = joined.Substring(unitStart);
            }
            else
            {
                return false;
            }

            // 3. A sign on the amount says which way too
            if (amountText.StartsWith("+", StringComparison.Ordinal) || amountText.StartsWith("-", StringComparison.Ordinal))
            {
                if (amountText[0] == '-')
                {
                    sign = -sign;
                }

                amountText = amountText.Substring(1);
                saysWhichWay = true;
            }

            if (!saysWhichWay
                || !TryReadAmount(amountText, out int amount)
                || !TryGetUnit(unitText, context, out eOffsetUnit unit))
            {
                return false;
            }

            // 4. Hours and minutes count from this moment, and everything longer from today
            carriesTimeOfDay = (unit == eOffsetUnit.Minute) || (unit == eOffsetUnit.Hour);
            return TryOffset(carriesTimeOfDay ? context.Now : context.Now.Date, sign * amount, unit, out day);
        }

        private static bool TryReadAmount (string text, out int amount)
        {
            if (kNumberWords.TryGetValue(text, out amount))
            {
                return true;
            }

            return Int32.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out amount) && (amount <= kLargestOffsetAmount);
        }

        private static bool TryGetUnit (string text, DateTimeTextContext context, out eOffsetUnit unit)
        {
            // A lone "m" is months wherever a date is edited, since that is the unit a date moves by, and minutes where
            //  only a time is
            if (text == "m")
            {
                unit = context.ShowsDate ? eOffsetUnit.Month : eOffsetUnit.Minute;
                return true;
            }

            return kUnits.TryGetValue(text, out unit);
        }

        // How many days on from one day of the week the next is, 0 to 6
        private static int CountDaysBetween (DayOfWeek from, DayOfWeek to) => (kDaysPerWeek + (int)to - (int)from) % kDaysPerWeek;

        // How many days until the first such day after today, 1 to 7: today is never the answer, "today" is how to say that
        private static int CountDaysToComing (DayOfWeek weekday, DateTime today)
        {
            int days = CountDaysBetween(today.DayOfWeek, weekday);
            return (days == 0) ? kDaysPerWeek : days;
        }

        // Moves a date and time by an amount of a unit. False when that lands outside what a DateTime holds, checked
        //  ahead rather than by catching what DateTime's own Add methods throw.
        private static bool TryOffset (DateTime from, long amount, eOffsetUnit unit, out DateTime result)
        {
            result = from;
            if ((unit == eOffsetUnit.Month) || (unit == eOffsetUnit.Year))
            {
                // Months are counted from year 0's January, moved, and split back into a year and month. A day past the
                //  end of the month it lands in (the 31st into February) becomes that month's last day.
                long months = ((long)from.Year * kMonthsPerYear) + (from.Month - 1) + (amount * ((unit == eOffsetUnit.Year) ? kMonthsPerYear : 1));
                long year = months / kMonthsPerYear;
                if ((months < 0) || (year < DateTime.MinValue.Year) || (year > DateTime.MaxValue.Year))
                {
                    return false;
                }

                int month = (int)(months % kMonthsPerYear) + 1;
                int dayOfMonth = Math.Min(from.Day, DateTime.DaysInMonth((int)year, month));
                result = new DateTime((int)year, month, dayOfMonth, 0, 0, 0, from.Kind) + from.TimeOfDay;
                return true;
            }

            long ticksPerUnit = unit switch
            {
                eOffsetUnit.Minute => TimeSpan.TicksPerMinute,
                eOffsetUnit.Hour => TimeSpan.TicksPerHour,
                eOffsetUnit.Week => TimeSpan.TicksPerDay * kDaysPerWeek,
                _ => TimeSpan.TicksPerDay,
            };

            long ticks = from.Ticks + (amount * ticksPerUnit);
            if ((ticks < DateTime.MinValue.Ticks) || (ticks > DateTime.MaxValue.Ticks))
            {
                return false;
            }

            result = new DateTime(ticks, from.Kind);
            return true;
        }

        // ===========[ Subclasses/structs ]====================================
        private enum eOffsetUnit
        {
            /// <summary>Counted from this moment</summary>
            Minute,

            /// <summary>Counted from this moment</summary>
            Hour,

            /// <summary>Counted from today</summary>
            Day,

            /// <summary>Seven days, counted from today</summary>
            Week,

            /// <summary>The same day of the month that many months on, or that month's last day when it has no such day</summary>
            Month,

            /// <summary>The same day that many years on, with February 29th becoming the 28th outside a leap year</summary>
            Year,
        }
    }
}
