namespace AJutShowRoomUIKit.UI.Diagnostics
{
    using AJut.UX.CodeBinding;
    using AJut.UX.Theming;
    using UIKit;

    /// <summary>
    /// What the app's <see cref="AppThemeManager"/> says now: the choice the theme bar gave it, the theme in effect, and
    /// the system's own light or dark, so following the system under System can be checked from the page. The bindings
    /// are made when the page appears and disposed when it goes.
    /// </summary>
    public sealed class AppThemeManagerPage : ShowroomPageController
    {
        private readonly UILabel m_choice = new UILabel { Lines = 0 };
        private readonly UILabel m_theme = new UILabel { Lines = 0 };
        private readonly UILabel m_system = new UILabel { Lines = 0 };
        private BindingSet m_bindings;

        // ===========[ Setup/Construction/Teardown ]===================================
        public AppThemeManagerPage () : base("AppThemeManager")
        {
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear(animated);

            AppThemeResolver resolver = ShowroomSettings.Theme.Resolver;
            m_bindings?.Dispose();
            m_bindings = new BindingSet();
            m_bindings.BindLabel(m_choice, resolver, nameof(AppThemeResolver.ThemeConfiguration), () => $"Choice: {resolver.ThemeConfiguration}");
            m_bindings.BindLabel(m_theme, resolver, nameof(AppThemeResolver.CurrentTheme), () => $"Theme in effect: {resolver.CurrentTheme}");

            // The scene's traits are the system's, whatever the window was pinned to; read again whenever the theme in
            // effect moves, which under System is the system moving
            m_bindings.BindLabel(m_system, resolver, nameof(AppThemeResolver.CurrentTheme), this.DescribeSystem);
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear(animated);
            m_bindings?.Dispose();
            m_bindings = null;
        }

        protected override void BuildPage ()
        {
            this.AddHeading("The app's AppThemeManager");
            this.AddText("Dark, Light and System on the bar at the top set the app's AppThemeManager, the way every AJut showroom picks light or dark. It sets the light or dark of every window, and of each window shown later.");
            this.AddDemo(m_choice);
            this.AddDemo(m_theme);
            this.AddDemo(m_system);
            this.AddText("To try: pick System, then switch the system's appearance (Control Center, or Settings, Display and Brightness). The theme in effect follows it as the showroom comes back. Pick Dark or Light and switch it again, and the theme in effect stays on the pick.");
        }

        // ===========[ Helper Methods ]===================================
        private string DescribeSystem ()
        {
            UIWindowScene scene = this.View.Window?.WindowScene;
            if (scene == null)
            {
                return "The system: not known until the page is showing";
            }

            return $"The system: {(scene.TraitCollection.UserInterfaceStyle == UIUserInterfaceStyle.Dark ? "Dark" : "Light")}";
        }
    }
}
