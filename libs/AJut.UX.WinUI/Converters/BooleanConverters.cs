namespace AJut.UX.Converters
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Media;
    using Windows.UI;

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
    public abstract class BooleanConverter<T> : SimpleValueConverter<bool, T>
    {
        public BooleanConverter (T trueValue, T falseValue)
        {
            TrueValue = trueValue;
            FalseValue = falseValue;
        }

        public T TrueValue { get; set; }
        public T FalseValue { get; set; }
        protected override T Convert (bool value)
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
        public BooleanToVisibilityConverter () : base(Visibility.Visible, Visibility.Collapsed) { }
    }

    public sealed class BooleanInverseConverter : BooleanConverter<bool>
    {
        public BooleanInverseConverter () : base(false, true) { }
    }

    public sealed class BooleanToDoubleConverter : BooleanConverter<double>
    {
        public BooleanToDoubleConverter () : base(1.0, 0.0) { }
    }

    public sealed class BooleanToColorConverter : BooleanConverter<Color>
    {
        private static Color kTransparent = new Color();
        public BooleanToColorConverter() : base(kTransparent , kTransparent)
        {
        }
    }

    public sealed class BooleanToBrushConverter : BooleanConverter<Brush>
    {
        private static Brush kTransparent = new SolidColorBrush(new Color());
        public BooleanToBrushConverter() : base(kTransparent, kTransparent)
        {
        }
    }
}
