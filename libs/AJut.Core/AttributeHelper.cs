namespace AJut
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public delegate bool AttributeTester<TAttribute>(TAttribute attribute) where TAttribute : Attribute;
    public static class AttributeHelper
    {
        /// <summary>
        /// Returns all attributes of the indicated type and matching the indicated parameters
        /// </summary>
        /// <param name="validator">A validator (default == null, everything)</param>
        /// <param name="allowDerivedAttributeTypes">Should types derived from <see cref="TAttribute"/> be allowed?</param>
        /// <returns>An enumerable collection of the attribute types requested</returns>
        public static IEnumerable<TAttribute> SearchForAttributes<TAttribute>(IEnumerable attributes, AttributeTester<TAttribute> validator = null, bool allowDerivedAttributeTypes = true) where TAttribute : Attribute
        {
            validator = validator ?? new AttributeTester<TAttribute>(attr => true);
            foreach (object obj in attributes)
            {
                if (allowDerivedAttributeTypes)
                {
                    if (obj is TAttribute casted && validator(casted))
                    {
                        yield return casted;
                    }
                }
                else
                {
                    if (typeof(TAttribute) == obj.GetType() && validator((TAttribute)obj))
                    {
                        yield return (TAttribute)obj;
                    }
                }
            }
        }

        public static bool HasAny<TAttribute>(IEnumerable attributes, AttributeTester<TAttribute> validator = null, bool allowDerivedAttributeTypes = true) where TAttribute : Attribute
        {
            return SearchForAttributes(attributes, validator, allowDerivedAttributeTypes).Any();
        }
    }
}
