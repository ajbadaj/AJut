namespace AJut.UX.Theming.TabControlAJutStyling
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;

    public static class TabControlXTA
    {
        private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(TabControlXTA));

        public static DependencyProperty TabUnselectedPadProperty = APUtils.Register(GetTabUnselectedPad, SetTabUnselectedPad, 3);
        public static int GetTabUnselectedPad (DependencyObject obj) => (int)obj.GetValue(TabUnselectedPadProperty);
        public static void SetTabUnselectedPad (DependencyObject obj, int value) => obj.SetValue(TabUnselectedPadProperty, value);


        public static DependencyProperty TabSelectionIndicatorSizeProperty = APUtils.Register(GetTabSelectionIndicatorSize, SetTabSelectionIndicatorSize);
        public static double GetTabSelectionIndicatorSize (DependencyObject obj) => (double)obj.GetValue(TabSelectionIndicatorSizeProperty);
        public static void SetTabSelectionIndicatorSize (DependencyObject obj, double value) => obj.SetValue(TabSelectionIndicatorSizeProperty, value);



        public static DependencyProperty TabSelectionIndicatorMarginProperty = APUtils.Register(GetTabSelectionIndicatorMargin, SetTabSelectionIndicatorMargin);
        public static Thickness GetTabSelectionIndicatorMargin (DependencyObject obj) => (Thickness)obj.GetValue(TabSelectionIndicatorMarginProperty);
        public static void SetTabSelectionIndicatorMargin (DependencyObject obj, Thickness value) => obj.SetValue(TabSelectionIndicatorMarginProperty, value);

    }
}
