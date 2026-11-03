namespace AJut.UX.Tests
{
    using Foundation;
    using UIKit;

    /// <summary>Makes the test app's one window when its scene connects, and starts the run in it.</summary>
    [Register(nameof(TestRunnerSceneDelegate))]
    public sealed class TestRunnerSceneDelegate : UIResponder, IUIWindowSceneDelegate
    {
        [Export("window")]
        public UIWindow Window { get; set; }

        [Export("scene:willConnectToSession:options:")]
        public void WillConnect (UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
        {
            if (scene is not UIWindowScene windowScene)
            {
                return;
            }

            this.Window = new UIWindow(windowScene);
            TestRunnerAppDelegate.BeginRun(this.Window);
        }
    }
}
