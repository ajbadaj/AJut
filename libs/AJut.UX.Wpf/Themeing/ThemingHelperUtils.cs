namespace AJut.UX.Themeing
{
    using System.Windows;
    using System.Windows.Media;


    public static class ThemingHelperUtils
    {
        private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(ThemingHelperUtils));


        // Adding a window border glow brush for theming the way it's done in ajut ThemedControlStylesBase
        public static DependencyProperty WindowBorderGlowBrushProperty = APUtils.Register(GetWindowBorderGlowBrush, SetWindowBorderGlowBrush);
        public static Brush GetWindowBorderGlowBrush (DependencyObject obj) => (Brush)obj.GetValue(WindowBorderGlowBrushProperty);
        public static void SetWindowBorderGlowBrush (DependencyObject obj, Brush value) => obj.SetValue(WindowBorderGlowBrushProperty, value);

    }
}
