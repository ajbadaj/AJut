namespace AJut.Storage
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using AJut.TypeManagement;

    // The element types Stratabase makes lists of, each one able to make its own List<T> and list access.
    //
    // Stratabase stores a list as a List<T> of its element type and reads it back as one, but it often only has the
    //  element type as a Type: a List<int> property found by reflection, the first element's GetType(), or the type a
    //  stored list was made with. Making a List<T> from a Type (MakeGenericType) needs that List<T>'s code made at
    //  runtime when it was never compiled ahead of time, which an iOS build cannot do for a value type. Every reference
    //  type shares one compiled List<T>, so those are still made from their Type there.
    //
    // So each element type is registered by code that names it, where the compiler sees the List<T> it needs:
    //  StrataPropertyListAccess<T> registers its element type, so do the typed Stratabase calls and
    //  Stratabase.RegisterListElementType<T>, and the common built-in value types are registered from the start. Where
    //  dynamic code is supported (anywhere with a JIT) an unregistered type is still made from its Type, so nothing
    //  changes there. A value type that is neither, on a platform without dynamic code, fails with what to register.
    internal abstract class StrataListElementType
    {
        private static readonly ConcurrentDictionary<Type, StrataListElementType> g_elementTypes = new ConcurrentDictionary<Type, StrataListElementType>();

        // ===========[ Setup/Construction/Teardown ]===========================
        static StrataListElementType ()
        {
            Register<bool>();
            Register<byte>();
            Register<sbyte>();
            Register<char>();
            Register<short>();
            Register<ushort>();
            Register<int>();
            Register<uint>();
            Register<long>();
            Register<ulong>();
            Register<float>();
            Register<double>();
            Register<decimal>();
            Register<Guid>();
            Register<DateTime>();
            Register<DateTimeOffset>();
            Register<TimeSpan>();
        }

        // ===========[ Public Interface Methods ]==============================
        /// <summary>Registers <typeparamref name="TElement"/> as an element type Stratabase can make lists of; registering one twice is harmless</summary>
        public static void Register<TElement> ()
        {
            if (!g_elementTypes.ContainsKey(typeof(TElement)))
            {
                g_elementTypes.TryAdd(typeof(TElement), new ElementType<TElement>());
            }
        }

        /// <summary>The element type for <paramref name="elementType"/>; throws when it cannot be made on this platform</summary>
        public static StrataListElementType For (Type elementType)
        {
            if (g_elementTypes.TryGetValue(elementType, out StrataListElementType registered))
            {
                return registered;
            }

            if (elementType.IsValueType && !DynamicCodeAvailability.IsSupported)
            {
                throw new InvalidOperationException($"Stratabase cannot make a list of {elementType.FullName} on a platform without dynamic code (an iOS build compiled ahead of time) until that element type is registered. Call Stratabase.{nameof(Stratabase.RegisterListElementType)}<{elementType.Name}>() before storing one.");
            }

            return g_elementTypes.GetOrAdd(elementType, MakeFromType);
        }

        /// <summary>A new, empty List of this element type</summary>
        public abstract IList CreateList ();

        /// <summary>A List of this element type holding <paramref name="elements"/>, as a list property's baseline</summary>
        public abstract IList CreateBaselineStorage (object[] elements);

        /// <summary>A <see cref="StrataPropertyListAccess{TElement}"/> of this element type for the given object and property</summary>
        public abstract IDisposable CreateListAccess (Stratabase stratabase, Guid id, string propertyName);

        // ===========[ Helper Methods ]========================================
        [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Reached for a value type only where dynamic code is supported (For() checks first); a reference type's ElementType<T> runs on the shared code an ahead of time build compiles for every reference type.")]
        private static StrataListElementType MakeFromType (Type elementType)
        {
            Type closedType = typeof(ElementType<>).MakeGenericType(elementType);
            return (StrataListElementType)Activator.CreateInstance(closedType);
        }

        // ===========[ Subclasses/structs ]====================================
        private sealed class ElementType<TElement> : StrataListElementType
        {
            public override IList CreateList () => new List<TElement>();
            public override IList CreateBaselineStorage (object[] elements) => (IList)StrataPropertyListAccess<TElement>.GenerateStorageForBaselineListAccess(elements);
            public override IDisposable CreateListAccess (Stratabase stratabase, Guid id, string propertyName) => stratabase.GenerateListPropertyAccess<TElement>(id, propertyName);
        }
    }
}
