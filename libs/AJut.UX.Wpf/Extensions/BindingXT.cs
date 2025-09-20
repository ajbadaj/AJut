namespace AJut.UX
{
    using System.Windows;
    using System.Windows.Data;
    public static class BindingXT
    {
        public static Binding CreateAttachedBinding (this object This, DependencyProperty target, BindingMode mode = BindingMode.Default)
        {
            return new Binding() { Source = This, Path = new PropertyPath($"({target.OwnerType.FullName}.{target.Name})"), Mode = mode };
        }

        public static Binding CreateBinding (this object This, string path, BindingMode mode = BindingMode.Default)
        {
            return new Binding(path) { Source = This, Mode = mode };
        }

        public static void ClearBinding (this DependencyObject This, DependencyProperty target)
        {
            BindingOperations.ClearBinding(This, target);
        }
    }
}