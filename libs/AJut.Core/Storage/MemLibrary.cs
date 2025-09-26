namespace AJut.Storage
{
    using System;
    using System.Collections;

    /// <summary>
    /// Creates a low-heap-allocated experience for creating (and recycling) memory fast!
    /// </summary>
    public static class MemLibrary
    {
        private static int g_initialShelfCapacities = 500;
        private static bool g_supportConcurrency = true;

        // ============================================ [ Setup ] ==========================================

        /// <summary>
        /// Sets up the <see cref="MemLibrary"/>. This is meant to be called one time at startup, this will NOT change pools already allocated.
        /// </summary>
        /// <param name="shelfCapacities">Every type gets it's own shelf, how big should the shelves be at maximum? (default = 500)</param>
        /// <param name="supportConcurrency">Should special actions be taken to enable concurrent access (marginally slower - default = true)</param>
        public static void Setup(int shelfCapacities = 500, bool supportConcurrency = true)
        {
            g_initialShelfCapacities = shelfCapacities;
            g_supportConcurrency = supportConcurrency;
        }


        // ============================================ [ Primary Interface Methods ] ==========================================

        /// <summary>
        /// Take memory from the library that will return itself when the returned <see cref="ManagedShelfItem{T}"/> is disposed
        /// </summary>
        public static ManagedShelfItem<T> Checkout<T>() where T : new()
        {
            return new ManagedShelfItem<T>(Take<T>());
        }

        /// <summary>
        /// Take memory from the library that will return itself when the returned <see cref="ManagedShelfItem{T}"/> is disposed
        /// </summary>
        public static ManagedShelfItem<TValue> Checkout<TValue, TConcierge>(TConcierge concierge)
            where TValue : new()
            where TConcierge : IMemLibraryConcierge<TValue>
        {
            return new ManagedShelfItem<TValue>(Take<TValue, TConcierge>(concierge));
        }

        /// <summary>
        /// Take memory from the library directly (unmanaged) - just remember to return it manually later, or face the librarian!
        /// </summary>
        public static T Take<T>() where T : new()
        {
            return MemShelving<T>.FindOrMake();
        }

        /// <summary>
        /// Take memory from the library directly (unmanaged) - just remember to return it manually later, or face the librarian!
        /// </summary>
        /// <param name="concierge">Manages the checkout experience</param>
        public static TValue Take<TValue, TConcierge>(TConcierge concierge)
            where TValue : new()
            where TConcierge : IMemLibraryConcierge<TValue>
        {
            TValue value = MemShelving<TValue>.FindOrMake();
            concierge.Prepare(value);
            return value;
        }

        /// <summary>
        /// Returns memory back to the library
        /// </summary>
        public static bool Return<T>(T value) where T : new()
        {
            if (value is IList list)
            {
                list.Clear();
            }

            return MemShelving<T>.AddIfSpace(value);
        }

        // ============================================ [ Sub Classes ] ==========================================

        /// <summary>
        /// Allows checkout from the <see cref="MemLibrary"/> using the disposable pattern, 
        /// where memory is returned to the <see cref="MemLibrary"/> automatically on dispose
        /// </summary>
        public struct ManagedShelfItem<T> : IDisposable
                where T : new()
        {
            public ManagedShelfItem(T value)
            {
                this.Value = value;
            }

            public T Value { get; }
            public void Dispose()
            {
                MemLibrary.Return(this.Value);
            }
        }

        /// <summary>
        /// Manages the memory for the given type
        /// </summary>
        private class MemShelving<T> where T : new()
        {
            private static T[] g_elements;
            private static object g_shelfLock = new object();
            private static volatile bool g_isInitialized = false;
            private static int g_count = 0;

            private static void EnsureInitialized()
            {
                if (!g_isInitialized) // Fast check
                {
                    lock (g_shelfLock) // Lock for thread safety
                    {
                        if (!g_isInitialized) // Slow check after acquiring lock
                        {
                            // The single, intended HEAP ALLOCATION (at startup)
                            g_elements = new T[g_initialShelfCapacities];
                            g_isInitialized = true;
                        }
                    }
                }
            }

            public static T FindOrMake()
            {
                EnsureInitialized();

                if (g_supportConcurrency)
                {
                    lock (g_shelfLock)
                    {
                        return _DoFindOrMake();
                    }
                }

                return _DoFindOrMake();

                static T _DoFindOrMake()
                {
                    if (g_count > 0)
                    {
                        g_count--;
                        T item = g_elements[g_count];
                        g_elements[g_count] = default!; // The bang operator tells the NRT "shut up man, I'm not breaking nullable references, trust me bro" (see Stack.cs TryPop)
                        return item;
                    }

                    return new T();
                }
            }

            public static bool AddIfSpace(T value)
            {
                EnsureInitialized();

                if (g_supportConcurrency)
                {
                    lock (g_shelfLock)
                    {
                        return _DoAdd(value);
                    }
                }
                else
                {
                    return _DoAdd(value);
                }

                static bool _DoAdd(T value)
                {
                    // If there's room, add it on
                    if (g_count < g_elements.Length)
                    {
                        g_elements[g_count] = value;
                        ++g_count;
                        return true;
                    }
                    //  Shelf is full, item is discarded for GC
                    else
                    {
                        return false;
                    }
                }
            }
        }
    }
}
