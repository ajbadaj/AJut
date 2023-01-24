namespace AJut.UX.Theming.AJutStyleExtensionsForBuiltInWpfControls
{
    using System.Windows;

    public class AJutThemedListControlXTA
    {
        private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(AJutThemedListControlXTA));

        public static DependencyProperty ListItemsShowHoverProperty = APUtils.Register(GetListItemsShowHover, SetListItemsShowHover, new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.Inherits));
        public static bool GetListItemsShowHover (DependencyObject obj) => (bool)obj.GetValue(ListItemsShowHoverProperty);
        public static void SetListItemsShowHover (DependencyObject obj, bool value) => obj.SetValue(ListItemsShowHoverProperty, value);
    }
}
