namespace AJut.MathUtilities
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    /// Math oriented calculation helpers
    /// </summary>
    /// <remarks>
    /// Typed generic math (<see cref="INumber{TSelf}"/>) rather than C# dynamic, so these work in an ahead of time compiled
    /// app (iOS, NativeAOT), where dynamic throws. Arithmetic happens in the element type: the mean of ints divides as ints.
    /// Sums are checked, as <see cref="Enumerable.Sum(IEnumerable{int})"/> is, so a sum past the element type's range throws an
    /// <see cref="OverflowException"/> rather than wrapping around (floating point types go to infinity instead, as usual). The
    /// QuickEvaluate checks are the exception: a sum past the type's maximum is past any limit, so they answer false.
    /// </remarks>
    public static class Calculate
    {
        /// <summary>
        /// Calculate the mean average of the number set
        /// </summary>
        public static T Mean<T> (IEnumerable<T> numbers) where T : INumber<T>
        {
            return SumAndCount(numbers, out int count) / T.CreateChecked(count);
        }

        /// <summary>
        /// Calculate the mean average of the number set
        /// </summary>
        public static T Mean<T> (params T[] numbers) where T : INumber<T>
        {
            return Mean((IEnumerable<T>)numbers);
        }

        /// <summary>
        /// Calculate the least common multiple of the number set
        /// </summary>
        public static T LeastCommonMultiple<T> (IEnumerable<T> numbers) where T : INumber<T>
        {
            return numbers.Aggregate(LeastCommonMultipleFixed);
        }

        /// <summary>
        /// Calculate the least common multiple of the number set
        /// </summary>
        public static T LeastCommonMultiple<T> (params T[] numbers) where T : INumber<T>
        {
            return numbers.Aggregate(LeastCommonMultipleFixed);
        }

        /// <summary>
        /// Calculate the greatest common denominator of the number set
        /// </summary>
        public static T GreatestCommonDenominator<T> (IEnumerable<T> numbers) where T : INumber<T>
        {
            return numbers.Aggregate(GreatestCommonDenominatorFixed);
        }

        /// <summary>
        /// Calculate the greatest common denominator of the number set
        /// </summary>
        public static T GreatestCommonDenominator<T> (params T[] numbers) where T : INumber<T>
        {
            return numbers.Aggregate(GreatestCommonDenominatorFixed);
        }

        /// <summary>
        /// Allows you to iterate over a sequence once and extract min and max values at the same time.
        /// </summary>
        /// <param name="numbers">The numbers to evaluate</param>
        /// <param name="min">The found min value</param>
        /// <param name="max">The found max value</param>
        public static void MinAndMaxValuesIn<T> (IEnumerable<T> numbers, out T min, out T max) where T : IComparisonOperators<T, T, bool>
        {
            min = max = numbers.First();
            foreach (T value in numbers.Skip(1))
            {
                if (value < min)
                {
                    min = value;
                }
                else if (value > max)
                {
                    max = value;
                }
            }
        }

        /// <summary>
        /// Sums the sequence and counts it in one pass, in the element type.
        /// </summary>
        /// <exception cref="OverflowException">The sum goes past the element type's range</exception>
        public static T SumAndCount<T> (IEnumerable<T> numbers, out int count) where T : INumber<T>
        {
            count = 0;
            T sum = T.Zero;
            foreach (T value in numbers)
            {
                sum = checked(sum + value);
                ++count;
            }

            return sum;
        }

        /// <summary>
        /// Evaluates if a sum is less than the target. Checks each iteration and fails as soon as the sum is larger than or equal to the target,
        /// rather than evaluating the whole sequence for the sum and checking that final result.
        /// </summary>
        /// <remarks>
        /// A running sum that would go past the element type's maximum is past any limit of that type, so the answer is false.
        /// </remarks>
        public static bool QuickEvaluateIfSumIsLessThan<T> (IEnumerable<T> numbers, T lessThan) where T : INumber<T>, IMinMaxValue<T>
        {
            T sum = T.Zero;
            foreach (T value in numbers)
            {
                if (WouldPassMaxValue(sum, value))
                {
                    return false;
                }

                sum = checked(sum + value);
                if (sum >= lessThan)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Evaluates if a sum is less than or eqaul to the target. Checks each iteration and fails as soon as the sum is larger than the target,
        /// rather than evaluating the whole sequence for the sum and checking that final result.
        /// </summary>
        /// <remarks>
        /// A running sum that would go past the element type's maximum is past any limit of that type, so the answer is false.
        /// </remarks>
        public static bool QuickEvaluateIfSumIsLessThanOrEqualTo<T> (IEnumerable<T> numbers, T lessThanOrEqualTo) where T : INumber<T>, IMinMaxValue<T>
        {
            T sum = T.Zero;
            foreach (T value in numbers)
            {
                if (WouldPassMaxValue(sum, value))
                {
                    return false;
                }

                sum = checked(sum + value);
                if (sum > lessThanOrEqualTo)
                {
                    return false;
                }
            }

            return true;
        }

        // ==========================[ Private Utility Functions ]===========================================

        // Whether sum + value would go past T's maximum, worked out without doing the add, so nothing throws or wraps.
        // Only a positive value added to a positive sum can go past the maximum, and with a positive sum, MaxValue - sum
        // cannot itself overflow (with a negative sum it would, which is why the sum's sign is checked before the subtraction). A sum that
        // goes below the minimum is left to the checked add, which throws: that sum could still come back up, so there
        // is no answer to give without a wider type.
        private static bool WouldPassMaxValue<T> (T sum, T value) where T : INumber<T>, IMinMaxValue<T>
        {
            return T.IsPositive(value)
                && T.IsPositive(sum)
                && (value > T.MaxValue - sum);
        }

        private static T LeastCommonMultipleFixed<T> (T a, T b) where T : INumber<T>
        {
            return T.Abs(a * b) / GreatestCommonDenominatorFixed(a, b);
        }

        private static T GreatestCommonDenominatorFixed<T> (T a, T b) where T : INumber<T>
        {
            return T.IsZero(b) ? a : GreatestCommonDenominatorFixed(b, a % b);
        }
    }
}
