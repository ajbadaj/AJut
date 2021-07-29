namespace AJut.Application.Converters
{
    using System;

#if WINDOWS_UWP
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Data;
    using Windows.UI.Xaml.Markup;
#else
    using System.Globalization;
    using System.Linq;
    using System.Windows.Data;
    using System.Windows.Markup;
#endif

    // ===============================================================================================
    // Since most of the time, I don't care about the culture info, globalization, etc. - and I want
    //  almost all converters to work this way, making one nasty file with all the #if statements
    //  and deriving the others from this one
    // ===============================================================================================
    public abstract class SimpleValueConverter<TSource,TTarget> : MarkupExtension, IValueConverter
    {
        // =========================== Markup Extension Part ======================================
#if WINDOWS_UWP
        protected override object ProvideValue() => this.DoProvideValue();
#else
        public override object ProvideValue (IServiceProvider serviceProvider) => this.DoProvideValue();
#endif
        protected virtual object DoProvideValue () => this;

        // =========================== Converter Part ==============================================
        public object Convert(object value, Type targetType, object parameter,
#if WINDOWS_UWP
            string language
#else
            CultureInfo culture
#endif
            )
        {
            return this.Convert((TSource)value, targetType, parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter,
#if WINDOWS_UWP
            string language
#else
            CultureInfo culture
#endif
            )
        {
            return this.ConvertBack((TTarget)value, targetType, parameter);
        }


        protected virtual TTarget Convert(TSource value, Type targetType, object parameter)
        {
            return this.Convert(value, parameter);
        }
        protected virtual TTarget Convert(TSource value, object parameter)
        {
            return this.Convert(value);
        }
        protected virtual TTarget Convert(TSource value)
        {
            throw new NotImplementedException();
        }

        protected virtual TSource ConvertBack(TTarget convertedValue, Type sourceType, object parameter)
        {
            return this.ConvertBack(convertedValue, parameter);
        }
        protected virtual TSource ConvertBack(TTarget convertedValue, object parameter)
        {
            return this.ConvertBack(convertedValue);
        }
        protected virtual TSource ConvertBack(TTarget convertedValue)
        {
            throw new NotImplementedException();
        }
    }

    public abstract class SimpleValueConverter<TTarget> : SimpleValueConverter<object, TTarget> { }

    public abstract class SimpleValueConverter : SimpleValueConverter<object,object> { }

#if !WINDOWS_UWP
    public abstract class SimpleMultiValueConverter<TSource, TTarget> : MarkupExtension, IMultiValueConverter
    {
        public override object ProvideValue (IServiceProvider serviceProvider) => this;

        public object Convert (object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return this.Convert(this.UpCast(values), targetType, parameter);
        }

        public object[] ConvertBack (object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return this.DownCast(this.ConvertBack((TTarget)value, parameter));
        }

        private TSource[] UpCast (object[] data)
        {
            return data.OfType<TSource>().ToArray();
        }
        private object[] DownCast(TSource[] data)
        {
            return data.OfType<object>().ToArray();
        }


        protected virtual TTarget Convert (TSource[] values, Type targetType, object parameter)
        {
            return this.Convert(values, parameter);
        }
        protected virtual TTarget Convert (TSource[] values, object parameter)
        {
            return this.Convert(values);
        }
        protected virtual TTarget Convert (TSource[] values)
        {
            throw new NotImplementedException();
        }

        protected virtual TSource[] ConvertBack (TTarget convertedValue, Type sourceType, object parameter)
        {
            return this.ConvertBack(convertedValue, parameter);
        }
        protected virtual TSource[] ConvertBack (TTarget convertedValue, object parameter)
        {
            return this.ConvertBack(convertedValue);
        }
        protected virtual TSource[] ConvertBack (TTarget convertedValue)
        {
            throw new NotImplementedException();
        }
    }
    public abstract class SimpleMultiValueConverter<TTarget> : SimpleMultiValueConverter<object, TTarget> { }
    public abstract class SimpleMultiValueConverter : SimpleMultiValueConverter<object, object> { }
#endif
}
