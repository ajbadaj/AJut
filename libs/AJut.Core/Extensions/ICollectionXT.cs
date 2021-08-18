namespace AJut
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public static class ICollectionXT
    {
        public static bool IsNullOrEmpty<T> (this T[] arr)
        {
            return arr == null || arr.Length == 0;
        }

        public static bool IsNullOrEmpty (this ICollection list)
        {
            return list == null || list.Count == 0;
        }

        public static bool AddIfUnique<T> (this ICollection<T> This, T item)
        {
            if (This.Contains(item))
            {
                return false;
            }

            This.Add(item);
            return true;
        }


        public static void AddEachUnique<T> (this ICollection<T> This, IEnumerable<T> others)
        {
            if (others != null)
            {
                foreach (T item in others)
                {
                    AddIfUnique<T>(This, item);
                }
            }
        }

        public static void InsertEach<T> (this IList<T> This, int index, IEnumerable<T> others)
        {
            foreach (T otherItem in others is IList<T> list ? list.EnumerateReversed() : others.Reverse())
            {
                This.Insert(index, otherItem);
            }
        }

        public static void AddEach<T> (this ICollection<T> This, IEnumerable<T> others)
        {
            foreach (T otherItem in others)
            {
                This.Add(otherItem);
            }
        }

        public static void AddEach<T> (this ICollection<T> This, IEnumerable<T> others, Action<T> runOnEach)
        {
            foreach (T otherItem in others)
            {
                runOnEach?.Invoke(otherItem);
                This.Add(otherItem);
            }
        }

        public static void RemoveEach<T> (this ICollection<T> This, IEnumerable<T> others)
        {
            foreach (T otherItem in others)
            {
                This.Remove(otherItem);
            }
        }

        public static void RemoveEach<T> (this ICollection<T> This, IEnumerable<T> others, Action<T> runOnEach)
        {
            foreach (T otherItem in others)
            {
                runOnEach?.Invoke(otherItem);
                This.Add(otherItem);
            }
        }

        public static void RemoveAll<T> (this ICollection<T> This, Predicate<T> predicate)
        {
            foreach (T item in This.ToArray())
            {
                if (predicate(item))
                {
                    This.Remove(item);
                }
            }
        }

        /// <summary>
        /// Clears the list, then adds each of the other
        /// </summary>
        /// <typeparam name="T">The type of list it is</typeparam>
        /// <param name="This">The list this is called on</param>
        /// <param name="otherList">The list whose contents are added to this list</param>
        public static void ResetWith<T> (this ICollection<T> This, IEnumerable<T> otherList)
        {
            if (This == null)
            {
                return;
            }

            This.Clear();
            This.AddEach(otherList);
        }

        /// <summary>
        /// Removes any items from This list that matches an item out of the otherList according to the matcherFunc
        /// </summary>
        /// <typeparam name="T">The type of items we're testing for</typeparam>
        /// <param name="This">The list this is called on</param>
        /// <param name="otherList">The list of items which may match to items in This list</param>
        /// <param name="matcherFunc">The test to perform that indicates whether or not to T items match</param>
        public static void RemoveAny<T> (this IList<T> This, ICollection<T> otherList, Matcher<T> matcherFunc)
        {
            for (int nThisInd = This.Count - 1; nThisInd >= 0; --nThisInd)
            {
                T thisItem = This[nThisInd];
                foreach (T otherItem in otherList)
                {
                    if (matcherFunc(thisItem, otherItem))
                    {
                        This.RemoveAt(nThisInd);
                        continue;
                    }
                }
            }
        }

        public static void ClearAndDisposeAll<T> (this ICollection<T> target) where T : IDisposable
        {
            foreach (T item in target)
            {
                item.Dispose();
            }

            target.Clear();
        }
    }
}
