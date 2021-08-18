namespace AJut.Algorithms
{
    using System;
    using System.Collections.Generic;

    public static partial class Sort
    {
        private static readonly Random g_rng = new Random(DateTime.Now.Millisecond);
        public static GenerateBinaryPivot QuickSortPivot { get; set; } = DefaultPivotGenerator;

        public static void QuickSortInplace<T> (IList<T> source, int start = 0, int end = -1)
        {
            if (typeof(IComparable<T>).IsAssignableFrom(typeof(T)))
            {
                QuickSortInplace(source, (l,r)=> ((IComparable<T>)l).CompareTo(r), start, end);
            }
            else
            {
                QuickSortInplace(source, Comparer<T>.Default.Compare, start, end);
            }
        }
        public static void QuickSortInplace<T> (IList<T> source, IComparer<T> comparer, int start = 0, int end = -1)
        {
            QuickSortInplace(source, comparer.Compare, start, end);
        }
        public static void QuickSortInplace<T> (IList<T> source, Comparison<T> comparison, int start = 0, int end = -1)
        {
            end = end < 0 ? source.Count - 1 : Math.Min(end, source.Count - 1);
            DoQuickSortInplace(source, comparison, start, end);
        }

        private static void DoQuickSortInplace<T> (IList<T> source, Comparison<T> compare, int start, int end)
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
                    _Swap(start, end);
                }

                return;
            }

            // 3) Select pivot
            int pivotIndex = QuickSortPivot(start, end);
            T pivot = source[pivotIndex];
            _Swap(pivotIndex, end); // put it at the end of the list, for branch complexity reduction

            // 4) Parition list
            int smallSideSwapToIndex = start - 1;
            if (compare(source[start], pivot) < 0)
            {
                ++smallSideSwapToIndex;
            }

            for (int partitionEvalIndex = start + 1; partitionEvalIndex < end; ++partitionEvalIndex)
            {
                if (compare(source[partitionEvalIndex], pivot) < 0)
                {
                    _Swap(++smallSideSwapToIndex, partitionEvalIndex);
                }
            }

            // Finish partition by swaping the pivot back to the midpoint
            _Swap(++smallSideSwapToIndex, end);
            pivotIndex = smallSideSwapToIndex;

            // 5) Recurse left
            DoQuickSortInplace(source, compare, start, pivotIndex - 1);

            // 6) Recurse right
            DoQuickSortInplace(source, compare, pivotIndex + 1, end);


            void _Swap (int i, int j)
            {
                T temp = source[i];
                source[i] = source[j];
                source[j] = temp;
            }
        }


        private static int DefaultPivotGenerator (int start, int end) => g_rng.Next(start, end);
    }
}
