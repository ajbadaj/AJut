namespace AJut.Text
{
    using System;
    using System.Globalization;

    /// <summary>
    /// How formulas print and round numbers. Formulas do spreadsheet math, so these follow what a spreadsheet shows
    /// rather than what .NET does by default.
    /// </summary>
    internal static class FormulaNumbers
    {
        /// <summary>
        /// 2^53 - 1, the largest whole number a formula holds. A double holds 2^53 exactly too, but 2^53 + 1 rounds to it,
        /// so a result of 2^53 can't be told from one that went past, and is an error with everything beyond it.
        /// </summary>
        public const double kLargestExactWhole = 9007199254740991;

        // A spreadsheet shows 15 significant digits, which is what hides 0.1 + 0.2 coming out as 0.30000000000000004
        private const string kShownFormat = "G15";

        // ===========[ Public Interface Methods ]=============================

        public static bool IsExactWhole (double value) => Math.Abs(value) <= kLargestExactWhole;

        /// <summary>
        /// Prints a number the way a formula does: culture invariant, a whole number with no decimal point, and anything else
        /// with at most 15 significant digits.
        /// </summary>
        public static string Format (double value)
        {
            // -0 is a thing a double can be, and nothing anybody wants to read
            if (value == 0)
            {
                return "0";
            }

            if ((Math.Abs(value) <= kLargestExactWhole) && (value == Math.Floor(value)))
            {
                return ((long)value).ToString(CultureInfo.InvariantCulture);
            }

            return value.ToString(kShownFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Rounds to a whole number the way <paramref name="rounding"/> says, starting from the number as it's shown.
        /// </summary>
        /// <remarks>
        /// Rounding works on the number as it prints, not its binary value. A double can't hold most decimal fractions
        /// exactly: 2.345 is held as 2.34499999999999997..., and (0.7 + 0.1) * 10 comes out as 7.999999999999999. Whoever
        /// typed the formula sees 2.345 and 8, so rounding the binary value would give 2.34 and floor would give 7, both of
        /// which read as wrong. So the number is first taken at the 15 significant digits a spreadsheet shows, as a decimal
        /// (which holds those digits exactly), and rounded from there. A whole number is left as it is, and a number a
        /// decimal can't hold is rounded by its binary value.
        /// </remarks>
        public static double RoundWhole (double value, eRounding rounding)
        {
            // A whole number has nothing to round, and every double past 2^52 is whole
            if (!Double.IsFinite(value) || (value == Math.Floor(value)))
            {
                return value;
            }

            if (!TryTakeShown(value, out decimal shown))
            {
                return rounding switch
                {
                    eRounding.Truncate => Math.Truncate(value),
                    eRounding.Floor => Math.Floor(value),
                    eRounding.Ceiling => Math.Ceiling(value),
                    _ => Math.Round(value, MidpointRounding.AwayFromZero),
                };
            }

            decimal rounded = rounding switch
            {
                eRounding.Truncate => Math.Truncate(shown),
                eRounding.Floor => Math.Floor(shown),
                eRounding.Ceiling => Math.Ceiling(shown),
                _ => Math.Round(shown, MidpointRounding.AwayFromZero),
            };

            return (double)rounded;
        }

        /// <summary>
        /// Rounds to <paramref name="places"/> decimal places, halves away from zero, from the number as it's shown (see
        /// <see cref="RoundWhole"/> for why).
        /// </summary>
        public static double RoundToPlaces (double value, int places)
        {
            if (!TryTakeShown(value, out decimal shown))
            {
                return Double.IsFinite(value) ? Math.Round(value, places, MidpointRounding.AwayFromZero) : value;
            }

            return (double)Math.Round(shown, places, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Prints with exactly <paramref name="places"/> decimal places, halves away from zero, from the number as it's shown
        /// (see <see cref="RoundWhole"/> for why), so <c>fixed(3.5, 2)</c> is 3.50 and <c>fixed(2.345, 2)</c> is 2.35.
        /// </summary>
        public static string Fixed (double value, int places)
        {
            string format = "F" + places.ToString(CultureInfo.InvariantCulture);
            if (!TryTakeShown(value, out decimal shown))
            {
                return value.ToString(format, CultureInfo.InvariantCulture);
            }

            return Math.Round(shown, places, MidpointRounding.AwayFromZero).ToString(format, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The remainder the way a spreadsheet's MOD gives it, taking the sign of the divisor, so <c>-1 % 100</c> is 99
        /// where C# gives -1. That's what makes <c>($hour - 3) % 24</c> go back past midnight.
        /// </summary>
        public static double Mod (double dividend, double divisor)
        {
            double remainder = dividend % divisor;
            if ((remainder != 0) && ((remainder < 0) != (divisor < 0)))
            {
                remainder += divisor;
            }

            return remainder;
        }

        // ===========[ Helpers ]==============================================

        // The number as it's shown, as a decimal. A whole number is taken exactly rather than at 15 digits, since it prints
        //  exactly. A number too small for a decimal to hold would come out as 0, and anything too big for one can't be
        //  taken at all, so both are refused and rounded by their binary value instead.
        private static bool TryTakeShown (double value, out decimal shown)
        {
            const double kLongRange = 9.2e18;

            shown = 0;
            if (!Double.IsFinite(value))
            {
                return false;
            }

            if ((value == Math.Floor(value)) && (Math.Abs(value) < kLongRange))
            {
                shown = new Decimal((long)value);
                return true;
            }

            string printed = value.ToString(kShownFormat, CultureInfo.InvariantCulture);
            return Decimal.TryParse(printed, NumberStyles.Float, CultureInfo.InvariantCulture, out shown) && (shown != 0);
        }

        // ===========[ Subclasses/structs ]===================================

        /// <summary>
        /// Which way <see cref="RoundWhole"/> goes.
        /// </summary>
        public enum eRounding
        {
            /// <summary>
            /// Toward zero, like trunc()
            /// </summary>
            Truncate,

            /// <summary>
            /// Down, like floor()
            /// </summary>
            Floor,

            /// <summary>
            /// Up, like ceil()
            /// </summary>
            Ceiling,

            /// <summary>
            /// To the nearest, with halves away from zero, like round() and a spreadsheet's ROUND
            /// </summary>
            AwayFromZero,
        }
    }
}
