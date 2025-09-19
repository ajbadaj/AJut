namespace AJut.UX.Converters
{
    using System;
    using System.Globalization;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Data;
    using Microsoft.UI.Xaml.Markup;

    // ===============================================================================================
    // Since most of the time, I don't care about the culture info, globalization, etc. - and I want
    //  almost all converters to work this way, making one nasty file with all the #if statements
    //  and deriving the others from this one
    // ===============================================================================================
    public abstract class SimpleValueConverter<TSource, TTarget> : MarkupExtension, IValueConverter
    {
        // =========================== Markup Extension Part ======================================
        protected override object ProvideValue () => this.DoProvideValue(null);

        protected override object ProvideValue (IXamlServiceProvider serviceProvider) => this.DoProvideValue(serviceProvider);
        protected virtual object DoProvideValue (IXamlServiceProvider _) => this;

        // =========================== Converter Part ==============================================
        public object Convert (object value, Type targetType, object parameter, string language)
        {
            return this.Convert((TSource)value, targetType, parameter);
        }

        public object ConvertBack (object value, Type targetType, object parameter, string language)
        {
            return this.ConvertBack((TTarget)value, targetType, parameter);
        }


        protected virtual TTarget Convert (TSource value, Type targetType, object parameter)
        {
            return this.Convert(value, parameter);
        }
        protected virtual TTarget Convert (TSource value, object parameter)
        {
            return this.Convert(value);
        }
        protected virtual TTarget Convert (TSource value)
        {
            throw new NotImplementedException();
        }

        protected virtual TSource ConvertBack (TTarget convertedValue, Type sourceType, object parameter)
        {
            return this.ConvertBack(convertedValue, parameter);
        }
        protected virtual TSource ConvertBack (TTarget convertedValue, object parameter)
        {
            return this.ConvertBack(convertedValue);
        }
        protected virtual TSource ConvertBack (TTarget convertedValue)
        {
            throw new NotImplementedException();
        }
    }

    public abstract class SimpleValueConverter<TTarget> : SimpleValueConverter<object, TTarget> { }

    public abstract class SimpleValueConverter : SimpleValueConverter<object, object> { }
}
