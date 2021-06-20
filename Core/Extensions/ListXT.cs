namespace AJut
{
    using AJut.Interfaces;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

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

    public delegate int RandomNumberBetween (int min, int max);

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
            foreach (T otherItem in (others is IList<T> list ? list.EnumerateReversed() : This.Reverse()))
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
        /// Updates each <see cref="IAmUpdatable">IAmUpdatable</see> object with one that <see cref="IAmUpdatable.Matches">Matches</see> it from the passed in otherList
        /// </summary>
        /// <param name="This">The list this is being called on</param>
        /// <param name="otherList">The list containing the updates</param>
        /// <param name="addExtraneous">Bool indicating whether or not the unmatched items should be added back to the end of the list after the update is run</param>
        public static void UpdateWith (this IList This, ICollection otherList, bool addExtraneous)
        {
            if (This == null)
                return;

            ArrayList toAdd;

            if (This.Count > 0)
            {
                toAdd = new ArrayList();

                // Update all the ones that match
                foreach (object item in otherList)
                {
                    bool bFound = false;
                    for (int nCurrInd = 0; nCurrInd < This.Count; ++nCurrInd)
                    {
                        IAmUpdatable currItem = This[nCurrInd] as IAmUpdatable;
                        if (currItem == null)
                            continue;

                        if (currItem.Matches(item))
                        {
                            currItem.UpdateWith(item);
                            bFound = true;
                            break;
                        }
                    }
                    if (!bFound)
                    {
                        toAdd.Add(item);
                    }
                }
            }
            else
            {
                toAdd = new ArrayList(otherList);
            }

            // Add all the ones that were different
            if (addExtraneous)
            {
#if WINDOWS_UWP
                Type genericType = This.GetType().GenericTypeArguments[0];
#else
                Type genericType = This.GetType().GetGenericArguments()[0];
#endif
                foreach (var itemToAdd in toAdd)
                {
                    This.Add(itemToAdd.BoxCast(genericType));
                }
            }
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
    }

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

        public static void Swap<T> (this IList<T> list, int moveFrom, int moveTo)
        {
            if (moveFrom == moveTo)
            {
                return;
            }

            int smaller = System.Math.Min(moveFrom, moveTo);
            int larger = smaller == moveFrom ? moveTo : moveFrom;

            T largerItem = list[larger];
            T smallerItem = list[smaller];
            list[larger] = smallerItem;
            list[smaller] = largerItem;
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


        public static int BinarySearchXT<T> (this IList<T> list, T item) where T : IComparable<T>
        {
            return BinarySearchXT(list, source => item.CompareTo(source));
        }

        public static int BinarySearchXT<T> (this IList<T> list, ItemComparisonToOther<T> compareTargetToItem)
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

        /// <summary>
        /// Inserts the comparable item into the container
        /// </summary>
        /// <typeparam name="T">An <see cref="IComparable{T}"/> type</typeparam>
        /// <param name="list">The list to insert into</param>
        /// <param name="item">The item to insert</param>
        /// <returns>The index location of the inserted item</returns>
        public static int InsertSorted<T> (this IList<T> list, T item) where T : IComparable<T>
        {
            int index = list.BinarySearchXT(source => item.CompareTo(source));
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
            int index = list.BinarySearchXT(source => comparisonMethod(targetItem, source));
            if (index < 0)
            {
                index = ~index;
            }

            list.Insert(index, targetItem);
            return index;
        }

        private static int CalculatePivot (int start, int end)
        {
            return start + (end - start) / 2;
        }

        private static int BinarySearchXT_Helper<T> (this IList<T> list, int start, int end, int pivot, ItemComparisonToOther<T> compareTargetToItem)
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
                return BinarySearchXT_Helper(list, start, pivot, CalculatePivot(start, pivot), compareTargetToItem);
            }

            // Otherwise the target is > pivot item, and we need to check pivot->end
            return BinarySearchXT_Helper(list, pivot, end, CalculatePivot(pivot, end), compareTargetToItem);
        }

        public static void ClearAndDisposeAll<T> (this IList<T> target) where T : IDisposable
        {
            foreach (T item in target)
            {
                item.Dispose();
            }

            target.Clear();
        }
    }

    public static class IReadOnlyListXT
    {
        public static int IndexOfReadOnly<T> (this IReadOnlyList<T> This, Predicate<T> predicate, int startIndex = 0)
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

        public static int IndexOfReadOnly<T> (this IReadOnlyList<T> This, T equalTo, int startIndex = 0)
        {
            return This.IndexOfReadOnly(i => i.Equals(equalTo), startIndex);
        }

        public static int IndexOfReadOnly<T, U> (this IReadOnlyList<T> This, U test, int startIndex = 0)
            where T : IEquatable<U>
        {
            return This.IndexOfReadOnly(i => ((IEquatable<U>)i).Equals(test), startIndex);
        }
    }
}
