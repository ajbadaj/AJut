namespace AJutShowRoomUIKit
{
    using AJutShowRoomUIKit.UI;
    using Foundation;
    using UIKit;

    [Register(nameof(SceneDelegate))]
    public class SceneDelegate : UIResponder, IUIWindowSceneDelegate
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

            this.Window = new UIWindow(windowScene)
            {
                RootViewController = ShowroomRoot.Create(),
            };
            this.Window.MakeKeyAndVisible();
        }
    }
}
