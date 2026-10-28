namespace AJut
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using AJut.TypeManagement;

    public static class ReflectionXT
    {
        private const string kNamedMethodMessage = "Finds a method by name on a type known only at runtime, and trimming can remove a method nothing else calls. Call the method directly where the type is known.";
        private const string kPropertyPathMessage = "Reaches properties by name, or creates a property's value, on types known only at runtime, and trimming can remove a property or constructor nothing else uses.";
        private const string kGenericMethodMessage = "Makes a generic method for type arguments known only at runtime, which an ahead of time compiled build (iOS, NativeAOT) may never have compiled.";

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
        public static object CreateInstance ([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] this Type type)
        {
            return AJutActivator.CreateInstanceOf(type);
        }

        [RequiresUnreferencedCode(kPropertyPathMessage)]
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

        [RequiresUnreferencedCode(kPropertyPathMessage)]
        public static PropertyInfo GetComplexProperty (this object item, string propertyPath, out object childTarget)
        {
            return GetComplexProperty(item, propertyPath, out childTarget, false);
        }

        [RequiresUnreferencedCode(kPropertyPathMessage)]
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

        [RequiresUnreferencedCode(kPropertyPathMessage)]
        public static T GetComplexPropertyValue<T> (this object item, string propertyPath)
        {
            return GetComplexPropertyValue<T>(item, propertyPath, ensureSubObjectPath: false);
        }

        [RequiresUnreferencedCode(kPropertyPathMessage)]
        public static T GetComplexPropertyValue<T> (this object item, string propertyPath, bool ensureSubObjectPath)
        {
            object value = GetComplexPropertyValue(item, propertyPath, null, ensureSubObjectPath);
            if (value == null)
            {
                return default;
            }

            return (T)value;
        }

        [RequiresUnreferencedCode(kPropertyPathMessage)]
        public static T GetComplexPropertyValue<T> (this object item, string propertyPath, object[] index)
        {
            return GetComplexPropertyValue<T>(item, propertyPath, index, ensureSubObjectPath: false);
        }

        [RequiresUnreferencedCode(kPropertyPathMessage)]
        public static T GetComplexPropertyValue<T> (this object item, string propertyPath, object[] index, bool ensureSubObjectPath)
        {
            object value = GetComplexPropertyValue(item, propertyPath, index);
            if (value == null)
            {
                return default;
            }

            return (T)value;
        }

        [RequiresUnreferencedCode(kPropertyPathMessage)]
        public static object GetComplexPropertyValue (this object item, string propertyPath)
        {
            return item.GetComplexPropertyValue(propertyPath, null, ensureSubObjectPath:false);
        }
        [RequiresUnreferencedCode(kPropertyPathMessage)]
        public static object GetComplexPropertyValue (this object item, string propertyPath, bool ensureSubObjectPath)
        {
            return item.GetComplexPropertyValue(propertyPath, null);
        }

        [RequiresUnreferencedCode(kPropertyPathMessage)]
        public static object GetComplexPropertyValue (this object item, string propertyPath, object[] index)
        {
            return GetComplexPropertyValue(item, propertyPath, index, ensureSubObjectPath: false);
        }
        [RequiresUnreferencedCode(kPropertyPathMessage)]
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

        [RequiresUnreferencedCode(kPropertyPathMessage)]
        public static bool SetPropertyByComplexPath<T> (this object item, string propertyPath, T value)
        {
            return item.SetPropertyByComplexPath(propertyPath, value, null);
        }

        [RequiresUnreferencedCode(kPropertyPathMessage)]
        public static bool SetPropertyByComplexPath<T> (this object item, string propertyPath, T value, object[] index)
        {
            return item.GetComplexProperty(propertyPath, out object childTarget)
                        ?.SetValueExtended(item, propertyPath, childTarget, value, index) ?? false;
        }

        [RequiresUnreferencedCode(kPropertyPathMessage)]
        public static bool SetValueExtended<T> (this PropertyInfo prop, object source, string propertyPath, object target, T value)
        {
            return prop.SetValueExtended(source, propertyPath, target, value, null);
        }

        [RequiresUnreferencedCode(kPropertyPathMessage)]
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
        #endregion

        #region ======= Regular Method Invocation ===========
        [RequiresUnreferencedCode(kNamedMethodMessage)]
        public static object InvokeMethod(this object This, string methodName)
        {
            return RunInstanceMethod(This, methodName, null);
        }
        [RequiresUnreferencedCode(kNamedMethodMessage)]
        public static object InvokeMethod(this object This, string methodName, params object[] methodArgs)
        {
            return RunInstanceMethod(This, methodName, methodArgs);
        }

        [RequiresUnreferencedCode(kNamedMethodMessage)]
        public static object InvokeExtensionMethod(this object This, Type extensionDeclarer, string methodName)
        {
            return RunStaticMethod(extensionDeclarer, methodName, This);
        }
        [RequiresUnreferencedCode(kNamedMethodMessage)]
        public static object InvokeExtensionMethod(this object This, Type extensionDeclarer, string methodName, params object[] methodArgs)
        {
            List<object> fullMethodSet = new List<object>(methodArgs);
            fullMethodSet.Insert(0, This);
            return RunStaticMethod(extensionDeclarer, methodName, fullMethodSet.ToArray());
        }

        [RequiresUnreferencedCode(kNamedMethodMessage)]
        public static object RunInstanceMethod(object instanceToRunOn, string methodName, object[] methodArgs)
        {
            return RunMethod(null, instanceToRunOn, methodName, methodArgs);
        }
        [RequiresUnreferencedCode(kNamedMethodMessage)]
        public static object RunStaticMethod(Type methodOwner, string methodName, params object[] methodArgs)
        {
            return RunMethod(methodOwner, null, methodName, methodArgs);
        }
        #endregion

        #region ================== Templated Method Invocation ==================

        [RequiresUnreferencedCode(kNamedMethodMessage)]
        [RequiresDynamicCode(kGenericMethodMessage)]
        public static object InvokeTemplatedMethod(this object This, string methodName, params Type[] templateArgs)
        {
            return RunTemplatedInstanceMethod(This, methodName, templateArgs, null);
        }
        [RequiresUnreferencedCode(kNamedMethodMessage)]
        [RequiresDynamicCode(kGenericMethodMessage)]
        public static object InvokeTemplatedMethod(this object This, string methodName, Type templateArg, params object[] methodArgs)
        {
            return RunTemplatedInstanceMethod(This, methodName, new Type[] { templateArg }, methodArgs);
        }
        [RequiresUnreferencedCode(kNamedMethodMessage)]
        [RequiresDynamicCode(kGenericMethodMessage)]
        public static object InvokeTemplatedMethod(this object This, string methodName, Type[] templateArgs, object[] methodArgs)
        {
            return RunTemplatedInstanceMethod(This, methodName, templateArgs, methodArgs);
        }

        [RequiresUnreferencedCode(kNamedMethodMessage)]
        [RequiresDynamicCode(kGenericMethodMessage)]
        public static object InvokeTemplatedExtensionMethod(this object This, Type extensionDeclarer, string methodName, Type[] templateArgs, object[] methodArgs)
        {
            List<object> fullMethodSet = new List<object>(methodArgs);
            fullMethodSet.Insert(0, This);
            return RunStaticTemplatedMethod(extensionDeclarer, methodName, templateArgs, fullMethodSet.ToArray());
        }
        [RequiresUnreferencedCode(kNamedMethodMessage)]
        [RequiresDynamicCode(kGenericMethodMessage)]
        public static object InvokeTemplatedExtensionMethod(this object This, Type extensionDeclarer, string methodName, params Type[] templateArgs)
        {
            return RunStaticTemplatedMethod(extensionDeclarer, methodName, templateArgs, This);
        }
        [RequiresUnreferencedCode(kNamedMethodMessage)]
        [RequiresDynamicCode(kGenericMethodMessage)]
        public static object InvokeTemplatedExtensionMethod(this object This, Type extensionDeclarer, string methodName, Type templateArg, params object[] methodArgs)
        {
            List<object> fullMethodSet = new List<object>(methodArgs);
            fullMethodSet.Insert(0, This);
            return RunStaticTemplatedMethod(extensionDeclarer, methodName, new Type[] { templateArg }, fullMethodSet.ToArray());
        }

        [RequiresUnreferencedCode(kNamedMethodMessage)]
        [RequiresDynamicCode(kGenericMethodMessage)]
        public static object RunTemplatedInstanceMethod(object instanceToRunOn, string methodName, Type[] templateArgs, object[] methodArgs)
        {
            return RunTemplatedMethod(null, instanceToRunOn, methodName, templateArgs, methodArgs);
        }
        [RequiresUnreferencedCode(kNamedMethodMessage)]
        [RequiresDynamicCode(kGenericMethodMessage)]
        public static object RunStaticTemplatedMethod (Type methodOwner, string methodName, Type templateArg, params object[] methodArgs)
        {
            return RunTemplatedMethod(methodOwner, null, methodName, new Type[] { templateArg }, methodArgs);
        }

        [RequiresUnreferencedCode(kNamedMethodMessage)]
        [RequiresDynamicCode(kGenericMethodMessage)]
        public static object RunStaticTemplatedMethod(Type methodOwner, string methodName, Type[] templateArgs, params object[] methodArgs)
        {
            return RunTemplatedMethod(methodOwner, null, methodName, templateArgs, methodArgs);
        }

        #endregion

        [RequiresUnreferencedCode(kNamedMethodMessage)]
        [RequiresDynamicCode(kGenericMethodMessage)]
        private static object RunTemplatedMethod(Type staticMethodOwner, object instanceToRunOn, string methodName, Type[] templateArgs, object[] methodArgs)
        {
            Type methodOwner = staticMethodOwner ?? instanceToRunOn.GetType();
            BindingFlags flags = (staticMethodOwner != null ? BindingFlags.Static : BindingFlags.Instance) | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo method = methodOwner.GetMethods(flags).Where(_Filter).FirstOrDefault();
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
                if (parameters.Length != (methodArgs?.Length ?? 0))
                {
                    return false;
                }

                for (int index = 0; index < parameters.Length; ++index)
                {
                    // A parameter typed by the method's own type arguments (a T value) isn't known until it is made
                    //  generic, so only the others are checked against what was passed
                    Type parameterType = parameters[index].ParameterType;
                    if (parameterType.ContainsGenericParameters || methodArgs[index] == null)
                    {
                        continue;
                    }

                    if (!parameterType.IsAssignableFrom(methodArgs[index].GetType()))
                    {
                        return false;
                    }
                }

                // This actually could lead to incorrect results as you might have two methods with the same number of generics - but different constraints
                return _method.GetGenericArguments().Length == templateArgs.Length;
            }
        }

        [RequiresUnreferencedCode(kNamedMethodMessage)]
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
