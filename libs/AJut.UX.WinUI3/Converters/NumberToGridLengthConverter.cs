using Microsoft.UI.Xaml;

namespace AJut.UX.Converters;

public class NumberToGridLengthConverter : SimpleValueConverter<Microsoft.UI.Xaml.GridLength>
{
    protected override GridLength Convert(object objValue)
    {
        double dblValue = System.Convert.ToDouble(objValue);
        return new GridLength(dblValue);
    }
}
