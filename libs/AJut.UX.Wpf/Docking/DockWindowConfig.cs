namespace AJut.UX.Docking
{
    using System;
    using System.Windows;

    public static class DockWindowConfig
    {
        private static readonly APUtilsRegistrationHelper APUtils = new APUtilsRegistrationHelper(typeof(DockWindowConfig));


        public static DependencyProperty DockingTearoffWindowRootZoneProperty = APUtils.Register(GetDockingTearoffWindowRootZone, SetDockingTearoffWindowRootZone, (d,e)=>SetIsDockingTearoffWindow(d, e.HasNewValue));
        public static DockZoneViewModel GetDockingTearoffWindowRootZone(DependencyObject obj) => (DockZoneViewModel)obj.GetValue(DockingTearoffWindowRootZoneProperty);
        public static void SetDockingTearoffWindowRootZone(DependencyObject obj, DockZoneViewModel value) => obj.SetValue(DockingTearoffWindowRootZoneProperty, value);


        private static DependencyPropertyKey IsDockingTearoffWindowPropertyKey = APUtils.RegisterReadOnly(GetIsDockingTearoffWindow, SetIsDockingTearoffWindow);
        public static DependencyProperty IsDockingTearoffWindowProperty = IsDockingTearoffWindowPropertyKey.DependencyProperty;
        public static bool GetIsDockingTearoffWindow(DependencyObject obj) => (bool)obj.GetValue(IsDockingTearoffWindowProperty);
        private static void SetIsDockingTearoffWindow(DependencyObject obj, bool value) => obj.SetValue(IsDockingTearoffWindowPropertyKey, value);
    }
}
