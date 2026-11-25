namespace AJut.UX.Controls
{
    using System;
    using System.Globalization;
    using System.Text;

    // ===========[ TimeOfDayRules ]=============================================
    // The arithmetic behind a time of day editor that steps its hour and minute with buttons: how a time reads on a
    // twelve hour clock, where a step up or down lands, and what an hour or a minute typed over the one shown comes to.
    // Every step stays inside the one day, and a step of the minute either stays inside the hour, as a spinner's does,
    // or carries into the hour, as a clock does. A stack with no time picker of its own (WPF) builds one on this.
    /// <summary>
    /// How a time of day reads on a clock, where stepping its hour or minute lands, and what a typed hour or minute comes
    /// to, for a stack that draws its own time editor
    /// </summary>
    public static class TimeOfDayRules
    {
        private const int kHoursPerDay = 24;
        private const int kHoursPerHalfDay = 12;
        private const int kMinutesPerHour = 60;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Whether <paramref name="culture"/> writes its short time on a twelve hour clock (3:05 PM) rather than a twenty four hour one (15:05)</summary>
        public static bool UsesTwelveHourClock (CultureInfo culture)
        {
            // In a .NET time format, 'h' is the hour on a twelve hour clock and 'H' on a twenty four hour one
            const char kTwelveHourClockHour = 'h';
            return culture.DateTimeFormat.ShortTimePattern.IndexOf(kTwelveHourClockHour) != -1;
        }

        /// <summary>
        /// Whether a time is shown and picked on a twelve hour clock for <paramref name="timeFormat"/>: the clock
        /// <paramref name="culture"/> writes its short time on, unless one is chosen
        /// </summary>
        public static bool UsesTwelveHourClock (eTimeFormat timeFormat, CultureInfo culture)
        {
            return timeFormat switch
            {
                eTimeFormat.TwelveHour => true,
                eTimeFormat.TwentyFourHour => false,
                _ => UsesTwelveHourClock(culture),
            };
        }

        /// <summary>
        /// <paramref name="culture"/>'s short time pattern on the clock <paramref name="timeFormat"/> asks for. The
        /// culture's own pattern when that is its clock already; otherwise the same pattern with its hour moved to the other
        /// clock: on twenty four hours, a two digit hour and no AM or PM ("h:mm tt" becomes "HH:mm"), and on twelve, an hour
        /// with no leading zero and the AM or PM after it ("HH:mm" becomes "h:mm tt").
        /// </summary>
        public static string GetShortTimePattern (eTimeFormat timeFormat, CultureInfo culture)
        {
            string pattern = culture.DateTimeFormat.ShortTimePattern;
            bool usesTwelveHourClock = UsesTwelveHourClock(timeFormat, culture);
            if (usesTwelveHourClock == UsesTwelveHourClock(culture))
            {
                return pattern;
            }

            return usesTwelveHourClock ? MoveToTwelveHourClock(pattern) : MoveToTwentyFourHourClock(pattern);
        }

        /// <summary>
        /// The hour a clock shows for <paramref name="timeOfDay"/>: 12, then 1 to 11, on a twelve hour clock, and 0 to 23
        /// otherwise
        /// </summary>
        public static int GetClockHour (TimeSpan timeOfDay, bool usesTwelveHourClock)
        {
            if (!usesTwelveHourClock)
            {
                return timeOfDay.Hours;
            }

            int hourOfHalfDay = timeOfDay.Hours % kHoursPerHalfDay;
            return (hourOfHalfDay == 0) ? kHoursPerHalfDay : hourOfHalfDay;
        }

        /// <summary>Whether <paramref name="timeOfDay"/> is in the second half of the day, noon onwards</summary>
        public static bool IsAfternoon (TimeSpan timeOfDay) => timeOfDay.Hours >= kHoursPerHalfDay;

        /// <summary>
        /// <paramref name="timeOfDay"/> moved by <paramref name="hours"/> hours (back, when negative), going round from
        /// the end of the day to its start rather than into another day
        /// </summary>
        public static TimeSpan StepHour (TimeSpan timeOfDay, int hours)
        {
            int hour = Wrap(timeOfDay.Hours + hours, kHoursPerDay);
            return new TimeSpan(hour, timeOfDay.Minutes, timeOfDay.Seconds);
        }

        /// <summary>
        /// <paramref name="timeOfDay"/> with its minute moved <paramref name="steps"/> steps of
        /// <paramref name="minuteIncrement"/> minutes (back, when negative). The minute lands on a multiple of the
        /// increment. It goes round inside the hour (stepping up from 10:59 is 10:00), or with
        /// <paramref name="carriesIntoHour"/> it moves the hour as a clock does (stepping up from 10:59 is 11:00), going
        /// round inside the day either way.
        /// </summary>
        public static TimeSpan StepMinute (TimeSpan timeOfDay, int steps, int minuteIncrement, bool carriesIntoHour = false)
        {
            const int kMinutesPerDay = kHoursPerDay * kMinutesPerHour;
            int increment = Math.Clamp(minuteIncrement, 1, kMinutesPerHour);

            // A minute between two multiples of the increment first steps onto the nearest one in the way it is going
            int minute = timeOfDay.Minutes;
            int pastMultiple = minute % increment;
            if (pastMultiple != 0)
            {
                minute -= pastMultiple;
                if (steps < 0)
                {
                    ++steps;
                }
            }

            if (carriesIntoHour)
            {
                int minuteOfDay = Wrap((timeOfDay.Hours * kMinutesPerHour) + minute + (steps * increment), kMinutesPerDay);
                return new TimeSpan(minuteOfDay / kMinutesPerHour, minuteOfDay % kMinutesPerHour, timeOfDay.Seconds);
            }

            minute = Wrap(minute + (steps * increment), kMinutesPerHour);
            return new TimeSpan(timeOfDay.Hours, minute, timeOfDay.Seconds);
        }

        /// <summary><paramref name="timeOfDay"/> at the same time in the other half of the day: 3:05 AM for 3:05 PM</summary>
        public static TimeSpan ToggleHalfOfDay (TimeSpan timeOfDay) => StepHour(timeOfDay, kHoursPerHalfDay);

        /// <summary>
        /// Reads an hour typed over the hour <paramref name="timeOfDay"/> shows. On a twelve hour clock, 1 to 12 stay in
        /// the half of the day already held (12 is noon in the afternoon and midnight in the morning), and 0 or 13 to 23
        /// are read as a twenty four hour clock reads them, on either clock. Returns false, with
        /// <paramref name="result"/> left as <paramref name="timeOfDay"/>, for text that isn't an hour.
        /// </summary>
        public static bool TryReadHour (string? text, TimeSpan timeOfDay, bool usesTwelveHourClock, out TimeSpan result)
        {
            result = timeOfDay;
            if (!TryReadWholeNumber(text, out int hour) || (hour >= kHoursPerDay))
            {
                return false;
            }

            if (usesTwelveHourClock && (hour >= 1) && (hour <= kHoursPerHalfDay))
            {
                // 12 is the first hour of its half of the day, so it reads as that half's 0
                hour %= kHoursPerHalfDay;
                if (IsAfternoon(timeOfDay))
                {
                    hour += kHoursPerHalfDay;
                }
            }

            result = new TimeSpan(hour, timeOfDay.Minutes, timeOfDay.Seconds);
            return true;
        }

        /// <summary>
        /// Reads a minute, 0 to 59, typed over the minute <paramref name="timeOfDay"/> shows. Returns false, with
        /// <paramref name="result"/> left as <paramref name="timeOfDay"/>, for text that isn't a minute.
        /// </summary>
        public static bool TryReadMinute (string? text, TimeSpan timeOfDay, out TimeSpan result)
        {
            result = timeOfDay;
            if (!TryReadWholeNumber(text, out int minute) || (minute >= kMinutesPerHour))
            {
                return false;
            }

            result = new TimeSpan(timeOfDay.Hours, minute, timeOfDay.Seconds);
            return true;
        }

        // ===========[ Helper Methods ]========================================
        // The remainder that is never negative, so stepping back from 0 comes round to the top
        private static int Wrap (int value, int size) => ((value % size) + size) % size;

        // A .NET custom time pattern is specifiers between literals: quoted text ('...' or "...") and characters escaped
        //  with a backslash are copied as they are, and only what is outside them is a specifier. In the hour, 'h' is the
        //  twelve hour clock and 'H' the twenty four hour one, and 't' is the AM or PM designator ("tt" in full).
        private static string MoveToTwentyFourHourClock (string pattern)
        {
            const string kTwoDigitHour = "HH";
            var moved = new StringBuilder(pattern.Length);
            ForEachSpecifierRun(pattern, moved, _OnSpecifierRun);

            // The designator taken out leaves the space that set it apart ("h:mm tt", "tt h:mm")
            return moved.ToString().Trim().Replace("  ", " ");

            void _OnSpecifierRun (char specifier, int runLength)
            {
                if (specifier == 'h')
                {
                    moved.Append(kTwoDigitHour);
                }
                else if (specifier != 't')
                {
                    moved.Append(specifier, runLength);
                }
            }
        }

        private static string MoveToTwelveHourClock (string pattern)
        {
            const string kHourWithNoLeadingZero = "h";
            const string kDesignatorAfter = " tt";
            var moved = new StringBuilder(pattern.Length + kDesignatorAfter.Length);
            bool hasDesignator = false;
            ForEachSpecifierRun(pattern, moved, _OnSpecifierRun);
            if (!hasDesignator)
            {
                moved.Append(kDesignatorAfter);
            }

            return moved.ToString();

            void _OnSpecifierRun (char specifier, int runLength)
            {
                if (specifier == 'H')
                {
                    moved.Append(kHourWithNoLeadingZero);
                    return;
                }

                hasDesignator |= (specifier == 't');
                moved.Append(specifier, runLength);
            }
        }

        // Copies the quoted and escaped literals of a custom pattern into the builder, and hands every other run of one
        //  character ("HH", ":", "mm", " ", "tt") to onSpecifierRun to write as it likes
        private static void ForEachSpecifierRun (string pattern, StringBuilder literals, Action<char, int> onSpecifierRun)
        {
            int index = 0;
            while (index < pattern.Length)
            {
                char current = pattern[index];
                if ((current == '\'') || (current == '"'))
                {
                    int close = pattern.IndexOf(current, index + 1);
                    int end = (close == -1) ? pattern.Length : close + 1;
                    literals.Append(pattern, index, end - index);
                    index = end;
                }
                else if (current == '\\')
                {
                    int end = Math.Min(index + 2, pattern.Length);
                    literals.Append(pattern, index, end - index);
                    index = end;
                }
                else
                {
                    int runEnd = index + 1;
                    while ((runEnd < pattern.Length) && (pattern[runEnd] == current))
                    {
                        ++runEnd;
                    }

                    onSpecifierRun(current, runEnd - index);
                    index = runEnd;
                }
            }
        }

        // Digits alone, with space around them allowed: no sign, no separators, no decimals
        private static bool TryReadWholeNumber (string? text, out int value)
        {
            return Int32.TryParse(text, NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out value);
        }
    }
}
