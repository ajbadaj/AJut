﻿namespace AJut
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public delegate bool Matcher<T>(T first, T second);

	public static class EnumerableExtensions
	{
        public static bool IsEmpty<T>(this IEnumerable<T> This)
        {
            return !This.Any();
        }
        public static bool IsNullOrEmpty<T>(this IEnumerable<T> This)
        {
            return This == null || !This.Any();
        }
		public static bool IsNotEmpty<T>(this IEnumerable<T> This)
		{
			return This.Any();
		}
		public static bool IsNotNullOrEmpty<T>(this IEnumerable<T> This)
		{
			return This != null && This.Any();
		}
		public static IEnumerable Cast(this IEnumerable This, Type type)
		{
			return This.InvokeTemplatedExtensionMethod(typeof(Enumerable), "Cast", type) as IEnumerable;
		}
		public static Array ToArray(this IEnumerable This, Type type)
		{
			return This.InvokeTemplatedExtensionMethod(typeof(Enumerable), "ToArray", type) as Array;
		}
		public static Array CastToArrayOfType(this IEnumerable This, Type type)
		{
			IEnumerable result = This.Cast(type);
            if (result == null)
            {
                return null;
            }

			return result.ToArray(type);
		}

		public static string ConcatToString(this IEnumerable This, string separator)
		{
            return String.Join(separator, This.OfType<object>().Select(_ => _.ToString()).ToArray());
		}

		public static string ConcatToString(this IEnumerable This, string separator, string prefix, string suffix)
		{
            return prefix + This.ConcatToString(separator) + suffix;
		}

		public static bool ContainsAny<T>(this IEnumerable<T> This, Comparison<T> comparer, IEnumerable<T> other)
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
        public static bool ContainsAnyThatPass<T>(this IEnumerable<T> This, Func<T, bool> matcher)
        {
            foreach(T element in This)
            {
                if(matcher(element))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ContainsAny<T>(this IEnumerable<T> This, IEnumerable<T> other)
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

        public static void ForEach(this IEnumerable This, Action<object> toDo)
        {
            foreach (object element in This)
            {
                toDo?.Invoke(element);
            }
        }

        public static void ForEach<T>(this IEnumerable<T> This, Action<T> toDo)
        {
            foreach(T element in This)
            {
                toDo?.Invoke(element);
            }
        }
	}    
}
