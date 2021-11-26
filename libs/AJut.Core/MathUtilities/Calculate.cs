namespace AJut.MathUtilities
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public static class Calculate
    {
        public static double Mean (IEnumerable<double> numbers)
        {
            return numbers.Sum() / numbers.Count();
        }
        public static double Mean (params double[] numbers)
        {
            return numbers.Sum() / numbers.Length;
        }
        public static T LeastCommonMultiple<T> (IEnumerable<T> numbers)
        {
            return numbers.Aggregate((T a, T b) => (T)LeastCommonMultipleFixed(a, b));
        }

        public static dynamic LeastCommonMultiple (params dynamic[] numbers)
        {
            return numbers.Aggregate(LeastCommonMultipleFixed);
        }

        private static dynamic LeastCommonMultipleFixed (dynamic a, dynamic b)
        {
            return Math.Abs(a * b) / GreatestCommonDenominator(a, b);
        }

        public static T GreatestCommonDenominator<T> (IEnumerable<T> numbers)
        {
            return numbers.Aggregate((T a, T b) => (T)GreatestCommonDenominatorFixed(a, b));
        }

        public static dynamic GreatestCommonDenominator (params dynamic[] numbers)
        {
            return numbers.Aggregate(GreatestCommonDenominatorFixed);
        }

        public static void MinAndMaxValuesIn<T> (IEnumerable<T> numbers, out T min, out T max)
        {
            MinAndMaxValuesIn(numbers.ToArray(), out min, out max);
        }

        public static void MinAndMaxValuesIn (dynamic[] numbers, out dynamic min, out dynamic max)
        {
            min = numbers[0];
            max = numbers[0];
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

        private static dynamic GreatestCommonDenominatorFixed (dynamic a, dynamic b)
        {
            return b == 0 ? a : GreatestCommonDenominator(b, a % b);
        }
    }
}
