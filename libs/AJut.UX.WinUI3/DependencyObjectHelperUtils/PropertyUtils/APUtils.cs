namespace AJut.UX
{
    using System;
    using System.Diagnostics;
    using System.Reflection;
    using Microsoft.UI.Xaml;

    public static class APUtils<TOwner>
    {
        // ==============================================================================================================
        // ====             Registering Regular Attached Properties                                                 =====
        // ==============================================================================================================

        /// <summary>
        /// Registers an attached property with the dependency property system.
        /// Note the default value for this attached property will be default(<see cref="TProperty"/>)
        /// </summary>
        /// <typeparam name="TProperty">The type of the attached property (inferred)</typeparam>
        /// <param name="getter">The attached property's getter (verified for required verbiage and type).</param>
        /// <param name="setter">The attached property's setter (verified for required verbiage and type).</param>
        /// <returns>The <see cref="DependencyProperty"/> that was registered for this attached property.</returns>
        public static DependencyProperty Register<TProperty> (StaticAPUtils.GetterFunc<TProperty> getter, StaticAPUtils.SetterFunc<TProperty> setter)
        {
            return Register(getter, setter, new PropertyMetadata(default(TProperty)));
        }

        /// <summary>
        /// Registers an attached property with the dependency property system.
        /// </summary>
        /// <typeparam name="TProperty">The type of the attached property (inferred)</typeparam>
        /// <param name="getter">The attached property's getter (verified for required verbiage and type).</param>
        /// <param name="setter">The attached property's setter (verified for required verbiage and type).</param>
        /// <param name="defaultValue">The default value for the attached property.</param>
        /// <returns>The <see cref="DependencyProperty"/> that was registered for this attached property.</returns>
        public static DependencyProperty Register<TProperty> (StaticAPUtils.GetterFunc<TProperty> getter, StaticAPUtils.SetterFunc<TProperty> setter, TProperty defaultValue)
        {
            return Register(getter, setter, new PropertyMetadata(defaultValue));
        }

        /// <summary>
        /// Registers an attached property with the dependency property system.
        /// Note the default value for this attached property will be default(<see cref="TProperty"/>)
        /// </summary>
        /// <typeparam name="TProperty">The type of the attached property (inferred)</typeparam>
        /// <param name="getter">The attached property's getter (verified for required verbiage and type).</param>
        /// <param name="setter">The attached property's setter (verified for required verbiage and type).</param>
        /// <param name="propChanged">The property changed callback delegate.</param>
        /// <returns>The <see cref="DependencyProperty"/> that was registered for this attached property.</returns>
        public static DependencyProperty Register<TProperty> (StaticAPUtils.GetterFunc<TProperty> getter, StaticAPUtils.SetterFunc<TProperty> setter, CastedPropertyChangedCallback<TProperty> propChanged)
        {
            return Register(getter, setter, new PropertyMetadata(default(TProperty), DownCast(propChanged)));
        }

        /// <summary>
        /// Registers an attached property with the dependency property system.
        /// </summary>
        /// <typeparam name="TProperty">The type of the attached property (inferred)</typeparam>
        /// <param name="getter">The attached property's getter (verified for required verbiage and type).</param>
        /// <param name="setter">The attached property's setter (verified for required verbiage and type).</param>
        /// <param name="defaultValue">The default value for the attached property.</param>
        /// <param name="propChanged">The property changed callback delegate.</param>
        /// <returns>The <see cref="DependencyProperty"/> that was registered for this attached property.</returns>
        public static DependencyProperty Register<TProperty> (StaticAPUtils.GetterFunc<TProperty> getter, StaticAPUtils.SetterFunc<TProperty> setter, TProperty defaultValue, CastedPropertyChangedCallback<TProperty> propChanged)
        {
            return Register(getter, setter, new PropertyMetadata(defaultValue, DownCast(propChanged)));
        }

        /// <summary>
        /// Registers an attached property with the dependency property system.
        /// </summary>
        /// <typeparam name="TProperty">The type of the attached property (inferred)</typeparam>
        /// <param name="getter">The attached property's getter (verified for required verbiage and type).</param>
        /// <param name="setter">The attached property's setter (verified for required verbiage and type).</param>
        /// <param name="metadata">The property metadata to register for this attached property with the dependency property system.</param>
        /// <returns>The <see cref="DependencyProperty"/> that was registered for this attached property.</returns>
        /// <exception cref="System.Exception">
        /// APUtils::Register - Getter Error - Attached properties require a getter formatted Get{name}.
        /// or
        /// APUtils::Register - Setter Error - Attached properties require a getter formatted Set{name}.
        /// or
        /// APUtils::Register - Attached properties require a getter and setter formatted Get{name}/Set{name} where the names match.
        /// </exception>
        public static DependencyProperty Register<TProperty> (StaticAPUtils.GetterFunc<TProperty> getter, StaticAPUtils.SetterFunc<TProperty> setter, PropertyMetadata metadata)
        {
            StaticAPUtils.ValidateAccessors(getter, setter);

            // Could use getter or setter, it's the same either way
            string name = getter.GetMethodInfo().Name.Substring(3);

            return DependencyProperty.RegisterAttached(name, typeof(TProperty), typeof(TOwner), metadata);
        }

        /// <summary>
        /// Represents the callback that is invoked when the effective property value
        /// of a dependency property changes.
        /// </summary>
        /// <param name="self">The <see cref="TOwnerObject"/> that changed.</param>
        /// <param name="e">The <see cref="DependencyPropertyChangedEventArgs"/> instance containing the information about what changed.</param>
        public delegate void CastedPropertyChangedCallback<T> (DependencyObject target, DependencyPropertyChangedEventArgs<T> e);

        /// <summary>
        /// Converts a <see cref="CastedPropertyChangedCallback"/> to a <see cref="PropertyChangedCallback"/>.
        /// </summary>
        /// <param name="callback">The callback.</param>
        /// <returns>a <see cref="PropertyChangedCallback"/> or <c>null</c> if the input is null.</returns>
        internal static PropertyChangedCallback DownCast<T> (CastedPropertyChangedCallback<T> callback)
        {
            return (callback == null) ? null : new PropertyChangedCallback((d, e) => callback(d, new DependencyPropertyChangedEventArgs<T>(e)));
        }
    }

    /// <summary>
    /// Slightly different than DPUtils since attached properties are usually registered to static classes, instead of a using, you
    /// can build attached properties with an APUtilsRegistrar static variable, ie:
    /// 
    /// public static class MyAPClass {
    ///     private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(MyAPClass));
    ///     
    ///     public DependencyProperty StatusProperty = APUtils.Register(GetStatus, SetStatus);
    ///     
    ///     public static bool GetStatus(DependencyObject obj) { ... }
    ///     public static void SetStatus(DependencyObject obj, bool value) { ... }
    /// }
    /// </summary>
    public class APUtilsRegistrationHelper
    {
        Type m_targetType;
        public APUtilsRegistrationHelper (Type t)
        {
            m_targetType = t;
        }

        // ==============================================================================================================
        // ====             Registering Regular Attached Properties                                                 =====
        // ==============================================================================================================

        /// <summary>
        /// Registers an attached property with the dependency property system.
        /// Note the default value for this attached property will be default(<see cref="TProperty"/>)
        /// </summary>
        /// <typeparam name="TProperty">The type of the attached property (inferred)</typeparam>
        /// <param name="getter">The attached property's getter (verified for required verbiage and type).</param>
        /// <param name="setter">The attached property's setter (verified for required verbiage and type).</param>
        /// <returns>The <see cref="DependencyProperty"/> that was registered for this attached property.</returns>
        public DependencyProperty Register<TProperty> (StaticAPUtils.GetterFunc<TProperty> getter, StaticAPUtils.SetterFunc<TProperty> setter)
        {
            return Register(getter, setter, new PropertyMetadata(default(TProperty)));
        }

        /// <summary>
        /// Registers an attached property with the dependency property system.
        /// </summary>
        /// <typeparam name="TProperty">The type of the attached property (inferred)</typeparam>
        /// <param name="getter">The attached property's getter (verified for required verbiage and type).</param>
        /// <param name="setter">The attached property's setter (verified for required verbiage and type).</param>
        /// <param name="defaultValue">The default value for the attached property.</param>
        /// <returns>The <see cref="DependencyProperty"/> that was registered for this attached property.</returns>
        public DependencyProperty Register<TProperty> (StaticAPUtils.GetterFunc<TProperty> getter, StaticAPUtils.SetterFunc<TProperty> setter, TProperty defaultValue)
        {
            return Register(getter, setter, new PropertyMetadata(defaultValue));
        }

        /// <summary>
        /// Registers an attached property with the dependency property system.
        /// Note the default value for this attached property will be default(<see cref="TProperty"/>)
        /// </summary>
        /// <typeparam name="TProperty">The type of the attached property (inferred)</typeparam>
        /// <param name="getter">The attached property's getter (verified for required verbiage and type).</param>
        /// <param name="setter">The attached property's setter (verified for required verbiage and type).</param>
        /// <param name="propChanged">The property changed callback delegate.</param>
        /// <returns>The <see cref="DependencyProperty"/> that was registered for this attached property.</returns>
        public DependencyProperty Register<TProperty> (StaticAPUtils.GetterFunc<TProperty> getter, StaticAPUtils.SetterFunc<TProperty> setter, CastedPropertyChangedCallback<TProperty> propChanged)
        {
            return Register(getter, setter, new PropertyMetadata(default(TProperty), DownCast(propChanged)));
        }

        /// <summary>
        /// Registers an attached property with the dependency property system.
        /// </summary>
        /// <typeparam name="TProperty">The type of the attached property (inferred)</typeparam>
        /// <param name="getter">The attached property's getter (verified for required verbiage and type).</param>
        /// <param name="setter">The attached property's setter (verified for required verbiage and type).</param>
        /// <param name="defaultValue">The default value for the attached property.</param>
        /// <param name="propChanged">The property changed callback delegate.</param>
        /// <returns>The <see cref="DependencyProperty"/> that was registered for this attached property.</returns>
        public DependencyProperty Register<TProperty> (StaticAPUtils.GetterFunc<TProperty> getter, StaticAPUtils.SetterFunc<TProperty> setter, TProperty defaultValue, CastedPropertyChangedCallback<TProperty> propChanged)
        {
            return Register(getter, setter, new PropertyMetadata(defaultValue, DownCast(propChanged)));
        }

        /// <summary>
        /// Registers an attached property with the dependency property system.
        /// </summary>
        /// <typeparam name="TProperty">The type of the attached property (inferred)</typeparam>
        /// <param name="getter">The attached property's getter (verified for required verbiage and type).</param>
        /// <param name="setter">The attached property's setter (verified for required verbiage and type).</param>
        /// <param name="metadata">The property metadata to register for this attached property with the dependency property system.</param>
        /// <returns>The <see cref="DependencyProperty"/> that was registered for this attached property.</returns>
        /// <exception cref="System.Exception">
        /// APUtils::Register - Getter Error - Attached properties require a getter formatted Get{name}.
        /// or
        /// APUtils::Register - Setter Error - Attached properties require a getter formatted Set{name}.
        /// or
        /// APUtils::Register - Attached properties require a getter and setter formatted Get{name}/Set{name} where the names match.
        /// </exception>
        public DependencyProperty Register<TProperty> (StaticAPUtils.GetterFunc<TProperty> getter, StaticAPUtils.SetterFunc<TProperty> setter, PropertyMetadata metadata)
        {
            StaticAPUtils.ValidateAccessors(getter, setter);

            // Could use getter or setter, it's the same either way
            string name = getter.GetMethodInfo().Name.Substring(3);

            return DependencyProperty.RegisterAttached(name, typeof(TProperty), m_targetType, metadata);
        }

        /// <summary>
        /// Represents the callback that is invoked when the effective property value
        /// of a dependency property changes.
        /// </summary>
        /// <param name="self">The <see cref="TOwnerObject"/> that changed.</param>
        /// <param name="e">The <see cref="DependencyPropertyChangedEventArgs"/> instance containing the information about what changed.</param>
        public delegate void CastedPropertyChangedCallback<T> (DependencyObject target, DependencyPropertyChangedEventArgs<T> e);

        /// <summary>
        /// Converts a <see cref="CastedPropertyChangedCallback"/> to a <see cref="PropertyChangedCallback"/>.
        /// </summary>
        /// <param name="callback">The callback.</param>
        /// <returns>a <see cref="PropertyChangedCallback"/> or <c>null</c> if the input is null.</returns>
        internal static PropertyChangedCallback DownCast<T> (CastedPropertyChangedCallback<T> callback)
        {
            return (callback == null) ? null : new PropertyChangedCallback((d, e) => callback(d, new DependencyPropertyChangedEventArgs<T>(e)));
        }
    }


    /// <summary>
    /// A version of the APUtils that is usable by static classes (which is a common use case for attached properties). To note, static classes are not
    /// avaialble to be used as template arguments, which is why this class was created. The only difference between this class and the typed APUtils 
    /// is that the type is specified per register call, making this utility only slightly less verbose than declaring attached properties regularly, but 
    /// with the added static validation.
    /// </summary>
    public static class StaticAPUtils
    {
        /// <summary>
        /// The delegate used to specify a getter for an attached property.
        /// </summary>
        /// <typeparam name="TProperty">The type of the attached property (inferred).</typeparam>
        /// <param name="obj">The <see cref="DependencyObject"/> to get the attached property value from.</param>
        /// <returns>The attached property value.</returns>
        public delegate TProperty GetterFunc<out TProperty> (DependencyObject obj);

        /// <summary>
        /// The delegate used to specify a setter for an attached property.
        /// </summary>
        /// <typeparam name="TProperty">The type of the attached property (inferred).</typeparam>
        /// <param name="obj">The <see cref="DependencyObject"/> to set the attached property value on.</param>
        /// <param name="propType">The new attached property value.</param>
        public delegate void SetterFunc<in TProperty> (DependencyObject obj, TProperty propType);

        // ==============================================================================================================
        // ====             Registering Regular Attached Properties                                                 =====
        // ==============================================================================================================

        /// <summary>
        /// Registers an attached property with the dependency property system.
        /// Note the default value for this attached property will be default(<see cref="TProperty"/>)
        /// </summary>
        /// <typeparam name="TProperty">The type of the attached property (inferred)</typeparam>
        /// <param name="ownerType">The attached property owner -- can't be inferred :'(</param>
        /// <param name="getter">The attached property's getter (verified for required verbiage and type).</param>
        /// <param name="setter">The attached property's setter (verified for required verbiage and type).</param>
        /// <returns>The <see cref="DependencyProperty"/> that was registered for this attached property.</returns>
        public static DependencyProperty Register<TProperty> (Type ownerType, GetterFunc<TProperty> getter, SetterFunc<TProperty> setter)
        {
            return Register(ownerType, getter, setter, new PropertyMetadata(default(TProperty)));
        }

        /// <summary>
        /// Registers an attached property with the dependency property system.
        /// </summary>
        /// <typeparam name="TProperty">The type of the attached property (inferred)</typeparam>
        /// <param name="ownerType">The attached property owner -- can't be inferred :'(</param>
        /// <param name="getter">The attached property's getter (verified for required verbiage and type).</param>
        /// <param name="setter">The attached property's setter (verified for required verbiage and type).</param>
        /// <param name="defaultValue">The default value for the attached property.</param>
        /// <returns>The <see cref="DependencyProperty"/> that was registered for this attached property.</returns>
        public static DependencyProperty Register<TProperty> (Type ownerType, GetterFunc<TProperty> getter, SetterFunc<TProperty> setter, TProperty defaultValue)
        {
            return Register(ownerType, getter, setter, new PropertyMetadata(defaultValue));
        }

        /// <summary>
        /// Registers an attached property with the dependency property system.
        /// Note the default value for this attached property will be default(<see cref="TProperty"/>)
        /// </summary>
        /// <typeparam name="TProperty">The type of the attached property (inferred)</typeparam>
        /// <param name="ownerType">The attached property owner -- can't be inferred :'(</param>
        /// <param name="getter">The attached property's getter (verified for required verbiage and type).</param>
        /// <param name="setter">The attached property's setter (verified for required verbiage and type).</param>
        /// <param name="propChanged">The property changed callback delegate.</param>
        /// <returns>The <see cref="DependencyProperty"/> that was registered for this attached property.</returns>
        public static DependencyProperty Register<TProperty> (Type ownerType, GetterFunc<TProperty> getter, SetterFunc<TProperty> setter, PropertyChangedCallback propChanged)
        {
            return Register(ownerType, getter, setter, new PropertyMetadata(default(TProperty), propChanged));
        }

        /// <summary>
        /// Registers an attached property with the dependency property system.
        /// </summary>
        /// <typeparam name="TProperty">The type of the attached property (inferred)</typeparam>
        /// <param name="ownerType">The attached property owner -- can't be inferred :'(</param>
        /// <param name="getter">The attached property's getter (verified for required verbiage and type).</param>
        /// <param name="setter">The attached property's setter (verified for required verbiage and type).</param>
        /// <param name="defaultValue">The default value for the attached property.</param>
        /// <param name="propChanged">The property changed callback delegate.</param>
        /// <returns>The <see cref="DependencyProperty"/> that was registered for this attached property.</returns>
        public static DependencyProperty Register<TProperty> (Type ownerType, GetterFunc<TProperty> getter, SetterFunc<TProperty> setter, TProperty defaultValue, PropertyChangedCallback propChanged)
        {
            return Register(ownerType, getter, setter, new PropertyMetadata(defaultValue, propChanged));
        }

        /// <summary>
        /// Registers an attached property with the dependency property system.
        /// </summary>
        /// <typeparam name="TProperty">The type of the attached property (inferred)</typeparam>
        /// <param name="ownerType">The attached property owner -- can't be inferred :'(</param>
        /// <param name="getter">The attached property's getter (verified for required verbiage and type).</param>
        /// <param name="setter">The attached property's setter (verified for required verbiage and type).</param>
        /// <param name="metadata">The property metadata to register for this attached property with the dependency property system.</param>
        /// <returns>The <see cref="DependencyProperty"/> that was registered for this attached property.</returns>
        /// <exception cref="System.Exception">
        /// APUtils::Register - Getter Error - Attached properties require a getter formatted Get{name}.
        /// or
        /// APUtils::Register - Setter Error - Attached properties require a getter formatted Set{name}.
        /// or
        /// APUtils::Register - Attached properties require a getter and setter formatted Get{name}/Set{name} where the names match.
        /// </exception>
        public static DependencyProperty Register<TProperty> (Type ownerType, GetterFunc<TProperty> getter, SetterFunc<TProperty> setter, PropertyMetadata metadata)
        {
            ValidateAccessors(getter, setter);

            // Could use getter or setter, it's the same either way
            string name = getter.GetMethodInfo().Name.Substring(3);

            return DependencyProperty.RegisterAttached(name, typeof(TProperty), ownerType, metadata);
        }

        /// <summary>
        /// Validates the getter and setter functions for a particular attached property (Debug Only).
        /// </summary>
        /// <typeparam name="TProperty">The property type.</typeparam>
        /// <param name="getter">The getter method.</param>
        /// <param name="setter">The setter method.</param>
        /// <exception cref="System.Exception">
        /// APUtils::Registration -- Attached Properties must have a getter!
        /// or
        /// APUtils::Registration -- Attached Properties must have a setter!
        /// or
        /// APUtils::Registration -- Getter Error: Attached properties require a getter formatted Get{name}.
        /// or
        /// APUtils::Registration -- Setter Error: Attached properties require a setter formatted Set{name}.
        /// or
        /// APUtils::Register -- Attached properties require a getter and setter formatted Get{name}/Set{name} where the names match.
        /// </exception>
        [Conditional("DEBUG")]
        public static void ValidateAccessors<TProperty> (GetterFunc<TProperty> getter, SetterFunc<TProperty> setter)
        {
            if (getter == null)
            {
                throw new Exception("Attached Properties must have a getter!");
            }

            if (setter == null)
            {
                throw new Exception("Attached Properties must have a setter!");
            }

            string getterName = getter.GetMethodInfo().Name;
            string setterName = setter.GetMethodInfo().Name;
            if (!getterName.StartsWith("Get"))
            {
                throw new Exception("Getter Error: Attached properties require a getter formatted Get{name}.");
            }

            if (!setterName.StartsWith("Set"))
            {
                throw new Exception("Setter Error: Attached properties require a setter formatted Set{name}.");
            }

            if (getterName.Substring(3) != setterName.Substring(3))
            {
                throw new Exception("Attached properties require a getter and setter formatted Get{name}/Set{name} where the names match.");
            }
        }

    }
}