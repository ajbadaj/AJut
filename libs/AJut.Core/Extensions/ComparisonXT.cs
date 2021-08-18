namespace AJut.Extensions
{
    using System;
    using System.Collections.Generic;

    public static class ComparisonXT
    {
        public static Comparison<T> ToComparison<T> (this IComparer<T> comparer)
        {
            if (comparer == null)
            {
                comparer = Comparer<T>.Default;
            }

            return (left, right) => comparer.Compare(left, right);
        }
    }
}
