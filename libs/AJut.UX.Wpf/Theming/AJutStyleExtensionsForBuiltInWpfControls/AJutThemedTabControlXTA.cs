namespace AJut.UX.Theming.AJutStyleExtensionsForBuiltInWpfControls
{
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Controls;

    public static class AJutThemedTabControlXTA
    {
        private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(AJutThemedTabControlXTA));

        public static DependencyProperty TabUnselectedPadProperty = APUtils.Register(GetTabUnselectedPad, SetTabUnselectedPad, 3);
        public static int GetTabUnselectedPad (DependencyObject obj) => (int)obj.GetValue(TabUnselectedPadProperty);
        public static void SetTabUnselectedPad (DependencyObject obj, int value) => obj.SetValue(TabUnselectedPadProperty, value);


        public static DependencyProperty TabSelectionIndicatorSizeProperty = APUtils.Register(GetTabSelectionIndicatorSize, SetTabSelectionIndicatorSize);
        public static double GetTabSelectionIndicatorSize (DependencyObject obj) => (double)obj.GetValue(TabSelectionIndicatorSizeProperty);
        public static void SetTabSelectionIndicatorSize (DependencyObject obj, double value) => obj.SetValue(TabSelectionIndicatorSizeProperty, value);
    }
}
