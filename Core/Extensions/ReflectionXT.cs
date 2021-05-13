namespace AJut
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using AJut.TypeManagement;

    public static class ReflectionXT
    {
        /// <summary>
        /// Tests to see if the type is an anonymous type
        /// </summary>
        public static bool IsAnonymous(this Type type)
        {
            return type.Namespace == null && type.Name.Contains("<>") && type.Name.Contains("Anonymous");
        }

        #region ========== Attributes =========

        public static IEnumerable<TAttribute> GetAttributes<TAttribute> (this MemberInfo member, AttributeTester<TAttribute> validator = null, bool allowDerivedAttributeTypes = true, bool checkInherited = true) where TAttribute : Attribute
        {
            return AttributeHelper.SearchForAttributes(member.GetCustomAttributes(checkInherited), validator, allowDerivedAttributeTypes);
        }

        public static bool IsTaggedWithAttribute<TAttribute> (this MemberInfo member, AttributeTester<TAttribute> validator = null, bool allowDerivedAttributeTypes = true, bool checkInherited = true) where TAttribute : Attribute
        {
            return member.GetAttributes(validator, allowDerivedAttributeTypes, checkInherited).Any();
        }

        #endregion

        #region ========== Property Management ============

        /// <summary>
        /// Creates an instance of the given type, takes into account special cases
        /// </summary>
        public static object CreateInstance (this Type type)
        {
            return AJutActivator.CreateInstance(type);
        }

        public static object StockWithInstanceIn (this PropertyInfo property, object owner)
        {
            object value = property.PropertyType.CreateInstance();
            property.SetValue(owner, value);
            return value;
        }

        public static string GetPropertyName<TOwnerObject, TProperty> (this TOwnerObject item, Expression<Func<TOwnerObject, TProperty>> propertyLambda)
        {
            var expression = propertyLambda.Body as MemberExpression;
            if (expression == null || expression.Member == null)
            {
                return String.Empty;
            }

            return expression.Member.Name;
        }

        public static PropertyInfo GetComplexProperty (this object item, string propertyPath, out object childTarget)
        {
            return GetComplexProperty(item, propertyPath, out childTarget, false);
        }

        public static PropertyInfo GetComplexProperty (this object item, string propertyPath, out object childTarget, bool ensureSubObjectPath)
        {
            if (item == null || string.IsNullOrEmpty(propertyPath))
            {
                childTarget = null;
                return null;
            }

            int separatorIndex = propertyPath.IndexOf('.');

            // If there is no separator, then this is the end of the line
            if (separatorIndex == -1)
            {
                childTarget = item;
                return childTarget.GetType().GetProperty(propertyPath);
            }
            // Otherwise, recurse (if possible)
            else
            {
                var propertyPartPath = propertyPath.Substring(0, separatorIndex);
                var propertyPart = item.GetType().GetProperty(propertyPartPath);
                if (propertyPart == null)
                {
                    childTarget = null;
                    return null;
                }

                var valuePart = propertyPart.GetValue(item, null);
                if (valuePart == null && ensureSubObjectPath)
                {
                    valuePart = propertyPart.StockWithInstanceIn(item);
                }

                return GetComplexProperty(valuePart, propertyPath.Substring(separatorIndex + 1), out childTarget);
            }
        }

        public static T GetComplexPropertyValue<T> (this object item, string propertyPath)
        {
            return GetComplexPropertyValue<T>(item, propertyPath, ensureSubObjectPath: false);
        }

        public static T GetComplexPropertyValue<T> (this object item, string propertyPath, bool ensureSubObjectPath)
        {
            object value = GetComplexPropertyValue(item, propertyPath, null, ensureSubObjectPath);
            if (value == null)
            {
                return default;
            }

            return (T)value;
        }

        public static T GetComplexPropertyValue<T> (this object item, string propertyPath, object[] index)
        {
            return GetComplexPropertyValue<T>(item, propertyPath, index, ensureSubObjectPath: false);
        }

        public static T GetComplexPropertyValue<T> (this object item, string propertyPath, object[] index, bool ensureSubObjectPath)
        {
            object value = GetComplexPropertyValue(item, propertyPath, index);
            if (value == null)
            {
                return default;
            }

            return (T)value;
        }

        public static object GetComplexPropertyValue (this object item, string propertyPath)
        {
            return item.GetComplexPropertyValue(propertyPath, null, ensureSubObjectPath:false);
        }
        public static object GetComplexPropertyValue (this object item, string propertyPath, bool ensureSubObjectPath)
        {
            return item.GetComplexPropertyValue(propertyPath, null);
        }

        public static object GetComplexPropertyValue (this object item, string propertyPath, object[] index)
        {
            return GetComplexPropertyValue(item, propertyPath, index, ensureSubObjectPath: false);
        }
        public static object GetComplexPropertyValue (this object item, string propertyPath, object[] index, bool ensureSubObjectPath)
        {
            if (propertyPath == "." || propertyPath == "")
            {
                return item;
            }

            int openBracketTextIndex = propertyPath.IndexOf('[');
            while (openBracketTextIndex != -1)
            {
                string subPath = propertyPath.Substring(0, openBracketTextIndex);
                object childItem = item.GetComplexPropertyValue(subPath);
                if (childItem is IList childList && _DoBracketEvaluation(out int childItemElementIndex, out int bracketTextEndIndex))
                {
                    item = childList[childItemElementIndex];
                    openBracketTextIndex = propertyPath.IndexOf('[', openBracketTextIndex + 1);

                    // The bracket was the last thing, return the item
                    if (bracketTextEndIndex + 1 >= propertyPath.Length)
                    {
                        return item;
                    }

                    // Reduce the property path to be what comes after the bracket
                    propertyPath = propertyPath.Substring(bracketTextEndIndex + 1);
                }
                else
                {
                    return null;
                }
            }

            return item.GetComplexProperty(propertyPath, out object childTarget, ensureSubObjectPath)?.GetValue(childTarget, index);

            bool _DoBracketEvaluation (out int index, out int bracketEndIndex)
            {
                index = -1;
                bracketEndIndex = propertyPath.IndexOf(']');
                if (bracketEndIndex == -1)
                {
                    return false;
                }

                if (int.TryParse(propertyPath.SubstringByInd(openBracketTextIndex + 1, bracketEndIndex - 1), out index))
                {
                    return true;
                }

                return false;
            }
        }

        public static bool SetPropertyByComplexPath<T> (this object item, string propertyPath, T value)
        {
            return item.SetPropertyByComplexPath(propertyPath, value, null);
        }

        public static bool SetPropertyByComplexPath<T> (this object item, string propertyPath, T value, object[] index)
        {
            return item.GetComplexProperty(propertyPath, out object childTarget)
                        ?.SetValueExtended(item, propertyPath, childTarget, value, index) ?? false;
        }

        public static bool SetValueExtended<T> (this PropertyInfo prop, object source, string propertyPath, object target, T value)
        {
            return prop.SetValueExtended(source, propertyPath, target, value, null);
        }

        public static bool SetValueExtended<T> (this PropertyInfo prop, object source, string propertyPath, object target, T value, object[] index)
        {
            if (index != null)
            {
                prop.SetValue(target, value, index);
            }
            else
            {
                prop.SetValue(target, value);
            }

            while (target?.GetType().IsValueType ?? false)
            {
                propertyPath = ReduceComplexPath(propertyPath);
                if (propertyPath == null)
                {
                    break;
                }

                source.SetPropertyByComplexPath(propertyPath, target);
            }
            return true;
        }

        private static string ReduceComplexPath(string propertyPath)
        {
            int index = propertyPath.LastIndexOf('.');
            if (index == -1)
            {
                return null;
            }

            return propertyPath.Substring(0, index);
        }


#if WINDOWS_UWP
        public static bool HasPublicGetProperty(this PropertyInfo prop)
        {
            return prop.SetMethod != null && prop.GetMethod.IsPublic;
        }

        public static bool HasPublicSetProperty(this PropertyInfo prop)
        {
            return prop.SetMethod != null && prop.SetMethod.IsPublic;
        }
#endif
        #endregion

        #region ======= Regular Method Invocation ===========
        public static object InvokeMethod(this object This, string methodName)
        {
            return RunInstanceMethod(This, methodName, null);
        }
        public static object InvokeMethod(this object This, string methodName, params object[] methodArgs)
        {
            return RunInstanceMethod(This, methodName, methodArgs);
        }

        public static object InvokeExtensionMethod(this object This, Type extensionDeclarer, string methodName)
        {
            return RunStaticMethod(extensionDeclarer, methodName, This);
        }
        public static object InvokeExtensionMethod(this object This, Type extensionDeclarer, string methodName, params object[] methodArgs)
        {
            List<object> fullMethodSet = new List<object>(methodArgs);
            fullMethodSet.Insert(0, This);
            return RunStaticMethod(extensionDeclarer, methodName, fullMethodSet.ToArray());
        }

        public static object RunInstanceMethod(object instanceToRunOn, string methodName, object[] methodArgs)
        {
            return RunMethod(null, instanceToRunOn, methodName, methodArgs);
        }
        public static object RunStaticMethod(Type methodOwner, string methodName, params object[] methodArgs)
        {
            return RunMethod(methodOwner, null, methodName, methodArgs);
        }
        #endregion

        #region ================== Templated Method Invocation ==================

        public static object InvokeTemplatedMethod(this object This, string methodName, params Type[] templateArgs)
        {
            return RunTemplatedInstanceMethod(This, methodName, templateArgs, null);
        }
        public static object InvokeTemplatedMethod(this object This, string methodName, Type templateArg, params object[] methodArgs)
        {
            return RunTemplatedInstanceMethod(This, methodName, new Type[] { templateArg }, methodArgs);
        }
        public static object InvokeTemplatedMethod(this object This, string methodName, Type[] templateArgs, object[] methodArgs)
        {
            return RunTemplatedInstanceMethod(This, methodName, templateArgs, methodArgs);
        }

        public static object InvokeTemplatedExtensionMethod(this object This, Type extensionDeclarer, string methodName, Type[] templateArgs, object[] methodArgs)
        {
            List<object> fullMethodSet = new List<object>(methodArgs);
            fullMethodSet.Insert(0, This);
            return RunStaticTemplatedMethod(extensionDeclarer, methodName, templateArgs, fullMethodSet.ToArray());
        }
        public static object InvokeTemplatedExtensionMethod(this object This, Type extensionDeclarer, string methodName, params Type[] templateArgs)
        {
            return RunStaticTemplatedMethod(extensionDeclarer, methodName, templateArgs, This);
        }
        public static object InvokeTemplatedExtensionMethod(this object This, Type extensionDeclarer, string methodName, Type templateArg, params object[] methodArgs)
        {
            List<object> fullMethodSet = new List<object>(methodArgs);
            fullMethodSet.Insert(0, This);
            return RunStaticTemplatedMethod(extensionDeclarer, methodName, new Type[] { templateArg }, fullMethodSet.ToArray());
        }

        public static object RunTemplatedInstanceMethod(object instanceToRunOn, string methodName, Type[] templateArgs, object[] methodArgs)
        {
            return RunTemplatedMethod(null, instanceToRunOn, methodName, templateArgs, methodArgs);
        }
        public static object RunStaticTemplatedMethod (Type methodOwner, string methodName, Type templateArg, params object[] methodArgs)
        {
            return RunTemplatedMethod(methodOwner, null, methodName, new Type[] { templateArg }, methodArgs);
        }

        public static object RunStaticTemplatedMethod(Type methodOwner, string methodName, Type[] templateArgs, params object[] methodArgs)
        {
            return RunTemplatedMethod(methodOwner, null, methodName, templateArgs, methodArgs);
        }

        #endregion

        private static object RunTemplatedMethod(Type staticMethodOwner, object instanceToRunOn, string methodName, Type[] templateArgs, object[] methodArgs)
        {
            Type methodOwner = staticMethodOwner ?? instanceToRunOn.GetType();
            MethodInfo method = methodOwner.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(_Filter).FirstOrDefault();
            if (method == null)
            {
                return null;
            }

            method = method.MakeGenericMethod(templateArgs);

            if (staticMethodOwner != null)
            {
                return method.Invoke(null, methodArgs);
            }
            else
            {
                return method.Invoke(instanceToRunOn, methodArgs);
            }

            bool _Filter(MethodInfo _method)
            {
                if (_method.Name != methodName)
                {
                    return false;
                }

                var parameters = _method.GetParameters();
                for(int index = 0; index < parameters.Length; ++index)
                {
                    if (!parameters[index].ParameterType.IsAssignableFrom(methodArgs[index].GetType()))
                    {
                        return false;
                    }
                }

                // This actually could lead to incorrect results as you might have two methods with the same number of generics - but different constraints
                return _method.GetGenericArguments().Length == templateArgs.Length;
            }
        }

        private static object RunMethod(Type staticMethodOwner, object instanceToRunOn, string methodName, object[] methodArgs)
        {
            Type methodOwner;
            BindingFlags flags;
            if (staticMethodOwner != null)
            {
                methodOwner = staticMethodOwner;
                flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            }
            else
            {
                methodOwner = instanceToRunOn.GetType();
                flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            }

            MethodInfo method = methodOwner?.GetMethod(methodName, flags);
            if (method == null)
            {
                return null;
            }

            if (staticMethodOwner != null)
            {
                return method.Invoke(null, methodArgs);
            }
            else
            {
                return method.Invoke(instanceToRunOn, methodArgs);
            }
        }
    }
}
