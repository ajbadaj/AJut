namespace AJut.Application.AttachedProperties
{
    using System.Windows;

    public static class BorderXTA
    {
        private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(BorderXTA));

        public static DependencyProperty CornerRadiusProperty = APUtils.Register(GetCornerRadius, SetCornerRadius);

        public static CornerRadius GetCornerRadius (DependencyObject obj) => (CornerRadius)obj.GetValue(CornerRadiusProperty);
        public static void SetCornerRadius (DependencyObject obj, CornerRadius value) => obj.SetValue(CornerRadiusProperty, value);
    }
}
