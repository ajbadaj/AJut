namespace AJut
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;

    /// <summary>
    /// Extensions for <see cref="IEnumerable"/> and <see cref="IEnumerable{T}"/>
    /// </summary>
    public static class EnumerableXT
    {
        private const string kRuntimeArrayMessage = "Makes an array of an element type known only at runtime, which a NativeAOT build can only do for an array type the app uses elsewhere. Use Cast<T> and ToArray<T> where the type is known.";

        public static IEnumerable<TSource> AllNonNullElements<TSource> (this IEnumerable<TSource> source) => source.Where(x => x != null);

        public static bool IsEmpty<T> (this IEnumerable<T> This)
        {
            return !This.Any();
        }
        public static bool IsNullOrEmpty<T> ([NotNullWhen(false)] this IEnumerable<T> This)
        {
            return This == null || !This.Any();
        }
        public static bool IsNotEmpty<T> (this IEnumerable<T> This)
        {
            return This.Any();
        }
        public static bool IsNotNullOrEmpty<T> ([NotNullWhen(true)] this IEnumerable<T> This)
        {
            return This != null && This.Any();
        }
        /// <summary>The elements as <paramref name="type"/>: an array of it, which is also an IEnumerable of it. An element that can't be stored as one throws; a primitive that widens to it (an int into a double array) is stored widened.</summary>
        [RequiresDynamicCode(kRuntimeArrayMessage)]
        public static IEnumerable Cast (this IEnumerable This, Type type) => This.ToArray(type);

        /// <summary>The elements in a new array of <paramref name="type"/>. An element that can't be stored as one throws; a primitive that widens to it (an int into a double array) is stored widened.</summary>
        [RequiresDynamicCode(kRuntimeArrayMessage)]
        public static Array ToArray (this IEnumerable This, Type type)
        {
            // The array is made for the element type and filled through Array.SetValue, rather than through LINQ's
            //  Cast<T> and ToArray<T>. Those would need the generic methods made for the type at runtime, which an
            //  ahead of time compiled build (iOS) cannot do for a value type it never compiled them for.
            var elements = new List<object>();
            foreach (object element in This)
            {
                elements.Add(element);
            }

            Array result = Array.CreateInstance(type, elements.Count);
            for (int index = 0; index < elements.Count; ++index)
            {
                result.SetValue(elements[index], index);
            }

            return result;
        }

        /// <summary>The elements in a new array of <paramref name="type"/>, the same as <see cref="ToArray(IEnumerable, Type)"/></summary>
        [RequiresDynamicCode(kRuntimeArrayMessage)]
        public static Array CastToArrayOfType (this IEnumerable This, Type type) => This.ToArray(type);

        public static string ConcatToString (this IEnumerable This, string separator)
        {
            return String.Join(separator, This.OfType<object>().Select(_ => _.ToString()).ToArray());
        }

        public static string ConcatToString (this IEnumerable This, string separator, string prefix, string suffix)
        {
            return prefix + This.ConcatToString(separator) + suffix;
        }

        public static bool ContainsAny<T> (this IEnumerable<T> This, Comparison<T> comparer, IEnumerable<T> other)
        {
            foreach (T thisObj in This)
            {
                foreach (T otherObj in other)
                {
                    if (comparer(thisObj, otherObj) == 0)
                        return true;
                }
            }

            return false;
        }
        public static bool ContainsAnyThatPass<T> (this IEnumerable<T> This, Func<T, bool> matcher)
        {
            foreach (T element in This)
            {
                if (matcher(element))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ContainsAny<T> (this IEnumerable<T> This, IEnumerable<T> other)
        {
            foreach (object thisObj in This)
            {
                foreach (object otherObj in other)
                {
                    if (thisObj == otherObj)
                        return true;
                }
            }

            return false;
        }

        public static void ForEach (this IEnumerable This, Action<object> toDo)
        {
            foreach (object element in This)
            {
                toDo?.Invoke(element);
            }
        }

        public static void ForEach<T> (this IEnumerable<T> This, Action<T> toDo)
        {
            foreach (T element in This)
            {
                toDo?.Invoke(element);
            }
        }

        /// <summary>
        /// To use when you are consuming an enumerable that you need IList interface for, but you
        /// might be using something casted to IEnumerable. ToList is the fastest if you're converting
        /// something - but if you don't need to, casting is fastest. This might be good if you need
        /// array indexing, count, or just to do more than one thing in a situation where you don't
        /// know if you *can* go over the list twice.
        /// 
        /// In short, this will, give you the fastest possible <see cref="IReadOnlyList{T}"/> version 
        /// of the given enumerable that you can do what you like with.
        /// </summary>
        public static IReadOnlyList<T> ToReadOnlyListTryCastingFirst<T>(this IEnumerable<T> enumerable)
        {
            if (enumerable is IReadOnlyList<T> readOnlyList)
            {
                return readOnlyList;
            }

            return enumerable.ToList();
        }
    }
}
