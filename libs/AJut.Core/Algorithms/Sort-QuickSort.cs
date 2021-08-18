namespace AJut.Algorithms
{
    using System;
    using System.Collections.Generic;

    public delegate void ListElementSwapByIndex (int left, int right);
    public static partial class Sort
    {
        private static readonly Random g_rng = new Random(DateTime.Now.Millisecond);
        public static GenerateBinaryPivot QuickSortPivot { get; set; } = DefaultPivotGenerator;

        public static void QuickSortInplace<T> (IList<T> source, ListElementSwapByIndex swap = null, int start = 0, int end = -1)
        {
            if (typeof(IComparable<T>).IsAssignableFrom(typeof(T)))
            {
                QuickSortInplace(source, (l,r)=> ((IComparable<T>)l).CompareTo(r), swap, start, end);
            }
            else
            {
                QuickSortInplace(source, Comparer<T>.Default.Compare, swap, start, end);
            }
        }
        public static void QuickSortInplace<T> (IList<T> source, IComparer<T> comparer, ListElementSwapByIndex swap = null, int start = 0, int end = -1)
        {
            QuickSortInplace(source, comparer.Compare, swap, start, end);
        }
        public static void QuickSortInplace<T> (IList<T> source, Comparison<T> comparison, ListElementSwapByIndex swap = null, int start = 0, int end = -1)
        {
            end = end < 0 ? source.Count - 1 : Math.Min(end, source.Count - 1);
            DoQuickSortInplace(source, comparison, swap ?? _DefaultSwap, start, end);

            void _DefaultSwap(int left, int right)
            {
                T temp = source[left];
                source[left] = source[right];
                source[right] = temp;
            }
        }

        private static void DoQuickSortInplace<T> (IList<T> source, Comparison<T> compare, ListElementSwapByIndex swap, int start, int end)
        {
            // 1) Perform recursion exit testing
            if (start >= end)
            {
                return;
            }

            // 2) Simplify branch complexity by doing small numberset alternative exit here
            if (end - start == 1)
            {
                if (compare(source[start], source[end]) > 0)
                {
                    swap(start, end);
                }

                return;
            }

            // 3) Select pivot
            int pivotIndex = QuickSortPivot(start, end);
            T pivot = source[pivotIndex];
            swap(pivotIndex, end); // put it at the end of the list, for branch complexity reduction

            // 4) Parition list

            // Starting off, we can't swap because our swap target would be the first element
            //  what we will be doing is potentially advancing the swap target though if the start
            //  element is smaller than the pivot
            int smallSideSwapToIndex = start - 1;
            if (compare(source[start], pivot) < 0)
            {
                ++smallSideSwapToIndex;
            }

            for (int partitionEvalIndex = start + 1; partitionEvalIndex < end; ++partitionEvalIndex)
            {
                if (compare(source[partitionEvalIndex], pivot) < 0
                    && ++smallSideSwapToIndex != partitionEvalIndex)
                {
                    swap(smallSideSwapToIndex, partitionEvalIndex);
                }
            }

            // Finish partition by swaping the pivot back to the midpoint
            swap(++smallSideSwapToIndex, end);
            pivotIndex = smallSideSwapToIndex;

            // 5) Recurse left
            DoQuickSortInplace(source, compare, swap, start, pivotIndex - 1);

            // 6) Recurse right
            DoQuickSortInplace(source, compare, swap, pivotIndex + 1, end);
        }

        private static int DefaultPivotGenerator (int start, int end) => g_rng.Next(start, end);
    }
}
