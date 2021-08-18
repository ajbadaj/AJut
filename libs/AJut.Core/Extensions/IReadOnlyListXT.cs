namespace AJut.Extensions
{
    using System;
    using System.Collections.Generic;

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
