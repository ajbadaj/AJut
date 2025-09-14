namespace AJut.Algorithms
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Capture an item in a delegate to compare to another item
    /// </summary>
    /// <typeparam name="T">The item type</typeparam>
    /// <param name="other">The item to compare to</param>
    /// <returns>
    ///     -1: Captured item is &lt; other
    ///      0: Captured item is == other
    ///      1: Captured item is &gt; other
    /// </returns>
    public delegate int ItemComparisonToOther<T> (T other);
    public delegate int GenerateBinaryPivot (int start, int end);

    public static partial class Search
    {
        public static GenerateBinaryPivot BinarySearchPivot { get; set; } = DefaultPivotGenerator;
        public static int BinarySearch<T> (IList<T> list, T item)
        {
            if (list is List<T> genericList)
            {
                return genericList.BinarySearch(item);
            }

            // If it's IComparable use that
            if (item is IComparable<T> comparable)
            {
                return BinarySearch(list, source => comparable.CompareTo(source));
            }

            // Otherwise use the default comparer
            return BinarySearch(list, _DefaultComparison);
            int _DefaultComparison (T other)
            {
                return Comparer<T>.Default.Compare(item, other);
            }
        }

        public static int BinarySearch<T> (IList<T> list, ItemComparisonToOther<T> compareTargetToItem)
        {
            if (list.Count == 0)
            {
                return 0;
            }

            if (list.Count == 1)
            {
                int comparisonResult = compareTargetToItem(list[0]);
                return comparisonResult <= 0 ? ~0 : ~1;
            }

            return BinarySearchXT_Helper(list, 0, list.Count - 1, list.Count / 2, compareTargetToItem);
        }

        public static int DefaultPivotGenerator (int start, int end)
        {
            return start + (end - start) / 2;
        }

        private static int BinarySearchXT_Helper<T> (IList<T> list, int start, int end, int pivot, ItemComparisonToOther<T> compareTargetToItem)
        {
            // Search is over, we found the negative spot
            if (start == end)
            {
                return ~start;
            }

            // There is no pivot, just the start and end, so it must be one of these
            if (end - start == 1)
            {
                // Check the start
                int targetComparedToStart = compareTargetToItem(list[start]);
                if (targetComparedToStart < 0)
                {
                    return ~start;
                }
                if (targetComparedToStart == 0)
                {
                    return start;
                }

                // Check the end
                int tagetComparedToEnd = compareTargetToItem(list[end]);
                if (tagetComparedToEnd < 0)
                {
                    return ~end;
                }
                if (tagetComparedToEnd == 0)
                {
                    return end;
                }

                return ~(end + 1);
            }

            // Check the pivot and continue searching
            int targetComparedToPivot = compareTargetToItem(list[pivot]);
            if (targetComparedToPivot == 0)
            {
                return pivot;
            }

            // Is the target is < pivot item, then we need to check start->pivot
            if (targetComparedToPivot < 0)
            {
                return BinarySearchXT_Helper(list, start, pivot, BinarySearchPivot(start, pivot), compareTargetToItem);
            }

            // Otherwise the target is > pivot item, and we need to check pivot->end
            return BinarySearchXT_Helper(list, pivot, end, BinarySearchPivot(pivot, end), compareTargetToItem);
        }
    }
}
