namespace AJut.MathUtilities
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Math oriented calculation helpers
    /// </summary>
    public static class Calculate
    {
        /// <summary>
        /// Calculate the mean average of the number set
        /// </summary>
        public static T Mean<T> (IEnumerable<T> numbers)
        {
            return SumAndCount(numbers, out int count) / count;
        }

        /// <summary>
        /// Calculate the mean average of the number set
        /// </summary>
        public static dynamic Mean (params dynamic[] numbers)
        {
            return SumAndCount(numbers, out int count) / count;
        }

        /// <summary>
        /// Calculate the least common multiple of the number set
        /// </summary>
        public static dynamic LeastCommonMultiple (IEnumerable<dynamic> numbers)
        {
            return numbers.Aggregate((dynamic a, dynamic b) => LeastCommonMultipleFixed(a, b));
        }

        /// <summary>
        /// Calculate the least common multiple of the number set
        /// </summary>
        public static dynamic LeastCommonMultiple (params dynamic[] numbers)
        {
            return numbers.Aggregate(LeastCommonMultipleFixed);
        }

        /// <summary>
        /// Calculate the greatest common denominator of the number set
        /// </summary>
        public static dynamic GreatestCommonDenominator (IEnumerable<dynamic> numbers)
        {
            return numbers.Aggregate((dynamic a, dynamic b) => GreatestCommonDenominatorFixed(a, b));
        }

        /// <summary>
        /// Calculate the greatest common denominator of the number set
        /// </summary>
        public static dynamic GreatestCommonDenominator (params dynamic[] numbers)
        {
            return numbers.Aggregate(GreatestCommonDenominatorFixed);
        }

        /// <summary>
        /// Allows you to iterate over a sequence once and extract min and max values at the same time.
        /// </summary>
        /// <param name="numbers">The numbers to evaluate</param>
        /// <param name="min">The found min value</param>
        /// <param name="max">The found max value</param>
        public static void MinAndMaxValuesIn<T> (IEnumerable<T> numbers, out T min, out T max)
        {
            min = max = numbers.First();
            foreach (dynamic value in numbers.Skip(1))
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
        /// Similar to the <see cref="Enumerable.Sum"/> extension functions, except accepting of dynamic
        /// </summary>
        public static dynamic SumAndCount<T> (IEnumerable<T> numbers, out int count)
        {
            count = 0;
            dynamic sum = 0;
            foreach (dynamic value in numbers)
            {
                sum += value;
                ++count;
            }

            return sum;
        }

        /// <summary>
        /// Evaluates if a sum is less than the target. Checks each iteration and fails as soon as the sum is larger than or equal to the target, 
        /// rather than evaluating the whole sequence for the sum and checking that final result.
        /// </summary>
        public static bool QuickEvaluateIfSumIsLessThan<T> (IEnumerable<T> numbers, dynamic lessThan)
        {
            dynamic sum = 0;
            foreach (dynamic value in numbers)
            {
                sum += value;
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
        public static bool QuickEvaluateIfSumIsLessThanOrEqualTo<T> (IEnumerable<T> numbers, dynamic lessThanOrEqualTo)
        {
            dynamic sum = 0;
            foreach (dynamic value in numbers)
            {
                sum += value;
                if (sum > lessThanOrEqualTo)
                {
                    return false;
                }
            }

            return true;
        }

        // ==========================[ Private Utility Functions ]===========================================

        private static dynamic LeastCommonMultipleFixed (dynamic a, dynamic b)
        {
            return Math.Abs(a * b) / GreatestCommonDenominator(a, b);
        }

        private static dynamic GreatestCommonDenominatorFixed (dynamic a, dynamic b)
        {
            return b == 0 ? a : GreatestCommonDenominator(b, a % b);
        }
    }
}
