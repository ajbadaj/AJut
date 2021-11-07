namespace AJut.UX.Docking
{
    using System.Windows;

    public static class DockWindowConfig
    {
        private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(DockWindowConfig));

        public static DependencyProperty IsDockingTearoffWindowProperty = APUtils.Register(GetIsDockingTearoffWindow, SetIsDockingTearoffWindow);
        public static bool GetIsDockingTearoffWindow (DependencyObject obj) => (bool)obj.GetValue(IsDockingTearoffWindowProperty);
        public static void SetIsDockingTearoffWindow (DependencyObject obj, bool value) => obj.SetValue(IsDockingTearoffWindowProperty, value);
    }
}
