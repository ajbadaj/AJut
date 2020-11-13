namespace AJut
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

    public static class ReflectionXT
    {
        #region ========== Attributes =========

        public static IEnumerable<TAttribute> GetAttributes<TAttribute>(this MemberInfo member, AttributeTester<TAttribute> validator = null, bool allowDerivedAttributeTypes = true, bool checkInherited = true) where TAttribute : Attribute
        {
            return AttributeHelper.SearchForAttributes(member.GetCustomAttributes(checkInherited), validator, allowDerivedAttributeTypes);
        }

        public static bool IsTaggedWithAttribute<TAttribute>(this MemberInfo member, AttributeTester<TAttribute> validator = null, bool allowDerivedAttributeTypes = true, bool checkInherited = true) where TAttribute : Attribute
        {
            return member.GetAttributes(validator, allowDerivedAttributeTypes, checkInherited).Any();
        }

        #endregion

        #region ========== Property Fetching ============

        public static string GetPropertyName<TOwnerObject,TProperty>(this TOwnerObject item, Expression<Func<TOwnerObject, TProperty>> propertyLambda)
        {
            var expression = propertyLambda.Body as MemberExpression;
            if(expression == null || expression.Member == null)
            {
                return String.Empty;
            }

            return expression.Member.Name;
        }
        public static PropertyInfo GetComplexProperty(this object item, string propertyPath)
        {
            if (item == null || string.IsNullOrEmpty(propertyPath))
                return null;

            int index = propertyPath.IndexOf('.');
            if (index == -1)
            {
                return item.GetType().GetProperty(propertyPath);
            }
            else
            {
                var propertyPartPath = propertyPath.Substring(0, index);
                var propertyPart = item.GetType().GetProperty(propertyPartPath);
                if (propertyPart == null)
                    return null;

                var valuePart = propertyPart.GetValue(item, null);
                return GetComplexProperty(valuePart, propertyPath.Substring(index + 1));
            }
        }

        public static T GetComplexPropertyValue<T>(this object item, string propertyPath)
        {
            return (T)GetComplexPropertyValue(item, propertyPath);
        }

        public static object GetComplexPropertyValue(this object item, string propertyPath)
        {
            if (item == null || propertyPath == null)
            {
                return null;
            }

            if (propertyPath == "." || propertyPath == "")
            {
                return item;
            }

            int index = propertyPath.IndexOf('.');
            if (index == -1)
            {
                PropertyInfo itemProperty = item.GetType().GetProperty(propertyPath);
                if (itemProperty != null)
                {
                    return itemProperty.GetValue(item, null);
                }
                return null;
            }
            else
            {
                string sPropertyPartPath = propertyPath.Substring(0, index);
                PropertyInfo propertyPart = item.GetType().GetProperty(sPropertyPartPath);
                if (propertyPart == null)
                {
                    return null;
                }

                object valuePart = propertyPart.GetValue(item, null);
                return GetComplexPropertyValue(valuePart, propertyPath.Substring(index + 1));
            }
        }

        public static bool SetPropertyByComplexPath<T>(this object item, string propertyPath, T value)
        {
            PropertyInfo prop = item.GetComplexProperty(propertyPath);
            if(prop == null)
            {
                return false;
            }

            prop.SetValue(item, value);
            return true;
        }

        public static bool SetPropertyByComplexPath<T>(this object item, string propertyPath, T value, object[] index)
        {
            PropertyInfo prop = item.GetComplexProperty(propertyPath);
            if (prop == null)
            {
                return false;
            }

            prop.SetValue(item, value, index);
            return true;
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
            MethodInfo method = methodOwner.GetMethod(methodName);
            if (method == null)
                return null;

            method = method.MakeGenericMethod(templateArgs);

            if (staticMethodOwner != null)
            {
                return method.Invoke(null, methodArgs);
            }
            else
            {
                return method.Invoke(instanceToRunOn, methodArgs);
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
