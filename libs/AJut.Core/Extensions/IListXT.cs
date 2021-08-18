namespace AJut
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using AJut.Algorithms;

    public delegate int RandomNumberBetween (int min, int max);

    public static class IListXT
    {
        public static T FirstOrDefault<T> (this IList<T> list, int startingIndex, Predicate<T> predicate)
        {
            for (int index = startingIndex; index < list.Count; ++index)
            {
                T item = list[index];
                if (predicate(item))
                {
                    return item;
                }
            }

            return default;
        }

        public static int FirstIndexMatching<T> (this IList<T> list, Predicate<T> predicate)
        {
            return FirstIndexMatching<T>(list, 0, predicate);
        }

        public static int FirstIndexMatching<T> (this IList<T> list, int startingIndex, Predicate<T> predicate)
        {
            for (int index = startingIndex; index < list.Count; ++index)
            {
                if (predicate(list[index]))
                {
                    return index;
                }
            }

            return -1;
        }

        public static T RemoveFirst<T> (this IList<T> list, Predicate<T> predicate, int startingIndex = 0)
        {
            for (int index = startingIndex; index < list.Count; ++index)
            {
                var item = list[index];
                if (predicate(item))
                {
                    list.RemoveAt(index);
                    return item;
                }
            }

            return default;
        }

        public static bool Relocate (this IList list, int moveFromIndex, int moveToIndex)
        {
            if (list.IsReadOnly)
            {
                return false;
            }

            if (moveFromIndex == moveToIndex)
            {
                return true;
            }

            if (moveFromIndex < 0 || moveFromIndex >= list.Count)
            {
                return false;
            }

            if (moveToIndex < 0 || moveToIndex >= list.Count)
            {
                return false;
            }

            var toMove = list[moveFromIndex];
            list.RemoveAt(moveFromIndex);
            list.Insert(moveToIndex, toMove);
            return true;
        }

        public static bool Swap (this IList list, int item1Index, int item2Index)
        {
            if (list.IsReadOnly)
            {
                return false;
            }

            if (item1Index == item2Index)
            {
                return true;
            }

            if (item1Index < 0 || item1Index >= list.Count)
            {
                return false;
            }

            if (item2Index < 0 || item2Index >= list.Count)
            {
                return false;
            }

            int earlierIndex = Math.Min(item1Index, item2Index);
            int laterIndex = earlierIndex == item1Index ? item2Index : item1Index;

            var earlierItem = list[earlierIndex];
            var laterItem = list[laterIndex];

            list.RemoveAt(laterIndex);
            list.RemoveAt(earlierIndex);

            list.Insert(earlierIndex, laterItem);
            list.Insert(laterIndex, earlierItem);
            return true;
        }

        /// <summary>
        /// Equivallent to Clear() && AddEach(otherList) - except that it only removes items not contained in otherList and then inserts items
        /// from otherList to match. This allows to limit the ammount of changes we are making to only be the useful set (helpful in UI circumstances
        /// might respond to each change by tearing down and creating new UI).
        /// </summary>
        public static void ResetTo<T> (this IList<T> This, IList<T> otherList)
        {
            // Remove any not contained in other
            foreach (T item in This.ToList())
            {
                if (!otherList.Contains(item))
                {
                    This.Remove(item);
                }
            }

            // Insert items so that This matches otherList
            for (int index = 0; index < otherList.Count; ++index)
            {
                if (index >= This.Count)
                {
                    This.Add(otherList[index]);
                }
                else if (!This[index].Equals(otherList[index]))
                {
                    This.Insert(index, otherList[index]);
                }
            }
        }

        public static void Randomize (this IList list)
        {
            list.Randomize(DateTime.Now.Millisecond);
        }

        /// <summary>
        /// Randomize element order
        /// </summary>
        public static void Randomize (this IList list, int seed)
        {
            Random rng = new Random(seed);
            list.Randomize(rng.Next);
        }

        public static void Randomize (this IList list, RandomNumberBetween rng)
        {
            object[] copy = list.OfType<object>().ToArray();
            list.Clear();

            foreach (object obj in copy)
            {
                if (list.Count == 0)
                {
                    list.Add(obj);
                }
                else
                {
                    list.Insert(rng(0, list.Count - 1), obj);
                }
            }
        }

        /// <summary>
        /// Returns the reverse of the list, but enumerated (yield returned)
        /// </summary>
        /// <typeparam name="T">The item type</typeparam>
        /// <param name="This">The target list to reverse (the extension target)</param>
        /// <param name="startIndex">Where to start the enumeration (default is -1, start at the end)</param>
        /// <returns>An <see cref="IEnumerable{T}"/> yield returned one at a time.</returns>
        public static IEnumerable<T> EnumerateReversed<T> (this IList<T> This, int startIndex = -1)
        {
            if (startIndex == -1)
            {
                startIndex = This.Count - 1;
            }
            else
            {
                startIndex = This.Count - 1 - startIndex;
            }
            for (int index = startIndex; index >= 0; --index)
            {
                yield return This[index];
            }
        }

        public static int IndexOf<T> (this IList<T> This, Predicate<T> predicate, int startIndex = 0)
        {
            for (int index = startIndex; index < This.Count; ++index)
            {
                var curr = This[index];
                if (predicate(curr))
                {
                    return index;
                }
            }

            return -1;
        }

        public static int IndexOf<T> (this IList<T> This, T equalTo, int startIndex = 0)
        {
            return This.IndexOf(i => i.Equals(equalTo), startIndex);
        }

        public static int IndexOf<T, U> (this IList<T> This, U test, int startIndex = 0)
            where T : IEquatable<U>
        {
            return This.IndexOf(i => ((IEquatable<U>)i).Equals(test), startIndex);
        }

        public static int BinarySearchXT<T> (this IList<T> list, ItemComparisonToOther<T> compareTargetToItem)
        {
            return Search.BinarySearch(list, compareTargetToItem);
        }

        public static int BinarySearchXT<T> (this IList<T> list, T item)
        {
            return Search.BinarySearch(list, item);
        }

        /// <summary>
        /// Inserts the comparable item into the container
        /// </summary>
        /// <typeparam name="T">An <see cref="IComparable{T}"/> type</typeparam>
        /// <param name="list">The list to insert into</param>
        /// <param name="item">The item to insert</param>
        /// <returns>The index location of the inserted item</returns>
        public static int InsertSorted<T> (this IList<T> list, T item) where T : IComparable<T>
        {
            int index = Search.BinarySearch(list, source => item.CompareTo(source));
            if (index < 0)
            {
                index = ~index;
            }

            list.Insert(index, item);
            return index;
        }

        /// <summary>
        /// Inserts the comparable item into the container
        /// </summary>
        /// <typeparam name="T">An <see cref="IComparable{T}"/> type</typeparam>
        /// <param name="list">The list to insert into</param>
        /// <param name="targetItem">The item to insert</param>
        /// <param name="comparisonMethod">The comparison method used to create an <see cref="ItemComparisonToOther{T}"/></param>
        /// <returns>The index location of the inserted item</returns>
        public static int InsertSorted<T> (this IList<T> list, T targetItem, Comparison<T> comparisonMethod)
        {
            int index = Search.BinarySearch(list, source => comparisonMethod(targetItem, source));
            if (index < 0)
            {
                index = ~index;
            }

            list.Insert(index, targetItem);
            return index;
        }
    }
}
