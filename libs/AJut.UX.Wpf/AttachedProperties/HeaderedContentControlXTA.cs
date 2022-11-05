namespace AJut.UX.AttachedProperties
{
    using System.Windows;
    using System.Windows.Media;

    public static class HeaderedContentControlXTA
    {
        private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(HeaderedContentControlXTA));


        public static DependencyProperty HeaderPaddingProperty = APUtils.Register(GetHeaderPadding, SetHeaderPadding);
        public static Thickness GetHeaderPadding (DependencyObject obj) => (Thickness)obj.GetValue(HeaderPaddingProperty);
        public static void SetHeaderPadding (DependencyObject obj, Thickness value) => obj.SetValue(HeaderPaddingProperty, value);



        public static DependencyProperty HorizontalHeaderAlignmentProperty = APUtils.Register(GetHorizontalHeaderAlignment, SetHorizontalHeaderAlignment);
        public static HorizontalAlignment GetHorizontalHeaderAlignment (DependencyObject obj) => (HorizontalAlignment)obj.GetValue(HorizontalHeaderAlignmentProperty);
        public static void SetHorizontalHeaderAlignment (DependencyObject obj, HorizontalAlignment value) => obj.SetValue(HorizontalHeaderAlignmentProperty, value);


        public static DependencyProperty VerticalHeaderAlignmentProperty = APUtils.Register(GetVerticalHeaderAlignment, SetVerticalHeaderAlignment);
        public static VerticalAlignment GetVerticalHeaderAlignment (DependencyObject obj) => (VerticalAlignment)obj.GetValue(VerticalHeaderAlignmentProperty);
        public static void SetVerticalHeaderAlignment (DependencyObject obj, VerticalAlignment value) => obj.SetValue(VerticalHeaderAlignmentProperty, value);


        public static DependencyProperty HeaderFontSizeProperty = APUtils.Register(GetHeaderFontSize, SetHeaderFontSize);
        public static double GetHeaderFontSize (DependencyObject obj) => (double)obj.GetValue(HeaderFontSizeProperty);
        public static void SetHeaderFontSize (DependencyObject obj, double value) => obj.SetValue(HeaderFontSizeProperty, value);
    }
}
