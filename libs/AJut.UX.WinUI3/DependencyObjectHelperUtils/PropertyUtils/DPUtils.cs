namespace AJut.UX
{
    using System;
    using System.Diagnostics;
    using System.Linq.Expressions;
    using Microsoft.UI.Xaml;

    /// <summary>
    /// A utility set for simplifying the code required for registering dependency properties, and hopefully for increasing readability. 
    /// For ease of use, it may be helpful to declare a 'using DPUtils = DPUtils<YourType>;' at the top of your code file.
    /// </summary>
    public static class DPUtils<TOwnerObject> where TOwnerObject : DependencyObject
    {
        /// <summary>
        /// Represents the callback that is invoked when the effective property value
        /// of a dependency property changes.
        /// </summary>
        /// <param name="self">The <see cref="TOwnerObject"/> that changed.</param>
        /// <param name="e">The <see cref="DependencyPropertyChangedEventArgs"/> instance containing the information about what changed.</param>
        public delegate void CastedPropertyChangedCallback<T> (TOwnerObject self, DependencyPropertyChangedEventArgs<T> e);

        /// <summary>
        /// Provides a template for a method that is called whenever a dependency property
        /// value is being re-evaluated, or coercion is specifically requested.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="self">The object that the property exists on.</param>
        /// <param name="baseValue">The new value of the property, prior to any coercion attempt.</param>
        /// <returns>The coerced value (with appropriate type).</returns>
        public delegate T CastedCoerceValueCallback<T> (TOwnerObject self, object baseValue);

        /// <summary>
        /// Represents a method used as a callback that validates the effective value of a dependency property.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="value">The value to be validated.</param>
        /// <returns><c>true</c> if the value was validated; <c>false</c> if the submitted value was invalid.</returns>
        public delegate bool CastedValidateValueCallback<T> (T value);

        /// <summary>
        /// Converts a <see cref="CastedPropertyChangedCallback"/> to a <see cref="PropertyChangedCallback"/>.
        /// </summary>
        /// <param name="callback">The callback.</param>
        /// <returns>a <see cref="PropertyChangedCallback"/> or <c>null</c> if the input is null.</returns>
        private static PropertyChangedCallback DownCast<T> (CastedPropertyChangedCallback<T> callback)
        {
            return (callback == null) ? null : new PropertyChangedCallback((d, e) => callback((TOwnerObject)d, new DependencyPropertyChangedEventArgs<T>(e)));
        }

        // ==============================================================================================================
        // ====             Registering Regular Dependency Properties                                               =====
        // ==============================================================================================================

        /// <summary>
        /// Registers a dependency with the dependency property system for the <see cref="TOwnerObject"/> based on the property passed in.
        /// Note the default value for this dependency property will be default(<see cref="TProperty"/>)
        /// </summary>
        /// <typeparam name="TProperty">The property type (inferred from the lambda).</typeparam>
        /// <param name="propertyLambda">The property indicator lambda.</param>
        /// <returns>The <see cref="DependencyProperty"/> that was created and registered with the system.</returns>
        public static DependencyProperty Register<TProperty> (Expression<Func<TOwnerObject, TProperty>> propertyLambda)
        {
            return Register(propertyLambda, new PropertyMetadata(default(TProperty)));
        }

        /// <summary>
        /// Registers a dependency with the dependency property system for the <see cref="TOwnerObject"/> based on the property passed in.
        /// </summary>
        /// <typeparam name="TProperty">The property type (inferred from the lambda).</typeparam>
        /// <param name="propertyLambda">The property indicator lambda.</param>
        /// <param name="defaultValue">The default value for the dependency property.</param>
        /// <returns>The <see cref="DependencyProperty"/> that was created and registered with the system.</returns>
        public static DependencyProperty Register<TProperty> (Expression<Func<TOwnerObject, TProperty>> propertyLambda, TProperty defaultValue)
        {
            return Register(propertyLambda, new PropertyMetadata(defaultValue));
        }

        /// <summary>
        /// Registers a dependency with the dependency property system for the <see cref="TOwnerObject"/> based on the property passed in.
        /// Note the default value for this dependency property will be default(<see cref="TProperty"/>)
        /// </summary>
        /// <typeparam name="TProperty">The property type (inferred from the lambda).</typeparam>
        /// <param name="propertyLambda">The property indicator lambda.</param>
        /// <param name="propChanged">The property changed handler.</param>
        /// <returns>The <see cref="DependencyProperty"/> that was created and registered with the system.</returns>
        public static DependencyProperty Register<TProperty> (Expression<Func<TOwnerObject, TProperty>> propertyLambda, CastedPropertyChangedCallback<TProperty> propChanged)
        {
            return Register(propertyLambda, new PropertyMetadata(default(TProperty), DownCast(propChanged)));
        }

        /// <summary>
        /// Registers a dependency with the dependency property system for the <see cref="TOwnerObject"/> based on the property passed in.
        /// </summary>
        /// <typeparam name="TProperty">The property type (inferred from the lambda).</typeparam>
        /// <param name="propertyLambda">The property indicator lambda.</param>
        /// <param name="defaultValue">The default value.</param>
        /// <param name="propChanged">The property changed handler.</param>
        /// <returns>The <see cref="DependencyProperty"/> that was created and registered with the system.</returns>
        public static DependencyProperty Register<TProperty> (Expression<Func<TOwnerObject, TProperty>> propertyLambda, TProperty defaultValue, CastedPropertyChangedCallback<TProperty> propChanged)
        {
            return Register(propertyLambda, new PropertyMetadata(defaultValue, DownCast(propChanged)));
        }

        /// <summary>
        /// Registers a dependency with the dependency property system for the <see cref="TOwnerObject"/> based on the property passed in.
        /// </summary>
        /// <typeparam name="TProperty">The property type (inferred from the lambda).</typeparam>
        /// <param name="propertyLambda">The property indicator lambda.</param>
        /// <param name="metadata">The property metadata to register for this dependency property with the dependency property system.</param>
        /// <returns>The <see cref="DependencyProperty"/> that was created and registered with the system.</returns>
        /// <exception cref="System.Exception">DPUtils::Register requires an expression that indicates a property.</exception>
        public static DependencyProperty Register<TProperty> (Expression<Func<TOwnerObject, TProperty>> propertyLambda, PropertyMetadata metadata)
        {
            string name = null;
            switch (propertyLambda?.Body)
            {
                case MemberExpression member:
                    name = member.Member.Name;
                    break;

                case UnaryExpression unary:
                    name = (unary.Operand as MemberExpression)?.Member.Name;
                    break;
            }

            if (name == null)
            {
                throw new Exception("Invalid DPUtils target was null");
            }

            var expression = propertyLambda?.Body as MemberExpression;
            //ValidatePropertyExpression(expression);

            return DependencyProperty.Register(name, typeof(TProperty), typeof(TOwnerObject), metadata);
        }

        /// <summary>
        /// Validates the property expression and throws if it's invalid. This will only execute in DEBUG mode.
        /// </summary>
        /// <param name="expression">The expression.</param>
        [Conditional("DEBUG")]
        private static void ValidatePropertyExpression (MemberExpression expression)
        {
            if (expression == null)
            {
                throw new Exception("A valid property indicator expression must be provided.");
            }
        }
    }
}
