namespace AJut.UX.Converters
{
    using System;

    // ===========[ NumericConverterMath ]=======================================
    // The number handling behind AJut's numeric converters (the arithmetic converters and the numeric
    // comparison converters), kept free of any UI toolkit so every stack's converters apply the same
    // rules. The converters themselves stay in each stack, since a value converter is a XAML idea.
    //
    // Comparisons are written out per numeric type rather than done with dynamic. dynamic needs the
    // runtime binder, which emits code at run time, and an ahead of time compiled app (iOS, and
    // Android with AOT) can't do that.
    /// <summary>
    /// The arithmetic and comparison rules AJut's numeric converters share
    /// </summary>
    public static class NumericConverterMath
    {
        // ===========[ Public Interface Methods ]==============================
        /// <summary>
        /// Applies <paramref name="operation"/> to <paramref name="value"/> and <paramref name="operand"/>. Dividing by 0
        /// gives back <paramref name="value"/> as it was.
        /// </summary>
        public static double Apply (eArithmeticOperation operation, double value, double operand)
        {
            switch (operation)
            {
                case eArithmeticOperation.Add: return value + operand;
                case eArithmeticOperation.Subtract: return value - operand;
                case eArithmeticOperation.Multiply: return value * operand;
                case eArithmeticOperation.Divide: return operand != 0.0 ? value / operand : value;
                default: return value;
            }
        }

        /// <summary>
        /// How <paramref name="value"/> compares with <paramref name="to"/>. An enum value compares by its number, and an
        /// integer against a fraction compares as the fraction it is. False for a null value, which has nothing to compare.
        /// </summary>
        public static bool TryRelate (object? value, double to, out Relation relation)
        {
            if (value == null)
            {
                relation = default;
                return false;
            }

            // Convert.ToInt64 takes an enum of any backing type, where an (int) unbox only takes an int backed one
            if (value.GetType().IsEnum)
            {
                value = Convert.ToInt64(value);
            }

            switch (value)
            {
                case double d:
                    relation = new Relation(d < to, d == to, d > to);
                    return true;

                case float f:
                    float toFloat = (float)to;
                    relation = new Relation(f < toFloat, f == toFloat, f > toFloat);
                    return true;

                case decimal m:
                    relation = RelateExactly(m, to);
                    return true;

                case sbyte or byte or short or ushort or int or uint or long or ulong:
                    // Every integer type fits in a decimal exactly, so comparing there keeps a fractional "to" as it is
                    // rather than rounding it to the integer type first
                    relation = RelateExactly(Convert.ToDecimal(value), to);
                    return true;

                default:
                    relation = RelateSameType(value, Convert.ChangeType(to, value.GetType()));
                    return true;
            }
        }

        // ===========[ Helper Methods ]========================================
        // Compares a value held exactly as a decimal with a double. A NaN is none of less, equal or greater, and a double
        // past the decimal range (infinity included) is past every value a decimal holds.
        private static Relation RelateExactly (decimal value, double to)
        {
            if (double.IsNaN(to))
            {
                return new Relation(false, false, false);
            }

            if (to >= (double)decimal.MaxValue)
            {
                return new Relation(true, false, false);
            }

            if (to <= (double)decimal.MinValue)
            {
                return new Relation(false, false, true);
            }

            decimal toValue = (decimal)to;
            return new Relation(value < toValue, value == toValue, value > toValue);
        }

        // Compares a value that isn't a number with "to" converted to its type: by its own ordering if it has one, else
        // only for equality
        private static Relation RelateSameType (object value, object toValue)
        {
            if (value is IComparable comparable)
            {
                int comparison = comparable.CompareTo(toValue);
                return new Relation(comparison < 0, comparison == 0, comparison > 0);
            }

            return new Relation(false, value.Equals(toValue), false);
        }

        // ===========[ Subclasses/structs ]====================================
        /// <summary>
        /// How a value compares with another. A NaN is none of less, equal or greater, so for one only "not equal" holds.
        /// </summary>
        public readonly record struct Relation (bool IsLess, bool IsEqual, bool IsGreater);
    }
}
