namespace AJut.UX
{
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Data;

    public static class BindingXT
    {
        public static Binding CreateBinding (this object This, string path, BindingMode mode = BindingMode.OneWay)
        {
            return new Binding() { Source = This, Path = new PropertyPath(path), Mode = mode };
        }
    }
}