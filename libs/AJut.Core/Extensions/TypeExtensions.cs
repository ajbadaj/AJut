namespace AJut
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    [Flags]
    public enum eNumericTypeFlag
    {
        Integer = 0xb00000,
        Float = 0xb00001,
        Double = 0xb00010,
        Decimal = 0xb00100,

        /// <summary>
        /// Special types that are used as integers - currently only Enum types
        /// </summary>
        SpecialInteger = 0xb01000,

        /// <summary>
        /// Indicates all special numbers, eg GridLength, that can be cast to double, these are registered outside of this core dll
        /// </summary>
        SpecialDouble = 0xb10000,

        /// <summary>
        /// Float or Double or Decimal
        /// </summary>
        BaseFractionalNumber = Float | Double | Decimal,

        /// <summary>
        /// Integer or SpecialInteger values
        /// </summary>
        AnyIntegerNumber = Integer | SpecialInteger,

        /// <summary>
        /// Float or Double or Decimal or any of the SpecialDouble values
        /// </summary>
        AnyFractionalNumber = BaseFractionalNumber | SpecialDouble,

        Any = 0xb11111,
    };

    public delegate double CastToDoubleTyped<T> (T item);
    public delegate double CastToDouble (object item);

    public static class TypeXT
    {
        public static Dictionary<Type, CastToDouble> g_specialDoubleCasters = new Dictionary<Type, CastToDouble>();

        public static T BoxCast<T> (this object This)
        {
            return (T)This;
        }

        public static object BoxCast (this object This, Type t)
        {
            return This.InvokeTemplatedExtensionMethod(typeof(TypeXT), nameof(BoxCast), t);
        }

        public static void RegisterSpecialDouble<T> (CastToDoubleTyped<T> doubleCaster)
        {
            RegisterSpecialDouble(typeof(T), o => doubleCaster((T)o));
        }
        public static void RegisterSpecialDouble (Type sourceType, CastToDouble doubleCaster)
        {
            g_specialDoubleCasters[sourceType] = doubleCaster;
        }

        /// <summary>
        /// Checks if the type is a type simple type, primitives, strings, and other things expressable via constants (ie 6.28f).
        /// </summary>
        public static bool IsSimpleType (this Type source)
        {
            return source.IsPrimitive || source.IsNumericType() || source == typeof(string)
                    || source == typeof(DateTime) || source == typeof(TimeSpan);
        }

        /// <summary>
        /// Search for an interface up through the tree, ie v.GetType().FindInterfaceRecursive(typeof(IDictionary&lt;string, string&gt;)). This is preferable because
        /// you may have something derived from something, derived from something, that implements IDictionary that you want generic args for, where you couldn't do
        /// that directly on your v.GetyType.GetGenericArguments() would not work.
        /// </summary>
        public static Type FindBaseTypeOrInterface (this Type source, Type target)
        {
            if (source.TargetsSameTypeAs(target))
            {
                return source;
            }

            Type found = null;
            if (target.IsInterface)
            {
                found = source.GetInterfaces().FirstOrDefault(t => t.TargetsSameTypeAs(target));
            }

            if (found != null)
            {
                return found;
            }

            if (source.BaseType == null)
            {
                return null;
            }

            return source.BaseType.FindBaseTypeOrInterface(target);
        }

        /// <summary>
        /// Because of generics types can be the same type, ie List&lt;int&gt; == List&lt;,&gt; is the same class, but
        /// they are technically different types. This test will match them based on their generic
        /// </summary>
        public static bool TargetsSameTypeAs (this Type source, Type target)
        {
            if (!source.IsGenericType || !target.IsGenericType)
            {
                return source == target;
            }

            // Both are generic
            if (source.IsGenericTypeDefinition && target.IsGenericTypeDefinition)
            {
                return source == target;
            }
            // source is, target not
            else if (source.IsGenericTypeDefinition && !target.IsGenericTypeDefinition)
            {
                return source == target.GetGenericTypeDefinition();
            }
            // source not, target is
            else if (!source.IsGenericTypeDefinition && target.IsGenericTypeDefinition)
            {
                return source.GetGenericTypeDefinition() == target;
            }

            // Both not
            return source.GetGenericTypeDefinition() == target.GetGenericTypeDefinition();
        }

        /// <summary>
        /// Returns a bool indicating if a type is numeric
        /// </summary>
        public static bool IsNumericType (this Type This, eNumericTypeFlag flag = eNumericTypeFlag.Any)
        {
            if (flag.HasFlag(eNumericTypeFlag.Integer) && (This == typeof(Int16) || This == typeof(Int32) || This == typeof(Int64)))
            {
                return true;
            }

            if (flag.HasFlag(eNumericTypeFlag.SpecialInteger) && typeof(Enum).IsAssignableFrom(This))
            {
                return true;
            }

            if (flag.HasFlag(eNumericTypeFlag.Float) && This == typeof(float))
            {
                return true;
            }

            if (flag.HasFlag(eNumericTypeFlag.Double) && This == typeof(double))
            {
                return true;
            }

            if (flag.HasFlag(eNumericTypeFlag.Decimal) && This == typeof(decimal))
            {
                return true;
            }

            if (flag.HasFlag(eNumericTypeFlag.SpecialDouble) && g_specialDoubleCasters.ContainsKey(This))
            {
                return true;
            }

            return false;
        }

        public static bool TryCastSpecialDouble<T> (T inst, out double result)
        {
            CastToDouble caster;
            if (g_specialDoubleCasters.TryGetValue(typeof(T), out caster))
            {
                result = caster(inst);
                return true;
            }
            else
            {
                result = -1.0;
                return false;
            }
        }

        public static double CastSpecialDouble<T> (T inst)
        {
            return g_specialDoubleCasters[typeof(T)](inst);
        }

        /// <summary>
        /// Returns all attributes of the indicated type and matching the indicated parameters
        /// </summary>
        /// <param name="validator">A validator (default == null, everything)</param>
        /// <param name="allowDerivedAttributeTypes">Should types derived from <typeparamref name="TAttribute"/> be allowed?</param>
        /// <param name="checkInherited">Should attributes from ancestor classes of the passed in type be considered?</param>
        /// <returns>An enumerable collection of the attribute types requested</returns>
        public static IEnumerable<TAttribute> GetAttributes<TAttribute> (this Type type, AttributeTester<TAttribute> validator = null, bool allowDerivedAttributeTypes = true, bool checkInherited = true) where TAttribute : Attribute
        {
            return AttributeHelper.SearchForAttributes(type.GetCustomAttributes(checkInherited), validator, allowDerivedAttributeTypes);
        }

        public static bool IsTaggedWithAttribute<TAttribute> (this Type type, AttributeTester<TAttribute> validator = null, bool allowDerivedAttributeTypes = true, bool checkInherited = true) where TAttribute : Attribute
        {
            return type.GetAttributes(validator, allowDerivedAttributeTypes, checkInherited).Any();
        }
    }
}
