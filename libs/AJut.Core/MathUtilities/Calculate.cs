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
        public static dynamic Mean (IEnumerable<dynamic> numbers)
        {
            return Sum(numbers) / numbers.Count();
        }

        /// <summary>
        /// Calculate the least common multiple of the number set
        /// </summary>
        public static dynamic LeastCommonMultiple<dynamic> (IEnumerable<dynamic> numbers)
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
        public static dynamic GreatestCommonDenominator<dynamic> (IEnumerable<dynamic> numbers)
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
        public static void MinAndMaxValuesIn (IEnumerable<dynamic> numbers, out dynamic min, out dynamic max)
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
        public static dynamic Sum (IEnumerable<dynamic> numbers)
        {
            dynamic sum = 0;
            foreach (dynamic value in numbers)
            {
                sum += value;
            }

            return sum;
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
