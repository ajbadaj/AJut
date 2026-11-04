namespace AJutShowRoomAndroid
{
    using AJut.UX.CodeBinding;
    using AJut.UX.Theming;
    using Android.Views;
    using Android.Widget;

    /// <summary>
    /// What the app's <see cref="AppThemeManager"/> says now: the choice the theme bar gave it, the theme in effect, and
    /// the device's own light or dark, so following the device under System can be checked from the page.
    /// </summary>
    public sealed class AppThemeManagerPage : ShowroomPage
    {
        // ===========[ Properties ]===================================
        public override string Title => "AppThemeManager";

        public override int LayoutId => Resource.Layout.page_app_theme_manager;

        // ===========[ Public Interface Methods ]===================================
        public override void Bind (View root, MainActivity host, BindingSet binder)
        {
            AppThemeManager? theme = ShowroomSettings.Instance.Theme;
            if (theme == null)
            {
                return;
            }

            AppThemeResolver resolver = theme.Resolver;
            binder.BindLabel(root.FindViewById<TextView>(Resource.Id.app_theme_manager_choice)!, resolver, nameof(AppThemeResolver.ThemeConfiguration), () => $"Choice: {resolver.ThemeConfiguration}");
            binder.BindLabel(root.FindViewById<TextView>(Resource.Id.app_theme_manager_theme)!, resolver, nameof(AppThemeResolver.CurrentTheme), () => $"Theme in effect: {resolver.CurrentTheme}");

            // The application's night mode is the device's, whatever the activity was given
            bool isDeviceDark = ThemeColors.IsDark(host.ApplicationContext!);
            root.FindViewById<TextView>(Resource.Id.app_theme_manager_device)!.Text = $"The device: {(isDeviceDark ? "Dark" : "Light")}";
        }
    }
}
