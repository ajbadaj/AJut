namespace AJut.Application.Converters
{
    using System.Linq;
#if WINDOWS_UWP
    using Windows.UI.Xaml;
#else
    using System.Windows;
#endif

    /*Example
     * 
     * -----Single Usage-----
     * ++ You can use these converters directly inside an element by referencing it by namespace and name
     * <TextBlock Visibility="{Binding ElementName=This, Path=IsValidSelection, Converter={nameSpace:BooleanToVisibilityConverter TrueValue=Collapsed, FalseValue=Visible}}" >Color already selected</TextBlock>
     * 
     * -----Multiple Usage---
     * <ResourceDictionary>
            <nameSpace:BooleanToVisibilityConverter TrueValue="Visible" FalseValue="Hidden" x:Key="BoolToVis"/>
     * </ResourceDictionary>
     * 
     * ++ Occasionaly, there may be a situation when you want to bind to the same boolean flag, but you want to invert what
     * ++ the visibility converter does. In this case, you can pass the "Invert" Parameter.
     * 
     * <StackPanel>
     *   <TextBlock Visibility="{Binding Path=df_item.isValid, Converter={StaticResource BoolToVis}}">Invalid selection made</TextBlock>
     *   <Button Visibility="{Binding Path=df_item.isValid, Converter={StaticResource BoolToVis}, ConverterParameter=Invert}">Submit</Button>
     * </StackPanel
     * 
     * ++ In the above case, the textblock with it's error message will be displayed when the item is valid, and the submit button will not
     * ++  and when the item is valid, the submit button is visible, but the textblock is not
     * 
     * */
    public class BooleanConverter<T> : SimpleValueConverter<bool, T>
    {
        public BooleanConverter(T trueValue, T falseValue)
        {
            TrueValue = trueValue;
            FalseValue = falseValue;
        }

        public T TrueValue { get; set; }
        public T FalseValue { get; set; }
        protected override T Convert(bool value)
        {
            return value ? TrueValue : FalseValue;
        }
    }

    public class BooleanToValueConverter : BooleanConverter<object>
    {
        public BooleanToValueConverter (object trueValue, object falseValue) : base(trueValue, falseValue) { }
        public BooleanToValueConverter () : this(true, false) { }
    }

    public sealed class BooleanToVisibilityConverter : BooleanConverter<Visibility>
    {
        public BooleanToVisibilityConverter() : base(Visibility.Visible, Visibility.Collapsed) { }
    }

    public sealed class BooleanInverseConverter : BooleanConverter<bool>
    {
        public BooleanInverseConverter() : base(false, true) { }
    }

#if !WINDOWS_UWP
    public sealed class BooleanAndConverter : SimpleMultiValueConverter<bool,bool>
    {
        protected override bool Convert (bool[] values) => values.All(v=>v);
    }

    public sealed class BooleanOrConverter : SimpleMultiValueConverter<bool, bool>
    {
        protected override bool Convert (bool[] values) => values.Any(v => v);
    }

    public sealed class BooleanAndToVisibilityConverter : SimpleMultiValueConverter<bool, Visibility>
    {
        public Visibility WhenAllTrue { get; set; } = Visibility.Visible;
        public Visibility Else { get; set; } = Visibility.Collapsed;
        protected override Visibility Convert (bool[] values) => values.All(v => v) ? this.WhenAllTrue : this.Else;
    }
#endif
}
